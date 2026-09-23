using GameData;
using MikaProtocol;

namespace WSGameServer;

public partial class User
{
    /// <summary>
    /// 인챈트. 검증 → 아이템 소모 → 성공 판정 → 상태 갱신 → 저장 → 응답·싱크.
    ///
    /// <para>
    /// <b>정산도 속도 재확정도 하지 않는다.</b> 착용 중인 장비를 거절하므로(EnchantEquipped)
    /// 가동 중인 슬롯의 속도·경험치가 바뀔 수 없다 — 소급의 여지가 없다.
    /// 바뀐 값은 다음에 장착할 때 TryEquip이 반영한다.
    /// </para>
    /// </summary>
    public void TryEnchant(long equipId, int itemTid, Random? random = null)
    {
        if (!TryGetEquip(equipId, out var equip))
        {
            RejectEnchant(EResultCode.EquipNotOwned, equipId, $"미보유 장비 {equipId}");
            return;
        }

        // 착용 중 거부가 이 경로를 단순하게 만든다 — 정산 순서를 신경 쓸 필요가 없다.
        if (equip.IsEquipped)
        {
            RejectEnchant(EResultCode.EnchantEquipped, equipId, "착용 중");
            return;
        }

        if (!_enchantCatalog.TryGetItem(itemTid, out var item))
        {
            RejectEnchant(EResultCode.EnchantItemNotOwned, equipId, $"인챈트 아이템이 아님 {itemTid}");
            return;
        }

        // 동작을 모르는 아이템은 소모 전에 막는다 — 통과시키면 아이템만 사라지고 아무것도 안 바뀐다.
        if (!IsKnownAction(item.Action))
        {
            RejectEnchant(EResultCode.ItemNotUsable, equipId, $"알 수 없는 동작 {item.Action} ({itemTid})");
            return;
        }

        if (GetItemCount(itemTid) <= 0)
        {
            RejectEnchant(EResultCode.EnchantItemNotOwned, equipId, $"보유 0 {itemTid}");
            return;
        }

        var hasEnchant = equip.EnchantGrade != GlobalRarity.None;

        if (item.Action == EnchantAction.Grant && hasEnchant)
        {
            RejectEnchant(EResultCode.EnchantAlreadyRolled, equipId, "이미 인챈트 있음");
            return;
        }

        if (item.Action != EnchantAction.Grant && !hasEnchant)
        {
            RejectEnchant(EResultCode.EnchantNotRolled, equipId, "인챈트 없음");
            return;
        }

        if (item.Action == EnchantAction.ExpandLine && equip.EnchantLineCount >= EnchantCatalog.MaxLineCount)
        {
            RejectEnchant(EResultCode.EnchantLineMax, equipId, "줄 상한");
            return;
        }

        if (!TryConsumeItems(new Dictionary<int, int> { [itemTid] = 1 }, out var consumed))
        {
            RejectEnchant(EResultCode.EnchantItemNotOwned, equipId, $"소모 실패 {itemTid}");
            return;
        }

        var rng         = random ?? Random.Shared;
        var beforeGrade = equip.EnchantGrade;
        var success     = false;

        switch (item.Action)
        {
            case EnchantAction.Grant:
                success = Rolled(item.SuccessPermille, rng);
                if (success)
                {
                    // 부여는 항상 Rare에서 시작한다. 등급은 GradeUp으로만 오른다.
                    equip.SetEnchant(GlobalRarity.Rare,
                        _enchantCatalog.RollOptions(GlobalRarity.Rare, EnchantCatalog.BaseLineCount, rng));
                }
                break;

            case EnchantAction.GradeUp:
                // 상승 확률은 아이템이 아니라 현재 등급이 정한다(큐브가 1종이므로).
                // 최고 등급은 오를 곳이 없다 — 판정이 맞아도 오르지 않았으면 Success가 참일 수 없다.
                success = Rolled(_enchantCatalog.UpPermilleOf(beforeGrade), rng)
                          && EnchantCatalog.NextGrade(beforeGrade) != beforeGrade;

                var grade = success ? EnchantCatalog.NextGrade(beforeGrade) : beforeGrade;

                // 상승에 실패해도 줄은 다시 뽑는다 — 빈손이 없게 한 설계다. 줄 수는 유지한다.
                equip.SetEnchant(grade, _enchantCatalog.RollOptions(grade, equip.EnchantLineCount, rng));
                break;

            case EnchantAction.ExpandLine:
                success = Rolled(item.SuccessPermille, rng);
                if (success)
                {
                    var options = equip.EnchantOptions.ToList();
                    options.AddRange(_enchantCatalog.RollOptions(equip.EnchantGrade, 1, rng));
                    equip.SetEnchant(equip.EnchantGrade, options);
                }
                break;
        }

        var tids = equip.EnchantOptionTids;
        PostDBTask(new SaveEquipEnchantRepository(
            this, equipId, (int)equip.EnchantGrade,
            TidAt(tids, 0), TidAt(tids, 1), TidAt(tids, 2)));

        ServerLog.Info("인챈트", $"{item.Action} Uid={Uid} 장비 {equipId} 성공={success} 등급 {beforeGrade}→{equip.EnchantGrade}");

        Send(new S_EquipEnchantResponse
        {
            Result      = EResultCode.Ok,
            EquipId     = equipId,
            Success     = success,
            BeforeGrade = (int)beforeGrade,
            AfterGrade  = (int)equip.EnchantGrade,
            Options     = tids.ToList(),
        });

        Send(new S_EquipSyncResponse { Equips = new List<EquipInfo> { ToEquipInfo(equip) } });
        Send(new S_UpdateItemResponse { Result = EResultCode.Ok, ItemChangeInfos = consumed });
    }

    /// <summary>이 서버가 처리할 줄 아는 동작인가. 모르는 동작은 거절한다 — 표가 잘못 쓰여도 조용히 넘어가지 않게.</summary>
    private static bool IsKnownAction(EnchantAction action)
        => action is EnchantAction.Grant or EnchantAction.GradeUp or EnchantAction.ExpandLine;

    /// <summary>천분율 판정. 1000이면 항상 성공, 0이면 항상 실패다.</summary>
    private static bool Rolled(int permille, Random random)
        => permille > 0 && random.Next(1000) < permille;

    /// <summary>빈 줄은 0으로 저장한다.</summary>
    private static int TidAt(IReadOnlyList<int> tids, int index)
        => index < tids.Count ? tids[index] : 0;

    private void RejectEnchant(EResultCode code, long equipId, string reason)
    {
        ServerLog.Warn("인챈트", $"거절 {code} Uid={Uid} 장비={equipId} 사유={reason}");

        Send(new S_EquipEnchantResponse { Result = code, EquipId = equipId });
    }
}

using GameData;
using MikaProtocol;

namespace WSGameServer;

public partial class User
{
    /// <summary>
    /// 장비를 지우고 등급별 재료(<c>DisassembleTable</c>)를 인벤토리에 넣는다(T-131). 전부 검증한 뒤에만 지운다 — 하나라도 걸리면 아무것도 바뀌지 않는다.
    /// 착용 중 · 잠김 · 큐브 결과 고르기 전 · 재료가 인벤토리에 다 안 들어가면 거절한다.
    /// </summary>
    public void TryDisassembleEquips(IReadOnlyList<long> equipIds)
    {
        if (equipIds.Count == 0 || equipIds.Distinct().Count() != equipIds.Count)
        {
            Reject(EResultCode.InvalidDisassembleRequest, "빈 요청 또는 중복 장비");
            return;
        }

        var materials = new Dictionary<int, int>();

        foreach (var equipId in equipIds)
        {
            if (!TryGetEquip(equipId, out var equip))
            {
                Reject(EResultCode.EquipNotOwned, $"미보유 장비 {equipId}");
                return;
            }
            if (equip.IsLocked)
            {
                Reject(EResultCode.EntityLocked, $"잠긴 장비 {equipId}");
                return;
            }
            if (equip.IsEquipped)
            {
                Reject(EResultCode.DisassembleEquipWorn, $"착용 중 장비 {equipId}");
                return;
            }
            if (equip.HasPendingEnchant)
            {
                Reject(EResultCode.EnchantPending, $"고르기 전 장비 {equipId}");
                return;
            }
            if (!GameTable.DisassembleTable.TryGet(equip.Row.GlobalRarity, out var row))
            {
                Reject(EResultCode.InvalidDisassembleRequest, $"분해 표에 없는 등급 {equip.Row.GlobalRarity} 장비 {equipId}");
                return;
            }

            materials[row.ItemTID] = materials.GetValueOrDefault(row.ItemTID) + row.ItemCount;
        }

        // 우편 넘침 보관을 쓰지 않는다 — 다 안 들어가면 장비를 지우지 않는다(2026-10-12 결정).
        if (!HasStorageFor(materials.Select(m => (m.Key, m.Value)), 0, 0))
        {
            Reject(EResultCode.StorageFull, "재료가 인벤토리에 다 안 들어간다");
            return;
        }

        foreach (var equipId in equipIds)
        {
            RemoveEquip(equipId);
        }

        var changes = new List<ItemChangeInfo>();
        foreach (var (tid, count) in materials)
        {
            changes.AddRange(Inventory.AddItem(tid, count, MaxStackOf(tid), out _));
        }

        PostDBTask(new DisassembleEquipsRepository(this, equipIds.ToList(), changes));

        ServerLog.Info("분해", $"장비 분해 Uid={Uid} 장비 {equipIds.Count} 재료 {string.Join(",", materials.Select(m => $"{m.Key}×{m.Value}"))}");
        Send(new S_EquipDisassembleResponse { Result = EResultCode.Ok, ItemChangeInfos = changes });
        return;

        void Reject(EResultCode code, string reason)
        {
            ServerLog.Warn("분해", $"장비 분해 거절 {code} Uid={Uid} 사유={reason}");
            Send(new S_EquipDisassembleResponse { Result = code });
        }
    }
}

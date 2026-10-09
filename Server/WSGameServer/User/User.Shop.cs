using GameData;
using MikaProtocol;

namespace WSGameServer;

public partial class User
{
    /// <summary>
    /// 아이템을 팔고 대금을 지급한다. <b>보유량이 모자라면 지갑을 건드리지 않고 false를 돌려준다</b> —
    /// 아이템만 사라지는 경로를 만들지 않기 위해 차감이 먼저다.
    /// </summary>
    /// <returns>판매에 성공했으면 true.</returns>
    public bool TrySellItems(IReadOnlyDictionary<int, int> request, long gold, out List<ItemChangeInfo> changes)
    {
        if (!Inventory.TryRemoveItems(request, out changes))
        {
            return false;
        }

        // GainGold를 쓰지 않고 잔액을 직접 올린다 — 그쪽은 저장까지 해서 DB 작업이 인벤 차감과 둘로 갈라진다.
        // 대금은 BasePrice × 수량이라 음수가 될 수 없고, 0원 판매(BasePrice=0)도 그대로 통과시킨다.
        _gold = checked(_gold + gold);

        PostDBTask(new SellItemsRepository(this, changes, _gold));

        Send(new S_CurrencyResponse { Gold = _gold });

        return true;
    }

    /// <summary>
    /// 캐릭터·장비 개체를 팔고 대금을 지급한다. 전부 검증한 뒤에만 지운다 — 하나라도 걸리면 아무것도 팔리지 않는다.
    /// 착용 중인 장비 · 배치됐거나 장비를 낀 캐릭터 · 캐릭터를 0명으로 만드는 판매는 거절한다.
    /// </summary>
    public void TrySellEntities(IReadOnlyList<long> characterIds, IReadOnlyList<long> equipIds)
    {
        if (characterIds.Count + equipIds.Count == 0
            || characterIds.Distinct().Count() != characterIds.Count
            || equipIds.Distinct().Count() != equipIds.Count)
        {
            Reject(EResultCode.InvalidSellRequest, "빈 요청 또는 중복 개체");
            return;
        }

        long gold = 0;

        foreach (var equipId in equipIds)
        {
            if (!TryGetEquip(equipId, out var equip))
            {
                Reject(EResultCode.EquipNotOwned, $"미보유 장비 {equipId}");
                return;
            }
            if (equip.IsEquipped)
            {
                Reject(EResultCode.SellEquipWorn, $"착용 중 장비 {equipId}");
                return;
            }
            if (equip.HasPendingEnchant)
            {
                Reject(EResultCode.EnchantPending, $"고르기 전 장비 {equipId}");
                return;
            }

            gold += SellPrice(equip.Row.BasePrice);
        }

        foreach (var characterId in characterIds)
        {
            if (!TryGetCharacter(characterId, out var character))
            {
                Reject(EResultCode.CharacterNotOwned, $"미보유 캐릭터 {characterId}");
                return;
            }
            if (IsCharacterBusy(characterId))
            {
                Reject(EResultCode.SellCharacterBusy, $"배치·착용 중 캐릭터 {characterId}");
                return;
            }

            gold += SellPrice(character.Row.BasePrice);
        }

        // 받을 캐릭터(가챠 지급 대기)는 세지 않는다 — 지급이 실패하면 0명이 된다.
        if (characterIds.Count > 0 && characterIds.Count >= _characters.Count)
        {
            Reject(EResultCode.SellLastCharacter, "캐릭터가 하나도 남지 않는다");
            return;
        }

        foreach (var equipId in equipIds)
        {
            _equips.Remove(equipId);
        }
        foreach (var characterId in characterIds)
        {
            _characters.Remove(characterId);
        }

        _gold = checked(_gold + gold);

        PostDBTask(new SellEntitiesRepository(this, characterIds.ToList(), equipIds.ToList(), _gold));

        ServerLog.Info("상점", $"개체 판매 Uid={Uid} 캐릭터 {characterIds.Count} 장비 {equipIds.Count} +{gold}G");
        Send(new S_EntitySellResponse { Result = EResultCode.Ok, GainedGold = gold });
        Send(new S_CurrencyResponse { Gold = _gold });
        return;

        void Reject(EResultCode code, string reason)
        {
            ServerLog.Warn("상점", $"개체 판매 거절 {code} Uid={Uid} 사유={reason}");
            Send(new S_EntitySellResponse { Result = code });
        }
    }

    /// <summary>즉시 판매가 — 아이템 판매(ShopService)와 같은 비율이다.</summary>
    private static long SellPrice(int basePrice)
        => (long)basePrice * Constants.SellRatePermille / 1000;
}

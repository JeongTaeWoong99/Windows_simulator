// 개체(캐릭터·장비)·자원을 내놓을 수 없는 이유 → 사용자에게 보일 한 줄. 자원은 잠금(T-159)만 막는다.
//
// ■ 두 화면이 같은 판정을 쓴다
// 경매 등록('AuctionRegisterPresenter')과 즉시 판매 담기('InventoryGridPresenter')가 같은 개체를 막는다.
// 한쪽에만 두면 같은 캐릭터가 한 화면에서는 되고 다른 화면에서는 안 되는 것처럼 보인다.
//
// ■ 서버 판정을 미리 흉내 낸다
// 진짜 판정은 서버다(장비 착용 → SellEquipWorn·AuctionEquipWorn, 바쁜 캐릭터 → SellCharacterBusy·AuctionCharacterBusy,
// 마지막 캐릭터 → SellLastCharacter·AuctionLastCharacter, 잠금 → EntityLocked · T-137). 여기는 보내기 전에 흐리게 막고 이유를 적는 데만 쓴다.
// ※ 창고에 있는지는 보지 않는다 — 창고 것도 인벤토리 것과 똑같이 내놓는다(T-164 · 이슈 #74).
public static class EntityBlockText
{
    // 잠긴 개체의 사유 — 잠금이 가장 먼저다(풀기 전에는 다른 사유를 고쳐도 소용없다). 서버 'EntityLocked'.
    public static string LockedText => Lang.Get("UI.Block.Locked");

    // 이 자원을 내놓을 수 없는 이유. 내놓을 수 있으면 null (T-159).
    // ※ 자원은 잠금만 본다 — 종류(TID) 단위라 수량 전부가 함께 막힌다.
    public static string? ForItem(PlayerDataModel data, int itemId)
        => data.IsItemLocked(itemId) ? LockedText : null;

    // 이 장비를 내놓을 수 없는 이유. 내놓을 수 있으면 null.
    public static string? ForEquip(PlayerDataModel data, long equipId)
    {
        if (data.FindEquip(equipId)?.IsLocked == true)
        {
            return LockedText;
        }

        return data.IsEquipped(equipId) ? Lang.Get("UI.Block.EquipWorn") : null;
    }

    // 이 캐릭터를 내놓을 수 없는 이유. 내놓을 수 있으면 null.
    // ※ 'remainingAfter' — 이 캐릭터까지 내놓으면 남는 캐릭터 수. 0 이하면 마지막 캐릭터다.
    //   즉시 판매는 이미 담은 캐릭터를 빼고 세야 해서 호출하는 쪽이 계산한다.
    public static string? ForCharacter(PlayerDataModel data, long characterId, int remainingAfter)
    {
        if (data.FindCharacter(characterId)?.IsLocked == true)
        {
            return LockedText;
        }

        if (data.FindSlotIndexOf(characterId) >= 0)
        {
            return Lang.Get("UI.Block.CharacterAssigned");
        }

        if (data.IsWearingEquip(characterId))
        {
            return Lang.Get("UI.Block.CharacterWearing");
        }

        if (remainingAfter <= 0)
        {
            return Lang.Get("UI.Block.LastCharacter");
        }

        return null;
    }
}

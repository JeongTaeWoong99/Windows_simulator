// 개체(캐릭터·장비)를 내놓을 수 없는 이유 → 사용자에게 보일 한 줄.
//
// ■ 두 화면이 같은 판정을 쓴다
// 경매 등록('AuctionRegisterPresenter')과 즉시 판매 담기('InventoryGridPresenter')가 같은 개체를 막는다.
// 한쪽에만 두면 같은 캐릭터가 한 화면에서는 되고 다른 화면에서는 안 되는 것처럼 보인다.
//
// ■ 서버 판정을 미리 흉내 낸다
// 진짜 판정은 서버다(장비 착용 → SellEquipWorn·AuctionEquipWorn, 바쁜 캐릭터 → SellCharacterBusy·AuctionCharacterBusy,
// 마지막 캐릭터 → SellLastCharacter·AuctionLastCharacter). 여기는 보내기 전에 흐리게 막고 이유를 적는 데만 쓴다.
public static class EntityBlockText
{
    // 이 장비를 내놓을 수 없는 이유. 내놓을 수 있으면 null.
    public static string? ForEquip(PlayerDataModel data, long equipId)
    {
        return data.IsEquipped(equipId) ? "캐릭터가 끼고 있습니다 — 먼저 해제하세요" : null;
    }

    // 이 캐릭터를 내놓을 수 없는 이유. 내놓을 수 있으면 null.
    // ※ 'remainingAfter' — 이 캐릭터까지 내놓으면 남는 캐릭터 수. 0 이하면 마지막 캐릭터다.
    //   즉시 판매는 이미 담은 캐릭터를 빼고 세야 해서 호출하는 쪽이 계산한다.
    public static string? ForCharacter(PlayerDataModel data, long characterId, int remainingAfter)
    {
        if (data.FindSlotIndexOf(characterId) >= 0)
        {
            return "작업슬롯에 배치 중입니다 — 먼저 빼세요";
        }

        if (data.IsWearingEquip(characterId))
        {
            return "장비를 끼고 있습니다 — 먼저 해제하세요";
        }

        if (remainingAfter <= 0)
        {
            return "마지막 캐릭터는 내놓을 수 없습니다";
        }

        return null;
    }
}

using MikaProtocol;

// 서버 결과 코드('EResultCode')를 사용자에게 보일 우리말 문구로 바꾼다.
// 서버 enum을 화면에 그대로 노출하지 않기 위한 표다 — 결과 코드가 늘면 여기 한 줄을 더한다.
//
// 'ServerWaitManager'는 문구(문자열)만 다루고 결과 코드를 모른다 — 코드→문구 변환은
// 요청을 보낸 Presenter가 이 표로 마친 뒤 넘긴다(계층 의존 방향 규약).
public static class ResultMessages
{
    // 결과 코드를 사용자용 문구로 바꾼다. 모르는 코드는 코드 번호를 붙여 표시한다.
    public static string ToText(EResultCode code) => code switch
    {
        EResultCode.Ok                  => "정상 처리되었습니다.",
        EResultCode.NotLoggedIn         => "로그인이 필요합니다.",
        EResultCode.AlreadyLoggedIn     => "이미 접속 중인 계정입니다. 다른 창을 닫고 다시 시도해 주세요.",
        EResultCode.InvalidDrawCount    => "뽑기 횟수가 올바르지 않습니다.",
        EResultCode.InvalidGachaId      => "존재하지 않는 뽑기입니다.",
        EResultCode.NotEnoughCurrency   => "골드가 부족합니다.",
        // 뽑기와 상자 개봉이 같은 코드로 온다. 상자는 넘치면 우편으로 보관되므로, 이 코드면 우편함까지 찬 것이다.
        EResultCode.StorageFull         => "인벤토리가 가득 찼습니다. 정리한 뒤 다시 시도해 주세요.",
        EResultCode.InvalidSlotIndex    => "아직 열리지 않은 작업 슬롯입니다.",
        EResultCode.CharacterNotOwned   => "보유하지 않은 캐릭터입니다.",
        EResultCode.NoAptitude          => "이 캐릭터는 해당 산업 적성이 없습니다.",
        EResultCode.IndustryLevelLocked => "아직 열지 않은 산업 레벨입니다.",
        EResultCode.InvalidSellRequest  => "판매 요청이 올바르지 않습니다.",
        EResultCode.NotEnoughItem       => "보유량이 부족합니다.",
        EResultCode.SellEquipWorn       => "착용 중인 장비는 팔 수 없습니다. 먼저 벗겨 주세요.",
        EResultCode.SellCharacterBusy   => "배치됐거나 장비를 낀 캐릭터는 팔 수 없습니다. 빼고 벗긴 뒤 다시 시도해 주세요.",
        EResultCode.SellLastCharacter   => "캐릭터를 하나는 남겨야 합니다.",
        EResultCode.ItemNotUsable       => "사용할 수 없는 아이템입니다.",
        EResultCode.InvalidUseCount     => "사용 개수가 올바르지 않습니다.",
        EResultCode.InvalidUnlockTID    => "존재하지 않는 해금입니다.",
        EResultCode.AlreadyUnlocked     => "이미 해금되었습니다.",
        EResultCode.UnlockLocked        => "해금 조건을 만족하지 않습니다.",
        EResultCode.NotEnoughTraitPoint => "특성 포인트가 부족합니다.",
        EResultCode.InvalidUserTraitTID => "존재하지 않는 특성입니다.",
        // (결번) 특성이 해금 노드이던 시절의 값 — 레벨형으로 바뀐 뒤(2026-10-02 · T-108) 더 오지 않는다.
        EResultCode.TraitOnlyUnlock     => "특성 화면에서만 열 수 있습니다.",
        EResultCode.TraitMaxLevel       => "이미 최대 레벨입니다.",
        EResultCode.EquipNotOwned       => "보유하지 않은 장비입니다.",
        EResultCode.InvalidEquipSlot    => "올바르지 않은 장비 칸입니다.",
        EResultCode.EquipKindMismatch   => "이 칸에 낄 수 없는 장비입니다.",
        EResultCode.EquipSlotEmpty      => "이미 비어 있는 칸입니다.",
        EResultCode.MailNotFound        => "없는 우편입니다.",
        EResultCode.MailAlreadyClaimed  => "이미 받은 우편입니다.",
        // 안 받은 우편에는 삭제 버튼이 없다 — 정상 화면에서는 나오지 않는다.
        EResultCode.MailNotClaimed      => "받지 않은 우편은 지울 수 없습니다.",
        // 경매장·거래소 (1000~1015). 뜻은 'PacketEnum.cs' 주석.
        EResultCode.AuctionUnavailable     => "경매장이 점검 중입니다. 잠시 뒤 다시 시도해 주세요.",
        EResultCode.AuctionInvalidRequest  => "거래 요청이 올바르지 않습니다.",
        EResultCode.AuctionPriceOutOfBand  => $"경매 단가는 즉시 판매가 ~ {AuctionModel.PriceBandMultiplier:N0}배 사이로 정해 주세요.",
        EResultCode.AuctionListingLimit    => "판매 중인 매물이 너무 많습니다. 정리한 뒤 다시 등록해 주세요.",
        EResultCode.AuctionEquipped        => "착용 중인 장비는 올릴 수 없습니다. 먼저 벗겨 주세요.",
        EResultCode.AuctionNotFound        => "없는 매물입니다.",
        EResultCode.AuctionInProgress      => "다른 사람이 구매 중인 매물입니다. 잠시 뒤 다시 시도해 주세요.",
        EResultCode.AuctionSoldOut         => "이미 팔린 매물입니다.",
        EResultCode.AuctionClosed          => "취소되었거나 기간이 끝난 매물입니다.",
        EResultCode.AuctionPriceChanged    => "가격이 바뀌었습니다. 다시 검색해 주세요.",
        EResultCode.AuctionOwnListing      => "내가 올린 매물은 살 수 없습니다.",
        EResultCode.AuctionNotOwner        => "내 매물만 취소할 수 있습니다.",
        EResultCode.AuctionTooManyRequests => "조회가 너무 잦습니다. 잠시 뒤 다시 시도해 주세요.",
        EResultCode.AuctionCharacterBusy   => "배치됐거나 장비를 낀 캐릭터는 올릴 수 없습니다. 빼고 벗긴 뒤 다시 시도해 주세요.",
        EResultCode.AuctionLastCharacter   => "캐릭터를 하나는 남겨야 합니다.",
        EResultCode.MarketNotEnough        => "그 가격으로 원하는 수량을 다 살 수 없습니다. 아무것도 사지 않았습니다.",
        _                               => $"알 수 없는 오류가 발생했습니다. (코드 {(ushort)code})",
    };
}

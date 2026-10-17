using MikaProtocol;

// 서버 결과 코드('EResultCode')를 사용자에게 보일 문구로 바꾼다.
// 서버 enum을 화면에 그대로 노출하지 않기 위한 표다 — 결과 코드가 늘면 'Loc_UI.xlsx'에 'UI.Result.<코드 이름>' 한 줄을 더한다.
//
// 'ServerWaitManager'는 문구(문자열)만 다루고 결과 코드를 모른다 — 코드→문구 변환은
// 요청을 보낸 Presenter가 이 표로 마친 뒤 넘긴다(계층 의존 방향 규약).
//
// ※ 뽑기와 상자 개봉이 같은 'StorageFull'로 온다 — 상자는 넘치면 우편으로 보관되므로, 이 코드면 우편함까지 찬 것이다.
// ※ 'TraitOnlyUnlock'은 결번(2026-10-02 · T-108), 'MailNotClaimed'는 정상 화면에서 나오지 않는다 — 문구만 남겨 둔다.
public static class ResultMessages
{
    private const string KeyPrefix = "UI.Result.";

    // 결과 코드를 사용자용 문구로 바꾼다. 문구가 없는 코드는 코드 번호를 붙여 표시한다.
    public static string ToText(EResultCode code)
    {
        if (code == EResultCode.AuctionPriceOutOfBand)
        {
            return Lang.Format(KeyPrefix + code, AuctionModel.FormatMaxUnitPrice());
        }

        string key = KeyPrefix + code;

        return Lang.Has(key) ? Lang.Get(key) : Lang.Format(KeyPrefix + "Unknown", (ushort)code);
    }
}

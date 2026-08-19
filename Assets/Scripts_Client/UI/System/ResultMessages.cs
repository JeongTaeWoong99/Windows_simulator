using MikaProtocol;

/// <summary>
/// 서버 결과 코드('EResultCode')를 사용자에게 보일 우리말 문구로 바꾼다.
/// 서버 enum을 화면에 그대로 노출하지 않기 위한 표다 — 결과 코드가 늘면 여기 한 줄을 더한다.
/// </summary>
/// <remarks>
/// 'ServerWaitManager'는 문구(문자열)만 다루고 결과 코드를 모른다 — 코드→문구 변환은
/// 요청을 보낸 Presenter가 이 표로 마친 뒤 넘긴다(계층 의존 방향 규약).
/// </remarks>
public static class ResultMessages
{
    /// <summary>결과 코드를 사용자용 문구로 바꾼다. 모르는 코드는 코드 번호를 붙여 표시한다.</summary>
    public static string ToText(EResultCode code) => code switch
    {
        EResultCode.Ok                  => "정상 처리되었습니다.",
        EResultCode.NotLoggedIn         => "로그인이 필요합니다.",
        EResultCode.AlreadyLoggedIn     => "이미 접속 중인 계정입니다. 다른 창을 닫고 다시 시도해 주세요.",
        EResultCode.InvalidDrawCount    => "뽑기 횟수가 올바르지 않습니다.",
        EResultCode.InvalidGachaId      => "존재하지 않는 뽑기입니다.",
        EResultCode.InvalidSlotIndex    => "아직 열리지 않은 작업 슬롯입니다.",
        EResultCode.CharacterNotOwned   => "보유하지 않은 캐릭터입니다.",
        EResultCode.NoAptitude          => "이 캐릭터는 해당 산업 적성이 없습니다.",
        EResultCode.IndustryLevelLocked => "아직 해금하지 않은 산업입니다.",
        _                               => $"알 수 없는 오류가 발생했습니다. (코드 {(ushort)code})",
    };
}

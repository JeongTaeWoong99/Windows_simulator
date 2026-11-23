using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Steamworks;
using UnityEngine;

// 스팀 로그인 티켓을 받는 창구 (T-211 · 이슈 #144). 화면을 모르고, 서비스로 등록하지 않는 정적 도우미다.
//
// ■ 흐름
//   'SteamAPI.Init' → 'SteamUser.GetAuthTicketForWebApi(Identity)' → 콜백 'GetTicketForWebApiResponse_t'로 티켓 바이트
//   → hex 문자열 → 'C_LoginRequest.SteamTicket'. 서버가 Web API로 확인해 SteamID 계정으로 들인다(T-207).
//
// ■ AppID
//   지금은 Valve 시험 앱 480 — 에디터는 프로젝트 루트, 빌드는 exe 옆의 'steam_appid.txt'를 스팀이 읽는다.
//   우리 AppID가 나오면(T-210) 그 파일만 바꾼다. 스팀 클라이언트가 켜져 로그인돼 있어야 한다.
//
// ※ 콜백은 'SteamAPI.RunCallbacks'를 불러야 온다 — 상주 매니저가 없으니 티켓을 기다리는 동안만 여기서 돌린다.
public static class SteamAuth
{
    // 서버 'Game:Steam:Identity'와 같아야 티켓이 통과한다(#144 서버 코멘트).
    public const string Identity = "wsgame";

    private const int TicketTimeoutSeconds = 10;

    private static bool _isInitialized;

    // 스팀이 붙었나 — 실패 문구를 가른다(꺼져 있음 ↔ 인증 실패).
    public static bool IsInitialized => _isInitialized;

    // 스팀 닉네임 — 화면 표시용. 붙기 전이면 "Steam".
    public static string PersonaName => _isInitialized ? SteamFriends.GetPersonaName() : "Steam";

    // 스팀을 붙인다. 이미 붙었으면 그대로 참. 스팀이 꺼져 있거나 AppID를 못 찾으면 거짓과 사유.
    public static bool TryInit(out string error)
    {
        error = "";

        if (_isInitialized)
        {
            return true;
        }

        if (!Packsize.Test() || !DllCheck.Test())
        {
            error = "Steamworks 네이티브 DLL이 맞지 않는다";

            return false;
        }

        var result = SteamAPI.InitEx(out string message);

        if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
        {
            error = $"{result} — {message}";

            return false;
        }

        _isInitialized       = true;
        Application.quitting += Shutdown;   // 에디터에서는 플레이를 끌 때 불린다

        return true;
    }

    // 로그인용 티켓을 받아 hex로 돌려준다. 실패하면 null과 사유(로그로 남긴다).
    public static async UniTask<(string? Ticket, string Error)> RequestTicketHexAsync(CancellationToken token)
    {
        if (!TryInit(out string error))
        {
            return (null, error);
        }

        GetTicketForWebApiResponse_t? response = null;

        using var callback = Callback<GetTicketForWebApiResponse_t>.Create(r => response = r);

        SteamUser.GetAuthTicketForWebApi(Identity);

        float deadline = Time.realtimeSinceStartup + TicketTimeoutSeconds;

        while (response == null)
        {
            if (Time.realtimeSinceStartup > deadline)
            {
                return (null, $"티켓 응답이 {TicketTimeoutSeconds}초 안에 오지 않았다");
            }

            SteamAPI.RunCallbacks();
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        var r = response.Value;

        if (r.m_eResult != EResult.k_EResultOK)
        {
            return (null, $"티켓 발급 실패 — {r.m_eResult}");
        }

        return (ToHex(r.m_rgubTicket, r.m_cubTicket), "");
    }

    private static string ToHex(byte[] bytes, int length)
    {
        var sb = new StringBuilder(length * 2);

        for (int i = 0; i < length; i++)
        {
            sb.Append(bytes[i].ToString("X2"));
        }

        return sb.ToString();
    }

    private static void Shutdown()
    {
        Application.quitting -= Shutdown;

        if (_isInitialized)
        {
            SteamAPI.Shutdown();
            _isInitialized = false;
        }
    }
}

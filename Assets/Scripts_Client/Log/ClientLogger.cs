using System.Collections.Generic;
using MikaNetwork;
using MikaProtocol;
using UnityEngine;

/// <summary>
/// 클라이언트 로그의 단일 창구. 태그를 붙여 내보내는 방법만 알고,
/// 무엇을 남길지는 모른다 — 프로젝트 전체가 여기로 로그를 낸다.
/// </summary>
/// <remarks>
/// 'PlayerDataLogger'와의 분담 · 태그 규약 · 레벨 기준은 'Log 규칙.md' 참조.
/// </remarks>
public static class ClientLogger
{
    public const string Send    = "↑송신";
    public const string Recv    = "↓수신";
    public const string Network = "연결";
    public const string Data    = "데이터";
    public const string UI      = "UI";

    /// <summary>
    /// 평시에 로그를 남기지 않는 패킷.
    /// ⚠️ 주기적으로 오가는 것만 넣는다 — 드물게 오는 패킷을 숨기면
    /// "안 온 것"과 "숨긴 것"을 구분할 수 없게 된다 ('Log 규칙.md' 3장).
    /// </summary>
    private static readonly HashSet<ushort> QuietPacketIds = new HashSet<ushort>
    {
        (ushort)PacketId.C_PingRequest,
        (ushort)PacketId.S_PongResponse,
    };

    // 송신 로그 훅 등록 (Unity 런타임 초기화 — 씬 로드 전 1회)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void HookPacketSent()
    {
        // Protocol 계층은 로거를 모른다. 훅만 뚫려 있고 채우는 건 호스트 몫이다
        // (MikaSessionPacketExtensions 주석 — 서버는 ServerLog로, Unity는 여기로).
        MikaSessionPacketExtensions.Sent += OnPacketSent;
    }

    // 패킷을 보낸 직후 호출된다 (MikaSessionPacketExtensions.Sent 구독)
    private static void OnPacketSent(ISession session, ushort packetId, int frameSize)
    {
        if (QuietPacketIds.Contains(packetId))
            return;

        // PacketId enum 값이 곧 전송 id라, 역매핑 테이블 없이 캐스팅만으로 이름이 나온다.
        Info(Send, $"{(PacketId)packetId} ({frameSize}B)");
    }

    /// <summary>정상 흐름을 알린다. 태그는 이 클래스의 상수를 쓴다.</summary>
    /// <param name="context">
    /// 원인이 된 씬 오브젝트. 넘기면 콘솔에서 클릭했을 때 하이라키에서 선택된다 —
    /// "어느 오브젝트냐"가 곧 원인인 로그(인스펙터 연결 누락 등)에는 반드시 넘긴다.
    /// </param>
    public static void Info(string tag, string message, UnityEngine.Object? context = null)
    {
        Debug.Log($"[{tag}] {message}", context);
    }

    /// <summary>동작은 계속되지만 의도와 다른 상황을 알린다 (실패 응답·빈 데이터 등).</summary>
    public static void Warn(string tag, string message, UnityEngine.Object? context = null)
    {
        Debug.LogWarning($"[{tag}] {message}", context);
    }

    /// <summary>고쳐야 하는 상황을 알린다 (연결 소실·참조 누락 등).</summary>
    public static void Error(string tag, string message, UnityEngine.Object? context = null)
    {
        Debug.LogError($"[{tag}] {message}", context);
    }
}

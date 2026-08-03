using System.Collections.Generic;
using MikaNetwork;
using MikaProtocol;
using UnityEngine;

/// <summary>
/// 클라이언트 로그의 <b>단일 창구</b>. 태그 규약을 한곳에 모아 콘솔을 읽을 수 있게 유지한다.
///
/// <para>
/// ■ 왜 <c>Common/</c>이 아니라 여기인가<br/>
/// <c>Common/</c>은 <c>Services</c>·<c>MonoService</c>처럼 <b>어느 프로젝트에 옮겨도 그대로 쓰는</b> 토대다.
/// 이 로거는 <see cref="PacketId"/>와 이 게임의 태그 약속을 안다 — 성격이 달라 자리를 나눴다.
/// </para>
///
/// <para>
/// ■ 태그<br/>
/// <c>[↑송신]</c> 보낸 패킷 · <c>[↓수신]</c> 받은 패킷 · <c>[연결]</c> 접속/끊김 ·
/// <c>[데이터]</c> 테이블 · <c>[UI]</c> 패널.
/// 화살표는 <b>방향을 눈으로 훑기 위한 것</b>이다 — 콘솔에서 ↑↓가 짝을 이루는지만 봐도 왕복이 보인다.
/// 방향 개념이 없는 태그에는 붙이지 않는다.
/// </para>
/// </summary>
public static class ClientLog
{
    public const string Send    = "↑송신";
    public const string Recv    = "↓수신";
    public const string Network = "연결";
    public const string Data    = "데이터";
    public const string UI      = "UI";

    /// <summary>
    /// 평시에 로그를 남기지 않는 패킷. <b>주기적으로 오가는 것만</b> 넣는다.
    ///
    /// <para>
    /// 하트비트는 5초마다 왕복하므로 그대로 찍으면 <b>콘솔이 Ping/Pong으로 덮여</b>
    /// 정작 봐야 할 로그가 밀려난다. 대신 <c>HeartbeatManager</c>가 <b>상태가 바뀔 때만</b>
    /// (끊김 감지·복구) <c>[연결]</c>로 남긴다 — 조용하면 정상이라는 뜻이다.
    /// </para>
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

    /// <summary>
    /// 정상 흐름을 알린다. 태그는 이 클래스의 상수를 쓴다.
    /// </summary>
    /// <param name="context">
    /// 원인이 된 씬 오브젝트. 넘기면 콘솔에서 로그를 클릭했을 때 하이라키에서 그 오브젝트가 선택된다 —
    /// 인스펙터 연결 누락처럼 <b>"어느 오브젝트냐"가 곧 원인</b>인 로그에는 반드시 넘긴다.
    /// </param>
    public static void Info(string tag, string message, UnityEngine.Object? context = null)
    {
        Debug.Log($"[{tag}] {message}", context);
    }

    /// <summary>동작은 계속되지만 <b>의도와 다른</b> 상황을 알린다 (실패 응답·빈 데이터 등).</summary>
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

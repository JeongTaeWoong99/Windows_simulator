using System;
using MikaNetwork;
using MikaProtocol;
using UnityEngine;

// 서버의 "지금"(게임 시계)을 클라에서 읽는 창구. 서버 시각과 PC 시각의 차이 하나만 든다.
// 남은 시간 · 삭제까지 · 채취 진행도처럼 서버가 준 시각과 비교하는 표시는 'DateTimeOffset.UtcNow' 대신 여기서 받는다.
//
// 왜 정적 클래스인가 · 언제 맞춰지는가는 'Clock 규칙.md'.
public static class ServerClock
{
    private static long _offsetMs;   // 서버 시각 - PC 시각 (밀리초). 시간 치트로 넘긴 만큼 + PC 시계 오차

    // 서버 시각과 PC 시각의 차이 — 치트 창이 "넘긴 시간"으로 보여 준다
    public static TimeSpan Offset => TimeSpan.FromMilliseconds(_offsetMs);

    // 게임 시계 기준 지금
    public static DateTimeOffset UtcNow => DateTimeOffset.UtcNow.AddMilliseconds(_offsetMs);

    public static long UtcNowUnixMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + _offsetMs;

    // 플레이마다 차이를 비우고 서버 시각 알림을 구독한다 (Unity 런타임 초기화 훅)
    // ※ 도메인 리로드를 끈 플레이에서도 이전 값·중복 구독이 남지 않도록 비우고 다시 건다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        _offsetMs = 0;

        ServerPacketHandler.ServerTimeReceived -= OnServerTimeReceived;
        ServerPacketHandler.ServerTimeReceived += OnServerTimeReceived;
    }

    // 서버 시각 도착 — 로그인 직후 · 시간 치트 직후 (ServerPacketHandler.ServerTimeReceived 구독)
    // 전송 지연(로컬 수 ms)은 무시한다 — 표시는 분 단위다.
    private static void OnServerTimeReceived(S_ServerTimeResponse res)
    {
        _offsetMs = res.ServerNowUnixMs - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}

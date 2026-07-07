#if UNITY_5_3_OR_NEWER

using System;
using MikaProtocol;
using UnityEngine;

namespace MikaNetwork
{
    /// <summary>
    /// 서버가 보낸 응답 패킷을 받는 진입점.
    /// - [PacketHandler] 메서드는 Source Generator가 파라미터 타입 기준으로 자동 등록한다.
    /// - 실제 실행은 NetworkMessageQueue를 거쳐 Unity 메인 스레드에서 이뤄지므로
    ///   여기서 이벤트 발행·UI 갱신을 그대로 해도 안전하다.
    /// - 이 클래스는 "얇게" 유지하고, 실제 처리는 이벤트를 구독하는 클라이언트 코드에서 한다.
    /// </summary>
    public static class ServerPacketHandler
    {
        // 로그인 응답 도착 (Handle_S_LoginResponse에서 발행)
        public static event Action<S_LoginResponse>? LoginResponded;

        // 인벤토리 스냅샷 도착 — 로그인 직후 서버가 밀어줌 (Handle_S_InventoryResponse에서 발행)
        public static event Action<S_InventoryResponse>? InventoryReceived;

        // 가챠 결과 도착 (Handle_S_GachaDrawResponse에서 발행)
        public static event Action<S_GachaDrawResponse>? GachaDrawn;

        // 로그인 응답 — 성공 여부·세션ID를 이벤트로 전달 (S_LoginResponse 수신 시 자동 호출)
        [PacketHandler]
        public static void Handle_S_LoginResponse(ISession session, S_LoginResponse res)
        {
            Debug.Log($"[Client] Recv Login: success={res.Success}, sessionId={res.SessionId}");
            LoginResponded?.Invoke(res);
        }

        // 인벤토리 전체 스냅샷 (S_InventoryResponse 수신 시 자동 호출)
        [PacketHandler]
        public static void Handle_S_InventoryResponse(ISession session, S_InventoryResponse res)
        {
            int itemCount = res.Items?.Count ?? 0;
            Debug.Log($"[Client] Recv Inventory: {itemCount} items");
            InventoryReceived?.Invoke(res);
        }

        // 가챠 결과 — 뽑힌 보상 목록 (S_GachaDrawResponse 수신 시 자동 호출)
        [PacketHandler]
        public static void Handle_S_GachaDrawResponse(ISession session, S_GachaDrawResponse res)
        {
            int rewardCount = res.Rewards?.Count ?? 0;
            Debug.Log($"[Client] Recv Gacha: success={res.Success}, {rewardCount} rewards");
            GachaDrawn?.Invoke(res);
        }

        #region 테스트용 (연결 확인) — 추후 필요 없어지면 삭제

        // 에코 응답 — 왕복 연결 테스트용 (S_EchoResponse 수신 시 자동 호출)
        [PacketHandler]
        public static void Handle_S_EchoResponse(ISession session, S_EchoResponse res)
        {
            Debug.Log($"[Client] Recv Echo: {res.Message}");
        }

        #endregion
    }
}

#endif

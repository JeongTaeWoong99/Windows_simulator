using System.Collections.Generic;
using UnityEngine;
using MikaProtocol;

/// <summary>
/// 로그인·가챠 패킷 송신 테스트용 UI.
/// - 버튼 OnClick에 SendLogin() / SendGachaSingle() / SendGachaTen()을 연결한다.
/// - 실제 요청·상태·응답은 SessionManager가 담당하고, 여기선 버튼 트리거 + 결과 로그만 본다.
/// - 서버(10050)에 접속된 상태여야 동작하며, 가챠는 로그인 먼저 해야 처리된다.
/// </summary>
public class PacketTestPanelUI : MonoBehaviour
{
    [CenterHeader("로그인")]
    [SerializeField, Tooltip("로그인에 사용할 계정 Id")]
    private string _loginId = "test";

    [CenterHeader("가챠")]
    [SerializeField, Tooltip("뽑을 가챠 풀 Id")]
    private int _gachaId = 1;

    // 세션 매니저(서비스 로케이터로 획득) — OnEnable에서 한 번 확보해 캐시한다.
    // 모든 MonoService의 Awake 등록이 끝난 뒤 OnEnable이 돌므로 이 시점엔 조회가 안전하다.
    private SessionManager _session = null!;

    // 결과 로그용 이벤트 구독 (Unity 메시지)
    private void OnEnable()
    {
        _session = Services.Get<SessionManager>();
        
        _session.LoginCompleted   += OnLoginCompleted;
        _session.InventoryChanged += OnInventoryChanged;
        _session.GachaCompleted   += OnGachaCompleted;
    }

    // 결과 로그용 이벤트 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        _session.LoginCompleted   -= OnLoginCompleted;
        _session.InventoryChanged -= OnInventoryChanged;
        _session.GachaCompleted   -= OnGachaCompleted;
    }

    #region 송신 (버튼 OnClick)

    // 로그인 요청 (LoginBtn OnClick에 할당)
    public void SendLogin()
    {
        _session.Login(_loginId);
        Debug.Log($"[Client] Send Login: id={_loginId}");
    }

    // 단차(1회) 가챠 요청 (GachaSingleBtn OnClick에 할당)
    public void SendGachaSingle()
    {
        _session.DrawGacha(_gachaId, 1);
        Debug.Log($"[Client] Send Gacha: gachaId={_gachaId}, drawCount=1");
    }

    // 10연차 가챠 요청 (GachaTenBtn OnClick에 할당)
    public void SendGachaTen()
    {
        _session.DrawGacha(_gachaId, 10);
        Debug.Log($"[Client] Send Gacha: gachaId={_gachaId}, drawCount=10");
    }

    #endregion

    #region 결과 로그 (SessionManager 이벤트 구독)

    // 로그인 결과 (LoginCompleted 구독)
    private void OnLoginCompleted(bool success)
    {
        Debug.Log($"[Client] 로그인 {(success ? "성공" : "실패")} — sessionId={_session.SessionId}");
    }

    // 인벤토리 갱신 (InventoryChanged 구독)
    private void OnInventoryChanged()
    {
        var inventory = _session.Inventory;
        if (inventory.Count == 0)
        {
            Debug.Log("[Client] 인벤토리 비어있음");
            return;
        }

        Debug.Log($"[Client] 인벤토리 {inventory.Count}종:");
        foreach (var item in inventory)
            Debug.Log($"    itemId={item.ItemId}, count={item.Count}");
    }

    // 가챠 결과 (GachaCompleted 구독)
    private void OnGachaCompleted(List<GachaRewardInfo> rewards)
    {
        Debug.Log($"[Client] 가챠 결과 {rewards.Count}개:");
        foreach (var reward in rewards)
            Debug.Log($"    itemId={reward.ItemId}, count={reward.Count}, rarity={reward.Rarity}");
    }

    #endregion
}

using System.Collections.Generic;
using MikaNetwork;
using MikaProtocol;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 가챠 패널 — 거래 열의 뽑기 화면. 지금은 요청 버튼 두 개(1회 · 10연차)뿐이다.
///
/// ■ 왜 거래 열인가
/// 가챠는 기획상 상점에 속한다 — 가챠 티켓이 골드 상점 품목이다.
/// → GameDesign/기획/거래/README.md 3.2
///
/// ■ 결과 내용은 여기서 보지 않는다
/// 뽑힌 보상·인벤토리 반영은 'PlayerDataModel'가 처리하고
/// 'PlayerDataLogger'가 콘솔에 풀어 준다. 결과 팝업이 생기면 이 패널의 자식으로 붙는다 — 일감 "가챠 결과 팝업".
/// 다만 성공/실패 도착 여부는 여기서 구독한다 — 요청 중 로딩·버튼 잠금·실패 알림을 위해서다.
/// </summary>
public class GachaPresenter : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("단차(1회) 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button singleDrawButton = null!;

    [SerializeField, Tooltip("10연차 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button tenDrawButton = null!;

    [CenterHeader("설정")]
    [SerializeField, Tooltip("뽑을 가챠 풀 Id")]
    private int gachaId = 1;

    private PlayerDataModel   _data    = null!;
    private NetworkManager    _network = null!;
    private ServerWaitManager _wait    = null!;

    // 진행 중인 가챠 대기의 손잡이. 응답이 오면 결과를 보고하고, 무응답이면 스스로 타임아웃돼 잠금을 푼다.
    private ServerWaitHandle? _waitHandle;

    // 응답을 기다리는 중인가 — 연타로 두 번 나가면 인벤토리·재화가 꼬이므로 버튼을 잠근다.
    private bool _isWaiting;

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 초기화 순서로 진행한다 (클라 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다(MonoService 주석).
    private void Start()
    {
        this.RequireRef(singleDrawButton, nameof(singleDrawButton));
        this.RequireRef(tenDrawButton,    nameof(tenDrawButton));

        _data    = Services.Get<PlayerDataModel>();
        _network = NetworkManager.Instance;
        _wait    = Services.Get<ServerWaitManager>();

        Subscribe();

        singleDrawButton.onClick.AddListener(() => Draw(1));
        tenDrawButton.onClick.AddListener(() => Draw(10));

        _isReady = true;
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    private void OnEnable()
    {
        if (_isReady)
            Subscribe();
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
    }

    #region 구독

    // 가챠 성공·실패 도착 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
            return;

        _isSubscribed        = true;
        _data.GachaCompleted += OnGachaCompleted;
        _data.GachaFailed    += OnGachaFailed;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
            return;

        _isSubscribed        = false;
        _data.GachaCompleted -= OnGachaCompleted;
        _data.GachaFailed    -= OnGachaFailed;
    }

    #endregion

    /// <summary>
    /// 가챠를 'drawCount'회 요청한다 (버튼 OnClick에 코드로 연결).
    ///
    /// 로그인 전에 보내면 서버가 User를 못 찾아 조용히 버린다 — 클라 입장에선 응답도 오류도
    /// 없어서 "눌렀는데 아무 일도 안 일어난다"로만 보인다. 보내기 전에 여기서 끊고 이유를 남긴다.
    /// </summary>
    private void Draw(int drawCount)
    {
        if (_isWaiting)
            return; // 앞 요청의 응답을 기다리는 중 — 연타 방지

        if (!_data.IsLoggedIn)
        {
            ClientLogger.Warn(ClientLogger.Send, "가챠 요청을 보내지 않았다 — 로그인이 먼저다(서버가 응답 없이 버린다)");
            return;
        }

        _network.Send(new C_GachaDrawRequest
        {
            GachaId   = gachaId,
            DrawCount = drawCount
        });

        ClientLogger.Info(ClientLogger.Send, $"가챠 요청 — 풀={gachaId}, {drawCount}회");

        // 대기 시작 — 로딩 표시·무응답 감시·알림은 ServerWaitManager가 공통으로 처리한다.
        _isWaiting  = true;
        SetButtons(false);
        _waitHandle = _wait.Begin($"가챠 {drawCount}회", onClosed: OnWaitClosed);
    }

    // 대기가 끝났다(성공·실패·타임아웃 공통) — 버튼을 다시 연다 (ServerWaitManager.Begin의 onClosed)
    private void OnWaitClosed()
    {
        _isWaiting  = false;
        _waitHandle = null;
        SetButtons(true);
    }

    // 가챠 성공 도착 — 대기를 조용히 닫는다. 보상 표시는 다른 곳이 맡는다 (PlayerDataModel.GachaCompleted 구독)
    private void OnGachaCompleted(List<GachaRewardInfo> rewards)
    {
        _waitHandle?.Succeed();
    }

    // 가챠 실패 도착 — 사유를 사람이 읽을 문구로 옮겨 알림에 띄운다 (PlayerDataModel.GachaFailed 구독)
    private void OnGachaFailed(EResultCode code)
    {
        _waitHandle?.Fail(ResultMessages.ToText(code));
    }

    // 두 뽑기 버튼을 한꺼번에 여닫는다 (요청 중 잠금 · 대기 종료 시 해제)
    private void SetButtons(bool interactable)
    {
        singleDrawButton.interactable = interactable;
        tenDrawButton.interactable    = interactable;
    }
}

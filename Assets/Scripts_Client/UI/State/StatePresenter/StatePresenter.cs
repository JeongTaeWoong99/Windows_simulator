using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상태 패널 — 계정 이름과 골드를 표시하고, 메인 화면을 갈아 끼우는 버튼들을 갖는다.
///
/// ■ 버튼은 여기 있고, 무엇을 열지는 여기서 안 정한다
/// 버튼은 이 패널의 위젯이라 여기가 쥐지만, 어느 캔버스가 열리는지는 모른다.
/// 'MainScreen' 값만 'UIManager'에 넘긴다 — 그래야 화면이 늘어도
/// 이 패널이 캔버스 참조를 하나씩 더 들고 있지 않아도 된다.
///
/// ■ 화면 버튼을 하나 더 붙이려면
/// 'MainScreen'에 값을 추가하고, 인스펙터의 'Screen Buttons'에 한 줄,
/// 'UI Manager'의 'Main Screens'에 한 줄 넣는다. 이 클래스는 고치지 않는다.
///
/// 닉네임은 아직 서버가 돌려주지 않는다. 로그인에 쓴 Id('PlayerDataModel.LoginId')를
/// 그대로 보여 주고, 닉네임 패킷이 생기면 그때 바꾼다.
/// </summary>
public class StatePresenter : MonoBehaviour
{
    /// <summary>화면 버튼 하나와 그 버튼이 여는 화면. 인스펙터에서 짝지어 넣는다.</summary>
    [Serializable]
    private struct ScreenButton
    {
        [Tooltip("상태 패널의 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
        public Button button;

        [Tooltip("이 버튼이 여는 화면. UI Manager의 Main Screens에 같은 값이 있어야 한다")]
        public MainScreen screen;
    }

    [CenterHeader("참조")]
    [SerializeField, Tooltip("계정 이름. 지금은 로그인 Id를 그대로 표시한다")]
    private TMP_Text nickNameText = null!;

    [SerializeField, Tooltip("골드 보유량")]
    private TMP_Text goldText = null!;

    // ※ NonReorderable — reorderable list 로 그려지면 Unity 가 그 위의 [CenterHeader] 를 건너뛴다
    //   (UI 규칙 §6). 이 배열은 순서에 의미가 없지만 헤더는 보여야 한다.
    [CenterHeader("화면 버튼")]
    [SerializeField, NonReorderable, Tooltip("누르면 그 화면으로 갈아 끼운다. 같은 화면이 열려 있으면 작업슬롯으로 돌아간다")]
    private ScreenButton[] screenButtons = new ScreenButton[0];

    private PlayerDataModel _data = null!;
    private UIManager       _ui   = null!;
    private bool            _isSubscribed;
    private bool            _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 초기화 순서로 진행한다 (클라 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다(MonoService 주석).
    private void Start()
    {
        // 필수 참조 검증 — 미연결이면 여기서 멈춘다(SettingPresenter와 같은 규칙).
        this.RequireRef(nickNameText, nameof(nickNameText));
        this.RequireRef(goldText,     nameof(goldText));

        _data = Services.Get<PlayerDataModel>();
        _ui   = Services.Get<UIManager>();

        Subscribe();
        BindScreenButtons();
        Refresh(); // 이미 통지를 받은 뒤에 켜졌을 수 있다

        _isReady = true;
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    //
    // ★ 재구독만으로는 부족하다 — 닫혀 있는 동안 온 재화 변경을 놓쳤기 때문이다.
    //   캐시는 계속 살아 있으므로 다시 그리기만 하면 즉시 맞는다.
    private void OnEnable()
    {
        if (!_isReady)
            return;

        Subscribe();
        Refresh();
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
    }

    #region 구독

    // 로그인·재화 변경 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
            return;

        _isSubscribed         = true;
        _data.CurrencyChanged += Refresh;
        _data.LoginCompleted  += OnLoginCompleted;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
            return;

        _isSubscribed         = false;
        _data.CurrencyChanged -= Refresh;
        _data.LoginCompleted  -= OnLoginCompleted;
    }

    #endregion

    #region 화면 버튼

    /// <summary>
    /// 화면 버튼을 'UIManager'에 묶는다 (Start에서 한 번).
    ///
    /// ⚠️ 반복 변수를 람다에 그대로 넘기면 모든 콜백이 마지막 값을 본다. 복사본을 캡처한다
    /// ('WorkStationSelectPresenter.BindIndustryButtons'와 같은 이유).
    /// </summary>
    private void BindScreenButtons()
    {
        foreach (var entry in screenButtons)
        {
            if (entry.button == null)
            {
                ClientLogger.Warn(ClientLogger.UI,
                    $"화면 버튼 줄에 Button이 비어 있다 (화면={entry.screen}). 인스펙터를 확인할 것.", this);
                continue;
            }

            MainScreen screen = entry.screen;
            entry.button.onClick.AddListener(() => _ui.ToggleMainScreen(screen));
        }
    }

    #endregion

    #region 표시

    // 로그인 결과 도착 — 성공했을 때만 이름을 갱신한다 (LoginCompleted 구독)
    private void OnLoginCompleted(bool success)
    {
        if (success)
            Refresh();
    }

    // 이름·골드를 현재 값으로 갱신한다 (CurrencyChanged 구독 · 로그인 시)
    private void Refresh()
    {
        nickNameText.text = string.IsNullOrEmpty(_data.LoginId) ? "-" : _data.LoginId;
        goldText.text     = _data.Gold.ToString("N0"); // 천 단위 구분
    }

    #endregion
}

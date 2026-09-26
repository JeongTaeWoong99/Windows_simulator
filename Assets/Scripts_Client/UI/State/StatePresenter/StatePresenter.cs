using System;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 상태 패널 — 계정 이름과 재화(골드·다이아)를 표시하고, 메인 화면을 갈아 끼우는 버튼들을 갖는다.
//
// ■ 버튼은 여기 있고, 무엇을 열지는 여기서 안 정한다
// 버튼은 이 패널의 위젯이라 여기가 쥐지만, 어느 캔버스가 열리는지는 모른다.
// 'MainScreen' 값만 'UIManager'에 넘긴다 — 그래야 화면이 늘어도
// 이 패널이 캔버스 참조를 하나씩 더 들고 있지 않아도 된다.
//
// ■ 화면 버튼을 하나 더 붙이려면
// 'MainScreen'에 값을 추가하고, 인스펙터의 'Screen Buttons'에 한 줄,
// 'UI Manager'의 'Main Screens'에 한 줄 넣는다. 이 클래스는 고치지 않는다.
//
// 닉네임은 아직 서버가 돌려주지 않는다. 로그인에 쓴 Id('PlayerDataModel.LoginId')를
// 그대로 보여 주고, 닉네임 패킷이 생기면 그때 바꾼다.
//
// ■ 계정 레벨·경험치 바가 여기 사는 이유
// 계정 축(레벨·경험치·재화)은 어느 화면을 보고 있든 늘 보여야 하는 값이고, 이 패널이
// 그 축을 담는 유일한 상주 자리다. **남은 특성 포인트는 여기 두지 않는다** — 쓰는 곳이
// 특성 화면 하나뿐이라 그 화면 머리에 있다('TraitPresenter').
public class StatePresenter : MonoBehaviour
{
    // 화면 버튼 하나와 그 버튼이 여는 화면. 인스펙터에서 짝지어 넣는다.
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

    [SerializeField, Tooltip("다이아 보유량. ⏸ 지급·차감 경로가 없어 늘 0이다")]
    private TMP_Text diaText = null!;

    [CenterHeader("계정 레벨")]
    [SerializeField, Tooltip("닉 아이콘 아래쪽에 겹치는 레벨 배지 문구")]
    private TMP_Text levelText = null!;

    [SerializeField, Tooltip("닉 아이콘을 두르는 원형 진행도. Image Type을 Filled · Radial360으로 두고 fillAmount로 채운다")]
    private Image expFill = null!;

    [SerializeField, Tooltip("진행도 퍼센트 문구. ⏸ 씬에서 꺼 둔 상태다 — 배선만 살아 있어 켜면 바로 그려진다")]
    private TMP_Text expPercentText = null!;

    // ※ NonReorderable — reorderable list 로 그려지면 Unity 가 그 위의 [CenterHeader] 를 건너뛴다
    //   ('UI 규칙.md'의 "공통 작성 규약"). 이 배열은 순서에 의미가 없지만 헤더는 보여야 한다.
    [CenterHeader("화면 버튼")]
    [SerializeField, NonReorderable, Tooltip("누르면 그 화면으로 갈아 끼운다. 같은 화면이 열려 있으면 작업슬롯으로 돌아간다")]
    private ScreenButton[] screenButtons = new ScreenButton[0];

    // ※ 우편 버튼 자체는 위 배열의 한 줄이다 — 여기 드는 건 그 위에 겹친 점 하나뿐이다.
    //   숫자·기한·토스트 없이 점만 둔다(기획 '우편' 1장 18번 — 'GameDesign/design/mail/README.md').
    [CenterHeader("우편")]
    [SerializeField, Tooltip("우편 버튼 오른쪽 위의 점. 안 받은 우편이 있을 때만 켜진다")]
    private GameObject mailDot = null!;

    // ※ 화면 전환 버튼(위)과 역할이 다르다 — 화면을 여는 게 아니라 앱을 즉시 끈다. 그래서 배열이 아니라
    //   전용 필드로 가른다. 타이틀바를 끄면 창 'X'가 없어져 명시적 종료 출구가 필요하다.
    [CenterHeader("시스템")]
    [SerializeField, Tooltip("누르면 앱을 종료한다. OnClick은 코드가 연결한다")]
    private Button quitButton = null!;

    private PlayerDataModel _data = null!;
    private UIManager       _ui   = null!;
    private bool            _isSubscribed;
    private bool            _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 초기화 순서로 진행한다 (클라 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다(MonoService 주석).
    private void Start()
    {
        // 필수 참조 검증 — 미연결이면 여기서 멈춘다(SettingPresenter와 같은 규칙).
        this.RequireRef(nickNameText,   nameof(nickNameText));
        this.RequireRef(goldText,       nameof(goldText));
        this.RequireRef(diaText,        nameof(diaText));
        this.RequireRef(levelText,      nameof(levelText));
        this.RequireRef(expFill,        nameof(expFill));
        this.RequireRef(expPercentText, nameof(expPercentText));
        this.RequireRef(quitButton,     nameof(quitButton));
        this.RequireRef(mailDot,        nameof(mailDot));

        _data = Services.Get<PlayerDataModel>();
        _ui   = Services.Get<UIManager>();

        Subscribe();
        BindScreenButtons();
        quitButton.onClick.AddListener(Services.Get<WindowManager>().QuitApplication); // 종료는 WindowManager가 단일 경로(ESC와 공유)
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
        {
            return;
        }

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
        {
            return;
        }

        _isSubscribed              = true;
        _data.CurrencyChanged     += Refresh;
        _data.AccountLevelChanged += Refresh;
        _data.LoginCompleted      += OnLoginCompleted;
        _data.MailsChanged        += RefreshMailDot;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed              = false;
        _data.CurrencyChanged     -= Refresh;
        _data.AccountLevelChanged -= Refresh;
        _data.LoginCompleted      -= OnLoginCompleted;
        _data.MailsChanged        -= RefreshMailDot;
    }

    #endregion

    #region 화면 버튼

    // 화면 버튼을 'UIManager'에 묶는다 (Start에서 한 번).
    //
    // ⚠️ 반복 변수를 람다에 그대로 넘기면 모든 콜백이 마지막 값을 본다. 복사본을 캡처한다
    // ('WorkStationSelectPresenter.BindIndustryButtons'와 같은 이유).
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
    private void OnLoginCompleted(bool success, EResultCode code)
    {
        if (success)
        {
            Refresh();
        }
    }

    // 이름·재화·계정 레벨을 현재 값으로 갱신한다 (CurrencyChanged · AccountLevelChanged 구독 · 로그인 시)
    private void Refresh()
    {
        nickNameText.text = string.IsNullOrEmpty(_data.LoginId) ? "-" : _data.LoginId;
        goldText.text     = _data.Gold.ToString("N0"); // 천 단위 구분
        diaText.text      = _data.Dia.ToString("N0");

        RefreshAccountLevel();
        RefreshMailDot();
    }

    // 안 받은 우편이 있으면 점을 켠다 (MailsChanged 구독 · Refresh에서 호출)
    private void RefreshMailDot()
    {
        mailDot.SetActive(_data.HasUnclaimedMail);
    }

    // 레벨 배지와 경험치 진행도를 그린다 (Refresh에서 호출).
    //
    // ※ 'expPercentText'는 씬에서 꺼 둔 오브젝트다 — 꺼진 오브젝트에 문구를 넣어도 문제없고,
    //   켜는 순간 맞는 값이 이미 들어 있다. 여기서 켜짐 여부를 분기하지 않는다.
    //
    // ※ 분모는 **다음 레벨** 행의 'RequiredExp'다 — 'Exp'가 "현재 레벨에서 쌓은 양"이라
    //   지금 레벨 행을 나누면 이미 지나온 구간으로 나누게 된다(캐릭터 게이지와 같은 축).
    // ※ 행이 없으면 만렙이다 — 오류가 아니므로 경고하지 않고 바를 가득 채운다.
    private void RefreshAccountLevel()
    {
        levelText.text = $"Lv.{_data.AccountLevel}";

        if (!GameDataLoader.TryGetAccountRequiredExp(_data.AccountLevel + 1, out long required) || required <= 0L)
        {
            expFill.fillAmount  = 1f;
            expPercentText.text = "MAX";

            return;
        }

        float progress = Mathf.Clamp01((float)((double)_data.AccountExp / required));

        expFill.fillAmount  = progress;
        expPercentText.text = $"{progress * 100f:0.00}%";
    }

    #endregion
}

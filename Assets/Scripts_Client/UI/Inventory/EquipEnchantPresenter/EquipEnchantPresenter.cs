using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using GameData;
using MikaNetwork;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 큐브 창 — 장비 하나에 인챈트 큐브를 써서 능력치 칸을 다시 뽑는다 (T-095 · 이슈 #46).
//
// ■ 판매 목록 자리를 빌려 쓴다 (2026-10-03 사용자 결정)
// 장비 탭에서 장비를 **좌클릭**하면 판매 목록이 비워지며 물러나고 이 창이 그 자리에 열린다.
// 닫기 · 다른 탭 · 우클릭(판매 담기)이면 물러나고 판매 목록이 돌아온다.
// 판매 목록은 'OpenChanged'를 듣고 스스로 물러난다 — 이 창이 판매 목록을 쥐지 않는다.
//
// ■ 규칙 (서버 'EnchantCatalog' · 이슈 #46 서버 코멘트)
//   - 칸 수 = **장비 등급**(일반·고급 1 · 희귀·영웅 2 · 전설·신화 3). 인챈트 등급은 장비 하나에 하나다.
//   - 첫 사용은 판정 없이 **일반**으로 칸을 채운다. 그다음부터는 확률로 **한 단계** 오르고, 내려가지 않는다.
//   - 어느 쪽이든 **칸 전부를 다시 뽑는다**(칸 선택 없음). 같은 옵션이 겹쳐 나올 수 있다.
//   - 착용 중이면 서버가 거절한다(EnchantEquipped) → [해제하고 사용]이 해제부터 보낸다(사용자 결정 D4-B).
// 이 규칙은 문서를 안 읽으면 화면만 보고 알 수 없었다 — 한 줄 요약(규칙 줄)을 늘 보이고,
// 전체 규칙과 확률 표는 규칙 줄·큐브 버튼에 올리면 툴팁으로 펼친다('EquipLabel.BuildCubeRuleTooltip').
//
// ■ 결과는 응답이 그린다
// 이전 칸은 **보낼 때** 복사해 둔다(응답에 없다). 이후 칸·등급은 'S_EquipEnchantResponse'의 값이다.
// 캐시는 'S_EquipSyncResponse'가 따로 갱신한다 — 칸 네모·툴팁은 그쪽을 따라간다.
//
// ■ 상급 큐브는 고른다 (이슈 #53 · T-127)
// 상급 큐브('EnchantItemTable.CanKeepPrevious')는 결과를 장비에 넣지 않고 **보류**한다. 보류는 장비 캐시
// ('EquipInfo.PendingEnchantGrade'·'PendingEnchantOptions')에 실려 오므로 **응답이 아니라 캐시로 그린다** — 재접속해도 같다.
//   - BEFORE(지금 장비) · AFTER(보류된 새 값) 머리에 [선택]이 하나씩 붙는다 → 'C_EquipEnchantChooseRequest'.
//   - 고르지 않고 [사용]을 누르면 BEFORE를 남기고 다시 굴린다 — 서버는 보류 중 큐브를 거절하므로(EnchantPending)
//     **고르기(이전 값) → 큐브 사용**을 차례로 보낸다(해제하고 사용과 같은 두 단계).
//   - 고르기 전에는 **창을 떠날 수 없다** — 닫기 · 탭 · 다른 장비 좌클릭 · 우클릭 담기를 막고 알림을 띄운다(사용자 결정).
//     게임 종료처럼 막을 수 없는 경우엔 보류가 남고, 장비 칸 '선택' 배지로 다시 연다('EquipSlotSource.GetBadge').
//
// ■ 자동 — 사람이 누르는 흐름을 그대로, 2배 빠르기로 (이슈 #53)
// [자동]이 비교 상자 자리에 설정 칸('EnchantAutoPanelView' — 목표 등급 · 노릴 옵션 · 조합 · 상한)을 같은 높이로 갈아 끼우고,
// 규칙 줄은 예상 확률('EnchantForecast')을 보인다. [자동 시작]이면 **수동과 같은 요청**(큐브 사용 → 상급이면 고르기)을 차례로 보낸다.
//   - 목표 판정 · 상급 큐브의 남기기는 'EnchantAutoTarget'이 한다(오르면 새 값, 같으면 노린 칸이 많을 때만).
//   - 멈춤: 목표 달성 · 상한 · 큐브 소진 · [■ 멈춤] · 창 닫기·탭 · 서버 거절. 상급 큐브가 목표에 닿으면 보류를 남기고 멈춘다 — 사람이 [선택]한다.
//   - 착용 중 · 고르기 전 장비는 자동을 열지 않는다(먼저 벗기거나 고르게 알린다).
//
// ■ 공개 연출 — AFTER 상자의 'EnchantRevealFx' (이슈 #53)
// 응답이 오면 AFTER 칸이 같은 등급의 가짜 옵션으로 돌다 멈추고, 오르면 도장 · 번짐(영웅↑ 별 조각 · 신화 빛 줄).
// 돌기 중에는 AFTER 글씨를 연출이 쥐고 있다 — 그래서 AFTER 글씨는 늘 'EnchantRevealFx.SetResult'로 쓴다.
// 자동은 같은 연출을 2배 빠르기로 틀고, 연출이 끝나야 다음 큐브를 누른다. 목표에 닿으면 초록 테두리 + 입자.
// BEFORE 상자도 같은 컴포넌트를 쓴다 — 흐린 등급 테두리 · [선택] 뒤 금색 번쩍. 눈길은 AFTER로(진한 테두리 + 도는 빛 — 사용자 결정 2026-10-11).
//   - 공개 중(줄이 다 멈추기 전)엔 큐브 · [사용] · [선택] · [자동]을 잠근다 — 뽑기처럼 결과를 보기 전에 다시 누르지 못한다.
//   - 상급 큐브로 **등급이 오르면 새 값을 알아서 고른다**(수동·자동 모두) — 오른 등급을 버릴 까닭이 없다(사용자 결정 2026-10-11).
public class EquipEnchantPresenter : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("인벤토리 탭 줄. 장비 탭을 떠나면 이 창이 닫힌다")]
    private InventoryTabPresenter inventoryTabs = null!;

    [SerializeField, Tooltip("장비 아이콘 칸 (ItemIconView 프리팹) — 칸 네모까지 그린다")]
    private ItemIconView itemIcon = null!;

    [SerializeField, Tooltip("장비 이름 — 등급색")]
    private TMP_Text nameText = null!;

    [SerializeField, Tooltip("이름 아래 한 줄 — '희귀 무기 · 기본 농사 +30% · 능력치 칸 2개'")]
    private TMP_Text infoText = null!;

    [SerializeField, Tooltip("판매 목록으로 돌아가는 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button closeButton = null!;

    [CenterHeader("비교 상자")]
    [SerializeField, Tooltip("왼쪽 상자 제목 — 'BEFORE · 지금 장비 · 희귀' / 쓴 뒤 'BEFORE · 희귀'")]
    private TMP_Text beforeTitleText = null!;

    [SerializeField, Tooltip("왼쪽 상자 칸 목록 (칸마다 한 줄)")]
    private TMP_Text beforeOptionsText = null!;

    [SerializeField, Tooltip("왼쪽 상자 머리의 [선택] — 상급 큐브 결과를 고르기 전에만 켜진다. OnClick은 코드가 연결한다")]
    private Button keepBeforeButton = null!;

    [SerializeField, Tooltip("오른쪽 상자 제목 — 'AFTER · 등급 상승! 희귀 ▶ 영웅'. 상자는 늘 켜져 있다 — 크기는 그대로, 내용만 바뀐다")]
    private TMP_Text afterTitleText = null!;

    [SerializeField, Tooltip("오른쪽 상자 칸 목록 — 쓰기 전에는 흐린 안내 한 줄")]
    private TMP_Text afterOptionsText = null!;

    [SerializeField, Tooltip("오른쪽 상자 머리의 [선택] — 상급 큐브 결과를 고르기 전에만 켜진다. OnClick은 코드가 연결한다")]
    private Button keepAfterButton = null!;

    [SerializeField, Tooltip("왼쪽 상자의 표시 — 등급색 테두리 · [선택] 뒤 금색 번쩍. BEFORE 글씨는 이것을 거쳐 쓴다")]
    private EnchantRevealFx beforeFx = null!;

    [SerializeField, Tooltip("오른쪽 상자의 공개 연출 — 칸 돌기 · 등급 상승 도장. AFTER 글씨는 이것을 거쳐 쓴다")]
    private EnchantRevealFx afterFx = null!;

    [CenterHeader("규칙 · 큐브")]
    [SerializeField, Tooltip("한 줄 요약 — 이번에 쓰면 무엇이 되나. 전체 규칙·확률은 옆 도움말 아이콘")]
    private TMP_Text ruleText = null!;

    [SerializeField, Tooltip("규칙 줄 옆 도움말 아이콘(HelpIcon 프리팹)의 툴팁 트리거 — 큐브 규칙·확률 표")]
    private TooltipTrigger ruleTooltip = null!;

    [SerializeField, NonReorderable, Tooltip("큐브 버튼 — 'EnchantItemTable' 순서로 채운다. 남는 버튼은 꺼진다")]
    private EnchantCubeView[] cubeViews = new EnchantCubeView[0];

    [SerializeField, Tooltip("[큐브 사용] 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button confirmButton = null!;

    [SerializeField, Tooltip("버튼 글씨 — '상급 인챈트 큐브 사용'. 코드가 채운다")]
    private TMP_Text confirmLabel = null!;

    [CenterHeader("자동")]
    [SerializeField, Tooltip("비교 상자 줄(BEFORE · AFTER) — 자동 설정 중에는 꺼지고 같은 자리에 설정 칸이 켜진다")]
    private GameObject compareRow = null!;

    [SerializeField, Tooltip("자동 설정 칸 — 비교 상자와 같은 높이")]
    private EnchantAutoPanelView autoPanel = null!;

    [SerializeField, Tooltip("[자동] · [취소] · [■ 멈춤] 버튼 — [큐브 사용] 오른쪽. OnClick은 코드가 연결한다")]
    private Button autoButton = null!;

    [SerializeField, Tooltip("자동 버튼 글씨. 코드가 채운다")]
    private TMP_Text autoLabel = null!;

    [CenterHeader("색")]
    [SerializeField, Tooltip("[큐브 사용] — 지금 쓸 수 있을 때")]
    private UIThemeRole confirmReadyRole = UIThemeRole.ButtonPrimary;

    [SerializeField, Tooltip("[큐브 사용] — 큐브가 없거나 응답을 기다릴 때. 눌리기는 한다(누르면 이유 알림)")]
    private UIThemeRole confirmBlockedRole = UIThemeRole.ButtonDisabled;

    // 사람이 한 번 눌러 결과를 보는 시간(연출 포함)의 어림 — 자동은 이것을 2배 빠르게 돈다(사용자 결정 2026-10-10 — 처음 1.5배에서 올렸다).
    private const float ManualRevealSeconds = 0.9f;
    private const float AutoSpeed           = 2f;

    // 상급 큐브 자동이 [선택]을 누른 뒤 다음 굴림까지 (사람 기준 — 자동은 AutoSpeed로 나눈다).
    private const float ManualChooseSeconds = 0.3f;

    // 자동이 [큐브 사용]을 누르는 손짓 — 버튼이 살짝 눌렸다 돌아온다.
    private const float AutoPressPunch   = -0.06f;

    // 초록 [자동 시작] · 빨강 [■ 멈춤] 바탕을 의미색에서 어둡게 누르는 정도 — 흰 글씨가 읽히게
    private const float SignalButtonDarken = 0.35f;
    private const float AutoPressSeconds = 0.15f;

    // 자동 단계 — 꺼짐 · 설정 중(비교 상자 자리에 설정 칸) · 도는 중.
    private enum AutoState
    {
        Off,
        Setting,
        Running,
    }

    // 방금 쓴 결과 — 이전 ▶ 이후. 장비를 바꾸거나 창을 닫으면 버린다.
    private sealed class EnchantResult
    {
        public GlobalRarity BeforeGrade;
        public GlobalRarity AfterGrade;
        public bool         RankedUp;
        public readonly List<int> BeforeOptions = new List<int>();
        public readonly List<int> AfterOptions  = new List<int>();
    }

    // 창이 열렸는가. 판매 목록이 이걸 보고 물러난다.
    public bool IsOpen => _equipId != 0L;

    // 열림이 바뀌었다 ('SellCartPresenter'가 구독 — 열리면 카트를 비우고 물러난다).
    public event Action<bool>? OpenChanged;

    private readonly List<EnchantOptionTableRow?> _options = new List<EnchantOptionTableRow?>();
    private readonly List<int>                    _sending = new List<int>();
    private readonly List<EnchantOptionTableRow>  _rolling = new List<EnchantOptionTableRow>(); // 돌기 중 끼울 가짜 줄 — '_rollingGrade' 등급만
    private GlobalRarity                          _rollingGrade = (GlobalRarity)byte.MaxValue;
    private readonly StringBuilder                _text    = new StringBuilder();

    private PlayerDataModel   _data    = null!;
    private UIManager         _ui      = null!;
    private NetworkManager    _network = null!;
    private ServerWaitManager _wait    = null!;

    private long           _equipId;      // 창에 띄운 장비 개체. 0이면 닫혀 있다
    private int            _cubeIndex;    // 고른 큐브 — 'GameDataLoader.EnchantCubes'의 순서
    private EnchantResult? _result;       // 지금 장비에 방금 쓴 결과. 없으면 '현재' 상자만 그린다

    // 진행 중인 요청 — 해제(D4-B의 첫 단계) 또는 큐브 사용. null이면 기다리는 것이 없다.
    private ServerWaitHandle? _waitHandle;
    private long _unequipThenEnchantId;   // 해제 응답을 기다리는 장비. 0이면 해제를 보내지 않았다
    private long _enchantingEquipId;      // 큐브 응답을 기다리는 장비. 0이면 보내지 않았다
    private int  _enchantingCubeTid;
    private long _choosingEquipId;        // 고르기 응답을 기다리는 장비. 0이면 보내지 않았다
    private int  _enchantAfterChooseTid;  // 고르기가 끝나면 이어 쓸 큐브 — 보류 중 [사용](BEFORE 유지 + 다시). 0이면 고르기만
    private bool _choosingKeepNew;        // 보낸 고르기가 새 값(AFTER)인가 — 응답 뒤 비교 상자를 어떻게 그릴지
    private long _chosenEquipId;          // 고르기가 받아들여졌는데 캐시는 아직 보류 중인 장비 — 동기화가 오면 0. 'IsPending' 참고

    // 자동 — 목표는 창을 닫아도 남는다(다음 장비에서도 같은 설정으로 시작한다).
    private readonly EnchantAutoTarget _autoTarget = new EnchantAutoTarget();
    private AutoState                  _auto;
    private CancellationTokenSource?   _autoCts;
    private int                        _autoUsed;
    private int                        _autoCap;
    private string?                    _autoSummary;   // 자동이 끝난 뒤 규칙 줄에 남길 한 줄. 다음 조작에서 지운다
    private Tween?                     _autoPress;     // 자동의 누름 손짓 — 겹치면 크기가 어긋나 다시 누르기 전에 끊는다
    private S_EquipEnchantResponse?    _lastEnchant;   // 자동이 기다린 큐브 응답
    private EResultCode?               _lastChoose;    // 자동이 기다린 고르기 응답

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 배선 → 초기화 순서로 진행한다 (클라 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다.
    private void Start()
    {
        this.RequireRef(inventoryTabs,     nameof(inventoryTabs));
        this.RequireRef(itemIcon,          nameof(itemIcon));
        this.RequireRef(nameText,          nameof(nameText));
        this.RequireRef(infoText,          nameof(infoText));
        this.RequireRef(closeButton,       nameof(closeButton));
        this.RequireRef(beforeTitleText,   nameof(beforeTitleText));
        this.RequireRef(beforeOptionsText, nameof(beforeOptionsText));
        this.RequireRef(keepBeforeButton,  nameof(keepBeforeButton));
        this.RequireRef(afterTitleText,    nameof(afterTitleText));
        this.RequireRef(afterOptionsText,  nameof(afterOptionsText));
        this.RequireRef(keepAfterButton,   nameof(keepAfterButton));
        this.RequireRef(afterFx,           nameof(afterFx));
        this.RequireRef(beforeFx,          nameof(beforeFx));
        this.RequireRef(ruleText,          nameof(ruleText));
        this.RequireRef(ruleTooltip,       nameof(ruleTooltip));
        this.RequireRef(confirmButton,     nameof(confirmButton));
        this.RequireRef(confirmLabel,      nameof(confirmLabel));
        this.RequireRef(compareRow,        nameof(compareRow));
        this.RequireRef(autoPanel,         nameof(autoPanel));
        this.RequireRef(autoButton,        nameof(autoButton));
        this.RequireRef(autoLabel,         nameof(autoLabel));

        if (cubeViews.Length == 0)
        {
            throw new InvalidOperationException($"{name}: 'cubeViews'가 비어 있다 — 큐브 버튼을 인스펙터에 넣을 것.");
        }

        _data    = Services.Get<PlayerDataModel>();
        _ui      = Services.Get<UIManager>();
        _network = NetworkManager.Instance;
        _wait    = Services.Get<ServerWaitManager>();

        closeButton.onClick.AddListener(() => TryClose());
        confirmButton.onClick.AddListener(OnConfirmClicked);
        keepBeforeButton.onClick.AddListener(() => OnKeepClicked(false));
        keepAfterButton.onClick.AddListener(() => OnKeepClicked(true));
        autoButton.onClick.AddListener(OnAutoClicked);
        ruleTooltip.SetProvider(EquipLabel.BuildCubeRuleTooltip);

        autoPanel.GradeClicked += OnAutoGradeClicked;
        autoPanel.FocusClicked += OnAutoFocusClicked;
        autoPanel.ComboClicked += OnAutoComboClicked;
        autoPanel.CapClicked   += OnAutoCapClicked;

        for (int i = 0; i < cubeViews.Length; i++)
        {
            cubeViews[i].Clicked += OnCubeClicked;
        }

        // ⚠️ 탭·응답 구독은 Start/OnDestroy에 건다 — 이 창은 자기 오브젝트를 끈다.
        //    응답이 늦게 오는 사이 창을 닫아도 대기 손잡이가 닫혀야 로딩이 남지 않는다(격자의 상자 개봉과 같은 이유).
        inventoryTabs.TabChanged          += OnTabChanged;
        _data.EquipEnchantCompleted       += OnEnchantCompleted;
        _data.EquipEnchantChooseCompleted += OnChooseCompleted;
        _data.EquipCompleted              += OnUnequipCompleted;
        afterFx.Revealed                  += Redraw; // 공개가 끝나면 버튼을 풀고, 오른 등급이면 고른다
        inventoryTabs.AddLeaveGuard(BlockLeave);

        _isReady = true;

        Subscribe();

        // 닫힌 채 깨어났으면(씬에 켜진 채 저장됨) 바로 물러난다 — 판매 목록 자리를 비워 두면 안 된다.
        if (!IsOpen)
        {
            gameObject.SetActive(false);

            return;
        }

        Redraw();
    }

    // 탭·응답 구독 해제 (Unity 메시지). 자기 오브젝트를 끄므로 여기서만 푼다.
    private void OnDestroy()
    {
        if (!_isReady)
        {
            return;
        }

        inventoryTabs.TabChanged          -= OnTabChanged;
        _data.EquipEnchantCompleted       -= OnEnchantCompleted;
        _data.EquipEnchantChooseCompleted -= OnChooseCompleted;
        _data.EquipCompleted              -= OnUnequipCompleted;
        afterFx.Revealed                  -= Redraw;

        StopAuto();
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    // ★ 닫혀 있는 동안 큐브 수·장비가 바뀌었을 수 있다 — 다시 그린다.
    private void OnEnable()
    {
        if (!_isReady)
        {
            return;
        }

        Subscribe();
        Redraw();
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
    }

    #region 열고 닫기

    // 이 장비로 창을 연다 (인벤토리 격자가 장비 칸 좌클릭에서 호출).
    //
    // ※ 아직 'Start'가 안 돌았을 수 있다(꺼진 채 저장된 창) — 장비만 기억하고 켜면 'Start'가 그린다.
    public void Open(long equipId)
    {
        if (equipId == 0L || equipId == _equipId)
        {
            return;
        }

        // 지금 장비가 상급 큐브 결과를 고르기 전이면 다른 장비로 바꾸지 않는다.
        if (BlockLeave())
        {
            return;
        }

        bool wasOpen = IsOpen;

        StopAuto();
        afterFx.Stop();
        beforeFx.Stop();

        _equipId     = equipId;
        _result      = null; // 다른 장비의 결과를 이 장비에 그리지 않는다
        _auto        = AutoState.Off;
        _autoSummary = null;
        _autoTarget.ResetCombos(); // 칸 수가 다를 수 있다

        gameObject.SetActive(true);

        if (_isReady)
        {
            Redraw();
        }

        if (!wasOpen)
        {
            OpenChanged?.Invoke(true);
        }
    }

    // 사용자가 창을 닫으려 한다 — 상급 큐브 결과를 고르기 전이면 막고 알린다 (닫기 버튼 · 격자의 우클릭 판매 담기).
    //   반환 : 닫혔거나 원래 닫혀 있었으면 true, 막았으면 false
    public bool TryClose()
    {
        if (BlockLeave())
        {
            return false;
        }

        Close();

        return true;
    }

    // 고르기 전이라 떠날 수 없으면 알리고 true (TryClose · Open · 탭 줄의 떠나기 조건).
    // ※ 아직 'Start'가 안 돌았으면 열린 적이 없다 — 'IsOpen'이 먼저 걸러 '_data'를 보지 않는다.
    // ※ 자동이 도는 중이면 먼저 멈춘다 — 떠나려는 것은 멈추려는 것이다. 그 뒤에도 고르기 전이면 막는다.
    private bool BlockLeave()
    {
        StopAuto();

        if (!HasPendingChoice)
        {
            return false;
        }

        _wait.RaiseNotice("아직 선택하지 않았습니다.\nBEFORE · AFTER 중 하나를 골라 주세요.");

        return true;
    }

    // 창에 띄운 장비가 상급 큐브 결과를 고르기 전인가 — 보류는 장비 캐시에 실려 온다.
    private bool HasPendingChoice => IsOpen && _data.FindEquip(_equipId) is { } equip && IsPending(equip);

    // 그 장비가 아직 고르기 전인가 — 캐시의 보류를 보되, 이미 고른 장비는 뺀다.
    // ⚠️ 고르기 응답이 장비 동기화보다 먼저 온다 — 그 틈에 캐시만 보면 고르기를 한 번 더 보내 서버가 거절했다(EnchantNoPending, 2026-10-11).
    private bool IsPending(EquipInfo equip) => equip.PendingEnchantGrade > 0 && _chosenEquipId != equip.EquipId;

    // 창을 닫고 판매 목록에 자리를 돌려준다 — 막지 않는다 (탭 전환 · 장비가 사라졌을 때 · TryClose).
    // ※ 탭 전환은 탭 줄이 떠나기 조건('BlockLeave')을 먼저 물어, 고르기 전이면 탭이 바뀌지 않는다.
    // ※ 응답을 기다리는 중이어도 닫는다 — 대기 손잡이는 응답 구독(Start/OnDestroy)이 닫는다.
    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        StopAuto();
        afterFx.Stop();
        beforeFx.Stop();

        _equipId       = 0L;
        _chosenEquipId = 0L;
        _result        = null;
        _auto          = AutoState.Off;
        _autoSummary   = null;

        gameObject.SetActive(false);
        OpenChanged?.Invoke(false);
    }

    // 장비 탭을 떠나면 닫는다 (InventoryTabPresenter.TabChanged 구독).
    private void OnTabChanged(InventoryTab tab)
    {
        if (tab != InventoryTab.Equipment)
        {
            Close();
        }
    }

    #endregion

    #region 구독

    // 장비·인벤토리 변경 구독 (Start · OnEnable에서 호출) — 칸이 바뀌고 큐브 수가 준다.
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed           = true;
        _data.EquipsChanged    += Redraw;
        _data.InventoryChanged += Redraw;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed           = false;
        _data.EquipsChanged    -= Redraw;
        _data.InventoryChanged -= Redraw;
    }

    #endregion

    #region 그리기

    // 창 전체를 다시 그린다 (Open · OnEnable · 장비·인벤토리 변경 · 큐브 고르기 · 응답).
    private void Redraw()
    {
        if (!IsOpen)
        {
            return;
        }

        EquipInfo? equip = _data.FindEquip(_equipId);

        // 팔았거나 경매에 올려 사라졌다 — 비어 있는 창을 남기지 않는다.
        if (equip == null || !GameDataLoader.TryGetEquip(equip.EquipTid, out EquipTableRow row))
        {
            Close();

            return;
        }

        // 고른 뒤 동기화가 왔다 — 틈을 닫는다
        if (_chosenEquipId == equip.EquipId && equip.PendingEnchantGrade == 0)
        {
            _chosenEquipId = 0L;
        }

        // 상급 큐브로 등급이 올랐다 — 새 값을 알아서 고른다. 고르기 응답이 오면 다시 그린다
        if (ShouldKeepRankUp(equip))
        {
            ClientLogger.Info(ClientLogger.UI, $"큐브 등급 상승 — 새 값을 자동으로 고른다 (장비 #{equip.EquipId}, {equip.EnchantGrade} → {equip.PendingEnchantGrade})");
            RequestChoose(equip.EquipId, true, 0);

            return;
        }

        var grade     = (GlobalRarity)equip.EnchantGrade;
        int slotCount = EquipLabel.GetStatSlotCount(row.GlobalRarity);

        DrawLocks();
        DrawHeader(equip, row, slotCount);
        DrawCubes(grade);

        // 비교 상자와 자동 설정 칸은 같은 자리를 번갈아 쓴다 — 높이가 같아 창이 출렁이지 않는다.
        bool setting = _auto == AutoState.Setting;

        compareRow.SetActive(!setting);
        autoPanel.gameObject.SetActive(setting);

        if (setting)
        {
            autoPanel.Bind(_autoTarget, grade, slotCount);
            DrawAutoSetting(equip, grade, slotCount);

            return;
        }

        DrawCompare(equip, grade);
        DrawRule(equip, grade, slotCount);
        DrawConfirm(equip);
        DrawAutoButton();
    }

    // 공개 중인가 — 줄이 다 멈추기 전. 이때는 결과를 보기 전이라 버튼을 잠근다(뽑기와 같은 원리).
    private bool IsRevealing => afterFx.IsSpinning;

    // 상급 큐브의 보류가 등급 상승인데, 사람이 고를 차례인가 (Redraw에서 호출).
    // 자동이 도는 중엔 자동 루프가 고른다('EnchantAutoTarget.PreferNew'도 오르면 새 값). 공개가 끝나야 고른다 — 결과를 먼저 보여 준다.
    private bool ShouldKeepRankUp(EquipInfo equip)
        => IsPending(equip)
        && equip.PendingEnchantGrade > equip.EnchantGrade
        && _auto != AutoState.Running
        && !IsWaiting
        && !IsRevealing;

    // 공개 중 잠금 — 큐브 · [사용] · [자동]. 자동이 도는 중엔 잠그지 않는다([■ 멈춤]은 늘 눌려야 하고, 매 굴림 깜빡인다).
    // ※ 잠금은 'interactable'로 한다 — 큐브가 없을 때처럼 "눌러서 이유를 듣는" 경우가 아니다. 잠깐 흐려졌다 풀린다.
    private void DrawLocks()
    {
        bool locked = IsRevealing && _auto != AutoState.Running;

        confirmButton.interactable = !locked;
        autoButton.interactable    = !locked;

        foreach (EnchantCubeView view in cubeViews)
        {
            view.SetLocked(locked);
        }
    }

    // 머리 — 아이콘 · 이름(등급색) · 등급·종류·기본 능력치·칸 수 한 줄.
    private void DrawHeader(EquipInfo equip, EquipTableRow row, int slotCount)
    {
        itemIcon.Bind(ItemIconContent.ForEquip(equip.EquipTid, equip.EnchantOptions));

        nameText.text  = row.Name;
        nameText.color = RarityPalette.Get(row.GlobalRarity);

        string worn = equip.EquippedCharacterId == 0L
            ? ""
            : $"{UIRichText.Dot}{_data.GetCharacterName(equip.EquippedCharacterId)} 착용 중";

        infoText.text = $"{RarityLabel.Get(row.GlobalRarity)} {EquipLabel.GetKindName(row.EquipKind)}{UIRichText.Dot}"
                      + $"기본 {EquipLabel.GetEffectText(equip.EquipTid)}{UIRichText.Dot}능력치 칸 {slotCount}개{worn}";
    }

    // 비교 상자 — 왼쪽 BEFORE · 오른쪽 AFTER. 두 상자는 늘 켜 두고 **내용만** 바꾼다(크기가 출렁이지 않게).
    //   고르기 전(보류)    : BEFORE = 지금 장비, AFTER = 보류된 새 값, 둘 다 [선택]
    //   인챈트 큐브를 쓴 뒤 : BEFORE = 쓰기 전, AFTER = 들어간 값
    //   쓰기 전            : BEFORE = 지금 장비, AFTER = 흐린 안내
    private void DrawCompare(EquipInfo equip, GlobalRarity grade)
    {
        var  pendingGrade = (GlobalRarity)equip.PendingEnchantGrade;
        bool pending      = IsPending(equip);

        bool canPick = pending && _auto != AutoState.Running && !IsRevealing; // 도는 중엔 자동이 고른다 · 공개 중엔 아직 못 고른다

        keepBeforeButton.gameObject.SetActive(canPick);
        keepAfterButton.gameObject.SetActive(canPick);

        if (pending)
        {
            beforeFx.SetResult($"BEFORE{UIRichText.Dot}{GradeOrNone(grade)}",
                               FormatOptions(equip.EquipTid, equip.EnchantOptions), grade);
            afterFx.SetResult($"AFTER{UIRichText.Dot}{ResultTitle(grade, pendingGrade)}",
                              FormatOptions(equip.EquipTid, equip.PendingEnchantOptions), pendingGrade);

            return;
        }

        if (_result != null)
        {
            beforeFx.SetResult($"BEFORE{UIRichText.Dot}{GradeOrNone(_result.BeforeGrade)}",
                               FormatOptions(equip.EquipTid, _result.BeforeOptions), _result.BeforeGrade);
            afterFx.SetResult($"AFTER{UIRichText.Dot}{ResultTitle(_result.BeforeGrade, _result.AfterGrade)}",
                              FormatOptions(equip.EquipTid, _result.AfterOptions), _result.AfterGrade);

            return;
        }

        beforeFx.SetResult($"BEFORE{UIRichText.Dot}{GradeOrNone(grade)}",
                           FormatOptions(equip.EquipTid, equip.EnchantOptions), grade);
        afterFx.SetResult("AFTER", Colorize("결과가 여기에 나옵니다", UIThemePalette.Of(UIThemeRole.TextDisabled)), GlobalRarity.None);
    }

    // AFTER 제목 — 오르면 '영웅 ▶ 전설', 아니면 등급만 (DrawCompare에서 호출).
    // ※ 짧게 쓴다 — 머리 줄에 [선택]이 함께 서서 '등급 상승! …'까지 넣으면 두 줄로 내려갔다(사용자 지적 2026-10-11).
    //   '등급 상승!'은 상자의 도장이 말한다. 제목 글씨는 줄바꿈 없이, 넘치면 '…'로 자른다(씬 설정).
    private static string ResultTitle(GlobalRarity before, GlobalRarity after)
    {
        return before != GlobalRarity.None && after > before
            ? $"{GradeText(before)} ▶ {GradeText(after)}"
            : GradeText(after);
    }

    // 등급 글씨 — 인챈트가 없으면 '인챈트 없음'.
    private static string GradeOrNone(GlobalRarity grade) => grade == GlobalRarity.None ? "인챈트 없음" : GradeText(grade);

    // 규칙 줄 — 이번에 쓰면 무엇이 되나 한 줄. 전체 규칙·확률은 옆 도움말 아이콘(?)의 툴팁이 펼친다.
    // ※ 문구 끝에 "(올리면 규칙·확률)"을 붙이던 안내는 아이콘으로 대신했다 — 도움말 아이콘 규약('Overlay 규칙.md' "툴팁").
    // ※ 첫 사용·신화에서 상급 큐브를 고르면 "인챈트 큐브와 결과가 같다"고 알리던 줄(A)은 지웠다 — 상급 큐브는 이전 값을 고를 수 있다(이슈 #53).
    private void DrawRule(EquipInfo equip, GlobalRarity grade, int slotCount)
    {
        if (_auto == AutoState.Running)
        {
            DrawAutoProgress();

            return;
        }

        // ※ 규칙 줄은 **두 줄 고정**이다(자동 크기 없음 — 글씨 바뀔 때마다 여러 번 재는 비용). 줄마다 한 덩어리만 쓴다.
        // 자동이 끝난 뒤 한 줄 — 무엇 때문에 멈췄고 몇 개 썼나. 상급 큐브가 목표에 닿았으면 고르기 안내가 뒤따른다.
        if (IsPending(equip))
        {
            string pick = Colorize("BEFORE · AFTER 중 하나를 [선택]하세요.", UIThemePalette.Of(UIThemeRole.Highlight));

            ruleText.text = _autoSummary == null
                ? $"{pick}\n고르지 않고 다시 사용하면 BEFORE가 남습니다."
                : $"{_autoSummary}\n{pick}";

            return;
        }

        if (_autoSummary != null)
        {
            ruleText.text = _autoSummary;

            return;
        }

        if (grade == GlobalRarity.None)
        {
            ruleText.text = $"처음 쓰면 {GradeText(GlobalRarity.Common)} 등급으로 칸 {slotCount}개를 채웁니다.";
        }
        else if (grade >= GlobalRarity.Mythic)
        {
            ruleText.text = $"최고 등급입니다. 쓰면 칸 {slotCount}개를 같은 등급으로 다시 뽑습니다.";
        }
        else
        {
            ruleText.text = $"쓰면 칸 {slotCount}개를 전부 다시 뽑고, 확률로 {GradeText(grade + 1)}(으)로 오릅니다. 내려가지 않습니다.";
        }

        if (IsKeepCube(_cubeIndex))
        {
            ruleText.text += "\n굴린 뒤 이전 값을 고를 수 있습니다. 등급이 오르면 새 값을 알아서 고릅니다.";
        }
    }

    // 그 큐브가 결과를 보류하고 고르게 하는가 — 상급 큐브 ('EnchantItemTable.CanKeepPrevious').
    private static bool IsKeepCube(int cubeIndex)
    {
        IReadOnlyList<EnchantItemTableRow> cubes = GameDataLoader.EnchantCubes;

        return cubeIndex < cubes.Count && cubes[cubeIndex].CanKeepPrevious;
    }

    // 큐브 버튼 — 'EnchantItemTable' 순서로 채우고 남는 버튼은 끈다.
    private void DrawCubes(GlobalRarity grade)
    {
        IReadOnlyList<EnchantItemTableRow> cubes = GameDataLoader.EnchantCubes;

        if (_cubeIndex >= cubes.Count)
        {
            _cubeIndex = 0;
        }

        for (int i = 0; i < cubeViews.Length; i++)
        {
            if (i >= cubes.Count)
            {
                cubeViews[i].Hide();

                continue;
            }

            int    tid   = cubes[i].ItemTID;
            int    owned = _data.GetItemCount(tid);
            string what  = grade == GlobalRarity.None ? "첫 사용 · 일반 확정"
                         : grade >= GlobalRarity.Mythic ? "옵션만 다시 뽑기"
                         : $"{RarityLabel.Get(grade + 1)} 상승 {EquipLabel.FormatPermyriad(GameDataLoader.GetEnchantUpPermyriad(grade, tid))}";

            cubeViews[i].Bind(GameDataLoader.GetItemName(tid),
                              $"보유 {owned:N0}개{UIRichText.Dot}{what}",
                              owned > 0,
                              i == _cubeIndex,
                              EquipLabel.BuildCubeRuleTooltip);
        }
    }

    // [큐브 사용] — 글씨와 색. 큐브 이름이 곧 버튼 글씨다('상급 인챈트 큐브 사용'). 착용 중이면 '해제하고 …'.
    private void DrawConfirm(EquipInfo equip)
    {
        IReadOnlyList<EnchantItemTableRow> cubes = GameDataLoader.EnchantCubes;

        if (cubes.Count == 0)
        {
            confirmLabel.text = "큐브 정보가 없습니다";
            PaintConfirm(false);

            return;
        }

        int    tid   = cubes[_cubeIndex].ItemTID;
        string cube  = GameDataLoader.GetItemName(tid);
        bool   owned = _data.GetItemCount(tid) > 0;

        // 도는 중엔 자동이 이 버튼을 누른다 — 응답 대기 글씨로 깜빡이지 않게 큐브 이름을 둔다.
        if (_auto == AutoState.Running)
        {
            confirmLabel.text = $"{cube} 사용";
            PaintConfirm(true);

            return;
        }

        confirmLabel.text = IsWaiting                          ? "응답을 기다리는 중"
                          : !owned                             ? $"{cube}가 없습니다"
                          : equip.EquippedCharacterId != 0L    ? $"해제하고 {cube} 사용"
                          : $"{cube} 사용";

        PaintConfirm(owned && !IsWaiting);
    }

    // 칸 목록 문구 — 칸마다 한 줄, 등급색. 빈 칸은 흐린 '비어 있음'.
    private string FormatOptions(int equipTid, IReadOnlyList<int> optionTids)
    {
        EquipLabel.ReadStatOptions(equipTid, optionTids, _options);

        _text.Clear();

        for (int i = 0; i < _options.Count; i++)
        {
            if (i > 0)
            {
                _text.Append('\n');
            }

            EnchantOptionTableRow? option = _options[i];

            _text.Append(option == null
                ? Colorize("비어 있음", UIThemePalette.Of(UIThemeRole.TextDisabled))
                : Colorize(EquipLabel.GetOptionText(option), RarityPalette.Get(option.Grade)));
        }

        return _text.ToString();
    }

    // 돌기 중 끼울 가짜 한 줄 — **결과와 같은 등급**의 아무 옵션 글씨 (공개 연출이 간격마다 부른다).
    // 다른 등급이 스쳐 보이면 결과보다 좋은(나쁜) 것이 나올 뻔한 것처럼 읽힌다 — 사용자 지적(2026-10-10)으로 등급을 거른다.
    private string RollFakeLine(GlobalRarity grade)
    {
        if (_rollingGrade != grade)
        {
            _rollingGrade = grade;
            _rolling.Clear();

            foreach (EnchantOptionTableRow row in GameDataLoader.EnchantOptions)
            {
                if (row.Grade == grade)
                {
                    _rolling.Add(row);
                }
            }
        }

        return _rolling.Count == 0 ? "" : EquipLabel.GetOptionText(_rolling[UnityEngine.Random.Range(0, _rolling.Count)]);
    }

    // [큐브 사용] 색 — 쓸 수 있으면 강조색, 아니면 회색 (DrawConfirm에서 호출).
    //
    // ※ 'interactable'은 끄지 않는다 — 눌러서 이유를 들을 수 있어야 한다('TraitPresenter.PaintConfirm'과 같은 공식).
    private void PaintConfirm(bool ready)
    {
        PaintButton(confirmButton, ready ? confirmReadyRole : confirmBlockedRole);
    }

    // 버튼 색 — 'Image.color'가 아니라 'ColorBlock'을 칠한다('Selectable'이 실행 중 'Image.color'를 덮어쓴다).
    private static void PaintButton(Button button, UIThemeRole role)
    {
        Color color = UIThemePalette.Of(role);

        // 의미색(초록·빨강)은 글씨용 밝기라 흰 글씨가 묻힌다 — 버튼 바탕으로 쓸 때는 어둡게 누른다
        if (role is UIThemeRole.Positive or UIThemeRole.Negative)
        {
            color = Color.Lerp(color, Color.black, SignalButtonDarken);
        }

        ColorBlock colors = button.colors;

        colors.normalColor      = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.15f);
        colors.pressedColor     = Color.Lerp(color, Color.black, 0.2f);
        colors.selectedColor    = color;

        button.colors = colors;
    }

    // 등급 이름을 그 등급색으로.
    private static string GradeText(GlobalRarity grade) => Colorize(RarityLabel.Get(grade), RarityPalette.Get(grade));

    private static string Colorize(string text, Color color) => $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";

    #endregion

    #region 큐브 사용

    private bool IsWaiting => _waitHandle != null;

    // 큐브 버튼을 눌렀다 — 고르기만 한다 (EnchantCubeView.Clicked 구독).
    private void OnCubeClicked(EnchantCubeView view)
    {
        int index = Array.IndexOf(cubeViews, view);

        if (index < 0 || index == _cubeIndex || _auto == AutoState.Running || IsRevealing)
        {
            return;
        }

        _cubeIndex = index;
        Redraw();
    }

    // [큐브 사용]을 눌렀다 (confirmButton OnClick에 코드로 연결).
    //
    // 판정 순서 — 대기 중이면 무시 → 큐브가 없으면 알림 → 착용 중이면 해제를 묻는다 → 보낸다.
    // ※ 덮어쓰기 경고는 두지 않는다(사용자 결정 D5-A) — 등급은 내려가지 않고, 지금 칸이 상자에 보인다.
    private void OnConfirmClicked()
    {
        if (_auto == AutoState.Setting)
        {
            StartAuto();

            return;
        }

        if (IsWaiting || !IsOpen || _auto == AutoState.Running || IsRevealing)
        {
            return;
        }

        _autoSummary = null;

        if (!_data.IsLoggedIn)
        {
            ClientLogger.Warn(ClientLogger.Send, "큐브 사용 요청을 보내지 않았다 — 로그인이 먼저다(서버가 응답 없이 버린다)");

            return;
        }

        EquipInfo?                         equip = _data.FindEquip(_equipId);
        IReadOnlyList<EnchantItemTableRow> cubes = GameDataLoader.EnchantCubes;

        if (equip == null || cubes.Count == 0)
        {
            return;
        }

        int cubeTid = cubes[_cubeIndex].ItemTID;

        if (_data.GetItemCount(cubeTid) <= 0)
        {
            _wait.RaiseNotice($"{GameDataLoader.GetItemName(cubeTid)}가 없습니다.");

            return;
        }

        // 고르지 않고 다시 쓴다 = BEFORE를 남기고 굴린다 — 고르기(이전 값)를 먼저 보내고, 응답이 오면 큐브를 보낸다.
        if (IsPending(equip))
        {
            RequestChoose(equip.EquipId, false, cubeTid);

            return;
        }

        if (equip.EquippedCharacterId != 0L)
        {
            long   equipId   = equip.EquipId;
            string character = _data.GetCharacterName(equip.EquippedCharacterId);

            _ui.AskConfirm($"{character}이(가) 끼고 있는 장비입니다.\n해제한 뒤 큐브를 사용합니다. (사용 후 다시 끼지 않습니다)",
                           () => RequestUnequip(equipId, cubeTid));

            return;
        }

        RequestEnchant(equip, cubeTid);
    }

    // 착용 중인 장비를 먼저 벗긴다 — 응답이 오면 큐브를 보낸다 (D4-B · AskConfirm 확인에서 호출).
    //
    // ※ 확인 팝업이 떠 있는 사이 상황이 바뀌었을 수 있다 — 다시 찾아 본다.
    private void RequestUnequip(long equipId, int cubeTid)
    {
        EquipInfo? equip = _data.FindEquip(equipId);

        if (IsWaiting || equip == null || equip.EquippedCharacterId == 0L)
        {
            return;
        }

        _network.Send(new C_UnequipRequest { CharacterId = equip.EquippedCharacterId, Slot = equip.EquippedSlot });

        ClientLogger.Info(ClientLogger.Send, $"큐브 사용 전 해제 — 장비 #{equipId} @캐릭터 {equip.EquippedCharacterId} {equip.EquippedSlot}");

        _unequipThenEnchantId = equipId;
        _enchantingCubeTid    = cubeTid;
        _waitHandle           = _wait.Begin("장비 해제", onClosed: OnWaitClosed);
        Redraw();
    }

    // 해제 결과 — 내가 보낸 해제면 이어서 큐브를 보낸다 (PlayerDataModel.EquipCompleted 구독).
    // ※ 장착·해제 응답은 작업슬롯 화면도 듣는다 — 내가 보낸 것이 없으면 무시한다.
    private void OnUnequipCompleted(bool success, EResultCode code)
    {
        if (_unequipThenEnchantId == 0L || _waitHandle == null)
        {
            return;
        }

        long equipId = _unequipThenEnchantId;
        int  cubeTid = _enchantingCubeTid;

        _unequipThenEnchantId = 0L;

        if (!success)
        {
            _waitHandle.Fail(ResultMessages.ToText(code));

            return;
        }

        _waitHandle.Succeed();

        // ★ 응답이 개체 동기화보다 먼저 올 수 있다 — 캐시가 아직 '착용 중'이어도 서버는 이미 벗겼다.
        EquipInfo? equip = _data.FindEquip(equipId);

        if (equip != null)
        {
            RequestEnchant(equip, cubeTid);
        }
    }

    // 큐브 사용을 보낸다 (OnConfirmClicked · 해제 성공 뒤).
    private void RequestEnchant(EquipInfo equip, int cubeTid)
    {
        // 이전 칸은 응답에 없다 — 보내는 순간을 복사해 둔다.
        _sending.Clear();
        _sending.AddRange(equip.EnchantOptions);

        _network.Send(new C_EquipEnchantRequest { EquipId = equip.EquipId, ItemTid = cubeTid });

        ClientLogger.Info(ClientLogger.Send, $"큐브 사용 — 장비 #{equip.EquipId}, 큐브 {cubeTid}, 지금 등급 {equip.EnchantGrade}");

        _enchantingEquipId = equip.EquipId;
        _waitHandle        = _wait.Begin("큐브 사용", onClosed: OnWaitClosed);
        Redraw();
    }

    // 큐브 사용 결과 (PlayerDataModel.EquipEnchantCompleted 구독).
    // ※ 창을 닫았거나 다른 장비로 바꿨어도 대기는 닫는다. 결과는 그 장비가 지금 창에 있을 때만 그린다.
    private void OnEnchantCompleted(S_EquipEnchantResponse res)
    {
        if (_enchantingEquipId == 0L || res.EquipId != _enchantingEquipId)
        {
            return;
        }

        _enchantingEquipId = 0L;
        _lastEnchant       = res;

        // 고른 뒤 바로 다시 굴렸다(보류 중 [사용]) — 새 보류가 생겼으니 '고른 장비' 표시를 푼다
        if (res.EquipId == _chosenEquipId)
        {
            _chosenEquipId = 0L;
        }

        if (res.Result != EResultCode.Ok)
        {
            _waitHandle?.Fail(ResultMessages.ToText(res.Result));

            return;
        }

        // 공개 연출 — 다시 그리기(대기 닫힘 · 동기화)보다 먼저 틀어야 결과 글씨를 연출이 쥔다.
        if (res.EquipId == _equipId)
        {
            EquipInfo? shown      = _data.FindEquip(res.EquipId);
            int        lineCount  = shown == null ? 0 : EquipLabel.GetStatSlotCount(GameDataLoader.GetEquipRarity(shown.EquipTid));
            var        afterGrade = (GlobalRarity)res.AfterGrade;

            afterFx.Play(afterGrade,
                         res.AfterGrade > res.BeforeGrade,
                         Math.Max(lineCount, res.Options.Count),
                         () => RollFakeLine(afterGrade),
                         _auto == AutoState.Running ? AutoSpeed : 1f);
        }

        // 상급 큐브는 장비에 들어가지 않았다 — 보류된 새 값은 뒤따르는 장비 동기화가 캐시에 싣고, 비교 상자는 캐시로 그린다.
        if (res.EquipId == _equipId && res.AwaitingChoice)
        {
            _result = null;
        }
        else if (res.EquipId == _equipId)
        {
            var result = new EnchantResult
            {
                BeforeGrade = (GlobalRarity)res.BeforeGrade,
                AfterGrade  = (GlobalRarity)res.AfterGrade,
                RankedUp    = res.Success,
            };

            result.BeforeOptions.AddRange(_sending);
            result.AfterOptions.AddRange(res.Options);

            _result = result;
        }

        _waitHandle?.Succeed();
    }

    // [선택]을 눌렀다 — BEFORE(false) · AFTER(true) (keepBeforeButton · keepAfterButton OnClick에 코드로 연결).
    private void OnKeepClicked(bool keepNew)
    {
        if (IsWaiting || !HasPendingChoice || _auto == AutoState.Running || IsRevealing)
        {
            return;
        }

        _autoSummary = null;
        RequestChoose(_equipId, keepNew, 0);
    }

    // 상급 큐브의 보류 결과 고르기를 보낸다 (OnKeepClicked · 보류 중 [사용]).
    //   thenCubeTid : 고르기가 끝나면 이어 쓸 큐브. 0이면 고르기만 한다
    private void RequestChoose(long equipId, bool keepNew, int thenCubeTid)
    {
        if (!_data.IsLoggedIn)
        {
            ClientLogger.Warn(ClientLogger.Send, "큐브 결과 고르기를 보내지 않았다 — 로그인이 먼저다(서버가 응답 없이 버린다)");

            return;
        }

        _network.Send(new C_EquipEnchantChooseRequest { EquipId = equipId, KeepNew = keepNew });

        ClientLogger.Info(ClientLogger.Send, $"큐브 결과 고르기 — 장비 #{equipId}, 새 값={keepNew}, 이어서 큐브 {thenCubeTid}");

        _choosingEquipId       = equipId;
        _choosingKeepNew       = keepNew;
        _enchantAfterChooseTid = thenCubeTid;
        _waitHandle            = _wait.Begin("큐브 결과 고르기", onClosed: OnWaitClosed);
        Redraw();
    }

    // 고르기 결과 — 이어 쓸 큐브가 있으면 보낸다 (PlayerDataModel.EquipEnchantChooseCompleted 구독).
    // ※ 응답이 장비 동기화보다 먼저 온다 — 캐시는 아직 보류 중이지만 서버는 이미 풀었다(해제하고 사용과 같은 순서).
    private void OnChooseCompleted(S_EquipEnchantChooseResponse res)
    {
        if (_choosingEquipId == 0L || res.EquipId != _choosingEquipId)
        {
            return;
        }

        long equipId = _choosingEquipId;
        int  cubeTid = _enchantAfterChooseTid;

        _choosingEquipId       = 0L;
        _enchantAfterChooseTid = 0;
        _lastChoose            = res.Result;

        if (res.Result == EResultCode.Ok)
        {
            _chosenEquipId = equipId;
        }

        if (res.Result != EResultCode.Ok)
        {
            _waitHandle?.Fail(ResultMessages.ToText(res.Result));

            return;
        }

        if (equipId == _equipId && _choosingKeepNew && TryKeepResultOfLastEnchant(equipId))
        {
            // 새 값을 골랐다 — 인챈트 큐브처럼 BEFORE = 굴리기 전 · AFTER = 고른 값으로 남긴다.
            // 등급 상승 연출(도장 · 입자)이 아직 돌면 끊지 않는다 — 금색 번쩍은 연출이 끝난 뒤일 때만.
            if (!afterFx.IsPlaying)
            {
                afterFx.FlashChosen();
            }
        }
        else if (equipId == _equipId)
        {
            _result = null; // 고른 값이 곧 지금 장비다 — BEFORE에 그대로 보인다

            afterFx.Stop();
            beforeFx.FlashChosen();
        }

        _waitHandle?.Succeed();

        EquipInfo? equip = _data.FindEquip(equipId);

        if (cubeTid != 0 && equip != null)
        {
            RequestEnchant(equip, cubeTid);
        }
    }

    // 새 값을 고른 뒤 비교 상자에 남길 결과 — 방금 굴린 큐브 응답과 보낼 때 복사한 이전 칸으로 만든다 (OnChooseCompleted에서 호출).
    // 창을 다시 열어 고르는 경우처럼 그 응답이 없으면 false — 그때는 고른 값이 BEFORE에 보인다.
    private bool TryKeepResultOfLastEnchant(long equipId)
    {
        S_EquipEnchantResponse? res = _lastEnchant;

        if (res == null || res.EquipId != equipId || !res.AwaitingChoice)
        {
            return false;
        }

        var result = new EnchantResult
        {
            BeforeGrade = (GlobalRarity)res.BeforeGrade,
            AfterGrade  = (GlobalRarity)res.AfterGrade,
            RankedUp    = res.AfterGrade > res.BeforeGrade,
        };

        result.BeforeOptions.AddRange(_sending);
        result.AfterOptions.AddRange(res.Options);

        _result = result;

        return true;
    }

    // 대기가 끝났다(성공·실패·타임아웃 공통) — 버튼을 푼다 (ServerWaitManager.Begin의 onClosed)
    // ※ 해제가 끝나고 큐브 대기가 이어 열렸으면 손잡이를 지우지 않는다 — 앞 손잡이의 닫힘이 뒤에 온다.
    private void OnWaitClosed()
    {
        if (_waitHandle != null && !_waitHandle.IsClosed)
        {
            return;
        }

        _waitHandle            = null;
        _unequipThenEnchantId  = 0L;
        _enchantingEquipId     = 0L;
        _choosingEquipId       = 0L;
        _enchantAfterChooseTid = 0;
        Redraw();
    }

    #endregion
    #region 자동

    // [자동] — 꺼져 있으면 설정 칸을 열고, 설정 중이면 닫고, 도는 중이면 멈춘다 (autoButton OnClick에 코드로 연결).
    private void OnAutoClicked()
    {
        switch (_auto)
        {
            case AutoState.Running:
                StopAuto();

                return;

            case AutoState.Setting:
                _auto = AutoState.Off;
                Redraw();

                return;
        }

        EquipInfo? equip = IsOpen ? _data.FindEquip(_equipId) : null;

        if (equip == null || IsWaiting || IsRevealing)
        {
            return;
        }

        if (IsPending(equip))
        {
            _wait.RaiseNotice("먼저 BEFORE · AFTER 중 하나를 골라 주세요.");

            return;
        }

        if (equip.EquippedCharacterId != 0L)
        {
            _wait.RaiseNotice("착용 중인 장비는 자동으로 돌릴 수 없습니다. 먼저 벗겨 주세요.");

            return;
        }

        FitTarget((GlobalRarity)equip.EnchantGrade);

        _auto        = AutoState.Setting;
        _autoSummary = null;
        _result      = null;
        Redraw();
    }

    // 목표를 지금 장비에 맞춘다 — 지금 등급 이하 목표는 한 단계 위로, 이미 신화면 신화(칸 조건을 골라야 한다).
    private void FitTarget(GlobalRarity current)
    {
        if (current >= GlobalRarity.Mythic)
        {
            _autoTarget.Grade = GlobalRarity.Mythic;
        }
        else if (_autoTarget.Grade <= current)
        {
            _autoTarget.Grade = current + 1;
        }
    }

    private void OnAutoGradeClicked(GlobalRarity grade)
    {
        _autoTarget.Grade = grade;
        Redraw();
    }

    private void OnAutoFocusClicked(EnchantAutoFocus focus)
    {
        _autoTarget.Focus = focus;
        Redraw();
    }

    private void OnAutoComboClicked(int allCount)
    {
        _autoTarget.ToggleCombo(allCount);
        Redraw();
    }

    private void OnAutoCapClicked(int cap)
    {
        _autoTarget.Cap = cap;
        Redraw();
    }

    // 설정 중 — 규칙 줄에 예상 확률, [큐브 사용] 자리에 [자동 시작] (Redraw에서 호출).
    private void DrawAutoSetting(EquipInfo equip, GlobalRarity grade, int slotCount)
    {
        int     cubeTid = CurrentCubeTid();
        int     cap     = EffectiveCap(cubeTid);
        string? blocked = AutoBlockReason(grade, slotCount, cap);

        // 첫 줄 = 목표 등급 + 예상(막혔으면 이유) · 둘째 줄 = 칸 조건(+ 한 번에 맞을 확률). 두 줄 고정
        if (blocked != null)
        {
            ruleText.text = $"{DescribeGoal()}{UIRichText.Dot}{Colorize(blocked, UIThemePalette.Of(UIThemeRole.Highlight))}\n"
                          + DescribeSlotRule(slotCount, null);
        }
        else
        {
            EnchantForecast.Result forecast = EnchantForecast.Compute(grade, slotCount, cubeTid, _autoTarget, cap);

            ruleText.text = $"{DescribeGoal()}{UIRichText.Dot}{FormatForecast(forecast, cap, cubeTid)}\n"
                          + DescribeSlotRule(slotCount, forecast.SlotChance);
        }

        // 시작 버튼은 초록 — 도는 중의 [■ 멈춤](빨강)과 짝이다(사용자 결정 2026-10-10)
        confirmLabel.text = "자동 시작 ▶";
        PaintButton(confirmButton, blocked == null && !IsWaiting ? UIThemeRole.Positive : confirmBlockedRole);
        DrawAutoButton();
    }

    // 자동을 시작할 수 없는 이유 — 없으면 null.
    private string? AutoBlockReason(GlobalRarity grade, int slotCount, int cap)
    {
        if (cap <= 0)
        {
            return $"{GameDataLoader.GetItemName(CurrentCubeTid())}가 없습니다.";
        }

        if (grade >= GlobalRarity.Mythic && !_autoTarget.UsesSlots)
        {
            return "이미 신화입니다. 노릴 옵션을 골라 주세요.";
        }

        if (_autoTarget.HasNoCombo(slotCount))
        {
            return "조합을 하나 이상 켜 주세요.";
        }

        return null;
    }

    // 예상 — '1,000개 안에 67.6% · 평균 2,232개 (약 33분)' (규칙 줄 첫 줄 뒤쪽).
    // 상한 안 확률은 색으로 읽힌다 — 90% 이상 초록 · 50% 이상 노랑 · 그 아래 빨강.
    // ※ '절반 n개'는 뺐다 — 두 줄 고정 규칙 줄에 다 들어가지 않는다(2026-10-11). 칸 조건 확률은 둘째 줄로 옮겼다.
    private string FormatForecast(EnchantForecast.Result forecast, int cap, int cubeTid)
    {
        UIThemeRole tone = forecast.WithinCap >= 0.9 ? UIThemeRole.Positive
                         : forecast.WithinCap >= 0.5 ? UIThemeRole.Highlight
                         : UIThemeRole.Negative;

        string within = Colorize(Percent(forecast.WithinCap), UIThemePalette.Of(tone));
        string mean   = forecast.Mean > 0 ? $"{forecast.Mean:N0}개 (약 {FormatDuration(forecast.Mean * AutoStepSeconds(cubeTid))})" : "—";

        return $"{cap:N0}개 안에 {within}{UIRichText.Dot}평균 {mean}";
    }

    // 확률 글씨 — 아주 작으면 '0.01% 미만'.
    private static string Percent(double p) => p > 0 && p < 0.0001 ? "0.01% 미만" : $"{p * 100:0.##}%";

    // 걸리는 시간 글씨 — 초 · 분 · 시간.
    private static string FormatDuration(double seconds)
    {
        if (seconds < 60)
        {
            return $"{Math.Ceiling(seconds):0}초";
        }

        return seconds < 90 * 60 ? $"{seconds / 60:0}분" : $"{seconds / 3600:0.#}시간";
    }

    // 자동 한 번에 걸리는 시간의 어림 — 결과 보기(+ 상급이면 고르기). 서버 왕복은 뺀다.
    private static float AutoStepSeconds(int cubeTid)
    {
        float step = ManualRevealSeconds / AutoSpeed;

        return IsKeepCubeTid(GameDataLoader.EnchantCubes, cubeTid) ? step + ManualChooseSeconds / AutoSpeed : step;
    }

    private static bool IsKeepCubeTid(IReadOnlyList<EnchantItemTableRow> cubes, int cubeTid)
    {
        foreach (EnchantItemTableRow cube in cubes)
        {
            if (cube.ItemTID == cubeTid)
            {
                return cube.CanKeepPrevious;
            }
        }

        return false;
    }

    // 지금 고른 큐브. 큐브 표가 비어 있으면 0.
    private int CurrentCubeTid()
    {
        IReadOnlyList<EnchantItemTableRow> cubes = GameDataLoader.EnchantCubes;

        return _cubeIndex < cubes.Count ? cubes[_cubeIndex].ItemTID : 0;
    }

    // 이번 자동에 쓸 수 있는 큐브 수 — 상한과 보유 중 작은 쪽('보유 전부'면 보유).
    private int EffectiveCap(int cubeTid)
    {
        int owned = cubeTid == 0 ? 0 : _data.GetItemCount(cubeTid);

        return _autoTarget.Cap <= 0 ? owned : Math.Min(_autoTarget.Cap, owned);
    }

    // 도는 중 — 규칙 줄에 진행 (DrawRule에서 호출).
    private void DrawAutoProgress()
    {
        string running = Colorize($"자동 진행 중 {_autoUsed:N0} / {_autoCap:N0}개", UIThemePalette.Of(UIThemeRole.Positive));

        ruleText.text = $"{running}{UIRichText.Dot}{DescribeGoal()}\n{DescribeSlotRule(CurrentSlotCount(), null)}";
    }

    // 목표 등급 — '목표 신화 이상' (규칙 줄 첫 줄 — 설정 중 · 도는 중).
    private string DescribeGoal() => $"목표 {GradeText(_autoTarget.Grade)} 이상";

    // 칸 조건 한 줄 — '칸 [전체/낚시/낚시] [전체/전체/전체] · 한 번에 7.41%' (규칙 줄 둘째 줄 — 설정 중 · 도는 중).
    // 켠 조합끼리 OR. 전부 켰으면 '칸마다 전체 또는 낚시'로 줄인다('EnchantAutoTarget.DescribeCombos').
    //   chance : 한 번 굴려 칸 조건이 맞을 확률. null이면 쓰지 않는다
    private string DescribeSlotRule(int slotCount, double? chance)
    {
        if (!_autoTarget.UsesSlots)
        {
            return Colorize("칸 조건 없음 — 등급만 봅니다", UIThemePalette.Of(UIThemeRole.TextSub));
        }

        string combos = Colorize(_autoTarget.DescribeCombos(slotCount), UIThemePalette.Of(UIThemeRole.Highlight));

        return chance == null ? combos : $"{combos}{UIRichText.Dot}한 번에 {Percent(chance.Value)}";
    }

    // 창에 띄운 장비의 칸 수 — 모르면 최대.
    private int CurrentSlotCount()
    {
        EquipInfo? equip = _data.FindEquip(_equipId);

        return equip != null && GameDataLoader.TryGetEquip(equip.EquipTid, out EquipTableRow row)
            ? EquipLabel.GetStatSlotCount(row.GlobalRarity)
            : EnchantAutoTarget.MaxSlots;
    }

    // [자동] 버튼 글씨 — 꺼짐 '자동' · 설정 중 '취소' · 도는 중 '■ 멈춤'.
    // 도는 중엔 빨강 — 딱 봐도 자동이 돌고 있다(사용자 결정 2026-10-10).
    private void DrawAutoButton()
    {
        autoLabel.text = _auto switch
        {
            AutoState.Setting => "취소",
            AutoState.Running => "■ 멈춤",
            _                 => "자동",
        };

        PaintButton(autoButton, _auto == AutoState.Running ? UIThemeRole.Negative : UIThemeRole.Button);
    }

    // [자동 시작] — 막을 이유가 없으면 루프를 띄운다 (OnConfirmClicked에서 호출).
    private void StartAuto()
    {
        EquipInfo? equip = _data.FindEquip(_equipId);

        if (equip == null || IsWaiting || !GameDataLoader.TryGetEquip(equip.EquipTid, out EquipTableRow row))
        {
            return;
        }

        int     cubeTid = CurrentCubeTid();
        int     cap     = EffectiveCap(cubeTid);
        string? blocked = AutoBlockReason((GlobalRarity)equip.EnchantGrade, EquipLabel.GetStatSlotCount(row.GlobalRarity), cap);

        if (blocked != null)
        {
            _wait.RaiseNotice(blocked);

            return;
        }

        StopAuto();

        _autoCts     = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
        _auto        = AutoState.Running;
        _autoUsed    = 0;
        _autoCap     = cap;
        _autoSummary = null;
        _result      = null;
        Redraw();

        ClientLogger.Info(ClientLogger.UI, $"큐브 자동 시작 — 장비 #{equip.EquipId}, 큐브 {cubeTid}, 목표 {_autoTarget.Grade} 이상 · {_autoTarget.Focus}, 상한 {cap}");

        RunAutoAsync(equip.EquipId, cubeTid, _autoCts.Token).Forget();
    }

    // 자동을 멈춘다 — 도는 루프의 토큰을 끊는다. 정리(상태 · 안내 줄)는 루프의 finally가 한다.
    // ※ 이미 보낸 요청의 응답은 평소 경로가 받는다 — 대기 손잡이가 닫힌다.
    private void StopAuto()
    {
        _autoCts?.Cancel();
    }

    // 자동이 [큐브 사용]을 누르는 손짓 — 살짝 눌렸다 돌아온다 (RunAutoAsync에서 큐브를 보낼 때마다).
    private void PressConfirm()
    {
        _autoPress?.Kill(true);
        _autoPress = confirmButton.transform.DOPunchScale(Vector3.one * AutoPressPunch, AutoPressSeconds, 1, 0f)
                                  .SetLink(confirmButton.gameObject)
                                  .SetUpdate(true);
    }

    // 자동 루프 — 사람이 누르는 순서 그대로: 큐브 사용 → 결과 보기 → (상급이면) 고르기 → 다시 (StartAuto에서 시작).
    // 응답은 평소 핸들러가 받고, 루프는 대기 손잡이가 닫히기를 기다린 뒤 기록('_lastEnchant'·'_lastChoose')을 읽는다.
    private async UniTask RunAutoAsync(long equipId, int cubeTid, CancellationToken ct)
    {
        string summary   = "자동을 멈췄습니다";
        bool   reached   = false;
        var    reveal    = TimeSpan.FromSeconds(ManualRevealSeconds / AutoSpeed);
        var    afterPick = TimeSpan.FromSeconds(ManualChooseSeconds / AutoSpeed);
        var    before    = new List<int>();

        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (_autoUsed >= _autoCap)
                {
                    summary = "상한에 닿아 멈췄습니다";

                    break;
                }

                EquipInfo? equip = _data.FindEquip(equipId);

                if (equip == null || _data.GetItemCount(cubeTid) <= 0)
                {
                    summary = "큐브가 떨어져 멈췄습니다";

                    break;
                }

                // 큐브 사용 — 응답이 오기를 기다린다
                var beforeGrade = (GlobalRarity)equip.EnchantGrade;

                before.Clear();
                before.AddRange(equip.EnchantOptions);

                _lastEnchant = null;
                PressConfirm();
                RequestEnchant(equip, cubeTid);

                await UniTask.WaitUntil(() => !IsWaiting, cancellationToken: ct);

                S_EquipEnchantResponse? res = _lastEnchant;

                if (res == null || res.Result != EResultCode.Ok)
                {
                    summary = "서버가 거절해 멈췄습니다";

                    break;
                }

                _autoUsed++;
                Redraw();

                // 결과 보기 — 사람이 결과를 읽는 시간의 절반. 도장 연출이 더 길면 끝까지 본다
                await UniTask.Delay(reveal, ignoreTimeScale: true, cancellationToken: ct);
                await UniTask.WaitUntil(() => !afterFx.IsPlaying, cancellationToken: ct);

                var afterGrade = (GlobalRarity)res.AfterGrade;

                if (_autoTarget.Matches(afterGrade, res.Options))
                {
                    summary = "목표 달성!";
                    reached = true;
                    afterFx.FlashGoal();

                    break;
                }

                if (!res.AwaitingChoice)
                {
                    continue;
                }

                // 상급 큐브 — 오르면 새 값, 같으면 노린 칸이 많을 때만 새 값
                bool keepNew = _autoTarget.PreferNew(beforeGrade, before, afterGrade, res.Options);

                _lastChoose = null;
                RequestChoose(equipId, keepNew, 0);

                await UniTask.WaitUntil(() => !IsWaiting, cancellationToken: ct);

                if (_lastChoose != EResultCode.Ok)
                {
                    summary = "서버가 거절해 멈췄습니다";

                    break;
                }

                await UniTask.Delay(afterPick, ignoreTimeScale: true, cancellationToken: ct);
            }
        }
        finally
        {
            ClientLogger.Info(ClientLogger.UI, $"큐브 자동 끝 — {summary}, {_autoUsed}개 사용, 목표 달성={reached}");

            _autoCts?.Dispose();
            _autoCts = null;

            // 창이 사라졌으면(씬 종료) 그리지 않는다
            if (this != null && _auto == AutoState.Running)
            {
                _auto        = AutoState.Off;
                _autoSummary = Colorize($"{summary} — {GameDataLoader.GetItemName(cubeTid)} {_autoUsed:N0}개 사용.",
                                        UIThemePalette.Of(reached ? UIThemeRole.Positive : UIThemeRole.Highlight));
                Redraw();
            }
        }
    }

    #endregion
}

using System;
using System.Collections.Generic;
using MikaNetwork;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// UnityEngine에도 CharacterInfo(폰트 글리프 정보)가 있어 이름이 겹친다. 우리가 쓰는 건 패킷 쪽이다.
using CharacterInfo = MikaProtocol.CharacterInfo;

// 작업슬롯 한 칸의 설정 화면. 목록에서 칸을 누르면 목록 대신 이 화면이 열린다.
//
// 머리 둘(Header · Industry)은 늘 보이고, 몸통 둘(배치 목록 ↔ 세팅)이 갈아 끼워진다.
//
// ■ 화면이 상태를 기억하지 않는다
// "지금 배치돼 있는가"는 서버 스냅샷('PlayerDataModel.WorkStationSlots')에서 읽는다.
// 자체 플래그를 들면 실패 응답이 왔을 때 화면과 서버가 어긋난다.
// 고른 산업만은 서버에 없는 값이라 여기서 들고 있는데, 단계마다 주인이 다르다 —
// 캐릭터 목록에선 화면이 소유한 '걸러 보는 값'이고, 세팅에선 슬롯의 실제 산업에서 '파생'된다
// ('SyncSelectedIndustryToSlot').
//
// ⚠️ 지금 고를 수 없는 캐릭터는 목록에서 뺀다 — 적성 0이거나 다른 슬롯에서 일하는 중이면 걸러진다.
// 예전에는 "숨기지 않고 잠근다"였다. 16마리를 기르면 낚시를 눌러도 16줄이 그대로 남아
// **누를 수 있는 것을 골라내는 일을 사람이 하게 되기 때문에** 뒤집었다(T-046).
// 목록이 통째로 비면 안내 문구 하나만 뜨고(빈 목록은 고장과 구분되지 않는다),
// "내 캐릭터가 어디 갔나"의 답은 창고 캐릭터 탭이 맡는다 — 거기에 적성 스트립(5칸 · 위치=산업)과 '배' 마크가 있다.
// 적성은 패킷('CharacterInfo.Aptitudes')에서 온다 — 테이블을 직접 읽지 않는다.
//
// ■ 세팅 단계의 구성 (목업 'GameDesign/design/ui/게임UI목업(2026-07-30 업데이트).html'의 슬롯 상세)
// 캐릭터 카드 · 장비 4칸 · 효율 계산. 카드는 목록 줄과 **같은 프리팹**이다('CharacterStateRowView').
// 임시로 둔 자리 — 데이터·리소스가 없어 흰 네모와 "추가 예정"만 있고, 코드는 건드리지 않는다.
//   · 산업 탭 아이콘 · 캐릭터 초상화 · 종족 → 엑셀 · 기획 · 리소스가 나오면 추가 예정 (일감 'T-054')
//   · 장비 4칸(무기 · 장신구 2 · 보석)    → 장비 시스템이 나오면 추가 예정 (일감 'T-002')
//   · 효율 계산의 가산 항목(장비 · 특성 · 액티브) → 서버가 내역을 주면 추가 예정 (일감 'T-055')
//
// 세 단계 흐름 · 응답을 기다렸다 넘어가는 규칙은 'Main 규칙.md'의 "전환 층은 하나다" 절 참조.
public class WorkStationSelectPresenter : MonoBehaviour
{
    [CenterHeader("공통 Header Panel (항상 보인다)")]
    [SerializeField, Tooltip("'슬롯 N 설정' — 어느 칸을 눌러 들어왔는지 알리는 유일한 단서다")]
    private TMP_Text titleText = null!;

    [SerializeField, Tooltip("어느 단계에 있든 슬롯 목록으로 나간다. OnClick은 코드가 연결한다")]
    private Button backButton = null!;

    // ※ NonReorderable 두 가지를 동시에 얻는다 —
    //   [1] 순서가 곧 산업이라 드래그로 뒤바뀌면 조용히 엉뚱한 산업이 나간다. 아예 못 끌게 막는다.
    //   [2] reorderable list 로 그려지면 Unity 가 그 위의 [CenterHeader] 를 건너뛴다 ('UI 규칙.md'의 "공통 작성 규약")
    // 세 색이 세 상태와 1:1이다 — 고름 / 고르지 않음 / 못 고름.
    // 못 고르는 것은 색만 흐린 게 아니라 실제로 잠긴다('CanSelectIndustry').
    [CenterHeader("공통 Industry Panel (항상 보인다)")]
    [SerializeField, Tooltip("고른 산업 버튼의 바탕색 (노랑)")]
    private Color selectedIndustryColor = new Color(0.839f, 0.682f, 0.067f, 1f);

    [SerializeField, Tooltip("고르지 않았지만 고를 수 있는 산업 버튼의 바탕색 (하양)")]
    private Color unselectedIndustryColor = Color.white;

    [SerializeField, Tooltip("잠긴 산업 버튼의 바탕색 (회색). 'Selectable'이 disabledColor로 따로 칠한다")]
    private Color disabledIndustryColor = new Color(0.55f, 0.55f, 0.55f, 1f);

    // ※ 버튼마다 위 'Icon (임시)' · 아래 텍스트다. 아이콘은 흰 네모 — 산업 아이콘 스프라이트가 나오면
    //   각 버튼의 Icon Image에 넣는다(일감 'T-054'). 코드는 버튼 바탕색만 칠하므로 고칠 곳이 없다.
    [SerializeField, NonReorderable, Tooltip("산업 버튼 5개. 인스펙터에 넣은 순서가 곧 산업 순서다(농사·낚시·채굴·벌목·사냥)")]
    private Button[] industryButtons = new Button[0];

    [CenterHeader("1단계 캐릭터 할당 패널 (전환)")]
    [SerializeField, Tooltip("Character Assign Scroll View Panel 오브젝트")]
    private GameObject assignPanel = null!;

    [SerializeField, Tooltip("캐릭터 줄 프리팹 (CharacterStateRowView 포함). 보일 수만큼 만들어 재사용한다")]
    private CharacterStateRowView rowPrefab = null!;

    [SerializeField, Tooltip("캐릭터 줄이 쌓이는 부모 — Viewport > Content")]
    private RectTransform rowParent = null!;

    [SerializeField, Tooltip("고를 캐릭터가 하나도 없을 때만 켜지는 안내. 빈 목록은 고장과 구분되지 않는다")]
    private TMP_Text emptyText = null!;

    [CenterHeader("2단계 캐릭터 세팅 패널 (전환)")]
    [SerializeField, Tooltip("Character Setting Panel 오브젝트")]
    private GameObject settingPanel = null!;

    // ※ 목록 줄과 같은 프리팹의 인스턴스다 — 버튼 라벨만 "해제"로 바꿔 쓴다.
    [SerializeField, Tooltip("배치된 캐릭터 카드 (CharacterStateRowView). 버튼은 해제만 한다")]
    private CharacterStateRowView assignedCard = null!;

    // ※ 장비 4칸은 필드가 없다 — 임시 자리라 코드가 그릴 것이 없다(머리 주석 참조).
    [SerializeField, Tooltip("효율 계산 줄 프리팹 (EfficiencyRowView 포함). 그릴 항목 수만큼 만들어 재사용한다")]
    private EfficiencyRowView efficiencyRowPrefab = null!;

    [SerializeField, Tooltip("효율 계산 줄이 쌓이는 부모 — Efficiency Scroll View Panel > Viewport > Content")]
    private RectTransform efficiencyRowParent = null!;

    // 효율 계산 줄 수 — 적성 기본값 · 현재 작업속도 · 실효 주기.
    private const int EfficiencyRowCount = 3;

    // 현재 ÷ 기본값이 1에서 이만큼 벗어나야 전역 배수로 본다 — 서버의 천분율 반올림 오차를 흡수한다.
    private const float GlobalMultiplierTolerance = 0.005f;

    // 보낸 요청의 종류. 응답에는 배치였는지 교체였는지 해제였는지가 안 실려 와서 보낸 쪽이 기억한다.
    private enum PendingRequest
    {
        None,
        Assign,
        Replace,  // 교체 — 나가는 패킷은 배치와 같고, 실패했을 때 물러나지 않는 것만 다르다
        Unassign,
    }

    // 버튼 순서와 1:1로 대응하는 산업 목록. enum 값을 인덱스로 직접 쓰면 None(0) 한 칸이 밀리므로
    // 별도 목록으로 들고 있는다.
    private readonly List<EIndustryType> _industries = new List<EIndustryType>();

    // 만들어 둔 줄. 산업을 바꿀 때마다 수가 오르내리므로 파괴하지 않고 꺼 두었다가 다시 쓴다.
    private readonly List<CharacterStateRowView> _rows = new List<CharacterStateRowView>();

    // 이번에 보일 캐릭터. 걸러 낸 결과라 'Characters'와 순번이 다르다 —
    // 매번 새로 만들지 않으려고 필드로 들고 재사용한다(상주 앱이라 GC가 쌓인다).
    private readonly List<CharacterInfo> _visible = new List<CharacterInfo>();

    // 캐릭터 줄의 순서 규칙('CompareRows'). 메서드 그룹을 매번 넘기면 호출마다 대리자가 새로 생겨 Start에서 한 번만 만든다.
    private Comparison<CharacterInfo> _rowOrder = null!;

    // 줄에 넘길 적성 5칸. 산업 목록 순서 그대로 담는다 — 줄마다 새로 만들지 않고 이 배열을 재사용한다.
    // ※ 줄이 받아 그리는 즉시 쓰임이 끝나므로 공유해도 된다('StorageGridPresenter.ReadAptitudes'와 같다).
    private byte[] _aptitudes = new byte[0];

    // 만들어 둔 효율 계산 줄. 캐릭터 줄과 같은 풀 규칙이다.
    private readonly List<EfficiencyRowView> _efficiencyRows = new List<EfficiencyRowView>();

    // 지금 다루는 슬롯 번호. Open이 정한다 — 아직 안 열렸으면 -1.
    private int _slotIndex = -1;

    // 지금 고른 산업. 서버가 모르는 값이라 화면이 들고 있는다.
    private int _selectedIndustry;

    // 응답을 기다리는 중인 요청. 없으면 None.
    private PendingRequest _pending = PendingRequest.None;

    // 진행 중인 대기의 손잡이. 응답이 오면 결과를 보고하고, 무응답이면 스스로 타임아웃돼 잠금을 푼다.
    private ServerWaitHandle? _waitHandle;

    // 응답을 기다리는 중인가. 그동안 배치·해제 버튼을 잠근다.
    private bool IsWaiting => _pending != PendingRequest.None;

    private PlayerDataModel   _data    = null!;
    private NetworkManager    _network = null!;
    private UIManager         _ui      = null!;
    private ServerWaitManager _wait    = null!;
    private bool              _isSubscribed;
    private bool              _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 초기화 순서로 진행한다 (클라 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다(MonoService 주석).
    private void Start()
    {
        this.RequireRef(titleText,           nameof(titleText));
        this.RequireRef(backButton,          nameof(backButton));
        this.RequireRef(assignPanel,         nameof(assignPanel));
        this.RequireRef(rowPrefab,           nameof(rowPrefab));
        this.RequireRef(rowParent,           nameof(rowParent));
        this.RequireRef(emptyText,           nameof(emptyText));
        this.RequireRef(settingPanel,        nameof(settingPanel));
        this.RequireRef(assignedCard,        nameof(assignedCard));
        this.RequireRef(efficiencyRowPrefab, nameof(efficiencyRowPrefab));
        this.RequireRef(efficiencyRowParent, nameof(efficiencyRowParent));

        _data    = Services.Get<PlayerDataModel>();
        _network = NetworkManager.Instance;
        _ui      = Services.Get<UIManager>();
        _wait    = Services.Get<ServerWaitManager>();

        _rowOrder = CompareRows;

        Subscribe();

        BuildIndustryList();
        BindIndustryButtons();

        backButton.onClick.AddListener(BackToSlotList);
        assignedCard.SetButtonLabel("해제");
        assignedCard.AssignClicked += OnAssignedCardClicked;

        OpenStageForSlot();

        _isReady = true;
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    //
    // ★ 재구독만으로는 부족하다 — 닫혀 있는 동안 슬롯 상태가 바뀌었으면 라벨이 낡은 채로 남는다.
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

    // 이 슬롯을 다루도록 열린다 ('WorkStationListPresenter'가 칸 클릭에서 호출).
    //
    // ※ 켜기 전에 번호부터 넣는다. 꺼져 있던 화면은 'Start'가 아직 안 돌았을 수 있는데,
    // 그때는 Start가 이어서 단계를 정한다. 이미 돌았으면 여기서 바로 정한다.
    //
    // ※ 여기서 켜고, 부른 쪽이 이어서 'UIManager.ShowMainScreen'으로 나머지 화면을 끈다.
    // 자리를 뺏는 일은 여기서 하지 않는다 — 이 화면은 자기 형제가 몇인지 모른다.
    public void Open(int slotIndex)
    {
        _slotIndex = slotIndex;

        // 닫히는 동안 구독이 끊겨 지난 응답을 놓쳤을 수 있다. 잠금을 들고 들어가지 않는다.
        _pending    = PendingRequest.None;
        _waitHandle = null; // 지난 대기 손잡이는 버린다(남아 있어도 매니저가 타임아웃으로 정리한다)

        gameObject.SetActive(true);

        if (_isReady)
        {
            OpenStageForSlot();
        }
    }

    #region 단계 전환

    // 슬롯 상태가 첫 단계를 정한다 — 빈 칸이면 캐릭터를 고르러, 찬 칸이면 세팅으로.
    // ('Start' · 'Open'에서 호출)
    private void OpenStageForSlot()
    {
        if (IsAssigned(FindSlot()))
        {
            ShowSetting();
        }
        else
        {
            ShowAssignList();
        }
    }

    // 2단계 — 캐릭터 목록을 보여 준다.
    private void ShowAssignList()
    {
        assignPanel.SetActive(true);
        settingPanel.SetActive(false);
        Refresh();
    }

    // 3단계 — 배치된 캐릭터의 세팅을 보여 준다.
    private void ShowSetting()
    {
        assignPanel.SetActive(false);
        settingPanel.SetActive(true);
        Refresh();
    }

    // 슬롯 목록으로 물러난다 (뒤로가기 버튼 · 요청 실패).
    //
    // 이 화면을 직접 끄지 않는다 — 'UIManager'가 목록을 켜면서 같은 자리에 있는 이 화면을 끈다.
    // 스스로 끄면 목록이 안 켜진 빈 칸이 남는 순간이 생긴다.
    private void BackToSlotList()
    {
        _ui.ShowMainScreen(MainScreen.WorkStationList);
    }

    #endregion

    #region 구독

    // 슬롯·캐릭터 캐시 변경 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed                    = true;
        _data.WorkStationSlotsChanged   += Refresh;
        _data.CharactersChanged         += Refresh; // 보유 캐릭터가 늘면 줄도 늘어야 한다
        _data.WorkStationAssignCompleted += OnAssignCompleted;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed                    = false;
        _data.WorkStationSlotsChanged   -= Refresh;
        _data.CharactersChanged         -= Refresh;
        _data.WorkStationAssignCompleted -= OnAssignCompleted;
    }

    #endregion

    #region 산업 선택

    // 채취 가능한 1차 산업만 목록에 담는다 (Start에서 호출)
    //
    // ※ EIndustryType은 1차 산업 5종만 담는 타입이라 범위 필터가 필요 없다 —
    //   아이템 분류(Misc·Special)가 섞여 있던 ItemType과 다르다(이슈 #13).
    private void BuildIndustryList()
    {
        _industries.Clear();

        foreach (EIndustryType industry in Enum.GetValues(typeof(EIndustryType)))
        {
            if (industry != EIndustryType.None) // None은 "배치 해제"라 고를 대상이 아니다
                _industries.Add(industry);
        }

        _aptitudes = new byte[_industries.Count];
    }

    // 산업 버튼을 목록 순서와 묶는다 (Start에서 호출).
    // 버튼 개수와 산업 개수가 다르면 조용히 어긋난다 — 그래서 여기서 먼저 알린다.
    private void BindIndustryButtons()
    {
        if (industryButtons.Length != _industries.Count)
        {
            ClientLogger.Error(ClientLogger.UI,
                $"산업 버튼이 {industryButtons.Length}개인데 채취 산업은 {_industries.Count}종이다. " +
                $"인스펙터의 버튼 목록을 산업 순서(농사·낚시·채굴·벌목·사냥)대로 채울 것.", this);
        }

        for (int i = 0; i < industryButtons.Length; i++)
        {
            if (industryButtons[i] == null)
            {
                continue;
            }

            // 반복 변수를 그대로 넘기면 모든 콜백이 마지막 값을 본다. 복사본을 캡처한다.
            int index = i;
            industryButtons[i].onClick.AddListener(() => SelectIndustry(index));
        }
    }

    // 산업을 고른다 (산업 버튼 OnClick에 코드로 연결)
    //
    // ⚠️ 같은 버튼을 두 단계가 다르게 쓴다. 캐릭터 목록에선 '걸러 보는 수단'이라 화면이 값을
    // 들고 즉시 반영하고, 세팅에선 '갈아 끼우는 수단'이라 요청만 보낸다 —
    // 세팅에서 값을 미리 바꾸지 않는 이유는 'SyncSelectedIndustryToSlot' 참조.
    //
    // ※ 색만 바꾸고 끝내지 않는다 — 산업이 바뀌면 각 캐릭터의 적성도 달라져서 잠금 상태가 뒤집힌다.
    private void SelectIndustry(int index)
    {
        if (settingPanel.activeSelf)
        {
            RequestIndustryChange(index);

            return;
        }

        _selectedIndustry = index;
        RefreshIndustryButtons();

        if (assignPanel.activeSelf)
        {
            RefreshRows();
        }
    }

    // 고른 산업을 슬롯에 실제로 배치된 산업으로 맞춘다 ('Refresh'가 세팅 단계에서 호출).
    //
    // 세팅 단계에서는 "켜진 버튼 = 지금 돌고 있는 산업"이어야 한다. 그래서 교체를 눌러도 값을
    // 미리 바꾸지 않고 여기서만 맞춘다 — 성공하면 슬롯 갱신이 'Refresh'를 불러 새 산업이 켜지고,
    // 실패하면 아무 일도 일어나지 않아 원래 산업이 그대로 남는다. 되돌리는 코드가 필요 없다.
    private void SyncSelectedIndustryToSlot()
    {
        var slot = FindSlot();

        if (slot == null)
        {
            return;
        }

        int index = _industries.IndexOf(slot.Industry);

        if (index < 0)
        {
            return; // 빈 칸(None)이거나 목록에 없는 산업 — 직전 값을 그대로 둔다
        }

        _selectedIndustry = index;
    }

    // 지금 고른 산업. 목록 범위를 벗어났으면 'None' (표시·송신에서 호출).
    private EIndustryType SelectedIndustry
        => _selectedIndustry >= 0 && _selectedIndustry < _industries.Count
            ? _industries[_selectedIndustry]
            : EIndustryType.None;

    // 고른 산업만 밝게 칠하고, 못 하는 산업은 잠근다 (표시 갱신 때 호출).
    //
    // ■ 잠그는 단계가 하나뿐이다
    // 배치 목록에서는 **걸러 보는 수단**이라 늘 눌려야 한다 — 못 하는 산업을 눌러야
    // "이 산업을 다루는 캐릭터가 없다"를 볼 수 있다.
    // 세팅에서는 **갈아 끼우는 수단**이라 배치된 캐릭터의 적성이 0인 산업은 잠근다.
    // 안 잠그면 눌리고 서버가 'NoAptitude'로 거절하는데, 목록 쪽은 이미 걸러 내고 있어
    // 같은 화면이 두 말을 하게 된다.
    //
    // ※ 'Selectable'은 실행 중 'colors.normalColor'로 바탕을 덮어쓰므로
    //   'Image.color'가 아니라 이쪽을 바꾼다.
    //   ⚠️ 잠긴 버튼은 'normalColor'가 아니라 **'disabledColor'로 칠해진다** —
    //   회색을 normalColor에 넣으면 잠근 순간 그 색이 무시되고 직전 색이 남는다.
    private void RefreshIndustryButtons()
    {
        var slot = settingPanel.activeSelf ? FindSlot() : null;

        for (int i = 0; i < industryButtons.Length; i++)
        {
            var button = industryButtons[i];

            if (button == null)
            {
                continue;
            }

            button.interactable = CanSelectIndustry(slot, i);

            // 네 상태를 같은 색으로 덮는다 — 이 버튼의 색은 '고른 산업인가'만 말해야 한다.
            // 기본값(highlighted·selected = 0.961 흰색)을 그대로 두면 **마우스를 올리거나 마지막으로
            // 누른 버튼이라는 이유로** 색이 바뀐다. 특히 'selected'는 EventSystem이 클릭한 버튼을
            // 계속 잡고 있어 **고른 표시가 엉뚱한 버튼에 남는다.**
            var tint = i == _selectedIndustry ? selectedIndustryColor : unselectedIndustryColor;

            var colors = button.colors;
            colors.normalColor      = tint;
            colors.highlightedColor = tint;
            colors.pressedColor     = tint;
            colors.selectedColor    = tint;
            colors.disabledColor    = disabledIndustryColor;
            button.colors           = colors;
        }
    }

    // 이 산업 버튼을 누를 수 있나 ('RefreshIndustryButtons'에서 호출).
    //   slot : 세팅 단계면 다루는 슬롯, 배치 목록 단계면 null(= 늘 누를 수 있다)
    private bool CanSelectIndustry(WorkStationSlotInfo? slot, int index)
    {
        if (slot == null || !IsAssigned(slot))
        {
            return true;
        }

        if (index < 0 || index >= _industries.Count)
        {
            return true; // 버튼과 산업 수가 어긋난 상태다 — 'BindIndustryButtons'가 이미 알렸다
        }

        return _data.GetAptitude(slot.CharacterId, _industries[index]) > 0;
    }

    #endregion

    #region 캐릭터 줄

    // 지금 고를 수 있는 캐릭터만 줄로 그린다 ('Refresh'에서 호출).
    // 이 목록은 빈 슬롯일 때만 보이므로 해제 줄은 없다 — 해제는 세팅 쪽 일이다.
    //
    // ⚠️ 걸러 낸 목록을 먼저 만들고 그것을 태운다. 'Characters'를 인덱스 그대로 태우면
    //   걸러 낸 만큼 줄과 캐릭터가 어긋난다.
    private void RefreshRows()
    {
        var characters = _data.Characters;
        var industry   = SelectedIndustry;

        _visible.Clear();

        foreach (CharacterInfo character in characters)
        {
            if (_data.GetAptitude(character.CharacterId, industry) == 0)
            {
                continue;
            }

            // 이미 다른 슬롯에서 일하는 중이면 고를 수 없다 — 판정은 창고 캐릭터 탭과 같은 곳에서 읽는다.
            if (_data.FindSlotIndexOf(character.CharacterId) >= 0)
            {
                continue;
            }

            _visible.Add(character);
        }

        // 고른 산업을 가장 잘하는 캐릭터가 위로 온다 — 순서가 곧 추천이다('CompareRows').
        _visible.Sort(_rowOrder);

        for (int i = 0; i < _visible.Count; i++)
        {
            CharacterInfo character   = _visible[i];
            long          characterId = character.CharacterId;
            var           row         = GetOrCreateRow(i);

            row.gameObject.SetActive(true);
            row.Bind(characterId, _data.GetCharacterName(characterId));
            row.SetRarity(GameDataLoader.GetCharacterRarity(character.CharacterTid));
            row.SetAptitudes(ReadAptitudes(characterId), _selectedIndustry);
            row.SetAssignable(!IsWaiting);
        }

        HideRowsFrom(_visible.Count);

        // 빈 목록은 고장과 구분되지 않는다 — 왜 비었는지만 알린다.
        // 숨긴 캐릭터가 누구인지는 여기서 세지 않는다. 그 답은 창고 캐릭터 탭(적성 스트립·'배' 마크)에 있다.
        emptyText.gameObject.SetActive(_visible.Count == 0);

        // 방금 만든 줄은 아직 프리팹에 저장된 크기 그대로다 — uGUI의 레이아웃 계산은 이 프레임
        // **맨 끝**(Canvas.willRenderCanvases)에 돌기 때문이다. 그 사이 'WidgetPositionLayout.VerifyNoOverflow'가
        // 'LateUpdate'에서 훑고 지나가 "자식이 부모보다 넓다" → "해소됐다"가 왕복으로 찍힌다
        // (판매 목록에서 겪은 그대로 — 'SellCartPresenter.Refresh').
        LayoutRebuilder.ForceRebuildLayoutImmediate(rowParent);
    }

    // 캐릭터 줄의 순서 — 고른 산업의 적성 높은 순 → 등급 높은 순 → 개체 번호 순 (RefreshRows의 정렬 비교자).
    //
    // 산업 버튼이 곧 정렬 기준이라 따로 정렬 UI를 두지 않는다. 버튼을 바꾸면 'SelectIndustry'가 다시 그린다.
    // ※ 개체 번호까지 가서 동점을 없앤다 — 'List.Sort'는 안정 정렬이 아니라 동점이면 다시 그릴 때마다 줄이 바뀐다.
    //   등급은 종류(TID)로 읽는다. enum 'CompareTo'는 박싱이 일어나 숫자로 바꿔 비교한다.
    private int CompareRows(CharacterInfo a, CharacterInfo b)
    {
        EIndustryType industry = SelectedIndustry;

        int byAptitude = _data.GetAptitude(b.CharacterId, industry).CompareTo(_data.GetAptitude(a.CharacterId, industry));

        if (byAptitude != 0)
        {
            return byAptitude;
        }

        int byRarity = ((byte)GameDataLoader.GetCharacterRarity(b.CharacterTid))
            .CompareTo((byte)GameDataLoader.GetCharacterRarity(a.CharacterTid));

        if (byRarity != 0)
        {
            return byRarity;
        }

        return a.CharacterId.CompareTo(b.CharacterId);
    }

    // 'index'번째 줄을 돌려준다. 아직 없으면 그때 만든다 (RefreshRows에서 호출).
    private CharacterStateRowView GetOrCreateRow(int index)
    {
        if (index < _rows.Count)
        {
            return _rows[index];
        }

        CharacterStateRowView row = Instantiate(rowPrefab, rowParent);

        // 줄은 파괴하지 않고 재사용하므로 만들 때 한 번만 구독한다 — 다시 걸면 중복으로 쌓인다.
        row.SetButtonLabel("배치");
        row.AssignClicked += OnRowAssignClicked;

        _rows.Add(row);

        return row;
    }

    // 이 캐릭터의 적성 5종을 산업 목록 순서대로 담아 돌려준다 (줄·카드를 그릴 때 호출).
    // ※ 돌려주는 배열은 재사용되는 하나다 — 받은 쪽이 들고 있으면 안 된다.
    private byte[] ReadAptitudes(long characterId)
    {
        for (int i = 0; i < _aptitudes.Length; i++)
        {
            _aptitudes[i] = _data.GetAptitude(characterId, _industries[i]);
        }

        return _aptitudes;
    }

    // 이번에 쓰이지 않은 줄을 비우고 꺼 둔다 (RefreshRows에서 호출).
    private void HideRowsFrom(int startIndex)
    {
        for (int i = startIndex; i < _rows.Count; i++)
        {
            _rows[i].Clear();
            _rows[i].gameObject.SetActive(false);
        }
    }

    #endregion

    #region 표시

    // 제목·산업 버튼·캐릭터 줄을 지금 상태로 맞춘다 (단계 전환 · 데이터 변경)
    //
    // ※ 여기서 단계를 바꾸지 않는다. 어느 단계에 있을지는 사용자의 조작이 정하고,
    //   이 메서드는 그 단계의 내용만 채운다.
    private void Refresh()
    {
        titleText.text = $"슬롯 {_slotIndex} 설정";

        // 세팅 단계의 불빛은 슬롯에서 파생한다 — 칠하기 전에 맞춘다(순서가 뒤집히면 한 프레임 늦는다).
        if (settingPanel.activeSelf)
        {
            SyncSelectedIndustryToSlot();
            RefreshAssignedCard();
            RefreshEfficiency();
        }

        RefreshIndustryButtons();

        if (assignPanel.activeSelf)
        {
            RefreshRows();
        }

        ApplyWaitingLock();
    }

    // 세팅 단계의 캐릭터 카드를 그린다 ('Refresh'가 세팅 단계에서 호출).
    //
    // ※ 산업 교체는 'WorkStationSlotsChanged', 캐릭터 값 변경은 'CharactersChanged'가 'Refresh'를
    //   부르므로 여기서 따로 구독하지 않는다. 강조 칸은 'SyncSelectedIndustryToSlot'이 맞춘 산업이다.
    private void RefreshAssignedCard()
    {
        var slot = FindSlot();

        if (slot == null || !IsAssigned(slot))
        {
            assignedCard.Clear(); // 세팅 단계인데 비었다 — 해제 응답 직전 한 순간뿐이다

            return;
        }

        assignedCard.Bind(slot.CharacterId, _data.GetCharacterName(slot.CharacterId));
        assignedCard.SetRarity(GameDataLoader.GetCharacterRarity(_data.GetCharacterTid(slot.CharacterId)));
        assignedCard.SetAptitudes(ReadAptitudes(slot.CharacterId), _selectedIndustry);
    }

    // 효율 계산 줄을 채운다 ('Refresh'가 세팅 단계에서 호출).
    //
    // ■ 서버가 주는 것은 확정 속도 하나다
    // 'CurrentWorkSpeed'는 보정이 전부 적용된 값이고 내역은 오지 않는다. 그래서 지금 그릴 수 있는 것은
    // 적성 기본값(정적 곡선) · 현재 속도 · 실효 주기 셋이다. 가산 항목은 일감 'T-055' 뒤에 줄로 늘어난다.
    //
    // ■ 개발용 전역 배수를 역산한다
    // 서버 식은 '기본값 × (1 + Σ가산) × 전역배수'인데 가산이 아직 하나도 없어서 '현재 ÷ 기본값'이 곧 전역 배수다.
    // 1이 아니면 값 아래에 알리고, 1이면(배포 설정, 일감 'T-004') 문구가 저절로 사라진다.
    // ⚠️ **가산이 하나라도 붙으면 이 역산은 틀린다** — 장비 +35%까지 "전역 배수"로 보인다.
    //   그 전에 서버가 배수를 명시 필드로 주도록 바꾼다(일감 'T-055').
    private void RefreshEfficiency()
    {
        var slot = FindSlot();

        if (slot == null || !IsAssigned(slot))
        {
            HideEfficiencyRowsFrom(0);

            return;
        }

        byte  aptitude  = _data.GetAptitude(slot.CharacterId, slot.Industry);
        int   baseSpeed = GameDataLoader.GetBaseWorkSpeed(aptitude);
        float cycle     = WorkStationProgress.CalculateCycleSeconds(slot);

        GetOrCreateEfficiencyRow(0).Bind("적성 기본값", FormatSpeed(baseSpeed));

        var speedRow = GetOrCreateEfficiencyRow(1);
        speedRow.Bind("현재 작업속도", FormatSpeed(slot.CurrentWorkSpeed));

        if (baseSpeed > 0)
        {
            float multiplier = slot.CurrentWorkSpeed / (float)baseSpeed;

            // 서버가 천분율 정수로 반올림하므로 딱 1.0이 아닐 수 있다 — 반올림 오차는 배수로 치지 않는다.
            if (Mathf.Abs(multiplier - 1f) > GlobalMultiplierTolerance)
            {
                speedRow.SetNote($"개발용 전역 배수 ×{multiplier:0.00} 적용 중");
            }
        }

        GetOrCreateEfficiencyRow(2).Bind("실효 주기", cycle > 0f ? $"{cycle:0.00}초" : "—");

        HideEfficiencyRowsFrom(EfficiencyRowCount);

        // 캐릭터 줄과 같은 이유 — 방금 켠 줄은 이 프레임 끝까지 프리팹 크기 그대로다('RefreshRows' 끝 주석).
        LayoutRebuilder.ForceRebuildLayoutImmediate(efficiencyRowParent);
    }

    // 천분율 속도를 "2.45배"로 적는다. 0이면 모르는 값이라 "—" (효율 계산에서 호출)
    private static string FormatSpeed(int permille)
        => permille > 0 ? $"{permille / 1000f:0.00}배" : "—";

    // 'index'번째 효율 계산 줄을 켜서 돌려준다. 아직 없으면 그때 만든다 (RefreshEfficiency에서 호출).
    private EfficiencyRowView GetOrCreateEfficiencyRow(int index)
    {
        if (index >= _efficiencyRows.Count)
        {
            _efficiencyRows.Add(Instantiate(efficiencyRowPrefab, efficiencyRowParent));
        }

        var row = _efficiencyRows[index];
        row.gameObject.SetActive(true);

        return row;
    }

    // 이번에 쓰이지 않은 효율 계산 줄을 비우고 꺼 둔다 (RefreshEfficiency에서 호출).
    private void HideEfficiencyRowsFrom(int startIndex)
    {
        for (int i = startIndex; i < _efficiencyRows.Count; i++)
        {
            _efficiencyRows[i].Clear();
            _efficiencyRows[i].gameObject.SetActive(false);
        }
    }

    // 담당 슬롯의 현재 상태를 찾는다. 서버가 주지 않은 번호면 null (단계 판정·클릭 처리에서 호출)
    private WorkStationSlotInfo? FindSlot()
    {
        foreach (var slot in _data.WorkStationSlots)
        {
            if (slot.SlotIndex == _slotIndex)
            {
                return slot;
            }
        }

        return null; // 눌러 보면 실패 응답이 온다
    }

    // 슬롯이 배치 상태인가 — 산업과 캐릭터가 둘 다 차 있어야 배치다 (단계 판정·클릭 처리에서 호출)
    private static bool IsAssigned(WorkStationSlotInfo? slot)
        => slot != null && slot.Industry != EIndustryType.None && slot.CharacterId != 0;

    #endregion

    #region 송신

    // 어느 줄의 배치를 눌렀다 (CharacterStateRowView.AssignClicked 구독)
    private void OnRowAssignClicked(CharacterStateRowView row)
    {
        if (!CanSend())
        {
            return;
        }

        var industry = SelectedIndustry;

        if (industry == EIndustryType.None)
        {
            ClientLogger.Error(ClientLogger.UI,
                $"고른 산업({_selectedIndustry})이 목록 범위(0~{_industries.Count - 1})를 벗어났다.", this);

            return;
        }

        // 서버는 캐릭터 종류(TID)가 아니라 개체 번호를 받는다. 누른 줄이 그 번호를 들고 있다.
        if (row.CharacterId == 0)
        {
            ClientLogger.Error(ClientLogger.UI, "누른 줄에 캐릭터가 묶여 있지 않다 — Bind를 거치지 않았다.", this);

            return;
        }

        Send(industry, row.CharacterId);
        ClientLogger.Info(ClientLogger.Send, $"작업슬롯 배치 요청 — 슬롯={_slotIndex}, 산업={industry}, 캐릭터개체={row.CharacterId}");

        BeginWaiting(PendingRequest.Assign); // 넘어갈지 물러날지는 응답이 정한다
    }

    // 배치된 슬롯의 산업을 갈아 끼운다 ('SelectIndustry'가 세팅 단계에서 호출)
    //
    // ⚠️ 해제 → 배치 2연발로 보내지 않는다. 서버가 정산을 두 번 돌리고, 그 사이 빈 슬롯 상태가
    // 한 번 내려와 칸이 깜빡인다. 서버의 배치는 이미 덮어쓰기라('User.AssignWorkStation')
    // 요청 한 번이면 교체가 끝난다.
    private void RequestIndustryChange(int index)
    {
        if (!CanSend())
        {
            return;
        }

        var slot = FindSlot();

        if (slot == null || !IsAssigned(slot))
        {
            ClientLogger.Error(ClientLogger.UI,
                $"세팅 단계인데 슬롯 {_slotIndex}이 비어 있다 — 단계 판정이 어긋났다.", this);

            return;
        }

        if (index < 0 || index >= _industries.Count)
        {
            ClientLogger.Error(ClientLogger.UI,
                $"누른 산업 버튼({index})이 목록 범위(0~{_industries.Count - 1})를 벗어났다.", this);

            return;
        }

        var industry = _industries[index];

        if (industry == slot.Industry)
        {
            return; // 같은 산업 — 보내 봐야 서버 정산만 한 번 더 돈다
        }

        // 캐릭터는 지금 배치된 그대로 싣는다. 바꾸는 것은 산업뿐이다.
        Send(industry, slot.CharacterId);
        ClientLogger.Info(ClientLogger.Send, $"작업슬롯 산업 교체 요청 — 슬롯={_slotIndex}, 산업={industry}, 캐릭터개체={slot.CharacterId}");

        BeginWaiting(PendingRequest.Replace);
    }

    // 카드의 해제를 눌렀다 (assignedCard.AssignClicked 구독)
    private void OnAssignedCardClicked(CharacterStateRowView card)
    {
        if (!CanSend())
        {
            return;
        }

        Send(EIndustryType.None, 0); // 산업 None·캐릭터 0 = 해제
        ClientLogger.Info(ClientLogger.Send, $"작업슬롯 해제 요청 — 슬롯={_slotIndex}");

        BeginWaiting(PendingRequest.Unassign);
    }

    // 응답이 올 때까지 배치·해제 버튼을 잠그고 대기를 연다 (요청을 보낸 뒤 호출)
    // ※ 로딩 표시·무응답 감시·알림은 ServerWaitManager가 공통으로 처리한다. 무응답이면 5초 뒤
    //   타임아웃돼 onClosed(OnWaitClosed)가 잠금을 푼다 — 예전의 "무응답 시 영구 잠김"이 사라진다.
    private void BeginWaiting(PendingRequest request)
    {
        _pending    = request;
        _waitHandle = _wait.Begin(WaitLabel(request), onClosed: OnWaitClosed);
        ApplyWaitingLock();
    }

    // 로딩·타임아웃 문구에 쓸 요청 이름 ('BeginWaiting'에서 호출)
    private static string WaitLabel(PendingRequest request)
        => request switch
        {
            PendingRequest.Assign  => "작업슬롯 배치",
            PendingRequest.Replace => "작업슬롯 산업 교체",
            _                      => "작업슬롯 해제",
        };

    // 대기가 끝났다(성공·실패·타임아웃 공통) — 잠금을 푼다 (ServerWaitManager.Begin의 onClosed)
    private void OnWaitClosed()
    {
        _pending    = PendingRequest.None;
        _waitHandle = null;
        ApplyWaitingLock();
    }

    // 응답이 왔다 — 성공이면 다음 단계로, 실패면 사유를 알리고 슬롯 목록으로 물러난다
    // (PlayerDataModel.WorkStationAssignCompleted 구독).
    //
    // 실패는 대개 아직 열리지 않은 슬롯이다. 그 칸에서는 배치도 해제도 할 수 없으니
    // 화면에 남겨 둘 이유가 없다. 사유('EResultCode')는 'ResultMessages'로 문구를 만들어 알림에 띄운다.
    //
    // ⚠️ 교체만 예외로 물러나지 않는다 — 아래 분기 참조.
    private void OnAssignCompleted(bool success, EResultCode code)
    {
        // Succeed/Fail이 onClosed(OnWaitClosed)를 통해 _pending을 지우므로, 그 전에 종류를 붙잡는다.
        var requested = _pending;

        if (requested == PendingRequest.None)
        {
            return; // 이 화면이 보낸 요청이 아니다(타임아웃으로 이미 닫혔거나 남의 응답)
        }

        if (!success)
        {
            _waitHandle?.Fail(ResultMessages.ToText(code));

            // 교체 실패는 물러나지 않는다. 이미 열린 칸에서 나는 거절(적성 0 등)이라 그 칸에서
            // 할 일이 남아 있고, 산업을 눌러 봤다는 이유로 화면이 튕기면 조작이 어렵다.
            // 켜진 버튼은 손대지 않아도 원래 산업 그대로다('SyncSelectedIndustryToSlot').
            if (requested == PendingRequest.Replace)
            {
                ClientLogger.Warn(ClientLogger.UI,
                    $"슬롯 {_slotIndex} 산업 교체가 거절됐다 — 세팅 화면에 남는다.", this);

                return;
            }

            ClientLogger.Warn(ClientLogger.UI,
                $"슬롯 {_slotIndex} 변경이 거절돼 슬롯 목록으로 돌아간다 (열리지 않은 슬롯일 수 있다).", this);
            BackToSlotList();

            return;
        }

        _waitHandle?.Succeed();

        // 해제만 캐릭터 목록으로 돌아간다. 배치·교체는 둘 다 세팅에 머문다.
        if (requested == PendingRequest.Unassign)
        {
            ShowAssignList();
        }
        else
        {
            ShowSetting();
        }
    }

    // 기다리는 동안 배치·해제만 잠근다. 뒤로가기는 잠그지 않는다 — 나갈 길은 늘 열려 있어야 한다
    private void ApplyWaitingLock()
    {
        assignedCard.SetAssignable(!IsWaiting);

        foreach (var row in _rows)
        {
            row.SetAssignable(!IsWaiting);
        }
    }

    // 보낼 수 있는 상태인가 (배치·해제 클릭에서 호출)
    private bool CanSend()
    {
        // 버튼을 잠가 두지만 잠금이 늦게 반영되는 경로가 있을 수 있어 여기서 한 번 더 막는다.
        // 같은 슬롯에 두 번 보내면 응답도 두 번 와서 단계가 엉뚱하게 튄다.
        if (IsWaiting)
        {
            return false;
        }

        if (_slotIndex < 0)
        {
            ClientLogger.Error(ClientLogger.UI, "다룰 슬롯이 정해지지 않았다 — Open()을 거치지 않고 열렸다.", this);

            return false;
        }

        // 로그인 전에 보내면 서버가 User를 못 찾아 조용히 버린다 — 클라 입장에선 응답도 오류도
        // 없어서 "눌렀는데 아무 일도 안 일어난다"로만 보인다. 보내기 전에 여기서 끊고 이유를 남긴다.
        if (!_data.IsLoggedIn)
        {
            ClientLogger.Warn(ClientLogger.Send, "작업슬롯 요청을 보내지 않았다 — 로그인이 먼저다(서버가 응답 없이 버린다)");

            return false;
        }

        return true;
    }

    // 담당 슬롯의 배치 요청을 보낸다. 산업 None·캐릭터 0으로 주면 해제다 (클릭 처리에서 호출)
    private void Send(EIndustryType industry, long characterId)
    {
        _network.Send(new C_WorkStationAssignRequest
        {
            SlotIndex   = _slotIndex,
            Industry    = industry,
            CharacterId = characterId
        });
    }

    #endregion
}

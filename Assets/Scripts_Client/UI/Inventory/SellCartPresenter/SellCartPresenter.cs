using System;
using System.Collections.Generic;
using GameData;
using MikaNetwork;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// 판매 창 — 담은 것들을 보여 주고 한 번에 판다(장비는 분해도 고른다).
//
// ■ 화면 가운데 팝업이다 (2026-11-21 사용자 — 응축 · 큐브 창과 같은 맥락)
//   예전엔 인벤토리 아래에 붙어 격자를 반으로 줄였다. 이제 '!Overlay Canvas'에 어두운 바탕과 함께 뜬다.
//   여는 길 둘 — 칸을 우클릭해 담으면(수량을 묻거나 1개면 바로) 그 물건이 담긴 채 열리고,
//   도구 줄 [일괄 판매]를 누르면 빈 채로 열린다. 닫기 · 바탕 클릭이면 담은 것을 비우고 닫는다.
//
// ■ 무엇을 보여 주나 — 위에서부터
//   제목 '판매' · [닫기] — 응축 · 큐브 창처럼 맨 위는 창 이름만 · 탭 줄([자원][캐릭터][장비]) · 일괄 담기 줄(제외 · 등급 · [담기] — 위젯은 'InventoryToolPresenter'가 다룬다)
//   · **지금 탭의 칸 목록**(바탕이 격자를 덮으므로 창 안에서 고른다 — 눌러 담기/빼기 · 팔 수 없는 칸은 흐림)
//   · 담은 목록 머리(무엇을 파는지 · 개수 · 빨간 주의 · [비우기]) · 담은 줄 목록 · 아래 버튼.
//   ※ 개수·주의는 판매 버튼 바로 위 — 누르기 직전에 읽히는 자리다(2026-11-21 사용자: 맨 위에 있으면 창 제목처럼 보였다).
//   ※ 정렬은 넣었다가 뺐다 — 버튼이 많아 예전 도구 줄처럼 다시 헷갈렸다. 칸 순서는 인벤토리 도구 줄의 정렬을 따른다. 칸 목록은 그 탭의 것만이라 장비 탭에서 열면 장비만 판다.
//
// ■ 한 종류만 · 탭을 바꾸면 비운다 (#83 · 2026-10-25 사용자 결정 · 목업 'GameDesign/design/ui/판매분해목업.html')
//   목록은 지금 탭의 종류만 담는다. 인벤토리 탭을 다른 종류로 바꾸면 비우고 토스트로 알린다.
//   아래 버튼은 세 종류가 같은 모양이다 — [골드로 판매 · 총 N 골드 획득]. 장비 탭(장비 · 아티팩트)일 때만 [마석으로 분해 · 총 재료 N개 획득]이 옆에 붙는다.
//   장비 줄의 값은 'N G OR 마석 N' — 둘 중 하나만 받는다는 뜻이다.
// 여기서 파는 값은 **즉시 판매가**(정해진 값)다. 더 비싸게 팔려면 경매장에 올린다(즉시 판매가 이상 — 'Auction 규칙.md').
// 담고 → 판매 버튼, 이 두 단계가 곧 확인 절차라 별도 확인 모달을 두지 않는다
// (화면 중앙 팝업을 최소화한다 — 기획 P1).
// ※ 예외 — 성장 개체(키운 캐릭터 · 인챈트 장비)가 섞였으면 [판매]·[분해] 전에 한 번 묻는다 (T-170 · 2026-10-27 사용자 지적).
//   목록 머리의 경고 한 줄은 보지 못하고 지나쳤다 — 다시 키우기 어려운 것만 막고, 나머지는 그대로 바로 판다.
//
// ■ 담긴 내용을 여기서 들고 있지 않다
// 격자('InventoryGridPresenter')도 같은 목록을 봐야 담김 표시를 켤 수 있다. 둘 중 하나가
// 들고 있으면 패널끼리 서로를 참조하게 되므로, 상태는 'SellCartModel'에 두고 양쪽이 구독한다
// ('Inventory 규칙.md').
//
// ※ 이 자리는 원래 "고른 항목의 상세"였다. 2026-09-04에 판매 목록이 들어오면서 역할이 바뀌었고,
//   이름도 2026-09-12에 'StorageInformationPresenter' → 'SellCartPresenter'로 맞췄다
//   ('SellCartModel'과 짝이 된다). 칸의 상세 정보는 이 패널로 돌아오지 않는다 —
//   커서 옆 호버 UI로 간다(일감 'T-050').
//
// ■ 자원과 개체는 패킷이 다르다 (T-075 · 2026-10-03)
//   자원은 'C_ItemSellRequest', 캐릭터·장비는 'C_EntitySellRequest'. 섞어 담았으면 [판매] 한 번에 둘 다 보내고
//   응답 둘을 다 받은 뒤에 대기를 닫는다. 각각 전부 되거나 전혀 안 되므로, 한쪽만 실패하면
//   성공한 쪽만 목록에서 빠지고 실패한 쪽은 사유와 함께 남는다.
//
// ■ 아티팩트 (T-151 · 2026-10-25 사용자 결정)
//   장비 탭의 아티팩트 칸을 우클릭하면 장비와 같은 목록에 담긴다 — 판매는 'C_ArtifactSellRequest'로 따로 나간다.
//   NEW(도감에 없는 번호)가 담기면 경고한다. 분해는 장비와 한 요청으로 나간다 — 'C_EquipDisassembleRequest.Artifacts'
//   (#89 · 마석 수는 'DisassembleTable.ArtifactItemCount' — 장비의 2배).
public class SellCartPresenter : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("판매 줄 프리팹 (SellCartRowView 포함). 담긴 수만큼 만들어 재사용한다")]
    private SellCartRowView rowPrefab = null!;

    [SerializeField, Tooltip("줄이 쌓이는 부모 — Sell Scroll View Panel > Viewport > Content")]
    private RectTransform rowParent = null!;

    [SerializeField, Tooltip("담긴 것이 없을 때의 안내 문구. 빈 칸은 고장과 구분되지 않는다")]
    private TMP_Text emptyText = null!;

    [SerializeField, Tooltip("목록 머리 — 무엇을 팔고 있는지 · 개수('장비 판매 / 분해 3개'). 문구는 코드가 쓴다")]
    [FormerlySerializedAs("totalText")]
    private TMP_Text kindText = null!;

    [SerializeField, Tooltip("상위 등급·성장 개체가 담겼을 때만 켜지는 경고 글씨. 오판매를 막는 자리다 — 문구는 코드가 쓴다(LocalizedText를 붙이지 않는다)")]
    private TMP_Text warningText = null!;

    [SerializeField, Tooltip("[골드로 판매] 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button sellButton = null!;

    [SerializeField, Tooltip("[골드로 판매] 글자 — 받을 총 골드를 코드가 쓴다(LocalizedText를 붙이지 않는다)")]
    private TMP_Text sellButtonText = null!;

    [SerializeField, Tooltip("[마석으로 분해] 버튼 — 장비를 담았을 때만 보인다. OnClick은 코드가 연결한다")]
    private Button disassembleButton = null!;

    [SerializeField, Tooltip("[마석으로 분해] 글자 — 받을 재료 수량을 코드가 쓴다(LocalizedText를 붙이지 않는다)")]
    private TMP_Text disassembleButtonText = null!;

    [SerializeField, Tooltip("[골드로 판매] 왼쪽 아이콘 — 동전. 그림은 코드가 넣는다(그림 저장소가 없으면 숨긴다)")]
    private Image sellIcon = null!;

    [SerializeField, Tooltip("[마석으로 분해] 왼쪽 아이콘 — 받을 재료(마석)의 아이템 아이콘. 그림은 코드가 넣는다")]
    private Image disassembleIcon = null!;

    [SerializeField, Tooltip("비우기 버튼 — 담은 것을 모두 빼고 패널을 닫는다. OnClick은 코드가 연결한다")]
    private Button clearButton = null!;

    [SerializeField, Tooltip("인벤토리 탭 줄")]
    [FormerlySerializedAs("storageTabs")]
    private InventoryTabPresenter inventoryTabs = null!;

    [SerializeField, Tooltip("큐브 창 — 이 자리를 빌려 쓴다. 열리면 판매 목록을 비우고 물러난다")]
    private EquipEnchantPresenter equipEnchant = null!;

    [SerializeField, Tooltip("응축 창 — 이 자리를 빌려 쓴다. 열리면 판매 목록을 비우고 물러난다")]
    private CharacterCondensePresenter characterCondense = null!;

    [SerializeField, Tooltip("아티팩트 창(가운데 팝업) — 열려 있는 동안 판매 목록은 물러난다(비우지 않는다)")]
    private ArtifactEquipPresenter artifactWindow = null!;

    [CenterHeader("창")]
    [SerializeField, Tooltip("[닫기] — 담은 것을 비우고 창을 닫는다. OnClick은 코드가 연결한다")]
    private Button closeButton = null!;

    [SerializeField, Tooltip("칸 목록이 쌓이는 부모(GridLayoutGroup이 있는 스크롤 Content)")]
    private RectTransform pickParent = null!;

    [SerializeField, Tooltip("칸 프리팹 — 인벤토리와 같은 SlotView")]
    private SlotView slotPrefab = null!;

    [SerializeField, NonReorderable, Tooltip("창 안 탭 버튼 — 자원 · 캐릭터 · 장비 순. 누르면 인벤토리 탭을 바꾼다(격자 · 칸 목록이 따라온다)")]
    private Button[] tabButtons = new Button[0];

    [SerializeField, NonReorderable, Tooltip("칸을 늘어놓을 격자 — 첫째가 기본(인벤토리). 우클릭으로 담은 격자의 칸을 늘어놓는다(창고 격자)")]
    private InventoryGridPresenter[] grids = new InventoryGridPresenter[0];

    // 창 안 탭 버튼의 순서 — 'tabButtons'와 같다
    private static readonly InventoryTab[] TabOrder = { InventoryTab.Resource, InventoryTab.Character, InventoryTab.Equipment };

    // 창이 열려 있나 — 담은 것이 없어도 [일괄 판매]로 연 창은 남는다.
    private bool _isOpen;

    // 창이 떴다 ('InventoryToolPresenter'가 구독 — 창 안 일괄 담기 드롭다운의 머리를 다시 쓴다).
    public event Action? Shown;

    // 칸 목록을 가져올 격자 — 마지막으로 우클릭 담기를 한 격자. 기본은 인벤토리.
    private InventoryGridPresenter _source = null!;

    // 칸 목록 — 열 때마다 개수가 달라 꺼 두었다가 다시 쓴다
    private UIRowList<SlotView> _pickSlots = null!;
    private readonly List<SlotData>            _pickData    = new List<SlotData>();
    private readonly List<int>                 _pickNumbers = new List<int>();
    private readonly Dictionary<SlotView, int> _pickIndex   = new Dictionary<SlotView, int>();

    // 만들어 둔 줄. 담고 빼기를 반복하므로 파괴하지 않고 꺼 두었다가 다시 쓴다.
    private UIRowList<SellCartRowView> _rows = null!;

    private PlayerDataModel   _data    = null!;
    private SellCartModel     _cart    = null!;
    private NetworkManager    _network = null!;
    private ServerWaitManager _wait    = null!;
    private UIManager         _ui      = null!;

    // 진행 중인 판매 대기의 손잡이. 응답이 오면 결과를 보고하고, 무응답이면 스스로 타임아웃돼 잠금을 푼다.
    private ServerWaitHandle? _waitHandle;

    // 응답을 기다리는 중인가 — 연타로 두 번 나가면 두 번 팔린다.
    private bool _isWaiting;

    // 아직 안 온 응답 수(자원·개체 각 1) · 그 사이에 받은 실패 — 둘 다 오면 대기를 한 번에 닫는다.
    private int          _pendingResponses;
    private EResultCode? _failedCode;
    private long         _gainedGold;   // 이번 판매로 받은 골드 — 응답이 다 오면 동전을 한 번에 날린다
    private int          _gainedShards; // 이번 분해로 받을 재료 수 — 성공 토스트에 쓴다(응답에는 델타가 없다)
    private int          _shardItemId;  // 이번 분해로 받을 재료 TID — 응답이 올 때는 카트가 이미 비어 있어 보낼 때 적어 둔다

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 배선 → 초기화 순서로 진행한다 (클라 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다.
    private void Start()
    {
        this.RequireRef(rowPrefab,         nameof(rowPrefab));
        this.RequireRef(rowParent,         nameof(rowParent));
        this.RequireRef(emptyText,         nameof(emptyText));
        this.RequireRef(kindText,          nameof(kindText));
        this.RequireRef(warningText,       nameof(warningText));
        this.RequireRef(sellButton,        nameof(sellButton));
        this.RequireRef(sellButtonText,    nameof(sellButtonText));
        this.RequireRef(disassembleButton, nameof(disassembleButton));
        this.RequireRef(disassembleButtonText, nameof(disassembleButtonText));
        this.RequireRef(sellIcon,              nameof(sellIcon));
        this.RequireRef(disassembleIcon,       nameof(disassembleIcon));
        this.RequireRef(clearButton,       nameof(clearButton));
        this.RequireRef(inventoryTabs,     nameof(inventoryTabs));
        this.RequireRef(equipEnchant,      nameof(equipEnchant));
        this.RequireRef(characterCondense, nameof(characterCondense));
        this.RequireRef(artifactWindow,    nameof(artifactWindow));
        this.RequireRef(closeButton,       nameof(closeButton));
        this.RequireRef(pickParent,        nameof(pickParent));
        this.RequireRef(slotPrefab,        nameof(slotPrefab));

        if (tabButtons.Length != TabOrder.Length)
        {
            throw new InvalidOperationException($"{name}: 'tabButtons'는 자원 · 캐릭터 · 장비 {TabOrder.Length}개여야 한다.");
        }

        for (int i = 0; i < tabButtons.Length; i++)
        {
            InventoryTab tab = TabOrder[i];

            tabButtons[i].onClick.AddListener(() => OnPopupTabClicked(tab));
        }

        if (grids.Length == 0)
        {
            throw new InvalidOperationException($"{name}: 'grids'가 비어 있다 — 인벤토리 격자를 첫째로 넣을 것.");
        }

        _source = grids[0];

        _data    = Services.Get<PlayerDataModel>();
        _cart    = Services.Get<SellCartModel>();
        _network = NetworkManager.Instance;
        _wait    = Services.Get<ServerWaitManager>();
        _ui      = Services.Get<UIManager>();

        // ⚠️ 탭 구독만 Start/OnDestroy에 건다 — 이 패널은 자기 오브젝트를 끄기 때문이다.
        //    'OnDisable'에서 풀면 다시 켤 신호를 받을 길이 사라져 자원 탭에 영영 못 돌아온다
        //    (도구 줄이 같은 이유로 그렇게 한다 → 'Inventory 규칙.md').
        inventoryTabs.TabChanged  += OnTabChanged;
        equipEnchant.OpenChanged  += OnEnchantOpenChanged;
        characterCondense.OpenChanged += OnEnchantOpenChanged;
        artifactWindow.OpenChanged    += OnArtifactWindowOpenChanged;
        _cart.Changed             += OnCartChanged; // 비어 있으면 꺼지므로 켜는 신호도 여기서 받는다

        // 줄은 재사용하므로 만들 때 한 번만 구독한다 — 다시 걸면 중복으로 쌓인다.
        _rows = new UIRowList<SellCartRowView>(rowPrefab, rowParent, row => row.RemoveClicked += OnRowRemoveClicked, row => row.Clear());
        _pickSlots = new UIRowList<SlotView>(slotPrefab, pickParent, onCreated: slot =>
        {
            slot.LeftClicked  += OnPickClicked;
            slot.RightClicked += OnPickRightClicked;
            slot.SetNameVisible(false); // 인벤토리 칸처럼 — 이름은 툴팁 제목
            slot.gameObject.AddComponent<TooltipTrigger>().SetProvider(() => BuildPickTooltip(slot));
        });

        foreach (InventoryGridPresenter grid in grids)
        {
            grid.Redrawn     += OnGridRedrawn;
            grid.SellToggled += OnGridSellToggled;
        }

        closeButton.onClick.AddListener(Close);
        BackdropClose.Bind(gameObject, Close);

        Subscribe();
        sellButton.onClick.AddListener(OnSellClicked);
        disassembleButton.onClick.AddListener(OnDisassembleClicked);
        clearButton.onClick.AddListener(OnClearClicked);
        Refresh(); // 인벤토리를 닫아 둔 사이에 담긴 것이 있을 수 있다

        _isReady = true;

        ApplyTab(inventoryTabs.CurrentTab); // 탭 줄이 먼저 돌아 통지가 지나갔을 수 있다
    }

    // 탭 구독 해제 (Unity 메시지). 자기 오브젝트를 끄므로 여기서만 푼다.
    private void OnDestroy()
    {
        inventoryTabs.TabChanged -= OnTabChanged;
        equipEnchant.OpenChanged -= OnEnchantOpenChanged;
        characterCondense.OpenChanged -= OnEnchantOpenChanged;
        artifactWindow.OpenChanged    -= OnArtifactWindowOpenChanged;
        _cart.Changed             -= OnCartChanged;

        foreach (InventoryGridPresenter grid in grids)
        {
            grid.Redrawn     -= OnGridRedrawn;
            grid.SellToggled -= OnGridSellToggled;
        }
    }

    // [일괄 판매]로 빈 창을 연다 ('InventoryToolPresenter') — 칸은 인벤토리 격자의 것.
    public void Open()
    {
        _source = grids[0];
        _isOpen = true;
        ApplyTab(inventoryTabs.CurrentTab);
    }

    // 담은 것을 비우고 닫는다 ([닫기] · 바탕 클릭). 응답을 기다리는 중이면 닫지 않는다 — 응답이 목록을 비운다.
    public void Close()
    {
        if (_isWaiting)
        {
            return;
        }

        _isOpen = false;
        _cart.Clear();
        ApplyTab(inventoryTabs.CurrentTab);
    }

    // 창을 보일지 정한다 (Start · TabChanged · 카트 변경 · 대기 종료 · 열기/닫기).
    //
    // 칸을 우클릭해 담는 순간 열리고('OnCartChanged'가 '_isOpen'을 켠다), [닫기]·바탕 클릭이면 닫힌다.
    // 다 팔아 비어도 열린 채 남는다 — 이어서 칸 목록에서 더 고를 수 있다.
    // ※ 판매 응답을 기다리는 동안은 비어도 남는다 — 응답 전에 카트가 먼저 비워진다.
    // ※ 큐브 창·응축 창·아티팩트 창이 열려 있으면 물러난다.
    private void ApplyTab(InventoryTab tab)
    {
        bool show    = (_isOpen || _cart.Count > 0 || _isWaiting) && !equipEnchant.IsOpen && !characterCondense.IsOpen && !artifactWindow.IsOpen;
        bool wasShown = gameObject.activeSelf;

        gameObject.SetActive(show);

        if (show && !wasShown)
        {
            Shown?.Invoke();
        }

        if (show && _isReady)
        {
            PaintTabs();
            DrawPicks();
        }
    }

    // 탭이 바뀌었다 — 담긴 종류와 다른 탭이면 목록을 비우고 알린다 (InventoryTabPresenter.TabChanged 구독 · Start/OnDestroy).
    // ※ 응답을 기다리는 중이면 비우지 않는다 — 응답이 성공한 쪽을 스스로 비운다.
    private void OnTabChanged(InventoryTab tab)
    {
        SellCartKind? tabKind = tab switch
        {
            InventoryTab.Resource  => SellCartKind.Item,
            InventoryTab.Character => SellCartKind.Character,
            InventoryTab.Equipment => SellCartKind.Equip,
            _                      => null,
        };

        if (!_isWaiting && tabKind != null && _cart.Kind is SellCartKind kind && kind != tabKind)
        {
            _cart.Clear();
            _wait.RaiseToast(Lang.Get("UI.Sell.ClearedByTab"));
        }

        ApplyTab(tab);
    }

    // 카트가 바뀌었다 — 비었으면 물러나고, 처음 담겼으면 나타난다 (SellCartModel.Changed 구독 · Start/OnDestroy).
    private void OnCartChanged()
    {
        if (_cart.Count > 0)
        {
            _isOpen = true; // 우클릭으로 담았다 — 비워도 창은 남는다
        }

        ApplyTab(inventoryTabs.CurrentTab);
    }

    // 격자가 다시 그렸다 — 칸 목록도 따라 그린다 (InventoryGridPresenter.Redrawn 구독).
    private void OnGridRedrawn()
    {
        if (_isReady && isActiveAndEnabled)
        {
            DrawPicks();
        }
    }

    // 그 격자에서 우클릭으로 담았다 — 칸 목록을 그 격자의 것으로 (InventoryGridPresenter.SellToggled 구독).
    private void OnGridSellToggled(InventoryGridPresenter grid)
    {
        _source = grid;
    }

    // 칸 목록을 그린다 — 지금 탭의 칸 전부, 격자와 같은 표시 (ApplyTab · OnGridRedrawn).
    private void DrawPicks()
    {
        _source.CollectSellSlots(_pickData, _pickNumbers);
        _pickIndex.Clear();

        for (int i = 0; i < _pickData.Count; i++)
        {
            SlotView slot = _pickSlots.Get(i);
            SlotData data = _pickData[i];

            slot.Bind(data);
            _source.DrawSellSlot(slot, data);
            _pickIndex[slot] = i;
        }

        _pickSlots.HideFrom(_pickData.Count);
    }

    // 창 안 탭을 눌렀다 — 인벤토리 탭을 바꾼다. 담긴 종류와 다르면 'OnTabChanged'가 목록을 비우고 알린다.
    // ※ 응답을 기다리는 중이면 바꾸지 않는다 — 보낸 목록과 화면이 어긋난다.
    private void OnPopupTabClicked(InventoryTab tab)
    {
        if (_isWaiting)
        {
            return;
        }

        inventoryTabs.RequestTab(tab);
        PaintTabs(); // 떠나기가 막혔으면 눌린 버튼을 되돌린다
    }

    // 지금 탭의 버튼만 선택 색으로 — 인벤토리 탭 줄과 같은 칠하기(ColorBlock).
    private void PaintTabs()
    {
        for (int i = 0; i < tabButtons.Length; i++)
        {
            Color      target = UIThemePalette.Of(TabOrder[i] == inventoryTabs.CurrentTab ? UIThemeRole.ButtonSelected : UIThemeRole.Button);
            ColorBlock colors = tabButtons[i].colors;

            colors.normalColor      = target;
            colors.highlightedColor = UIThemePalette.HoverOf(target);
            colors.pressedColor     = UIThemePalette.PressedOf(target);
            colors.selectedColor    = target;
            tabButtons[i].colors    = colors;
        }
    }

    // 칸을 눌렀다 — 담거나 뺀다(수량은 격자 우클릭과 같게 묻는다) (SlotView.LeftClicked · RightClicked 구독).
    private void OnPickClicked(SlotView slot)
    {
        if (_isWaiting || slot.IsEmpty)
        {
            return;
        }

        _source.ToggleSell(slot.Key, slot.transform);
    }

    // 칸을 우클릭했다 — Ctrl이면 경매 등록(창을 닫고 경매장으로), 아니면 담거나 뺀다 (SlotView.RightClicked 구독).
    // ※ 인벤토리 칸과 같게 (2026-11-25) — 같은 칸인데 판매 창 안에서만 Ctrl+우클릭이 담기로 읽혔다.
    private void OnPickRightClicked(SlotView slot)
    {
        if (_isWaiting || slot.IsEmpty)
        {
            return;
        }

        if (_source.TryQuickAuction(slot.Key, slot.transform))
        {
            Close();

            return;
        }

        _source.ToggleSell(slot.Key, slot.transform);
    }

    // 칸 툴팁 — 격자 칸과 같다.
    private TooltipContent? BuildPickTooltip(SlotView slot)
    {
        if (slot.IsEmpty || !_pickIndex.TryGetValue(slot, out int index) || index >= _pickNumbers.Count)
        {
            return null;
        }

        return _source.BuildSellTooltip(slot.Key, _pickNumbers[index]);
    }

    // 담은 것을 모두 뺀다 — 창은 남는다 (clearButton OnClick에 코드로 연결).
    private void OnClearClicked()
    {
        if (!_isWaiting)
        {
            _cart.Clear();
        }
    }

    // 큐브 창·응축 창이 열리고 닫혔다 — 열리면 목록을 비우고 물러난다 (둘의 OpenChanged 구독).
    // ※ 응축 창도 비운다 — 재료로 고른 캐릭터가 카트에도 남아 있으면 판매와 응축이 같은 개체를 두고 다툰다.
    //
    // ★ 비우는 이유(2026-10-03 사용자 결정): 보이지 않는 카트에 담긴 것이 남으면, 돌아왔을 때
    //   "언제 담았지"가 되고 큐브를 쓰려던 장비가 카트에 섞여 함께 팔릴 수 있다.
    // ※ 판매 응답을 기다리는 중이면 비우지 않는다 — 응답이 성공한 쪽을 스스로 비운다.
    private void OnEnchantOpenChanged(bool open)
    {
        if (open && !_isWaiting)
        {
            _isOpen = false;
            _cart.Clear();
        }

        ApplyTab(inventoryTabs.CurrentTab);
    }

    // 아티팩트 창이 열리고 닫혔다 — 물러나고 돌아온다 (ArtifactEquipPresenter.OpenChanged 구독).
    // ※ 큐브 창과 달리 비우지 않는다 — 아티팩트 창은 보기와 장착만 하고 칸을 바꾸지 않아, 담아 둔 것과 다툴 일이 없다.
    //   우클릭으로 담으면 창이 닫히고 목록이 돌아온다(격자).
    private void OnArtifactWindowOpenChanged(bool open)
    {
        ApplyTab(inventoryTabs.CurrentTab);
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    //
    // ★ 재구독만으로는 부족하다 — 닫혀 있는 동안 채취로 수량이 줄어 카트가 깎였을 수 있다.
    private void OnEnable()
    {
        if (_isReady)
        {
            Subscribe();
            Refresh();
        }
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
    }

    #region 구독

    // 판매 목록 변경·판매 응답 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed              = true;
        _cart.Changed             += Refresh;
        Lang.Changed              += Refresh;
        _data.ItemSellCompleted   += OnSellCompleted;
        _data.ItemSellFailed      += OnSellFailed;
        _data.EntitySellCompleted += OnEntitySellCompleted;
        _data.EntitySellFailed    += OnSellFailed;
        _data.EquipDisassembleCompleted += OnDisassembleCompleted;
        _data.EquipDisassembleFailed    += OnSellFailed;
        _data.ArtifactSellCompleted     += OnArtifactSellCompleted;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed              = false;
        _cart.Changed             -= Refresh;
        Lang.Changed              -= Refresh;
        _data.ItemSellCompleted   -= OnSellCompleted;
        _data.ItemSellFailed      -= OnSellFailed;
        _data.EntitySellCompleted -= OnEntitySellCompleted;
        _data.EntitySellFailed    -= OnSellFailed;
        _data.EquipDisassembleCompleted -= OnDisassembleCompleted;
        _data.EquipDisassembleFailed    -= OnSellFailed;
        _data.ArtifactSellCompleted     -= OnArtifactSellCompleted;
    }

    #endregion

    #region 목록 그리기

    // 줄 툴팁 — 담은 수량 · 보유 · 개당 즉시 판매가 · 합계, 그리고 경매로 올리면의 단가 범위.
    // ※ 담긴 수량은 올리는 순간의 카트에서 읽는다 — 줄을 그린 뒤 담기·빼기가 있었을 수 있다.
    private TooltipContent? BuildRowTooltip(int itemId)
    {
        long count = 0L;

        foreach (ItemInfo entry in _cart.Entries)
        {
            if (entry.ItemId == itemId)
            {
                count = entry.Count;
            }
        }

        if (count <= 0L)
        {
            return null;
        }

        GlobalRarity rarity    = GameDataLoader.GetItemRarity(itemId);
        int          basePrice = GameDataLoader.GetItemPrice(itemId);

        return new TooltipContent(GameDataLoader.GetItemName(itemId))
            .Grade(rarity)
            .Row(Lang.Get("UI.Sell.CartCount"), Lang.Format("UI.Inventory.CountUnit", $"{count:N0}"), $"{Lang.Get("UI.Inventory.Owned")} {_data.GetItemCount(itemId):N0}", null)
            .Row(Lang.Get("UI.Equip.InstantSellPrice"), Lang.Format("UI.Equip.Gold", $"{AuctionModel.InstantSellPrice(basePrice):N0}"), Lang.Get("UI.Sell.Each"), null)
            .Row(Lang.Get("UI.Sell.GoldToGet"), Lang.Format("UI.Equip.Gold", $"{AuctionModel.InstantSellTotal(basePrice, count):N0}"))
            .Header(Lang.Get("UI.Sell.IfAuctioned"))
            .Row(Lang.Get("UI.Equip.AuctionPrice"), AuctionModel.FormatBand(basePrice), Lang.Get("UI.Sell.UnitPrice"), null)
            .Row(Lang.Get("UI.Sell.SetInAuction"), "");
    }

    // 개체 줄 툴팁 — 등급 · 성장(있을 때) · 즉시 판매가, 그리고 경매로 올리면의 단가 범위.
    // 'addGrowthRow' — ★ · 인챈트 등급 줄. 성장하지 않은 개체는 null이다.
    private static TooltipContent BuildEntityTooltip(string name, GlobalRarity rarity, int basePrice, Action<TooltipContent>? addGrowthRow)
    {
        var content = new TooltipContent(name)
            .Grade(rarity);

        addGrowthRow?.Invoke(content);

        return content
            .Row(Lang.Get("UI.Equip.InstantSellPrice"), Lang.Format("UI.Equip.Gold", $"{AuctionModel.InstantSellTotal(basePrice, 1):N0}"))
            .Header(Lang.Get("UI.Sell.IfAuctioned"))
            .Row(Lang.Get("UI.Equip.AuctionPrice"), AuctionModel.FormatBand(basePrice), "", null)
            .Row(Lang.Get("UI.Sell.SetInAuction"), "");
    }

    // 담긴 목록·합계·경고·버튼 상태를 화면에 반영한다 (Start · OnEnable · SellCartModel.Changed 구독).
    // 줄 순서 = 자원 → 캐릭터 → 장비 → 아티팩트.
    private void Refresh()
    {
        int index = 0;

        foreach (ItemInfo entry in _cart.Entries)
        {
            SellCartRowView row       = _rows.Get(index++);
            int             itemId    = entry.ItemId;
            int             basePrice = GameDataLoader.GetItemPrice(itemId);

            row.Bind(SellCartKind.Item,
                     itemId,
                     GameDataLoader.GetItemName(itemId),
                     entry.Count,
                     UIRichText.GoldOnDark(AuctionModel.InstantSellTotal(basePrice, entry.Count)),
                     ItemIconContent.ForItem(itemId, 0L),
                     () => BuildRowTooltip(itemId));
        }

        foreach (long characterId in _cart.CharacterIds)
        {
            SellCartRowView row       = _rows.Get(index++);
            int             tid       = _data.GetCharacterTid(characterId);
            string          name      = GameDataLoader.GetCharacterName(tid);
            int             basePrice = GameDataLoader.GetCharacterPrice(tid);
            GlobalRarity    rarity    = GameDataLoader.GetCharacterRarity(tid);
            MikaProtocol.CharacterInfo? character = _data.FindCharacter(characterId);
            int             level     = character?.Level ?? 1;
            int             star      = character?.Star ?? 0;
            int             condensed = character?.CondenseCount ?? 0;
            bool            leveled   = level >= SellCartModel.GrownCharacterLevel;

            // 키운 캐릭터는 이름 뒤에 Lv · ★를 붙인다 — 성장 개체가 목록에서 바로 보이게(T-170).
            string label = name;

            if (leveled)
            {
                label += $" {UIRichText.Paint($"Lv{level}", UIThemeRole.Highlight)}";
            }

            if (star > 0)
            {
                label += $" {UIRichText.Paint(new string('★', star), UIThemeRole.Highlight)}";
            }

            row.Bind(SellCartKind.Character,
                     characterId,
                     label,
                     1,
                     UIRichText.GoldOnDark(AuctionModel.InstantSellTotal(basePrice, 1)),
                     ItemIconContent.ForCharacter(tid).WithWarehouse(_data.IsCharacterInWarehouse(characterId)),
                     () => BuildEntityTooltip(name, rarity, basePrice, c =>
                     {
                         RaceLabel.AddRow(c, tid);

                         if (leveled)
                         {
                             c.Row(Lang.Get("UI.Inventory.Level"), $"Lv{level}", "", null);
                         }

                         if (condensed > 0)
                         {
                             c.Row(Lang.Get("UI.Inventory.Condense"), Lang.Format("UI.Inventory.Star", star), "", null);
                         }
                     }));
        }

        foreach (EquipInfo equip in _data.Equips)
        {
            if (!_cart.ContainsEquip(equip.EquipId) || !GameDataLoader.TryGetEquip(equip.EquipTid, out EquipTableRow equipRow))
            {
                continue;
            }

            SellCartRowView row       = _rows.Get(index++);
            string          name      = Loc.Get(equipRow.NameLocKey);
            int             basePrice = equipRow.BasePrice;
            GlobalRarity    rarity    = equipRow.GlobalRarity;
            var             enchant   = (GlobalRarity)equip.EnchantGrade;
            bool            isGrown   = SellCartModel.IsGrown(equip);

            // 인챈트 장비는 이름 뒤에 인챈트 등급을 그 등급색으로 붙인다(T-170).
            // 값은 'N G OR 마석 N' — 판매와 분해 중 하나만 받는다(#83).
            row.Bind(SellCartKind.Equip,
                     equip.EquipId,
                     isGrown ? $"{name} {UIRichText.Paint(RarityLabel.Get(enchant), RarityPalette.Get(enchant))}" : name,
                     1,
                     EquipValueText(AuctionModel.InstantSellTotal(basePrice, 1), rarity),
                     ItemIconContent.ForEquip(equip.EquipTid, equip.EnchantOptions).WithWarehouse(equip.Container == EContainer.Warehouse),
                     () => BuildEntityTooltip(name, rarity, basePrice, isGrown ? c => c.Row(Lang.Get("UI.Equip.EnchantGrade"), RarityLabel.Get(enchant), "", RarityPalette.Get(enchant)) : null));
        }

        foreach (ArtifactInfo entry in _cart.Artifacts)
        {
            SellCartRowView row = _rows.Get(index++);
            int             tid = entry.ArtifactTid;

            // NEW(도감에 없는 번호)는 이름 뒤에 붙인다 — 팔면 도감에 남지 않는다
            string name = GameDataLoader.GetArtifactName(tid);

            if (!_data.IsArtifactEquipped(tid))
            {
                name += $" {UIRichText.Paint(Lang.Get("UI.Artifact.New"), UIThemeRole.Highlight)}";
            }

            // 값은 장비와 같은 'N G OR 마석 N'
            row.Bind(SellCartKind.Artifact,
                     tid,
                     name,
                     entry.Count,
                     ShardValueText(SellCartModel.ArtifactInstantPrice(tid) * entry.Count, SellCartModel.ArtifactShards(tid) * entry.Count),
                     ItemIconContent.ForArtifact(tid, 0L),
                     () => ArtifactText.BuildTooltip(_data, tid));
        }

        int count = index;

        _rows.HideFrom(count);

        emptyText.gameObject.SetActive(count == 0);
        kindText.text = KindText(count);

        // 상위 등급·성장 개체는 다시 모으기 어렵다. 목록에서 빼지 않고 눈에 띄게만 알린다 —
        // 빼 버리면 "왜 안 담기지"가 되고, 팔 자유는 남겨 둔다.
        ApplyWarning();

        ApplySellButton();

        // 방금 만든 줄은 아직 프리팹에 저장된 크기 그대로다 — uGUI의 레이아웃 계산은
        // 이 프레임 **맨 끝**(Canvas.willRenderCanvases)에 돌기 때문이다.
        // 그 사이 'WidgetPositionLayout.VerifyNoOverflow'가 'LateUpdate'에서 훑고 지나가
        // "자식이 부모보다 넓다" → 다음 검사에서 "해소됐다"가 왕복으로 찍힌다.
        // 여기서 미리 태워 두면 아무도 반쯤 놓인 줄을 보지 않는다.
        // ※ 프리팹 크기를 부모보다 작게 저장해 피하는 방법은 자식 하나만 커도 다시 터진다.
        LayoutRebuilder.ForceRebuildLayoutImmediate(rowParent);
    }

    // 경고 글씨 — 상위 등급 · 성장 개체(몇 개인지)를 줄로 쌓는다. 둘 다 없으면 끈다.
    // ※ 별도 확인 창을 띄우지 않는다 — [판매] 바로 위의 이 줄이 확인 절차다(클래스 머리 주석).
    private void ApplyWarning()
    {
        var lines = new List<string>(4);

        if (_cart.HasHighRarity)
        {
            lines.Add(Lang.Get("UI.Sell.HighRarity"));
        }

        if (_cart.GrownCount > 0)
        {
            lines.Add(Lang.Format("UI.Sell.GrownWarning", _cart.GrownCount));
        }

        if (_cart.HasUncollectedArtifact)
        {
            lines.Add(Lang.Get("UI.Artifact.SellUncollected"));
        }

        warningText.text = string.Join("\n", lines);
        warningText.color = UIThemePalette.OfFor(warningText, UIThemeRole.Negative); // 빨강 — 머리 글자(금색)와 갈려 주의로 읽힌다(2026-11-21 사용자)
        warningText.gameObject.SetActive(lines.Count > 0);
    }

    // 아래 버튼 — 받을 총량을 적고, 담긴 것이 없거나 응답을 기다리는 중이면 잠근다.
    // [마석으로 분해]는 장비 탭(장비 · 아티팩트)을 담았을 때 보인다(#83). 아티팩트도 함께 분해한다(#89).
    private void ApplySellButton()
    {
        bool isEquip = _cart.Kind == SellCartKind.Equip && _cart.Count > 0;

        sellButtonText.text = ButtonText(Lang.Get("UI.Sell.ToGold"),
                                         Lang.Format("UI.Sell.GoldGain", UIRichText.Paint($"{_cart.TotalPrice:N0}", UIThemeRole.Highlight)));
        disassembleButton.gameObject.SetActive(isEquip);
        SetIcon(sellIcon, FxSprites.CoinBody);

        if (isEquip)
        {
            SetIcon(disassembleIcon, VisualCatalog.ItemIconOf(_cart.ShardItemId));
            disassembleButtonText.text = ButtonText(Lang.Get("UI.Sell.ToShard"),
                                                    Lang.Format("UI.Sell.ShardGain", GameDataLoader.GetItemName(_cart.ShardItemId),
                                                                UIRichText.Paint($"{_cart.TotalShards:N0}", UIThemeRole.Highlight)));
        }

        sellButton.interactable        = !_isWaiting && _cart.Count > 0;
        disassembleButton.interactable = !_isWaiting && isEquip && _cart.TotalShards > 0;
        clearButton.interactable       = !_isWaiting;
    }

    // 버튼 왼쪽 아이콘 — 그림이 없으면(그림 저장소 없음) 숨긴다
    private static void SetIcon(Image icon, Sprite? sprite)
    {
        icon.sprite = sprite!;
        icon.gameObject.SetActive(sprite != null);
    }

    // 버튼 두 줄 — 위는 무엇을 하는지, 아래는 받을 총량.
    private static string ButtonText(string action, string gain) => $"{action}\n{UIRichText.Small(gain)}";

    // 목록 머리 — '자원 판매 2종' · '캐릭터 판매 3명' · '장비 판매 / 분해 3개'.
    private string KindText(int count)
    {
        // 비어 있으면 지금 탭의 종류로 — [일괄 판매]로 빈 창을 열었다
        SellCartKind kind = _cart.Kind ?? inventoryTabs.CurrentTab switch
        {
            InventoryTab.Character => SellCartKind.Character,
            InventoryTab.Equipment => SellCartKind.Equip,
            _                      => SellCartKind.Item,
        };

        string key = kind switch
        {
            SellCartKind.Character => "UI.Sell.KindCharacter",
            SellCartKind.Equip     => _cart.HasArtifacts && _cart.EquipIds.Count == 0 ? "UI.Sell.KindArtifact" : "UI.Sell.KindEquip",
            _                      => "UI.Sell.KindItem",
        };

        return Lang.Format(key, $"{count:N0}");
    }

    // 장비 줄 값 — 'N G OR 마석 N'. 분해 표에 없는 등급이면 골드만.
    private static string EquipValueText(long gold, GlobalRarity rarity)
    {
        if (!GameDataLoader.TryGetDisassemble(rarity, out DisassembleTableRow row))
        {
            return UIRichText.GoldOnDark(gold);
        }

        return ShardValueText(gold, row.ItemCount);
    }

    // 'N G OR 마석 N' — 장비 · 아티팩트 줄이 같이 쓴다
    private static string ShardValueText(long gold, int shards)
        => $"{UIRichText.GoldOnDark(gold)} {UIRichText.Label(Lang.Get("UI.Sell.Or"))} {Lang.Format("UI.Sell.ShardShort", $"{shards:N0}")}";

    #endregion

    #region 판매 요청

    // 줄의 빼기를 눌렀다 (SellCartRowView.RemoveClicked 구독)
    private void OnRowRemoveClicked(SellCartRowView row)
    {
        if (_isWaiting)
        {
            return; // 이미 나간 요청의 목록을 바꾸면 화면과 요청이 어긋난다
        }

        switch (row.Kind)
        {
            case SellCartKind.Character: _cart.RemoveCharacter(row.Key); break;
            case SellCartKind.Equip:     _cart.RemoveEquip(row.Key);     break;
            case SellCartKind.Artifact:  _cart.RemoveArtifact((int)row.Key); break;
            default:                     _cart.Remove((int)row.Key);     break;
        }
    }

    // 담긴 것을 한 번에 판다 (sellButton OnClick에 코드로 연결).
    //
    // 로그인 전에 보내면 서버가 User를 못 찾아 조용히 버린다 — 클라 입장에선 응답도 오류도
    // 없어서 "눌렀는데 아무 일도 안 일어난다"로만 보인다. 보내기 전에 여기서 끊고 이유를 남긴다.
    private void OnSellClicked()
    {
        if (_isWaiting || _cart.Count == 0)
        {
            return;
        }

        if (!_data.IsLoggedIn)
        {
            ClientLogger.Warn(ClientLogger.Send, "판매 요청을 보내지 않았다 — 로그인이 먼저다(서버가 응답 없이 버린다)");

            return;
        }

        AskIfGrown(sellButtonText.text, RequestSell);
    }

    // 성장 개체가 섞였으면 한 번 묻고, 아니면 바로 보낸다 (OnSellClicked · OnDisassembleClicked에서 호출).
    // 문구는 목록 머리 경고 한 줄 + 누른 버튼 글자(무엇을 · 얼마 받는지)다.
    // ※ 확인을 누른 시점에 다시 본다 — 팝업이 떠 있는 사이 응답·탭 전환으로 목록이 비었을 수 있다.
    private void AskIfGrown(string buttonText, Action send)
    {
        if (_cart.GrownCount == 0)
        {
            send();

            return;
        }

        // 누른 버튼 글자는 진홍 버튼 위에 맞춘 밝은 금색이라 밝은 팝업에서 안 보였다 — 어두운 상세 상자에 그대로 옮긴다(2026-11-15 사용자)
        _ui.AskConfirm(
            Lang.Format("UI.Sell.GrownWarning", _cart.GrownCount),
            () =>
            {
                if (!_isWaiting && _cart.Count > 0)
                {
                    send();
                }
            },
            $"<align=center>{buttonText}</align>");
    }

    // 담긴 것을 판매 요청으로 보낸다 (OnSellClicked · 확인 팝업에서 호출).
    private void RequestSell()
    {
        _pendingResponses = 0;
        _failedCode       = null;
        _gainedGold       = 0;

        if (_cart.HasItems)
        {
            _network.Send(new C_ItemSellRequest
            {
                Items = _cart.ToRequestItems()
            });
            _pendingResponses++;
        }

        if (_cart.HasEntities)
        {
            // 응답에 개체 ID가 없다 — 무엇을 지울지 모델에 먼저 적어 둔다.
            var characterIds = new List<long>(_cart.CharacterIds);
            var equipIds     = new List<long>(_cart.EquipIds);

            _data.BeginEntitySell(characterIds, equipIds);
            _network.Send(new C_EntitySellRequest
            {
                CharacterIds = characterIds,
                EquipIds     = equipIds,
            });
            _pendingResponses++;
        }

        if (_cart.HasArtifacts)
        {
            _network.Send(new C_ArtifactSellRequest { Artifacts = _cart.ToRequestArtifacts() });
            _pendingResponses++;
        }

        ClientLogger.Info(ClientLogger.Send, $"판매 요청 — {_cart.Count}줄(패킷 {_pendingResponses}개), 예상 {_cart.TotalPrice:N0} G");

        // 대기 시작 — 로딩 표시·무응답 감시·알림은 ServerWaitManager가 공통으로 처리한다.
        _isWaiting  = true;
        ApplySellButton();
        _waitHandle = _wait.Begin(Lang.Get("UI.Sell.Wait"), onClosed: OnWaitClosed);
    }

    // 담긴 장비 · 아티팩트를 분해한다 (disassembleButton OnClick에 코드로 연결) — T-131 · #83 · #89.
    // ※ 확인 창은 판매와 같다 — 성장 개체가 섞였을 때만 묻는다('AskIfGrown').
    private void OnDisassembleClicked()
    {
        if (_isWaiting || (_cart.EquipIds.Count == 0 && !_cart.HasArtifacts))
        {
            return;
        }

        if (!_data.IsLoggedIn)
        {
            ClientLogger.Warn(ClientLogger.Send, "분해 요청을 보내지 않았다 — 로그인이 먼저다(서버가 응답 없이 버린다)");

            return;
        }

        AskIfGrown(disassembleButtonText.text, RequestDisassemble);
    }

    // 담긴 장비 · 아티팩트를 분해 요청 한 번으로 보낸다 (OnDisassembleClicked · 확인 팝업에서 호출).
    // ※ 장비와 아티팩트는 전부 되거나 전혀 안 된다(서버) — 응답은 하나다. 줄어든 아티팩트 칸은 'S_ArtifactInventoryResponse'가 따로 온다.
    private void RequestDisassemble()
    {
        // 응답에 장비 ID가 없다 — 무엇을 지울지 모델에 먼저 적어 둔다.
        var equipIds  = new List<long>(_cart.EquipIds);
        var artifacts = _cart.ToRequestArtifacts();

        _pendingResponses = 1;
        _failedCode       = null;
        _gainedGold       = 0;
        _gainedShards     = _cart.TotalShards;
        _shardItemId      = _cart.ShardItemId;

        _data.BeginDisassemble(equipIds);
        _network.Send(new C_EquipDisassembleRequest { EquipIds = equipIds, Artifacts = artifacts });

        ClientLogger.Info(ClientLogger.Send, $"분해 요청 — 장비 {equipIds.Count}개 · 아티팩트 {artifacts.Count}종, 예상 재료 {_gainedShards}개");

        _isWaiting  = true;
        ApplySellButton();
        _waitHandle = _wait.Begin(Lang.Get("UI.Sell.WaitDisassemble"), onClosed: OnWaitClosed);
    }

    // 분해 성공 — 장비를 비우고 받은 재료를 알린다 (PlayerDataModel.EquipDisassembleCompleted 구독)
    // ※ 분해한 장비는 'PlayerDataModel'이 이미 지웠다 — 카트도 따라 빼지만, 남은 것이 없게 한 번 더 비운다.
    // ※ 재료 이름은 보낼 때 적어 둔 TID로 읽는다 — 이 응답보다 장비 목록 갱신이 먼저 와서 카트가 이미 비어 있다
    //   ('ShardItemId'가 0이라 토스트가 '?#0'으로 나왔다 · 2026-10-25).
    private void OnDisassembleCompleted()
    {
        string material = GameDataLoader.GetItemName(_shardItemId);

        ClientLogger.Info(ClientLogger.Recv, $"분해 완료 — 재료 {_gainedShards}개");

        _cart.ClearEntities();
        _cart.ClearArtifacts();
        OnResponsePart();
        _wait.RaiseToast(Lang.Format("UI.Sell.DisassembleDone", material, $"{_gainedShards:N0}"));
    }

    // 대기가 끝났다(성공·실패·타임아웃 공통) — 버튼 잠금을 푼다 (ServerWaitManager.Begin의 onClosed)
    private void OnWaitClosed()
    {
        _isWaiting        = false;
        _waitHandle       = null;
        _pendingResponses = 0;
        ApplySellButton();
        ApplyTab(inventoryTabs.CurrentTab); // 다 팔렸으면 이제 물러난다
    }

    // 자원 판매 성공 — 자원을 비운다 (PlayerDataModel.ItemSellCompleted 구독)
    //
    // ※ 골드 표시는 상태바가 'CurrencyChanged'로 따로 갱신한다. 여기서 잔액을 만지지 않는다.
    private void OnSellCompleted(long gainedGold)
    {
        ClientLogger.Info(ClientLogger.Recv, $"자원 판매 완료 — {gainedGold:N0} G 획득");

        _gainedGold += gainedGold;
        _cart.ClearItems(); // Changed가 발행되어 목록·합계가 다시 그려진다
        OnResponsePart();
    }

    // 개체 판매 성공 — 개체를 비운다 (PlayerDataModel.EntitySellCompleted 구독)
    // ※ 판 개체는 'PlayerDataModel'이 이미 지웠다 — 카트도 그 변경을 따라 빼지만, 남은 것이 없게 한 번 더 비운다.
    private void OnEntitySellCompleted(long gainedGold)
    {
        ClientLogger.Info(ClientLogger.Recv, $"개체 판매 완료 — {gainedGold:N0} G 획득");

        _gainedGold += gainedGold;
        _cart.ClearEntities();
        OnResponsePart();
    }

    // 아티팩트 판매 결과 — 성공이면 아티팩트를 비운다 (PlayerDataModel.ArtifactSellCompleted 구독)
    // ※ 칸은 서버가 보내는 칸 스냅샷으로 줄어든다 — 카트도 그 변경을 따라 빼지만, 남은 것이 없게 한 번 더 비운다.
    private void OnArtifactSellCompleted(EResultCode code, long gainedGold)
    {
        if (code != EResultCode.Ok)
        {
            OnSellFailed(code);

            return;
        }

        ClientLogger.Info(ClientLogger.Recv, $"아티팩트 판매 완료 — {gainedGold:N0} G 획득");

        _gainedGold += gainedGold;
        _cart.ClearArtifacts();
        OnResponsePart();
    }

    // 판매 실패 — 사유를 기억해 둔다 (PlayerDataModel.ItemSellFailed · EntitySellFailed 구독)
    //
    // ★ 실패한 쪽 목록을 비우지 않는다. 전부 되거나 전혀 안 되므로 아무것도 팔리지 않았고,
    //   담아 둔 것을 다시 고르게 하면 조작을 처음부터 반복시키는 셈이다.
    private void OnSellFailed(EResultCode code)
    {
        _failedCode ??= code;
        OnResponsePart();
    }

    // 응답 하나가 왔다 — 보낸 것이 다 오면 대기를 닫는다(실패가 하나라도 있으면 그 사유로).
    private void OnResponsePart()
    {
        if (_waitHandle == null || --_pendingResponses > 0)
        {
            return;
        }

        if (_failedCode is EResultCode code)
        {
            _waitHandle.Fail(ResultMessages.ToText(code));
        }
        else
        {
            _waitHandle.Succeed();
            Services.Get<SoundManager>().PlaySfx(Sounds.Sell);
        }

        // 자원·개체 응답이 따로 와도 동전은 한 번만 — 판매 버튼에서 상단 골드로
        if (_gainedGold > 0)
        {
            _wait.RaiseGoldGain(sellButton.transform, _gainedGold);
        }
    }

    #endregion
}

using System;
using System.Collections.Generic;
using System.Globalization;
using GameData;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 검색 탭의 분류 트리 한 칸. 값의 순서는 **화면의 트리 순서와 같게 유지한다** — 씬에 int로 저장된다.
//
// 목업은 자원 · 상자 · 기타를 맨 위 단에 두었지만, 서버 분류(Category)는 값 하나라 "산업 자원 5종 전부"를 고를 수 없다.
// 그래서 아이템을 한 부모로 묶고 산업 5종 · 상자 · 소모품 · 기타 · 유물을 그 아래에 둔다(T-124 · T-150의 ItemType 그대로).
public enum AuctionSearchNode
{
    All,
    Item, ItemFarming, ItemExcavation, ItemMining, ItemLogging, ItemHunting, ItemBox, ItemConsumable, ItemMisc, ItemRelic,
    Equip, EquipWeapon, EquipAccessory, EquipGem,
    Character,
    Artifact,
}

// 검색 탭 — 왼쪽 옵션(분류 트리 · 등급 · 분류별 상세 옵션 · 단가 범위) + 오른쪽 결과(건수 · 적용 조건 · 정렬 · 표).
// 경매장 목업(T-147)의 메이플식 검색이다. 정렬·건수·속성 필터는 서버가 한다(T-155).
//
// ■ 고치는 값과 조회한 값은 다르다
//   왼쪽에서 고친 값은 [검색]을 눌러야 조회된다 — 조회에 빈도 제한(순간 10회 · 1초마다 1회 회복)이 있어서다.
//   **분류만은 누르면 바로 조회한다**(목업). 정렬을 바꾸면 마지막으로 조회한 조건('_applied')에 정렬만 바꿔 다시 받는다.
// ■ 결과는 쪽으로 나눈다 — 한 쪽 9줄('AuctionModel.RowsPerPage'), 표 아래 [◀][▶]로 넘긴다(2026-10-14 사용자 결정).
//   쪽을 넘길 때마다 서버에 묻는다(빈도 제한 안) — 받는 동안 대기를 건다.
// ■ 조건 없는 '전체' 검색은 보내지 않는다 — 분류를 고르거나 이름을 넣어야 조회한다 (2026-10-14 사용자 결정).
//   분류 '전체' · 이름 없음이면 서버가 판매 중 매물 전부를 훑고 정렬한다(서버 확인 이슈). 처음 열 때도 같다 — 안내만 띄운다.
// ■ 이름은 공용 검색창에서 온다('IAuctionQueryTarget') — 클라가 TID 목록으로 바꿔 보낸다.
//   ⚠️ 맞는 이름이 없으면 보내지 않는다 — 빈 TID 목록은 서버가 "조건 없음"으로 읽어 전체를 돌려준다.
//   ⚠️ '전체' 분류의 이름 검색은 네 종류의 TID를 합쳐 보낸다 — 장비·캐릭터 TID가 겹쳐 다른 종류가 섞일 수 있어 그릴 때 이름으로 한 번 더 거른다.
// ■ 구매: 사기 전에 시세를 본다 — 확인 창에 최저가 · 최근가 · 전일 평균과 "전일 평균보다 몇 % 비싼가/싼가".
//   시세 요약이 없으면 한 번 받고(대기) 연다. 산 물건은 **우편**으로 온다.
// ■ 내 매물은 꼬리표를 달고 [구매]를 잠근다 — 판별 재료(내 매물 목록)는 화면을 열 때마다 조용히 받는다(빈도 제한 밖).
public class AuctionSearchPresenter : MonoBehaviour, IAuctionQueryTarget
{
    // 분류 트리 한 칸 — 노드와 그 버튼.
    [Serializable]
    private struct NodeEntry
    {
        [Tooltip("이 버튼이 어느 분류인가")]
        public AuctionSearchNode node;

        [Tooltip("트리의 버튼")]
        public Button button;

        [Tooltip("버튼 글자 — ▸/▾ 펼침 표시를 코드가 붙인다")]
        public TMP_Text label;
    }

    // 정렬 드롭다운 항목 — 항목 순서 = 이 순서. 서버 정렬(T-155)만 둔다.
    private enum SortPreset
    {
        PriceAsc,
        PriceDesc,
        ExpiresSoon,
        RarityDesc,
        SpeedDesc, // 장비 — 작업속도(기본 + 인챈트)
        LevelDesc, // 캐릭터
    }

    // 정렬 드롭다운의 문구 키 — 'SortPreset' 순서와 같다.
    private static readonly string[] SortLabelKeys =
    {
        "UI.Auction.SortPriceAsc", "UI.Auction.SortPriceDesc", "UI.Auction.SortExpires", "UI.Auction.SortRarity", "UI.Auction.SortSpeed", "UI.Auction.SortLevel",
    };

    private static readonly GlobalRarity[] RarityOptions =
    {
        GlobalRarity.None, GlobalRarity.Common, GlobalRarity.Uncommon, GlobalRarity.Rare,
        GlobalRarity.Epic, GlobalRarity.Legendary, GlobalRarity.Mythic,
    };

    private static readonly EIndustryType[] IndustryOptions =
    {
        EIndustryType.None, EIndustryType.Farming, EIndustryType.Mining, EIndustryType.Logging, EIndustryType.Hunting, EIndustryType.Excavation,
    };

    [CenterHeader("분류")]
    [SerializeField, NonReorderable, Tooltip("분류 트리 버튼 — AuctionSearchNode마다 한 줄. 자식 노드는 부모가 펼쳐졌을 때만 보인다")]
    private NodeEntry[] nodes = new NodeEntry[0];

    [SerializeField, Tooltip("등급 드롭다운 — 전체·일반~신화. 항목은 코드가 채운다")]
    private TMP_Dropdown rarityDropdown = null!;


    [CenterHeader("장비 옵션")]
    [SerializeField, Tooltip("장비 분류일 때만 보이는 묶음")]
    private GameObject equipOptions = null!;

    [SerializeField, Tooltip("산업 드롭다운 — 전체·농사~사냥")]
    private TMP_Dropdown equipIndustryDropdown = null!;

    [SerializeField, Tooltip("작업속도 % 이상 (기본 + 인챈트)")]
    private TMP_InputField equipSpeedInput = null!;

    [SerializeField, Tooltip("인챈트 등급 하한 드롭다운 — 전체·일반 이상~신화 이상")]
    private TMP_Dropdown enchantGradeDropdown = null!;

    [SerializeField, Tooltip("인챈트 작업속도 옵션 합 % 이상")]
    private TMP_InputField enchantSpeedInput = null!;

    [SerializeField, Tooltip("인챈트 경험치 옵션 합 % 이상")]
    private TMP_InputField enchantExpInput = null!;


    [CenterHeader("캐릭터 옵션")]
    [SerializeField, Tooltip("캐릭터 분류일 때만 보이는 묶음")]
    private GameObject characterOptions = null!;

    [SerializeField, Tooltip("레벨 하한")]
    private TMP_InputField levelMinInput = null!;

    [SerializeField, Tooltip("레벨 상한")]
    private TMP_InputField levelMaxInput = null!;

    [SerializeField, Tooltip("응축 ★ 하한 드롭다운 — 전체·★1 이상~")]
    private TMP_Dropdown starDropdown = null!;

    [SerializeField, Tooltip("적성 산업 드롭다운 — 전체·농사~사냥")]
    private TMP_Dropdown aptitudeDropdown = null!;

    [SerializeField, Tooltip("그 산업 적성 하한")]
    private TMP_InputField aptitudeMinInput = null!;


    [CenterHeader("단가 · 버튼")]
    [SerializeField, Tooltip("단가 하한")]
    private TMP_InputField priceMinInput = null!;

    [SerializeField, Tooltip("단가 상한")]
    private TMP_InputField priceMaxInput = null!;

    [SerializeField, Tooltip("[초기화] — 옵션만 비운다(검색어는 둔다). OnClick은 코드가 연결한다")]
    private Button resetButton = null!;

    [SerializeField, Tooltip("[검색]. OnClick은 코드가 연결한다")]
    private Button searchButton = null!;


    [CenterHeader("결과 머리")]
    [SerializeField, Tooltip("'검색 결과 N건'")]
    private TMP_Text countText = null!;

    [SerializeField, Tooltip("마지막으로 조회한 조건 — 분류 · 이름 · 등급 · 옵션 · 단가")]
    private TMP_Text appliedText = null!;

    [SerializeField, Tooltip("'n분 전에 받음'")]
    private TMP_Text receivedText = null!;

    [SerializeField, Tooltip("[새로고침] — 같은 조건으로 첫 쪽부터. OnClick은 코드가 연결한다")]
    private Button refreshButton = null!;

    [SerializeField, Tooltip("정렬 드롭다운. 항목은 코드가 채운다")]
    private TMP_Dropdown sortDropdown = null!;


    [CenterHeader("열 머리 정렬")]
    [SerializeField, Tooltip("'아이템 (등급)' 열 머리 — 등급 높은순")]
    private Button rarityHeaderButton = null!;

    [SerializeField, Tooltip("'단가' 열 머리 — 낮은순 ↔ 높은순")]
    private Button priceHeaderButton = null!;

    [SerializeField, Tooltip("'남은 시간' 열 머리 — 마감 임박순")]
    private Button timeHeaderButton = null!;


    [CenterHeader("목록")]
    [SerializeField, Tooltip("검색 결과 한 줄 프리팹 (AuctionSearchRowView)")]
    private AuctionTableRowView rowPrefab = null!;

    [SerializeField, Tooltip("줄이 쌓이는 Content (VLG + ContentSizeFitter)")]
    private Transform rowParent = null!;

    [SerializeField, Tooltip("결과 스크롤 — 쪽을 받으면 맨 위로 올린다")]
    private ScrollRect scrollRect = null!;

    [SerializeField, Tooltip("결과가 없을 때만 보인다")]
    private GameObject emptyText = null!;

    [SerializeField, Tooltip("표 아래 쪽 넘김 줄 — [◀] 'n / N 쪽' [▶]")]
    private AuctionPagerView pager = null!;


    [CenterHeader("탭 사이 길")]
    [SerializeField, Tooltip("경매장 캔버스 — 줄의 [시세]가 시세 탭을 연다")]
    private AuctionCanvasView canvasView = null!;

    private readonly List<AuctionTableRowContent> _contents = new List<AuctionTableRowContent>();
    private readonly HashSet<AuctionSearchNode>   _expanded = new HashSet<AuctionSearchNode>();

    private AuctionModel      _auction = null!;
    private PlayerDataModel   _data    = null!;
    private ServerWaitManager _wait    = null!;
    private UIManager         _ui      = null!;
    private AuctionTableRowList _rows  = null!;

    // 고치는 중인 분류와 검색어 — 왼쪽 옵션은 위젯 값 그대로 읽는다.
    private AuctionSearchNode _node = AuctionSearchNode.All;
    private string            _query = "";

    // 마지막으로 조회한 조건 — 정렬만 바꿔 다시 받을 때 쓴다. 이름 검색어도 함께 둔다(전체 분류의 이름 거르기).
    private C_AuctionSearchRequest? _applied;
    private string                  _appliedQuery = "";
    private string                  _appliedLabel = "";
    private AuctionSearchNode       _appliedNode  = AuctionSearchNode.All;

    private SortPreset _sort = SortPreset.PriceAsc;

    // 진행 중인 대기의 손잡이 — 첫 쪽 검색 · 구매 · 구매 전 시세.
    private ServerWaitHandle? _waitHandle;

    // 조건 없이 [검색]했다 — 결과 자리에 안내를 띄운다. 조건을 넣어 조회하면 풀린다.
    private bool _needsCondition;

    // 결과 없음 글자 — 평소 문구와 조건 안내를 오간다.
    private TMP_Text _emptyLabel = null!;

    // 구매 전 시세를 기다리는 매물 — 0이면 없다.
    private long _pendingBuyListingId;

    // Start 전에 들어온 "이 아이템 매물 보기"('SearchFor') — Start 끝에서 검색한다.
    private bool _hasPendingSearch;

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // ─── 공용 검색창 ───
    public string QueryPlaceholder => Lang.Get("UI.Auction.SearchPlaceholder");
    public string QueryHint        => Lang.Get("UI.Auction.SearchHint");
    public string Query            => _query;

    public void OnQueryChanged(string text) => _query = text;

    public void OnQuerySubmitted(string text)
    {
        _query = text;
        RunSearch();
    }

    // 지금 분류의 종류 안에서 이름이 맞는 것 — '전체'면 네 종류 모두.
    public void CollectSuggestions(string query, List<Suggestion> into, int max)
        => AuctionText.CollectCatalogSuggestions(GetNodeFilter(_node).kind, query, into, max);

    // 후보를 고르면 그 이름으로 바로 조회한다 — 왼쪽 옵션은 그대로 둔다.
    public void OnSuggestionPicked(Suggestion suggestion) => RunSearch();

    // 참조 확보 → 구독 → 배선 → 초기화 순서로 진행한다 (클라 공통 규약)
    private void Start()
    {
        this.RequireRef(rarityDropdown,        nameof(rarityDropdown));
        this.RequireRef(equipOptions,          nameof(equipOptions));
        this.RequireRef(equipIndustryDropdown, nameof(equipIndustryDropdown));
        this.RequireRef(equipSpeedInput,       nameof(equipSpeedInput));
        this.RequireRef(enchantGradeDropdown,  nameof(enchantGradeDropdown));
        this.RequireRef(enchantSpeedInput,     nameof(enchantSpeedInput));
        this.RequireRef(enchantExpInput,       nameof(enchantExpInput));
        this.RequireRef(characterOptions,      nameof(characterOptions));
        this.RequireRef(levelMinInput,         nameof(levelMinInput));
        this.RequireRef(levelMaxInput,         nameof(levelMaxInput));
        this.RequireRef(starDropdown,          nameof(starDropdown));
        this.RequireRef(aptitudeDropdown,      nameof(aptitudeDropdown));
        this.RequireRef(aptitudeMinInput,      nameof(aptitudeMinInput));
        this.RequireRef(priceMinInput,         nameof(priceMinInput));
        this.RequireRef(priceMaxInput,         nameof(priceMaxInput));
        this.RequireRef(resetButton,           nameof(resetButton));
        this.RequireRef(searchButton,          nameof(searchButton));
        this.RequireRef(countText,             nameof(countText));
        this.RequireRef(appliedText,           nameof(appliedText));
        this.RequireRef(receivedText,          nameof(receivedText));
        this.RequireRef(refreshButton,         nameof(refreshButton));
        this.RequireRef(sortDropdown,          nameof(sortDropdown));
        this.RequireRef(rarityHeaderButton,    nameof(rarityHeaderButton));
        this.RequireRef(priceHeaderButton,     nameof(priceHeaderButton));
        this.RequireRef(timeHeaderButton,      nameof(timeHeaderButton));
        this.RequireRef(rowPrefab,             nameof(rowPrefab));
        this.RequireRef(rowParent,             nameof(rowParent));
        this.RequireRef(scrollRect,            nameof(scrollRect));
        this.RequireRef(emptyText,             nameof(emptyText));

        _emptyLabel = emptyText.GetComponentInChildren<TMP_Text>(true);
        this.RequireRef(_emptyLabel, nameof(_emptyLabel));
        this.RequireRef(pager,                 nameof(pager));
        this.RequireRef(canvasView,            nameof(canvasView));

        _auction = Services.Get<AuctionModel>();
        _data    = Services.Get<PlayerDataModel>();
        _wait    = Services.Get<ServerWaitManager>();
        _ui      = Services.Get<UIManager>();
        _rows    = new AuctionTableRowList(rowPrefab, rowParent, OnRowBuyClicked, OnRowPriceClicked);

        ValidateNodes();
        FillDropdowns();
        Subscribe();
        BindWidgets();

        RefreshTree();
        RefreshOptions();
        Refresh();
        RequestMyListings();

        _isReady = true;

        if (_hasPendingSearch || (!_auction.HasSearched && _data.IsLoggedIn))
        {
            _hasPendingSearch = false;
            RunSearch();
        }
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    private void OnEnable()
    {
        if (!_isReady)
        {
            return;
        }

        Subscribe();
        OnLanguageChanged(); // 꺼져 있는 동안 언어가 바뀌었을 수 있다 — 드롭다운·트리까지 다시 쓴다(Refresh 포함)
        RequestMyListings();

        if (_hasPendingSearch)
        {
            _hasPendingSearch = false;
            RunSearch();
        }
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
    }

    #region 구독

    // 검색·구매·시세 응답과 재화 변경 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed = true;

        _auction.SearchCompleted     += OnSearchCompleted;
        _auction.BuyCompleted        += OnBuyCompleted;
        _auction.SummaryCompleted    += OnSummaryCompleted;
        _auction.MyListingsCompleted += OnMyListingsCompleted;
        _data.CurrencyChanged        += Refresh;
        Lang.Changed                 += OnLanguageChanged;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed = false;

        _auction.SearchCompleted     -= OnSearchCompleted;
        _auction.BuyCompleted        -= OnBuyCompleted;
        _auction.SummaryCompleted    -= OnSummaryCompleted;
        _auction.MyListingsCompleted -= OnMyListingsCompleted;
        _data.CurrencyChanged        -= Refresh;
        Lang.Changed                 -= OnLanguageChanged;
    }

    #endregion

    #region 초기화

    // 트리 배선이 'AuctionSearchNode'와 맞는지 본다 (Start에서 한 번).
    private void ValidateNodes()
    {
        foreach (AuctionSearchNode node in Enum.GetValues(typeof(AuctionSearchNode)))
        {
            if (Array.FindIndex(nodes, entry => entry.node == node && entry.button != null && entry.label != null) < 0)
            {
                ClientLogger.Error(ClientLogger.UI, $"검색 분류 '{node}'의 버튼이 없다. Search Presenter의 Nodes를 확인할 것.", this);
            }
        }
    }

    // 드롭다운 항목을 채운다 (Start에서 한 번). 이름은 공용 표시 이름을 쓴다.
    private void FillDropdowns()
    {
        var rarities = new List<string>();
        var grades   = new List<string>();

        foreach (GlobalRarity rarity in RarityOptions)
        {
            rarities.Add(rarity == GlobalRarity.None ? Lang.Get("UI.Auction.All") : RarityLabel.Get(rarity));
            grades.Add(rarity == GlobalRarity.None ? Lang.Get("UI.Auction.All") : Lang.Format("UI.Auction.OrHigher", RarityLabel.Get(rarity)));
        }

        var industries = new List<string>();

        foreach (EIndustryType industry in IndustryOptions)
        {
            industries.Add(industry == EIndustryType.None ? Lang.Get("UI.Auction.All") : IndustryLabel.Get(industry));
        }

        var stars = new List<string> { Lang.Get("UI.Auction.All") };

        for (int star = 1; star <= GameDataLoader.CondenseMaxStar; star++)
        {
            stars.Add(Lang.Format("UI.Auction.OrHigher", $"★{star}"));
        }

        Fill(rarityDropdown,        rarities);
        Fill(enchantGradeDropdown,  grades);
        Fill(equipIndustryDropdown, industries);
        Fill(aptitudeDropdown,      industries);
        Fill(starDropdown,          stars);
        Fill(sortDropdown,          new List<string>(Array.ConvertAll(SortLabelKeys, Lang.Get)));
    }

    // 항목을 바꿔 채운다 — 고른 값은 그대로 둔다(언어 변경 때 다시 채운다).
    private static void Fill(TMP_Dropdown dropdown, List<string> options)
    {
        int value = dropdown.value;

        dropdown.ClearOptions();
        dropdown.AddOptions(options);
        dropdown.SetValueWithoutNotify(Mathf.Clamp(value, 0, options.Count - 1));
    }

    // 언어가 바뀌었다 — 드롭다운 · 트리 · 결과 글자를 다시 쓴다 (Lang.Changed 구독).
    // ※ 적용 조건 칩은 다음 검색 때 새 언어로 바뀐다.
    private void OnLanguageChanged()
    {
        FillDropdowns();
        RefreshTree();
        Refresh();
    }

    // 버튼·드롭다운을 배선한다 (Start에서 한 번).
    private void BindWidgets()
    {
        foreach (NodeEntry entry in nodes)
        {
            if (entry.button == null)
            {
                continue;
            }

            // 반복 변수를 그대로 넘기면 모든 콜백이 마지막 값을 본다. 복사본을 캡처한다.
            AuctionSearchNode node = entry.node;
            entry.button.onClick.AddListener(() => OnNodeClicked(node));
        }

        resetButton.onClick.AddListener(OnResetClicked);
        searchButton.onClick.AddListener(RunSearch);
        refreshButton.onClick.AddListener(OnRefreshClicked);
        sortDropdown.onValueChanged.AddListener(index => ApplySort((SortPreset)index));
        rarityHeaderButton.onClick.AddListener(() => ApplySort(SortPreset.RarityDesc));
        priceHeaderButton.onClick.AddListener(() => ApplySort(_sort == SortPreset.PriceAsc ? SortPreset.PriceDesc : SortPreset.PriceAsc));
        timeHeaderButton.onClick.AddListener(() => ApplySort(SortPreset.ExpiresSoon));
        pager.PrevClicked += () => GoToPage(_auction.SearchPage - 1);
        pager.NextClicked += () => GoToPage(_auction.SearchPage + 1);
    }

    #endregion

    #region 분류 트리

    // 분류를 눌렀다 — 고르고 바로 조회한다. 부모는 펼친다(이미 고른 부모를 다시 누르면 접는다) (노드 버튼)
    private void OnNodeClicked(AuctionSearchNode node)
    {
        if (IsParent(node))
        {
            if (_node == node && _expanded.Contains(node))
            {
                _expanded.Remove(node);
            }
            else
            {
                _expanded.Add(node);
            }
        }

        _node = node;

        RefreshTree();
        RefreshOptions();
        RunSearch();
    }

    // 트리 글자·선택 표시·펼침을 맞춘다.
    private void RefreshTree()
    {
        foreach (NodeEntry entry in nodes)
        {
            if (entry.button == null || entry.label == null)
            {
                continue;
            }

            AuctionSearchNode? parent = GetParent(entry.node);

            entry.button.gameObject.SetActive(parent == null || _expanded.Contains(parent.Value));

            string caret = IsParent(entry.node) ? (_expanded.Contains(entry.node) ? "▼ " : "▶ ") : parent != null ? "      " : "   ";
            entry.label.text = caret + GetNodeName(entry.node);

            Color target = UIThemePalette.Of(entry.node == _node ? UIThemeRole.ButtonSelected : UIThemeRole.PanelSub);

            ColorBlock colors = entry.button.colors;
            colors.normalColor   = target;
            colors.selectedColor = target;
            entry.button.colors  = colors;
        }
    }

    // 분류에 맞는 상세 옵션 묶음만 보인다.
    private void RefreshOptions()
    {
        (EAuctionKind kind, int _) = GetNodeFilter(_node);

        equipOptions.SetActive(kind == EAuctionKind.Equip);
        characterOptions.SetActive(kind == EAuctionKind.Character);
    }

    // 노드 → (종류, 분류). 종류 None = 전부, 분류 0 = 그 종류 전부.
    private static (EAuctionKind kind, int category) GetNodeFilter(AuctionSearchNode node) => node switch
    {
        AuctionSearchNode.Item           => (EAuctionKind.Item, 0),
        AuctionSearchNode.ItemFarming    => (EAuctionKind.Item, (int)ItemType.Farming),
        AuctionSearchNode.ItemExcavation => (EAuctionKind.Item, (int)ItemType.Excavation),
        AuctionSearchNode.ItemMining     => (EAuctionKind.Item, (int)ItemType.Mining),
        AuctionSearchNode.ItemLogging    => (EAuctionKind.Item, (int)ItemType.Logging),
        AuctionSearchNode.ItemHunting    => (EAuctionKind.Item, (int)ItemType.Hunting),
        AuctionSearchNode.ItemBox        => (EAuctionKind.Item, (int)ItemType.Box),
        AuctionSearchNode.ItemConsumable => (EAuctionKind.Item, (int)ItemType.Consumable),
        AuctionSearchNode.ItemMisc       => (EAuctionKind.Item, (int)ItemType.Misc),
        AuctionSearchNode.ItemRelic      => (EAuctionKind.Item, (int)ItemType.Relic),
        AuctionSearchNode.Equip          => (EAuctionKind.Equip, 0),
        AuctionSearchNode.EquipWeapon    => (EAuctionKind.Equip, (int)EquipKind.Weapon),
        AuctionSearchNode.EquipAccessory => (EAuctionKind.Equip, (int)EquipKind.Accessory),
        AuctionSearchNode.EquipGem       => (EAuctionKind.Equip, (int)EquipKind.Gem),
        AuctionSearchNode.Character      => (EAuctionKind.Character, 0),
        AuctionSearchNode.Artifact       => (EAuctionKind.Artifact, 0),
        _                                => (EAuctionKind.None, 0),
    };

    // 이 아이템이 속하는 가장 좁은 노드 — "이 아이템 매물 보기".
    private static AuctionSearchNode GetNodeOf(EAuctionKind kind, int tid)
    {
        if (kind == EAuctionKind.Character) return AuctionSearchNode.Character;
        if (kind == EAuctionKind.Artifact)  return AuctionSearchNode.Artifact;

        if (kind == EAuctionKind.Equip)
        {
            EquipKind equipKind = GameDataLoader.TryGetEquip(tid, out EquipTableRow row) ? row.EquipKind : EquipKind.None;

            return equipKind switch
            {
                EquipKind.Weapon    => AuctionSearchNode.EquipWeapon,
                EquipKind.Accessory => AuctionSearchNode.EquipAccessory,
                EquipKind.Gem       => AuctionSearchNode.EquipGem,
                _                   => AuctionSearchNode.Equip,
            };
        }

        foreach (AuctionSearchNode node in Enum.GetValues(typeof(AuctionSearchNode)))
        {
            (EAuctionKind nodeKind, int category) = GetNodeFilter(node);

            if (nodeKind == EAuctionKind.Item && category != 0 && category == (int)GameDataLoader.GetItemType(tid))
            {
                return node;
            }
        }

        return AuctionSearchNode.Item;
    }

    private static bool IsParent(AuctionSearchNode node) => node == AuctionSearchNode.Item || node == AuctionSearchNode.Equip;

    private static AuctionSearchNode? GetParent(AuctionSearchNode node)
    {
        if (node > AuctionSearchNode.Item && node <= AuctionSearchNode.ItemRelic)    return AuctionSearchNode.Item;
        if (node > AuctionSearchNode.Equip && node <= AuctionSearchNode.EquipGem)    return AuctionSearchNode.Equip;

        return null;
    }

    private static string GetNodeName(AuctionSearchNode node) => node switch
    {
        AuctionSearchNode.All            => Lang.Get("UI.Auction.All"),
        AuctionSearchNode.Item           => Lang.Get("UI.Auction.NodeItem"),
        AuctionSearchNode.ItemFarming    => AuctionText.GetItemCategoryName(ItemType.Farming),
        AuctionSearchNode.ItemExcavation => AuctionText.GetItemCategoryName(ItemType.Excavation),
        AuctionSearchNode.ItemMining     => AuctionText.GetItemCategoryName(ItemType.Mining),
        AuctionSearchNode.ItemLogging    => AuctionText.GetItemCategoryName(ItemType.Logging),
        AuctionSearchNode.ItemHunting    => AuctionText.GetItemCategoryName(ItemType.Hunting),
        AuctionSearchNode.ItemBox        => AuctionText.GetItemCategoryName(ItemType.Box),
        AuctionSearchNode.ItemConsumable => AuctionText.GetItemCategoryName(ItemType.Consumable),
        AuctionSearchNode.ItemMisc       => AuctionText.GetItemCategoryName(ItemType.Misc),
        AuctionSearchNode.ItemRelic      => AuctionText.GetItemCategoryName(ItemType.Relic),
        AuctionSearchNode.Equip          => Lang.Get("UI.Equip.Gear"),
        AuctionSearchNode.EquipWeapon    => EquipLabel.GetKindName(EquipKind.Weapon),
        AuctionSearchNode.EquipAccessory => EquipLabel.GetKindName(EquipKind.Accessory),
        AuctionSearchNode.EquipGem       => EquipLabel.GetKindName(EquipKind.Gem),
        AuctionSearchNode.Character      => Lang.Get("UI.Common.Character"),
        _                                => Lang.Get("UI.Auction.KindArtifact"),
    };

    // "장비 › 무기" — 적용 조건 칩.
    private static string GetNodePath(AuctionSearchNode node)
    {
        AuctionSearchNode? parent = GetParent(node);

        return parent != null ? $"{GetNodeName(parent.Value)} › {GetNodeName(node)}" : GetNodeName(node);
    }

    #endregion

    #region 요청

    // 이 아이템의 매물만 찾는다 — 옵션을 비우고 분류·이름을 맞춘다 (AuctionCanvasView.OpenSearchFor).
    // ※ 탭이 아직 안 켜졌으면 켜질 때 검색한다.
    public void SearchFor(EAuctionKind kind, int tid)
    {
        _node  = GetNodeOf(kind, tid);
        _query = AuctionText.GetName(kind, tid);

        AuctionSearchNode? parent = GetParent(_node);

        if (parent != null)
        {
            _expanded.Add(parent.Value);
        }

        if (!_isReady || !isActiveAndEnabled)
        {
            _hasPendingSearch = true;

            return;
        }

        ResetOptions();
        RefreshTree();
        RefreshOptions();
        RunSearch();
    }

    // 지금 왼쪽 값으로 첫 쪽을 조회한다 (분류 · [검색] · 공용 검색창 · 처음 열 때).
    private void RunSearch()
    {
        if (_waitHandle != null || !_data.IsLoggedIn)
        {
            return;
        }

        // 조건 없는 '전체'는 보내지 않는다 — 머리 주석
        if (_node == AuctionSearchNode.All && string.IsNullOrWhiteSpace(_query))
        {
            _needsCondition = true;
            _applied        = null;
            _appliedLabel   = "";

            Refresh();

            return;
        }

        (EAuctionKind kind, int category) = GetNodeFilter(_node);

        List<int>? tids = AuctionText.FindTids(kind, _query);

        if (tids != null && tids.Count == 0)
        {
            _wait.RaiseRefusal(Lang.Format("UI.Auction.NoSuchKind", _query.Trim(), kind == EAuctionKind.None ? Lang.Get("UI.Auction.ItemWord") : AuctionText.GetKindName(kind)));

            return;
        }

        var labels  = new List<string> { GetNodePath(_node) };
        var request = new C_AuctionSearchRequest
        {
            Kind     = kind,
            Category = category,
            Tids     = tids,
            Attrs    = new List<AuctionAttrRangeInfo>(),
        };

        if (!string.IsNullOrWhiteSpace(_query))
        {
            labels.Add($"\"{_query.Trim()}\"");
        }

        GlobalRarity rarity = RarityOptions[rarityDropdown.value];

        if (rarity != GlobalRarity.None)
        {
            request.MinRarity = (int)rarity;
            request.MaxRarity = (int)rarity;
            labels.Add(RarityLabel.Get(rarity));
        }

        if (kind == EAuctionKind.Equip)
        {
            ReadEquipOptions(request, labels);
        }
        else if (kind == EAuctionKind.Character)
        {
            ReadCharacterOptions(request, labels);
        }

        long minPrice = ReadLong(priceMinInput);
        long maxPrice = ReadLong(priceMaxInput);

        request.MinUnitPrice = minPrice;
        request.MaxUnitPrice = maxPrice;

        if (minPrice > 0L || maxPrice > 0L)
        {
            labels.Add(Lang.Format("UI.Auction.PriceRange", minPrice.ToString("N0"), maxPrice > 0L ? maxPrice.ToString("N0") : "∞"));
        }

        _applied      = request;
        _appliedQuery = _query;
        _appliedNode  = _node;
        _appliedLabel = string.Join(UIRichText.Dot, labels);

        _needsCondition = false;

        SendApplied();
    }

    // 장비 옵션을 요청에 싣는다 — 산업 · 작업속도 · 인챈트 등급 · 인챈트 옵션 합 (RunSearch에서 호출).
    // ※ 속도·옵션 합은 천분율이다 — 화면의 %에 10을 곱한다.
    private void ReadEquipOptions(C_AuctionSearchRequest request, List<string> labels)
    {
        EIndustryType industry = IndustryOptions[equipIndustryDropdown.value];

        if (industry != EIndustryType.None)
        {
            request.Attrs!.Add(new AuctionAttrRangeInfo { Attr = EAuctionAttr.EquipIndustry, Min = (long)industry, Max = (long)industry });
            labels.Add(IndustryLabel.Get(industry));
        }

        AddPercentAttr(request, labels, equipSpeedInput,   EAuctionAttr.EquipSpeed,          Lang.Get("UI.Auction.AttrSpeed"));
        AddPercentAttr(request, labels, enchantSpeedInput, EAuctionAttr.EnchantSpeed,        Lang.Get("UI.Auction.AttrEnchantSpeed"));
        AddPercentAttr(request, labels, enchantExpInput,   EAuctionAttr.EnchantCharacterExp, Lang.Get("UI.Auction.AttrEnchantExp"));

        GlobalRarity grade = RarityOptions[enchantGradeDropdown.value];

        if (grade != GlobalRarity.None)
        {
            request.MinEnchantGrade = (int)grade;
            labels.Add(Lang.Format("UI.Auction.EnchantAtLeast", RarityLabel.Get(grade)));
        }
    }

    // 캐릭터 옵션을 요청에 싣는다 — 레벨 범위 · ★ · 적성 (RunSearch에서 호출).
    private void ReadCharacterOptions(C_AuctionSearchRequest request, List<string> labels)
    {
        long levelMin = ReadLong(levelMinInput);
        long levelMax = ReadLong(levelMaxInput);

        if (levelMin > 0L || levelMax > 0L)
        {
            request.Attrs!.Add(new AuctionAttrRangeInfo { Attr = EAuctionAttr.CharacterLevel, Min = levelMin, Max = levelMax });
            labels.Add($"Lv.{Math.Max(levelMin, 1L)}~{(levelMax > 0L ? levelMax.ToString() : "")}");
        }

        int star = starDropdown.value;

        if (star > 0)
        {
            request.Attrs!.Add(new AuctionAttrRangeInfo { Attr = EAuctionAttr.CharacterStar, Min = star });
            labels.Add($"★{star}↑");
        }

        EIndustryType industry = IndustryOptions[aptitudeDropdown.value];

        if (industry != EIndustryType.None)
        {
            long min = Math.Max(ReadLong(aptitudeMinInput), 1L);

            request.Attrs!.Add(new AuctionAttrRangeInfo { Attr = (EAuctionAttr)(20 + (int)industry), Min = min });
            labels.Add(Lang.Format("UI.Auction.AptitudeAtLeast", IndustryLabel.Get(industry), min));
        }
    }

    // "n % 이상" 입력칸을 천분율 속성 하한으로 싣는다. 비었거나 0이면 싣지 않는다.
    private static void AddPercentAttr(C_AuctionSearchRequest request, List<string> labels, TMP_InputField input, EAuctionAttr attr, string label)
    {
        if (!float.TryParse(input.text, NumberStyles.Float, CultureInfo.InvariantCulture, out float percent) || percent <= 0f)
        {
            return;
        }

        request.Attrs!.Add(new AuctionAttrRangeInfo { Attr = attr, Min = (long)Math.Round(percent * 10f) });
        labels.Add($"{label} {percent:0.#}%↑");
    }

    // 마지막으로 조회한 조건에 지금 정렬을 얹어 첫 쪽을 보낸다.
    private void SendApplied()
    {
        if (_applied == null)
        {
            return;
        }

        (_applied.Sort, _applied.SortAttr) = _sort switch
        {
            SortPreset.PriceDesc   => (EAuctionSort.PriceDesc,   EAuctionAttr.None),
            SortPreset.ExpiresSoon => (EAuctionSort.ExpiresSoon, EAuctionAttr.None),
            SortPreset.RarityDesc  => (EAuctionSort.RarityDesc,  EAuctionAttr.None),
            SortPreset.SpeedDesc   => (EAuctionSort.AttrDesc,    EAuctionAttr.EquipSpeed),
            SortPreset.LevelDesc   => (EAuctionSort.AttrDesc,    EAuctionAttr.CharacterLevel),
            _                      => (EAuctionSort.PriceAsc,    EAuctionAttr.None),
        };

        _auction.Search(_applied);
        BeginWait(Lang.Get("UI.Auction.WaitSearch"));
    }

    // 정렬을 바꾼다 — 서버 정렬이라 마지막 조건으로 다시 받는다 (정렬 드롭다운 · 열 머리)
    private void ApplySort(SortPreset sort)
    {
        _sort = sort;
        sortDropdown.SetValueWithoutNotify((int)sort);

        if (_waitHandle == null)
        {
            SendApplied();
        }

        Refresh();
    }

    // [새로고침] — 같은 조건으로 첫 쪽부터 다시 받는다
    private void OnRefreshClicked()
    {
        if (_waitHandle == null)
        {
            SendApplied();
        }
    }

    // [초기화] — 옵션만 비운다. 조회는 [검색]을 눌러야 한다(목업)
    private void OnResetClicked()
    {
        _node = AuctionSearchNode.All;

        ResetOptions();
        RefreshTree();
        RefreshOptions();
    }

    // 왼쪽 옵션 위젯을 처음 값으로 (OnResetClicked · SearchFor)
    private void ResetOptions()
    {
        rarityDropdown.SetValueWithoutNotify(0);
        equipIndustryDropdown.SetValueWithoutNotify(0);
        enchantGradeDropdown.SetValueWithoutNotify(0);
        starDropdown.SetValueWithoutNotify(0);
        aptitudeDropdown.SetValueWithoutNotify(0);

        foreach (TMP_InputField input in new[] { equipSpeedInput, enchantSpeedInput, enchantExpInput, levelMinInput, levelMaxInput, aptitudeMinInput, priceMinInput, priceMaxInput })
        {
            input.SetTextWithoutNotify("");
        }
    }

    // 같은 조건으로 그 쪽을 받는다 (pager [◀][▶])
    private void GoToPage(int page)
    {
        if (_waitHandle != null || !_auction.SearchGoToPage(page))
        {
            return;
        }

        BeginWait(Lang.Get("UI.Auction.WaitSearch"));
    }

    // 내 매물을 조용히 받는다 — 대기 없이, 결과가 오면 꼬리표만 다시 그린다 (Start · OnEnable).
    private void RequestMyListings()
    {
        if (_data.IsLoggedIn)
        {
            _auction.RequestMyListings();
        }
    }

    // 줄의 [시세] — 시세 탭을 연다 (AuctionTableRowView.SecondaryClicked)
    private void OnRowPriceClicked(long listingId)
    {
        AuctionListingInfo? listing = FindListing(listingId);

        if (listing != null)
        {
            canvasView.OpenPrice(listing.Kind, listing.Tid);
        }
    }

    // 줄의 [구매] — 시세 요약이 있으면 바로, 없으면 한 번 받고 확인 창을 연다 (AuctionTableRowView.PrimaryClicked)
    private void OnRowBuyClicked(long listingId)
    {
        if (_waitHandle != null)
        {
            return;
        }

        AuctionListingInfo? listing = FindListing(listingId);

        if (listing == null || _auction.IsMine(listingId))
        {
            return;
        }

        if (!_auction.RequestSummary(listing.Kind, listing.Tid))
        {
            AskBuy(listing);

            return;
        }

        _pendingBuyListingId = listingId;
        BeginWait(Lang.Get("UI.Auction.WaitPrice"));
    }

    // 구매 확인 창 — 시세(최저가 · 최근가 · 전일 평균)와 이 매물이 전일 평균보다 몇 % 비싼지 · 골드.
    private void AskBuy(AuctionListingInfo listing)
    {
        string          name    = AuctionText.GetName(listing.Kind, listing.Tid);
        MarketItemInfo? summary = _auction.GetSummary(listing.Kind, listing.Tid);
        long            after   = _data.Gold - listing.TotalPrice;

        string what = listing.Count > 1
            ? Lang.Format("UI.Auction.BuyWhatMulti", name, listing.Count.ToString("N0"), listing.UnitPrice.ToString("N0"), listing.TotalPrice.ToString("N0"))
            : $"<b>{name}</b> — {listing.TotalPrice:N0} G";

        _ui.AskConfirm(
            $"{what}\n\n" +
            $"{AuctionPriceText.FormatSummaryLine(summary)}\n" +
            $"{AuctionPriceText.FormatCompare(listing.UnitPrice, summary)}\n\n" +
            $"{UIRichText.Label(Lang.Get("UI.Inventory.Owned"))} {_data.Gold:N0} G{UIRichText.Dot}{UIRichText.Label(Lang.Get("UI.Auction.AfterBuy"))} {after:N0} G\n" +
            UIRichText.Small(UIRichText.Label(Lang.Get("UI.Auction.BuyNote"))),
            () =>
            {
                _auction.RequestBuy(listing);
                BeginWait(Lang.Get("UI.Auction.WaitBuy"));
            });
    }

    #endregion

    #region 응답 (AuctionModel 구독)

    // 검색 결과 도착 — 대기를 닫고 맨 위로 (AuctionModel.SearchCompleted 구독)
    private void OnSearchCompleted(EResultCode code)
    {
        CloseWait(code);
        scrollRect.verticalNormalizedPosition = 1f;

        Refresh();
    }

    // 시세 요약 도착 — 구매 전에 기다리던 것이면 확인 창을 연다. 못 받았어도 연다(시세 없이) (AuctionModel.SummaryCompleted 구독)
    private void OnSummaryCompleted(EResultCode code)
    {
        if (_pendingBuyListingId == 0L)
        {
            return;
        }

        AuctionListingInfo? listing = FindListing(_pendingBuyListingId);

        _pendingBuyListingId = 0L;
        _waitHandle?.Succeed();

        if (listing != null)
        {
            AskBuy(listing);
        }
    }

    // 내 매물 도착 — 꼬리표를 다시 맞춘다. 조용히 받은 것이라 대기는 없다 (AuctionModel.MyListingsCompleted 구독)
    private void OnMyListingsCompleted(EResultCode code)
    {
        Refresh();
    }

    // 구매 결과 — 내가 건 대기일 때만 닫고 알린다. 목록에서 빼는 일은 모델이 했다 (AuctionModel.BuyCompleted 구독)
    private void OnBuyCompleted(EResultCode code)
    {
        if (_waitHandle != null)
        {
            CloseWait(code);

            if (code == EResultCode.Ok)
            {
                _wait.RaiseToast(Lang.Get("UI.Auction.Bought"));
            }
        }

        Refresh();
    }

    #endregion

    #region 대기

    // 요청 대기를 연다 — 버튼을 잠그고, 닫히면 다시 연다.
    private void BeginWait(string label)
    {
        _waitHandle = _wait.Begin(label, onClosed: OnWaitClosed);
        Refresh();
    }

    // 응답 결과로 대기를 닫는다 — 실패면 사유를 알림에 띄운다.
    private void CloseWait(EResultCode code)
    {
        if (code == EResultCode.Ok)
        {
            _waitHandle?.Succeed();
        }
        else
        {
            _waitHandle?.Fail(ResultMessages.ToText(code));
        }
    }

    // 대기가 끝났다(성공·실패·타임아웃 공통) — 버튼을 다시 연다 (ServerWaitManager.Begin의 onClosed)
    private void OnWaitClosed()
    {
        _waitHandle          = null;
        _pendingBuyListingId = 0L;
        Refresh();
    }

    #endregion

    #region 표시 갱신

    // 결과 머리 · 표 · 아래 글자를 지금 상태로 그린다.
    //
    // [구매]는 세 가지를 함께 본다 — 내 매물이면, 누가 구매 중(Reserved)이면, 골드가 모자라면 잠근다.
    // ※ 골드 판단은 표시용이다. 실제 거절은 서버가 한다(NotEnoughCurrency · AuctionOwnListing).
    private void Refresh()
    {
        _contents.Clear();

        bool filterName = _appliedNode == AuctionSearchNode.All;

        foreach (AuctionListingInfo listing in _needsCondition ? Array.Empty<AuctionListingInfo>() : _auction.SearchResults)
        {
            if (filterName && !AuctionText.MatchesName(listing, _appliedQuery))
            {
                continue;
            }

            bool isMine = _auction.IsMine(listing.ListingId);
            bool canBuy = _waitHandle == null
                       && listing.State != EAuctionListingState.Reserved
                       && _data.Gold >= listing.TotalPrice;

            _contents.Add(AuctionText.ToSearchRow(listing, isMine, canBuy));
        }

        _rows.Show(_contents);
        emptyText.SetActive(_needsCondition || (_auction.HasSearched && _contents.Count == 0));
        _emptyLabel.text = Lang.Get(_needsCondition ? "UI.Auction.NeedsCondition" : "UI.Auction.SearchEmpty");

        countText.text    = Lang.Format("UI.Auction.SearchCount", UIRichText.Paint(FormatTotalCount(), UIThemeRole.Highlight));
        appliedText.text  = _appliedLabel;
        receivedText.text = _needsCondition ? "" : AuctionModel.FormatReceivedAgo(_auction.SearchReceivedAt);

        refreshButton.interactable = _waitHandle == null && _applied != null;
        searchButton.interactable  = _waitHandle == null;

        RefreshHeaders();
        RefreshFoot();
    }

    // 열 머리의 정렬 표시 — 지금 정렬인 열에 ▲▼.
    private void RefreshHeaders()
    {
        SetHeader(rarityHeaderButton, Lang.Get("UI.Auction.HeadItemRarity"), _sort == SortPreset.RarityDesc ? " ▼" : "");
        SetHeader(priceHeaderButton,  Lang.Get("UI.Auction.UnitPrice"),      _sort == SortPreset.PriceAsc ? " ▲" : _sort == SortPreset.PriceDesc ? " ▼" : "");
        SetHeader(timeHeaderButton,   Lang.Get("UI.Auction.TimeLeft"),       _sort == SortPreset.ExpiresSoon ? " ▲" : "");
    }

    private static void SetHeader(Button button, string label, string arrow)
    {
        var text = button.GetComponentInChildren<TMP_Text>();

        text.text = arrow.Length > 0 ? UIRichText.Paint(label + arrow, UIThemeRole.Accent) : label;
    }

    // 표 아래 쪽 넘김 — 'n / N 쪽' · 이 쪽이 몇째 건부터인지.
    private void RefreshFoot()
    {
        int page  = _needsCondition ? 0 : _auction.SearchPage;
        int total = _needsCondition ? 0 : _auction.SearchTotalCount;
        int shown = _needsCondition ? 0 : _auction.SearchResults.Count;
        int first = page * AuctionModel.RowsPerPage;

        string note = shown > 0 ? $"{first + 1:N0}–{first + shown:N0} / {FormatTotalCount()}" : "";

        // 건수가 상한에 걸리면 전체 쪽 수를 모른다 — 'n 쪽'만 적는다.
        bool isCapped  = !_needsCondition && _auction.IsSearchCountCapped;
        int  pageCount = isCapped ? 0 : AuctionPagerView.PageCount(total, AuctionModel.RowsPerPage);

        pager.Bind(page, pageCount,
                   canPrev: _waitHandle == null && page > 0,
                   canNext: _waitHandle == null && !_needsCondition && _auction.HasMoreResults,
                   note);
    }

    // 결과 건수 — 'n건', 서버가 세기를 멈췄으면 '1,000건 이상'.
    private string FormatTotalCount()
    {
        if (_needsCondition)
        {
            return Lang.Format("UI.Auction.Cases", 0);
        }

        return _auction.IsSearchCountCapped
            ? Lang.Format("UI.Auction.CasesOrMore", AuctionModel.SearchCountCap.ToString("N0"))
            : Lang.Format("UI.Auction.Cases", _auction.SearchTotalCount.ToString("N0"));
    }

    #endregion

    #region 보조

    // 검색 결과에서 매물을 찾는다. 없으면 null.
    private AuctionListingInfo? FindListing(long listingId)
    {
        foreach (AuctionListingInfo listing in _auction.SearchResults)
        {
            if (listing.ListingId == listingId)
            {
                return listing;
            }
        }

        return null;
    }

    // 정수 입력칸 값. 비었거나 숫자가 아니면 0.
    private static long ReadLong(TMP_InputField input) => long.TryParse(input.text, out long value) && value > 0L ? value : 0L;

    #endregion
}

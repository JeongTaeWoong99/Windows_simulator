using System.Collections.Generic;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 시세 탭 — 한 아이템의 최저가 · 판매 중 · 최근 거래가 · 전일 평균, 최근 14일 그래프, 지금 단가별 판매 수량 (T-147 · T-118 · T-119).
//
// ■ 볼 아이템을 늘어놓지 않는다(목업 3차) — 공용 검색창에 이름을 치면 뜨는 후보 · [검색], 검색 결과 줄의 [시세],
//   구매 확인·등록 창의 [시세 보기]로만 연다('AuctionCanvasView.OpenPrice').
// ■ 열 때 세 가지를 받는다 — 요약 · 가격대 · 일별 시세. 셋 다 빈도 제한에 든다(요약은 막 받은 것이 있으면 아낀다).
//   대기는 하나로 걸고 세 응답이 다 와야 닫는다.
// ■ [이 아이템 매물 보기] → 검색 탭이 그 아이템 매물만 찾는다 · [팔러 가기] → 판매 탭 + 등록 창(그 종류의 첫 물건).
//   아티팩트도 [팔러 가기]로 등록 창을 연다(T-151 · 2026-10-25) — 아티팩트 칸에 있는 것만 오른다.
public class AuctionPricePresenter : MonoBehaviour, IAuctionQueryTarget
{
    [CenterHeader("빈 화면")]
    [SerializeField, Tooltip("아직 아무것도 안 골랐을 때만 보인다 — '검색창에서 찾으세요'")]
    private GameObject emptyPanel = null!;

    [SerializeField, Tooltip("고른 뒤에만 보이는 내용 전부")]
    private GameObject contentPanel = null!;


    [CenterHeader("머리")]
    [SerializeField, Tooltip("큰 아이콘 (ItemIconView)")]
    private ItemIconView iconView = null!;

    [SerializeField, Tooltip("이름 (등급 색)")]
    private TMP_Text nameText = null!;

    [SerializeField, Tooltip("등급 · 분류 한 줄")]
    private TMP_Text subText = null!;

    [SerializeField, Tooltip("[이 아이템 매물 보기]. OnClick은 코드가 연결한다")]
    private Button showListingsButton = null!;

    [SerializeField, Tooltip("[팔러 가기]. OnClick은 코드가 연결한다")]
    private Button sellButton = null!;


    [CenterHeader("요약 4칸")]
    [SerializeField, Tooltip("최저가 값")]
    private TMP_Text lowestText = null!;

    [SerializeField, Tooltip("판매 중 값")]
    private TMP_Text availableText = null!;

    [SerializeField, Tooltip("최근 거래가 값 — 전일 평균 대비 ▲▼")]
    private TMP_Text recentText = null!;

    [SerializeField, Tooltip("전일 평균 값 — 그날 거래 수량")]
    private TMP_Text yesterdayText = null!;


    [CenterHeader("그래프 · 가격대")]
    [SerializeField, Tooltip("최근 14일 그래프")]
    private AuctionPriceChartView chart = null!;

    [SerializeField, Tooltip("지금 단가별 판매 수량 10줄")]
    private AuctionPriceLevelsView levels = null!;

    [SerializeField, Tooltip("'n분 전에 받음'")]
    private TMP_Text receivedText = null!;


    [CenterHeader("탭 사이 길")]
    [SerializeField, Tooltip("경매장 캔버스 — 매물 보기 · 팔러 가기")]
    private AuctionCanvasView canvasView = null!;

    private AuctionModel      _auction = null!;
    private PlayerDataModel   _data    = null!;
    private ServerWaitManager _wait    = null!;

    // 지금 보는 아이템. TID 0이면 아직 없다.
    private EAuctionKind _kind;
    private int          _tid;
    private string       _query = "";

    // 다 받으면 대기를 닫는다 — 남은 응답 수와 처음 실패한 코드.
    private int         _pendingResponses;
    private EResultCode _firstFailure = EResultCode.Ok;
    private System.DateTime? _receivedAt;

    private ServerWaitHandle? _waitHandle;

    // Start 전(또는 꺼져 있을 때) 들어온 'Open' — 켜질 때 받는다.
    private bool _hasPendingLoad;

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // ─── 공용 검색창 ───
    public string QueryPlaceholder => Lang.Get("UI.Auction.PriceQueryPlaceholder");
    public string QueryHint        => Lang.Get("UI.Auction.PriceQueryHint");
    public string Query            => _query;

    public void OnQueryChanged(string text) => _query = text;

    public void OnQuerySubmitted(string text)
    {
        _query = text;

        if (!AuctionText.TryFindByName(text, out EAuctionKind kind, out int tid))
        {
            _wait.RaiseRefusal(Lang.Format("UI.Auction.NoSuchItem", text.Trim()));

            return;
        }

        Open(kind, tid);
    }

    // 게임 데이터 전부에서 이름이 맞는 것 — 아직 매물이 없는 아이템도 시세(기록)는 볼 수 있다.
    public void CollectSuggestions(string query, List<Suggestion> into, int max)
        => AuctionText.CollectCatalogSuggestions(EAuctionKind.None, query, into, max);

    // 후보를 고르면 그 시세를 바로 연다.
    public void OnSuggestionPicked(Suggestion suggestion) => Open((EAuctionKind)suggestion.Kind, suggestion.Tid);

    // 참조 확보 → 구독 → 배선 → 초기화 순서로 진행한다 (클라 공통 규약)
    private void Start()
    {
        this.RequireRef(emptyPanel,         nameof(emptyPanel));
        this.RequireRef(contentPanel,       nameof(contentPanel));
        this.RequireRef(iconView,           nameof(iconView));
        this.RequireRef(nameText,           nameof(nameText));
        this.RequireRef(subText,            nameof(subText));
        this.RequireRef(showListingsButton, nameof(showListingsButton));
        this.RequireRef(sellButton,         nameof(sellButton));
        this.RequireRef(lowestText,         nameof(lowestText));
        this.RequireRef(availableText,      nameof(availableText));
        this.RequireRef(recentText,         nameof(recentText));
        this.RequireRef(yesterdayText,      nameof(yesterdayText));
        this.RequireRef(chart,              nameof(chart));
        this.RequireRef(levels,             nameof(levels));
        this.RequireRef(receivedText,       nameof(receivedText));
        this.RequireRef(canvasView,         nameof(canvasView));

        _auction = Services.Get<AuctionModel>();
        _data    = Services.Get<PlayerDataModel>();
        _wait    = Services.Get<ServerWaitManager>();

        Subscribe();

        showListingsButton.onClick.AddListener(() => canvasView.OpenSearchFor(_kind, _tid));
        sellButton.onClick.AddListener(() => canvasView.OpenRegisterFor(_kind, _tid));

        Refresh();

        _isReady = true;

        LoadIfPending();
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    private void OnEnable()
    {
        if (!_isReady)
        {
            return;
        }

        Subscribe();
        Refresh();
        LoadIfPending();
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
    }

    #region 구독

    // 시세 응답 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed = true;

        _auction.SummaryCompleted     += OnResponse;
        _auction.PriceLevelsCompleted += OnResponse;
        _auction.DailyPricesCompleted += OnResponse;
        Lang.Changed                  += Refresh; // 시세 칸 문구를 새 언어로
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed = false;

        _auction.SummaryCompleted     -= OnResponse;
        _auction.PriceLevelsCompleted -= OnResponse;
        _auction.DailyPricesCompleted -= OnResponse;
        Lang.Changed                  -= Refresh;
    }

    #endregion

    #region 요청

    // 이 아이템의 시세를 연다 (AuctionCanvasView.OpenPrice · 공용 검색창). 꺼져 있으면 켜질 때 받는다.
    public void Open(EAuctionKind kind, int tid)
    {
        _kind  = kind;
        _tid   = tid;
        _query = AuctionText.GetName(kind, tid);

        if (!_isReady || !isActiveAndEnabled)
        {
            _hasPendingLoad = true;

            return;
        }

        Load();
    }

    private void LoadIfPending()
    {
        if (_hasPendingLoad)
        {
            _hasPendingLoad = false;
            Load();
        }
    }

    // 요약 · 가격대 · 일별 시세를 받는다 — 대기 하나로 묶는다.
    private void Load()
    {
        if (_waitHandle != null || _tid == 0 || !_data.IsLoggedIn)
        {
            Refresh();

            return;
        }

        _pendingResponses = 2;
        _firstFailure     = EResultCode.Ok;

        if (_auction.RequestSummary(_kind, _tid))
        {
            _pendingResponses++;
        }

        _auction.RequestPriceLevels(_kind, _tid);
        _auction.RequestDailyPrices(_kind, _tid);

        _waitHandle = _wait.Begin(Lang.Get("UI.Auction.Price"), onClosed: OnWaitClosed);
        Refresh();
    }

    #endregion

    #region 응답 (AuctionModel 구독)

    // 요약·가격대·일별 시세 중 하나가 왔다 — 다 오면 대기를 닫는다 (AuctionModel.*Completed 구독)
    private void OnResponse(EResultCode code)
    {
        if (_pendingResponses <= 0)
        {
            Refresh();

            return;
        }

        if (code != EResultCode.Ok && _firstFailure == EResultCode.Ok)
        {
            _firstFailure = code;
        }

        _pendingResponses--;

        if (_pendingResponses == 0)
        {
            _receivedAt = System.DateTime.Now;

            if (_firstFailure == EResultCode.Ok)
            {
                _waitHandle?.Succeed();
            }
            else
            {
                _waitHandle?.Fail(ResultMessages.ToText(_firstFailure));
            }
        }

        Refresh();
    }

    // 대기가 끝났다(성공·실패·타임아웃 공통) (ServerWaitManager.Begin의 onClosed)
    private void OnWaitClosed()
    {
        _waitHandle       = null;
        _pendingResponses = 0;
        Refresh();
    }

    #endregion

    #region 표시 갱신

    // 고른 아이템의 머리 · 요약 · 그래프 · 가격대를 그린다. 받기 전이면 빈 값으로.
    private void Refresh()
    {
        bool has = _tid != 0;

        emptyPanel.SetActive(!has);
        contentPanel.SetActive(has);

        if (!has)
        {
            return;
        }

        var rarity = AuctionText.GetRarity(_kind, _tid);

        iconView.Bind(AuctionText.ToIcon(_kind, _tid));
        nameText.text  = AuctionText.GetName(_kind, _tid);
        nameText.color = RarityPalette.Get(rarity);
        subText.text   = $"{RarityLabel.Get(rarity)}{UIRichText.Dot}{AuctionText.FormatSub(_kind, _tid)}";

        showListingsButton.interactable = _waitHandle == null;
        sellButton.interactable         = _waitHandle == null;

        RefreshSummary();

        bool dailyMine = _auction.DailyTid == _tid && _auction.DailyKind == _kind;
        bool levelMine = _auction.PriceTid == _tid && _auction.PriceKind == _kind;

        chart.Bind(dailyMine ? _auction.DailyPrices : System.Array.Empty<MarketDailyPriceInfo>());
        levels.Bind(levelMine ? _auction.PriceLevels : System.Array.Empty<MarketPriceLevelInfo>());

        receivedText.text = AuctionModel.FormatReceivedAgo(_receivedAt);
    }

    // 요약 4칸 — 큰 숫자 + 작은 단위.
    private void RefreshSummary()
    {
        MarketItemInfo? summary = _auction.GetSummary(_kind, _tid);

        if (summary == null)
        {
            lowestText.text = availableText.text = recentText.text = yesterdayText.text = UIRichText.Label("—");

            return;
        }

        lowestText.text    = summary.AvailableCount > 0 ? Big(summary.LowestUnitPrice, "G") : $"—{Unit(" " + Lang.Get("UI.Auction.NoListings"))}";
        availableText.text = Big(summary.AvailableCount, Lang.Get("UI.Auction.CountWord"));
        recentText.text    = summary.RecentUnitPrice > 0
            ? $"{Big(summary.RecentUnitPrice, "G")} {UIRichText.Small(AuctionPriceText.FormatTrend(summary.RecentUnitPrice, summary.YesterdayAvgPrice))}"
            : $"—{Unit(" " + Lang.Get("UI.Auction.NoTrades"))}";

        long yesterdayCount = 0L;

        if (_auction.DailyTid == _tid && _auction.DailyKind == _kind && _auction.DailyPrices.Count >= 2)
        {
            yesterdayCount = _auction.DailyPrices[_auction.DailyPrices.Count - 2].Count;
        }

        yesterdayText.text = summary.YesterdayAvgPrice > 0
            ? $"{Big(summary.YesterdayAvgPrice, "G")}{Unit(" · " + Lang.Format("UI.Auction.TradeCount", yesterdayCount.ToString("N0")))}"
            : $"—{Unit(" " + Lang.Get("UI.Auction.NoTrades"))}";
    }

    // "12,345 G" — 값은 강조색, 단위는 작게.
    private static string Big(long value, string unit) => $"{UIRichText.Paint(value.ToString("N0"), UIThemeRole.Highlight)}{Unit(" " + unit)}";

    private static string Unit(string text) => $"<size=55%>{UIRichText.Label(text)}</size>";

    #endregion
}

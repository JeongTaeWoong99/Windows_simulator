using System;
using System.Collections.Generic;
using GameData;
using MikaNetwork;
using MikaProtocol;

// 경매장 창구 — 조회 결과(검색·시세 요약·가격대·일별 시세·거래 기록·내 매물)를 들고, 요청도 여기서 보낸다.
//
// ■ 왜 이 모델은 송신도 하나
// 'PlayerDataModel'·'SellCartModel'은 송신하지 않고 요청은 Presenter가 보낸다. 여기는 다르다 —
// 검색은 **응답이 준 커서(정렬 값, 매물 ID)로** 다음 쪽을 받는다. 검색 조건·쪽마다의 커서·지금 쪽이 한 곳에
// 있어야 앞뒤 쪽이 같은 조건으로 이어진다. 조건은 Presenter에, 결과는 모델에 두면 둘이 어긋난 채로 다음 쪽을 부른다.
// 대기 표시('ServerWaitManager')는 여전히 요청을 일으킨 Presenter가 건다.
//
// ■ 여기 없는 것
// - 인벤토리 반영: 등록하면 물건이 빠진다 — 'PlayerDataModel'이 같은 응답을 구독해 뺀다(내 계정 상태의 주인).
// - 산 물건·판매 대금·취소 반환: 전부 **우편**으로 온다('S_MailArrivedResponse' → 'PlayerDataModel').
// - 골드: 'S_CurrencyResponse' → 'PlayerDataModel.Gold'.
//
// ■ 가격 규칙은 표시용이다
// 두 가지 파는 법을 화면이 구분해 말하도록 식을 여기 모은다 — 판정은 서버가 한다.
//   즉시 판매(인벤토리): 즉시 판매가(BasePrice x SellRatePermille)에 바로 판다. 값이 정해져 있다.
//   경매 등록(경매장)  : 단가를 직접 정한다. 즉시 판매가 이상이면 얼마든(상한 1조 — 자율 경제, 2026-10-05 · 옛 x10 밴드 폐지 · #56).
// 수치는 'Constants.xlsx'의 Auction*·Market* 행 — 코드에 박지 않는다.
//
// ■ 내 매물 표시
// 매물에는 판매자 이름('SellerName')만 있다 — 이름은 겹칠 수 있어 내 것은 내 매물 ID로 가린다.
// 내 매물 조회는 빈도 제한 밖이라 검색 화면을 열 때마다 조용히 받는다('RequestMyListings').
//
// ■ 검색은 하나다 (T-147 2단계)
// 옛날엔 장비·캐릭터 화면이 따로라 축마다 상태를 들었다. 지금은 검색 탭 하나가 분류 트리로 종류를 고른다 —
// 조건·결과·커서·건수를 하나만 든다. 정렬·건수·속성 필터는 서버가 한다(T-155).
//
// ■ 시세 요약은 (종류, TID)마다 잠깐 기억한다 ('GetSummary')
// 시세 탭 · 구매 확인 · 등록 창이 같은 요약(최저가·판매 중·최근가·전일 평균)을 본다. 조회에 빈도 제한이 있어
// 'SummaryFreshFor' 안에 다시 물으면 보내지 않는다. 응답에는 종류가 없어 보낸 순서('_pendingSummaries')로 맞춘다.
//
// ■ 거래 기록 배지 (T-156)
// 팔리거나 만료돼도 푸시는 없고 우편만 온다. 그래서 **로그인할 때와 경매 우편이 올 때** 새 기록 수만 묻는다(PageSize 0).
// 완료 탭을 열면 "봤다"로 남긴다(MarkSeen) — 그 응답의 새 기록 수가 줄 앞의 점이 된다.
//
// ■ 쪽으로 나눈다 (2026-10-14 사용자 결정)
// 검색 결과·거래 기록은 한 화면에 보이는 줄 수('RowsPerPage')씩 쪽으로 받고, [◀][▶]로 갈아 끼운다 — 이어 붙이지 않는다.
// 서버 커서는 앞으로만 간다 — 받은 쪽마다 "그 다음 쪽을 받는 커서"를 적어 두었다가 앞 쪽으로 돌아갈 때 다시 쓴다.
//
// ■ 받은 시각
// 목록마다 마지막으로 받은 시각을 둔다 — 화면이 "n분 전에 받음"으로 목록이 얼마나 낡았는지 보인다.
public class AuctionModel : MonoService<AuctionModel>
{
    // 한 쪽의 줄 수 — 경매장 표에 스크롤 없이 보이는 줄 수다(줄 높이 62 · 목록 칸 588). 검색 · 거래 기록 · 판매 탭이 함께 쓴다.
    public const int RowsPerPage = 9;

    // 한 요청에 실을 수 있는 TID 수 — 서버 'User.MaxQueryTids'와 같다. 넘으면 서버가 요청째 거절한다.
    public const int MaxQueryTids = 100;

    // 검색 건수를 세다 멈추는 수 — 서버가 여기서 세기를 멈춘다. 이 값이 오면 "1,000건 이상"이다(#71).
    public const int SearchCountCap = 1000;

    // 시세 요약을 다시 묻지 않는 시간 — 등록 창에서 이것저것 고를 때 빈도 제한을 아낀다.
    private static readonly TimeSpan SummaryFreshFor = TimeSpan.FromSeconds(60);

    // 내 매물이 바뀌었다는 경매 우편 템플릿 — 서버 'AuctionMail'의 Bought·Sold·Expired·Cancelled·Failed.
    // 받으면 내 매물을 다시 받고 거래 기록 배지를 다시 묻는다.
    private static readonly HashSet<int> AuctionMailTids = new HashSet<int> { 3, 4, 5, 6, 7 };

    // ─── 검색 ───
    private readonly List<AuctionListingInfo> _searchResults = new List<AuctionListingInfo>();

    private C_AuctionSearchRequest? _searchRequest;    // 마지막 조건 — 다른 쪽은 커서만 바꿔 다시 보낸다

    // 쪽마다 그 쪽을 받는 커서(정렬 값, 매물 ID) — [0]은 (0, 0). 받은 쪽의 다음 커서를 뒤에 적는다.
    private readonly List<(long sortValue, long listingId)> _searchCursors = new List<(long, long)>();
    private int _pendingSearchPage;

    // ─── 시세 ───
    private readonly struct SummaryEntry
    {
        public readonly MarketItemInfo Info;
        public readonly DateTime       ReceivedAt;

        public SummaryEntry(MarketItemInfo info, DateTime receivedAt)
        {
            Info       = info;
            ReceivedAt = receivedAt;
        }
    }

    private readonly Dictionary<(EAuctionKind, int), SummaryEntry> _summaries        = new Dictionary<(EAuctionKind, int), SummaryEntry>();
    private readonly List<(EAuctionKind kind, int tid)>            _pendingSummaries = new List<(EAuctionKind, int)>();
    private readonly List<MarketPriceLevelInfo>                    _priceLevels      = new List<MarketPriceLevelInfo>();
    private readonly List<MarketDailyPriceInfo>                    _dailyPrices      = new List<MarketDailyPriceInfo>();

    // ─── 거래 기록 ───
    private readonly List<AuctionHistoryInfo> _history = new List<AuctionHistoryInfo>();

    // 보낸 기록 요청이 배지만 묻는 것(PageSize 0)인가 — 보낸 순서대로. 응답에는 구분이 없다.
    private readonly Queue<bool> _pendingHistoryIsBadge = new Queue<bool>();

    private EAuctionHistoryType _historyType;
    private int                 _historyDays;

    // 쪽마다 그 쪽을 받는 커서(앞 쪽 마지막 기록 ID) — [0]은 0.
    private readonly List<long> _historyCursors = new List<long>();
    private int                 _pendingHistoryPage;
    private bool                _pendingHistorySeen; // 보낸 쪽 요청이 "봤다"로 남기는 첫 쪽인가

    // ─── 내 매물 ───
    private readonly List<AuctionListingInfo> _myListings   = new List<AuctionListingInfo>();
    private readonly HashSet<long>            _myListingIds = new HashSet<long>();

    private NetworkManager  _network = null!;
    private PlayerDataModel _data    = null!;

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // ─── 검색 ───
    public IReadOnlyList<AuctionListingInfo> SearchResults => _searchResults;

    // 지금 쪽 뒤에 더 있나 — [▶]를 연다.
    public bool HasMoreResults { get; private set; }

    // 지금 보이는 쪽 (0부터).
    public int SearchPage { get; private set; }

    // 조건에 맞는 매물 수 — 받은 쪽과 상관없는 전체 건수(T-155).
    public int SearchTotalCount { get; private set; }

    // 서버가 건수를 끝까지 세지 않았나 — 그러면 'SearchTotalCount'는 하한이다("1,000건 이상").
    public bool IsSearchCountCapped { get; private set; }

    // 한 번이라도 검색했나. 화면을 처음 열 때만 자동으로 검색하려고 본다.
    public bool HasSearched => _searchRequest != null;

    // 검색 결과를 마지막으로 받은 시각(로컬). 아직 없으면 null.
    public DateTime? SearchReceivedAt { get; private set; }

    // ─── 시세 ───
    // 가격대를 받은 종류·TID. TID가 0이면 아직 없다.
    public EAuctionKind PriceKind { get; private set; }
    public int          PriceTid  { get; private set; }

    public IReadOnlyList<MarketPriceLevelInfo> PriceLevels => _priceLevels;

    // 일별 시세를 받은 종류·TID. TID가 0이면 아직 없다.
    public EAuctionKind DailyKind { get; private set; }
    public int          DailyTid  { get; private set; }

    // 최근 14일 — 오래된 날 → 오늘. 체결이 없는 날도 0으로 온다.
    public IReadOnlyList<MarketDailyPriceInfo> DailyPrices => _dailyPrices;

    // ─── 거래 기록 ───
    public IReadOnlyList<AuctionHistoryInfo> History => _history;

    public bool HasMoreHistory { get; private set; }

    // 지금 보이는 기록 쪽 (0부터).
    public int HistoryPage { get; private set; }

    // 안 본 거래 기록 수 — 완료 탭 배지.
    public int HistoryUnseenCount { get; private set; }

    // 마지막으로 완료 탭을 열었을 때 새로 들어와 있던 기록 수 — 맨 위에서 이만큼 줄 앞에 점을 찍는다.
    public int HistoryNewCount { get; private set; }

    public DateTime? HistoryReceivedAt { get; private set; }

    // ─── 내 매물 ───
    public IReadOnlyList<AuctionListingInfo> MyListings => _myListings;

    // 내 매물을 마지막으로 받은 시각(로컬). 아직 없으면 null.
    public DateTime? MyListingsReceivedAt { get; private set; }

    // 이 매물이 내가 올린 것인가 — 검색 결과에서 [구매]를 잠그고 '내 매물'로 표시한다.
    // ※ 방금 등록한 것도 참이다(등록 응답의 ListingId를 바로 넣는다). 내 매물 목록이 오면 그것으로 다시 맞춘다.
    public bool IsMine(long listingId) => _myListingIds.Contains(listingId);

    // 응답 이벤트 — 상태를 먼저 고친 뒤 결과 코드와 함께 쏜다. 받은 Presenter가 대기를 닫고 다시 그린다.
    public event Action<EResultCode>?       SearchCompleted;
    public event Action<EResultCode>?       SummaryCompleted;       // 시세 요약 — 'GetSummary'로 읽는다
    public event Action<EResultCode>?       PriceLevelsCompleted;
    public event Action<EResultCode>?       DailyPricesCompleted;
    public event Action<EResultCode>?       HistoryCompleted;
    public event Action?                    HistoryBadgeChanged;    // 'HistoryUnseenCount'가 바뀜 — 탭 배지
    public event Action<EResultCode>?       BuyCompleted;
    public event Action<EResultCode, long>? RegisterCompleted;      // 결과 코드 · 낸 등록비
    public event Action<EResultCode>?       CancelCompleted;
    public event Action<EResultCode>?       MyListingsCompleted;

    // 참조 확보 → 구독 순서로 진행한다 (클라 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다.
    private void Start()
    {
        _network = NetworkManager.Instance;
        _data    = Services.Get<PlayerDataModel>();

        Subscribe();

        _isReady = true;
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    private void OnEnable()
    {
        if (_isReady)
        {
            Subscribe();
        }
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
    }

    #region 구독

    // 수신 진입점 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed = true;

        ServerPacketHandler.MarketItemsReceived        += OnMarketItemsReceived;
        ServerPacketHandler.MarketPriceReceived        += OnMarketPriceReceived;
        ServerPacketHandler.MarketPriceHistoryReceived += OnMarketPriceHistoryReceived;
        ServerPacketHandler.AuctionHistoryReceived     += OnAuctionHistoryReceived;
        ServerPacketHandler.AuctionSearched            += OnAuctionSearched;
        ServerPacketHandler.AuctionBuyResponded        += OnAuctionBuyResponded;
        ServerPacketHandler.AuctionRegisterResponded   += OnAuctionRegisterResponded;
        ServerPacketHandler.AuctionCancelResponded     += OnAuctionCancelResponded;
        ServerPacketHandler.AuctionMyListingsReceived  += OnAuctionMyListingsReceived;
        ServerPacketHandler.MailArrived                += OnMailArrived;
        _data.LoginCompleted                           += OnLoginCompleted;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed = false;

        ServerPacketHandler.MarketItemsReceived        -= OnMarketItemsReceived;
        ServerPacketHandler.MarketPriceReceived        -= OnMarketPriceReceived;
        ServerPacketHandler.MarketPriceHistoryReceived -= OnMarketPriceHistoryReceived;
        ServerPacketHandler.AuctionHistoryReceived     -= OnAuctionHistoryReceived;
        ServerPacketHandler.AuctionSearched            -= OnAuctionSearched;
        ServerPacketHandler.AuctionBuyResponded        -= OnAuctionBuyResponded;
        ServerPacketHandler.AuctionRegisterResponded   -= OnAuctionRegisterResponded;
        ServerPacketHandler.AuctionCancelResponded     -= OnAuctionCancelResponded;
        ServerPacketHandler.AuctionMyListingsReceived  -= OnAuctionMyListingsReceived;
        ServerPacketHandler.MailArrived                -= OnMailArrived;
        _data.LoginCompleted                           -= OnLoginCompleted;
    }

    #endregion

    #region 요청 — 검색

    // 새 조건으로 첫 쪽을 검색한다 (AuctionSearchPresenter가 호출). 쪽 크기·커서는 여기서 채운다.
    public void Search(C_AuctionSearchRequest request)
    {
        request.PageSize = RowsPerPage;
        _searchRequest   = request;

        _searchCursors.Clear();
        _searchCursors.Add((0L, 0L));

        SendSearch(0);
    }

    // 같은 조건으로 지금 쪽을 다시 받는다 ([새로고침] · 구매 뒤).
    public void RefreshSearch()
    {
        if (_searchRequest != null)
        {
            SendSearch(SearchPage);
        }
    }

    // 같은 조건으로 그 쪽을 받는다 — 받아 본 쪽의 바로 다음까지만 갈 수 있다([◀][▶]). 못 가면 false.
    public bool SearchGoToPage(int page)
    {
        if (_searchRequest == null || page < 0 || page >= _searchCursors.Count)
        {
            return false;
        }

        SendSearch(page);

        return true;
    }

    // 그 쪽의 커서를 실어 보낸다. 첫 쪽은 커서가 둘 다 0이다.
    private void SendSearch(int page)
    {
        C_AuctionSearchRequest request = _searchRequest!;

        (request.CursorSortValue, request.CursorListingId) = _searchCursors[page];
        _pendingSearchPage = page;

        _network.Send(request);
        ClientLogger.Info(ClientLogger.Send, $"경매 검색 요청 — {request.Kind} 분류 {request.Category} 정렬 {request.Sort}, {page + 1}쪽");
    }

    // 매물 하나를 통째로 산다 (AuctionSearchPresenter가 호출).
    // ※ 목록에서 본 총액을 그대로 보낸다 — 그사이 가격이 바뀌었으면 서버가 AuctionPriceChanged로 거절한다.
    public void RequestBuy(AuctionListingInfo listing)
    {
        _network.Send(new C_AuctionBuyRequest { ListingId = listing.ListingId, ExpectedTotalPrice = listing.TotalPrice });
        ClientLogger.Info(ClientLogger.Send, $"경매 구매 요청 — 매물 {listing.ListingId}, {listing.TotalPrice}G");
    }

    #endregion

    #region 요청 — 시세

    // 받아 둔 시세 요약. 없거나 'SummaryFreshFor'보다 낡았으면 false.
    public bool TryGetSummary(EAuctionKind kind, int tid, out MarketItemInfo summary)
    {
        if (_summaries.TryGetValue((kind, tid), out SummaryEntry entry) && DateTime.Now - entry.ReceivedAt < SummaryFreshFor)
        {
            summary = entry.Info;

            return true;
        }

        summary = null!;

        return false;
    }

    // 받아 둔 시세 요약 — 낡았어도 돌려준다(화면 표시용). 받은 적 없으면 null.
    public MarketItemInfo? GetSummary(EAuctionKind kind, int tid)
        => _summaries.TryGetValue((kind, tid), out SummaryEntry entry) ? entry.Info : null;

    // 한 종류의 시세 요약을 받는다 — 막 받은 것이 있으면 보내지 않고 false (시세 탭 · 구매 확인 · 등록 창).
    // ※ 보냈으면 true — 부르는 쪽이 대기를 걸지 정한다.
    public bool RequestSummary(EAuctionKind kind, int tid)
    {
        if (TryGetSummary(kind, tid, out _))
        {
            return false;
        }

        _pendingSummaries.Add((kind, tid));

        _network.Send(new C_MarketItemsRequest { Kind = kind, Tids = new List<int> { tid } });
        ClientLogger.Info(ClientLogger.Send, $"시세 요약 요청 — {kind} {tid}");

        return true;
    }

    // 한 종류의 가격대(단가별 판매 중 수량, 싼 순 10칸)를 받는다 (시세 탭).
    public void RequestPriceLevels(EAuctionKind kind, int tid)
    {
        PriceKind = kind;

        _network.Send(new C_MarketPriceRequest { Kind = kind, Tid = tid });
        ClientLogger.Info(ClientLogger.Send, $"가격대 요청 — {kind} {tid}");
    }

    // 한 종류의 최근 14일 시세를 받는다 (시세 탭 그래프).
    public void RequestDailyPrices(EAuctionKind kind, int tid)
    {
        _network.Send(new C_MarketPriceHistoryRequest { Kind = kind, Tid = tid });
        ClientLogger.Info(ClientLogger.Send, $"일별 시세 요청 — {kind} {tid}");
    }

    #endregion

    #region 요청 — 거래 기록

    // 거래 기록 첫 쪽을 받고 "봤다"로 남긴다 (완료 탭을 열 때 · 구분·기간을 바꿀 때).
    //   type : None이면 전부
    //   days : 최근 며칠. 0이면 보관 기간(30일) 전부
    public void RequestHistory(EAuctionHistoryType type, int days)
    {
        _historyType = type;
        _historyDays = days;

        _historyCursors.Clear();
        _historyCursors.Add(0L);

        _pendingHistoryPage = 0;
        _pendingHistorySeen = true;
        SendHistory(0L, RowsPerPage, markSeen: true);
    }

    // 같은 조건으로 그 쪽을 받는다 — 받아 본 쪽의 바로 다음까지만 갈 수 있다([◀][▶]). 못 가면 false.
    public bool RequestHistoryPage(int page)
    {
        if (page < 0 || page >= _historyCursors.Count)
        {
            return false;
        }

        _pendingHistoryPage = page;
        _pendingHistorySeen = false;
        SendHistory(_historyCursors[page], RowsPerPage, markSeen: false);

        return true;
    }

    // 새 기록 수만 묻는다 — 배지용 (로그인 · 경매 우편 도착).
    private void RequestHistoryBadge()
    {
        SendHistory(0L, 0, markSeen: false);
    }

    private void SendHistory(long cursorHistoryId, int pageSize, bool markSeen)
    {
        _pendingHistoryIsBadge.Enqueue(pageSize == 0);

        _network.Send(new C_AuctionHistoryRequest
        {
            Type            = _historyType,
            Days            = _historyDays,
            CursorHistoryId = cursorHistoryId,
            PageSize        = pageSize,
            MarkSeen        = markSeen,
        });
        ClientLogger.Info(ClientLogger.Send, $"거래 기록 요청 — {_historyType} {_historyDays}일, 커서 {cursorHistoryId}, 쪽 {pageSize}");
    }

    #endregion

    #region 요청 — 등록 · 내 매물

    // 자원을 올린다 — 매물은 통째로만 팔린다 (AuctionRegisterPresenter가 호출).
    public void RegisterItem(int itemTid, int count, long unitPrice)
    {
        _network.Send(new C_AuctionRegisterRequest { Kind = EAuctionKind.Item, ItemTid = itemTid, Count = count, UnitPrice = unitPrice });
        ClientLogger.Info(ClientLogger.Send, $"경매 등록 요청 — 자원 {itemTid} x{count}, 단가 {unitPrice}");
    }

    // 장비 개체 하나를 올린다 — 인챈트까지 그대로 넘어간다 (AuctionRegisterPresenter가 호출).
    public void RegisterEquip(long equipId, long unitPrice)
    {
        _network.Send(new C_AuctionRegisterRequest { Kind = EAuctionKind.Equip, EquipId = equipId, Count = 1, UnitPrice = unitPrice });
        ClientLogger.Info(ClientLogger.Send, $"경매 등록 요청 — 장비 {equipId}, 단가 {unitPrice}");
    }

    // 캐릭터 개체 하나를 올린다 — 레벨·경험치·적성까지 그대로 넘어간다 (AuctionRegisterPresenter가 호출).
    public void RegisterCharacter(long characterId, long unitPrice)
    {
        _network.Send(new C_AuctionRegisterRequest { Kind = EAuctionKind.Character, CharacterId = characterId, Count = 1, UnitPrice = unitPrice });
        ClientLogger.Info(ClientLogger.Send, $"경매 등록 요청 — 캐릭터 {characterId}, 단가 {unitPrice}");
    }

    // 내 진행 중 매물을 받는다 (판매 탭 · 검색 탭을 열 때 · 등록 성공 뒤 · 경매 우편).
    // ※ 빈도 제한 밖이라 자주 불러도 된다. 검색 탭은 대기 없이 조용히 부른다 — 내 매물 표시용이다.
    public void RequestMyListings()
    {
        _network.Send(new C_AuctionMyListingsRequest());
        ClientLogger.Info(ClientLogger.Send, "내 매물 요청");
    }

    // 판매를 취소한다 — 물건은 우편으로 돌아오고 등록비는 돌아오지 않는다 (AuctionMyListingPresenter가 호출).
    public void RequestCancel(long listingId)
    {
        _network.Send(new C_AuctionCancelRequest { ListingId = listingId });
        ClientLogger.Info(ClientLogger.Send, $"경매 취소 요청 — 매물 {listingId}");
    }

    #endregion

    #region 응답 처리 (ServerPacketHandler 구독)

    // 검색 — 받은 쪽으로 갈아 끼우고, 다음 쪽 커서를 적어 둔다
    private void OnAuctionSearched(S_AuctionSearchResponse res)
    {
        if (res.Result == EResultCode.Ok)
        {
            int page = _pendingSearchPage;

            _searchResults.Clear();
            _searchResults.AddRange(res.Listings ?? new List<AuctionListingInfo>());

            SearchPage       = page;
            HasMoreResults   = res.HasMore;
            IsSearchCountCapped = res.TotalCount >= SearchCountCap;
            SearchTotalCount    = IsSearchCountCapped
                ? SearchCountCap
                : Math.Max(res.TotalCount, page * RowsPerPage + _searchResults.Count); // 건수를 안 주는 옛 경매 서버면 받은 줄 수라도
            SearchReceivedAt = DateTime.Now;

            // 이 쪽 뒤의 커서는 지금 받은 것으로 다시 쓴다 — 그사이 매물이 바뀌었으면 옛 커서가 어긋난다.
            TrimAfter(_searchCursors, page);

            if (res.HasMore)
            {
                _searchCursors.Add((res.NextCursorSortValue, res.NextCursorListingId));
            }
        }

        SearchCompleted?.Invoke(res.Result);
    }

    // 'page' 뒤에 적힌 커서를 지운다.
    private static void TrimAfter<T>(List<T> cursors, int page)
    {
        if (cursors.Count > page + 1)
        {
            cursors.RemoveRange(page + 1, cursors.Count - page - 1);
        }
    }

    // 시세 요약 — 보낸 순서의 (종류, TID)에 꽂는다. 요청한 TID는 매물이 없어도 0으로 온다
    private void OnMarketItemsReceived(S_MarketItemsResponse res)
    {
        if (_pendingSummaries.Count == 0)
        {
            SummaryCompleted?.Invoke(res.Result);

            return;
        }

        int index = 0;

        if (res.Items is { Count: > 0 })
        {
            int tid = res.Items[0].Tid;
            index   = Math.Max(0, _pendingSummaries.FindIndex(pending => pending.tid == tid));
        }

        (EAuctionKind kind, int _) = _pendingSummaries[index];
        _pendingSummaries.RemoveAt(index);

        if (res.Result == EResultCode.Ok && res.Items != null)
        {
            foreach (MarketItemInfo item in res.Items)
            {
                _summaries[(kind, item.Tid)] = new SummaryEntry(item, DateTime.Now);
            }
        }

        SummaryCompleted?.Invoke(res.Result);
    }

    // 가격대 — 성공이면 그 종류의 가격대로 갈아 끼운다
    private void OnMarketPriceReceived(S_MarketPriceResponse res)
    {
        if (res.Result == EResultCode.Ok)
        {
            PriceTid = res.Tid;
            _priceLevels.Clear();
            _priceLevels.AddRange(res.Levels ?? new List<MarketPriceLevelInfo>());
        }

        PriceLevelsCompleted?.Invoke(res.Result);
    }

    // 일별 시세 — 성공이면 갈아 끼운다
    private void OnMarketPriceHistoryReceived(S_MarketPriceHistoryResponse res)
    {
        if (res.Result == EResultCode.Ok)
        {
            DailyKind = res.Kind;
            DailyTid  = res.Tid;
            _dailyPrices.Clear();
            _dailyPrices.AddRange(res.Days ?? new List<MarketDailyPriceInfo>());
        }

        DailyPricesCompleted?.Invoke(res.Result);
    }

    // 거래 기록 — 배지 질문이면 수만, 쪽이면 줄을 받는다
    private void OnAuctionHistoryReceived(S_AuctionHistoryResponse res)
    {
        bool isBadge = _pendingHistoryIsBadge.Count > 0 && _pendingHistoryIsBadge.Dequeue();

        if (isBadge)
        {
            if (res.Result == EResultCode.Ok)
            {
                SetUnseen(res.UnseenCount);
            }

            return;
        }

        if (res.Result == EResultCode.Ok)
        {
            int page = _pendingHistoryPage;

            if (_pendingHistorySeen)
            {
                HistoryNewCount = res.UnseenCount; // "봤다"로 남긴 첫 쪽은 남기기 전의 새 기록 수를 함께 준다
                SetUnseen(0);
            }

            _history.Clear();
            _history.AddRange(res.Entries ?? new List<AuctionHistoryInfo>());

            HistoryPage       = page;
            HasMoreHistory    = res.HasMore;
            HistoryReceivedAt = DateTime.Now;

            TrimAfter(_historyCursors, page);

            if (res.HasMore && _history.Count > 0)
            {
                _historyCursors.Add(_history[_history.Count - 1].HistoryId);
            }
        }

        _pendingHistorySeen = false;
        HistoryCompleted?.Invoke(res.Result);
    }

    // 즉시구매 — 이 매물을 더는 살 수 없게 됐으면 목록에서 뺀다
    //
    // 산 경우뿐 아니라 이미 팔렸거나 닫힌 경우도 뺀다 — 남겨 두면 같은 줄을 또 눌러 같은 거절을 받는다.
    // 가격이 바뀐 경우(AuctionPriceChanged)는 남긴다 — 다시 검색하면 새 가격으로 온다.
    private void OnAuctionBuyResponded(S_AuctionBuyResponse res)
    {
        bool isGone = res.Result == EResultCode.Ok
                   || res.Result == EResultCode.AuctionSoldOut
                   || res.Result == EResultCode.AuctionClosed
                   || res.Result == EResultCode.AuctionNotFound;

        // 상한에 걸린 건수는 하한이라 하나 빠져도 "1,000건 이상"을 그대로 둔다.
        if (isGone && _searchResults.RemoveAll(listing => listing.ListingId == res.ListingId) > 0 && !IsSearchCountCapped)
        {
            SearchTotalCount = Math.Max(0, SearchTotalCount - 1);
        }

        BuyCompleted?.Invoke(res.Result);
    }

    // 등록 — 내 매물 ID에 넣고 결과를 알린다. 인벤토리에서 빼는 일은 'PlayerDataModel'이 같은 응답으로 한다
    //
    // ★ 성공하면 내 매물을 다시 받는다 — 응답에는 매물 ID만 있어 판매 탭 목록을 그릴 재료가 없다.
    private void OnAuctionRegisterResponded(S_AuctionRegisterResponse res)
    {
        if (res.Result == EResultCode.Ok)
        {
            _myListingIds.Add(res.ListingId);
            RequestMyListings();
        }

        RegisterCompleted?.Invoke(res.Result, res.ListingFee);
    }

    // 취소 — 성공이면 내 매물에서 뺀다(다시 받지 않아도 화면이 맞는다)
    private void OnAuctionCancelResponded(S_AuctionCancelResponse res)
    {
        if (res.Result == EResultCode.Ok)
        {
            _myListings.RemoveAll(listing => listing.ListingId == res.ListingId);
            _myListingIds.Remove(res.ListingId);
        }

        CancelCompleted?.Invoke(res.Result);
    }

    // 내 매물 — 성공이면 통째로 갈아 끼운다
    private void OnAuctionMyListingsReceived(S_AuctionMyListingsResponse res)
    {
        if (res.Result == EResultCode.Ok)
        {
            _myListings.Clear();
            _myListings.AddRange(res.Listings ?? new List<AuctionListingInfo>());
            MyListingsReceivedAt = DateTime.Now;

            _myListingIds.Clear();

            foreach (AuctionListingInfo listing in _myListings)
            {
                _myListingIds.Add(listing.ListingId);
            }
        }

        MyListingsCompleted?.Invoke(res.Result);
    }

    // 로그인 — 거래 기록 배지를 묻는다 (PlayerDataModel.LoginCompleted 구독)
    private void OnLoginCompleted(bool success, EResultCode code)
    {
        if (success)
        {
            RequestHistoryBadge();
        }
    }

    // 새 우편 — 경매 우편이 섞였으면 내 매물을 조용히 다시 받고 배지를 다시 묻는다.
    // 우편함 캐시는 'PlayerDataModel'이 같은 패킷으로 갱신한다.
    private void OnMailArrived(S_MailArrivedResponse res)
    {
        if (res.Mails == null || !res.Mails.Exists(mail => AuctionMailTids.Contains(mail.TemplateTid)))
        {
            return;
        }

        RequestMyListings();
        RequestHistoryBadge();
    }

    private void SetUnseen(int count)
    {
        if (HistoryUnseenCount == count)
        {
            return;
        }

        HistoryUnseenCount = count;
        HistoryBadgeChanged?.Invoke();
    }

    #endregion

    #region 가격 (표시용 — 판정은 서버가 한다)

    // "방금 받음" · "3분 전에 받음" · "2시간 전에 받음" — 목록이 얼마나 낡았는지 적는다. 받은 적 없으면 빈 문자열.
    // ※ 화면을 다시 그릴 때만 바뀐다(매초 세지 않는다) — 탭을 열거나 응답이 올 때 맞으면 충분하다.
    public static string FormatReceivedAgo(DateTime? receivedAt)
    {
        if (receivedAt == null)
        {
            return "";
        }

        TimeSpan ago = DateTime.Now - receivedAt.Value;

        if (ago.TotalMinutes < 1d)
        {
            return "방금 받음";
        }

        return ago.TotalHours >= 1d ? $"{(int)ago.TotalHours}시간 전에 받음" : $"{(int)ago.TotalMinutes}분 전에 받음";
    }

    // 즉시 판매가 — 인벤토리에서 바로 팔 때 받는 정해진 값(BasePrice x SellRatePermille).
    public static long InstantSellPrice(int basePrice) => basePrice * Constants.SellRatePermille / 1000L;

    // 즉시 판매 합계 — N개를 한꺼번에 팔 때. **서버 'ShopService'와 같은 순서로 곱한다**(개수까지 곱한 뒤 나눈다) —
    // 개당 값에 개수를 곱하면 비율이 1000이 아닐 때 끝자리가 서버와 어긋난다.
    public static long InstantSellTotal(int basePrice, long count) => basePrice * count * Constants.SellRatePermille / 1000L;

    // 경매 단가 하한 — 즉시 판매가가 곧 바닥이다. 서버 'AuctionRules.MinUnitPrice'와 같은 식(최소 1).
    public static long MinUnitPrice(int basePrice) => Math.Max(basePrice, 1L);

    // 경매 단가 상한 — 기준가와 무관한 절대값(1조 · 자율 경제 2026-10-05 · #56). 서버 'AuctionRules.MaxUnitPrice'와 같다.
    public static long MaxUnitPrice => Constants.AuctionMaxUnitPrice;

    // 한 번에 올리는 수량 상한(9,999 · #56). 서버 'AuctionRules.MaxListingCount'와 같다.
    // ※ 단가 상한과 **한 쌍**이다 — 최고 단가 × 이 수량의 수수료가 'long'을 넘지 않는 선이다.
    //   보유량은 'MaxStack'(9,999)을 넘지 않지만(서버 T-157 — 모든 지급 경로가 자른다) 등록 수량은 이 값으로 따로 자른다.
    public static long MaxListingCount => Constants.AuctionMaxListingCount;

    // 경매 등록가 범위 문구 — "12 G 이상". 상한은 터무니없는 값을 막는 선이라 줄마다 적지 않는다(안내 문구에 한 번).
    // 등록 창·인벤토리 툴팁이 같은 모양으로 말한다.
    public static string FormatBand(int basePrice) => $"{MinUnitPrice(basePrice):N0} G 이상";

    // 상한 문구 — "1조". 조 단위로 나누어떨어지면 조로, 아니면 숫자 그대로 적는다.
    public static string FormatMaxUnitPrice()
    {
        const long Jo = 1_000_000_000_000L;

        return MaxUnitPrice % Jo == 0 ? $"{MaxUnitPrice / Jo:N0}조" : MaxUnitPrice.ToString("N0");
    }

    // 등록비 비율(천분율) — 안내 문구의 '1%'에 쓴다.
    public static long ListingFeePermille => Constants.AuctionListingFeePermille;

    // 등록비 — 총액의 천분율, 최소 1. 서버 'AuctionRules.ListingFee'와 같은 식이다.
    public static long ListingFee(long totalPrice) => Math.Max(1L, totalPrice * Constants.AuctionListingFeePermille / 1000L);

    // 판매 수수료 — 총액의 천분율(내림). 서버 'AuctionRules.SaleFee'와 같은 식이다.
    public static long SaleFee(long totalPrice) => totalPrice * Constants.AuctionSaleFeePermille / 1000L;

    // 팔리면 받는 금액 — 총액에서 판매 수수료를 뺀 값.
    public static long SaleNet(long totalPrice) => totalPrice - SaleFee(totalPrice);

    // "1%" · "5%" — 천분율을 퍼센트 문구로.
    public static string FormatPermille(long permille) => $"{permille / 10f:0.#}%";

    #endregion
}

using System;
using System.Collections.Generic;
using GameData;
using MikaNetwork;
using MikaProtocol;

// 경매장 · 거래소 창구 — 조회 결과(거래소 목록·가격대·장비 검색·내 매물)를 들고, 요청도 여기서 보낸다.
//
// ■ 왜 이 모델은 송신도 하나
// 'PlayerDataModel'·'SellCartModel'은 송신하지 않고 요청은 Presenter가 보낸다. 여기는 다르다 —
// 장비 검색은 **다음 페이지를 마지막으로 본 (단가, 매물 ID) 커서로** 이어 받는다. 검색 조건·커서·
// 누적 결과가 한 곳에 있어야 [더 보기]가 앞 페이지에 이어 붙는다. 조건은 Presenter에, 결과는 모델에
// 두면 둘이 어긋난 채로 다음 페이지를 부른다. 그래서 요청을 만드는 일까지 여기서 한다.
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
//   경매 등록(경매장)  : 단가를 직접 정한다. 즉시 판매가 ~ 그 x10 사이(가격 밴드 — 기획 거래 3.4, 2026-09-30 유지 확정).
// 수치는 'Constants.xlsx'의 Auction*·Market* 행 — 코드에 박지 않는다.
//
// ■ 내 매물 표시
// 매물에는 판매자 이름('SellerName')만 있다 — 이름은 겹칠 수 있어 내 것은 내 매물 ID로 가린다.
// 내 매물 조회는 빈도 제한 밖이라 구매 화면을 열 때마다 조용히 받는다('RequestMyListings').
//
// ■ 검색 축은 장비 · 캐릭터 둘이다
// 두 화면이 같은 패킷으로 검색하지만 조건·결과·커서는 **축마다 따로** 든다('SearchState') —
// 하나로 두면 장비 화면의 [더 보기]가 캐릭터 검색의 커서로 이어 받는다.
// 응답에는 축이 없어 보낸 축('_pendingSearchKind')에 꽂는다. 검색은 대기 차단으로 한 번에 하나다.
//
// ■ 받은 시각
// 목록마다 마지막으로 받은 시각을 둔다 — 화면이 "n분 전에 받음"으로 시세가 얼마나 낡았는지 보인다.
//
// ■ 내 매물 소식
// 팔리거나 만료돼도 푸시는 없고 우편만 온다. 그래서 **경매 우편(판매 대금·만료·실패 반환)이 도착하면**
// 내 매물을 조용히 다시 받고 '소식 있음'을 켠다 — 탭 버튼이 배지를 달고, 내 매물 화면이 보이면 끈다.
public class AuctionModel : MonoService<AuctionModel>
{
    // 장비 검색 한 페이지의 줄 수. 0이면 서버 기본값(20)이다.
    private const int SearchPageSize = 20;

    // 한 요청에 실을 수 있는 TID 수 — 서버 'User.MaxQueryTids'와 같다. 넘으면 서버가 요청째 거절한다.
    public const int MaxQueryTids = 100;

    private readonly List<MarketItemInfo>       _marketItems  = new List<MarketItemInfo>();
    private readonly List<MarketPriceLevelInfo> _priceLevels  = new List<MarketPriceLevelInfo>();
    private readonly List<AuctionListingInfo>   _myListings   = new List<AuctionListingInfo>();
    private readonly HashSet<long>              _myListingIds = new HashSet<long>();

    // 검색 축 하나의 상태 — 마지막 조건 · 누적 결과 · 더 있나 · 받은 시각.
    private sealed class SearchState
    {
        // 마지막 검색 조건 — [더 보기]가 같은 조건에 커서만 바꿔 다시 보낸다.
        public C_AuctionSearchRequest? Request;

        public readonly List<AuctionListingInfo> Results = new List<AuctionListingInfo>();

        public bool      HasMore;
        public DateTime? ReceivedAt;
    }

    private readonly Dictionary<EAuctionKind, SearchState> _searches = new Dictionary<EAuctionKind, SearchState>
    {
        { EAuctionKind.Equip,     new SearchState() },
        { EAuctionKind.Character, new SearchState() },
    };

    // 지금 기다리는 검색의 축 — 응답에 축이 없어 여기에 꽂는다.
    private EAuctionKind _pendingSearchKind = EAuctionKind.Equip;

    // 마지막 거래소 목록 조건 — 구매 뒤 같은 조건으로 다시 받는다.
    private List<int>? _lastMarketTids;

    // 지금 기다리는 검색이 다음 페이지인가 — 응답을 앞 결과에 이어 붙일지 갈아 끼울지 정한다.
    private bool _isNextPagePending;

    // 내 매물이 바뀌었다는 경매 우편 템플릿 — 서버 'AuctionMail'의 Sold·Expired·Failed와 같다.
    // 구매(3)는 내 매물과 무관하고, 취소(5)는 내가 누른 것이라 화면이 이미 맞다.
    private static readonly HashSet<int> MyListingMailTids = new HashSet<int> { 4, 6, 7 };

    private NetworkManager _network = null!;

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // ─── 거래소(자원) ───
    public IReadOnlyList<MarketItemInfo> MarketItems => _marketItems;

    // 거래소 목록을 한 번이라도 받았나. 탭을 처음 열 때만 자동으로 받으려고 본다(조회에 빈도 제한이 있다).
    public bool HasMarketItems { get; private set; }

    // 가격대를 받은 자원. 0이면 아직 없다.
    public int PriceTid { get; private set; }

    public IReadOnlyList<MarketPriceLevelInfo> PriceLevels => _priceLevels;

    // 거래소 목록을 마지막으로 받은 시각(로컬). 아직 없으면 null.
    public DateTime? MarketItemsReceivedAt { get; private set; }

    // ─── 경매장(장비 · 캐릭터) ───
    public IReadOnlyList<AuctionListingInfo> GetSearchResults(EAuctionKind kind) => GetState(kind).Results;

    // 검색 결과가 더 있나 — [더 보기] 버튼을 켤지 정한다.
    public bool HasMoreResults(EAuctionKind kind) => GetState(kind).HasMore;

    // 이 축을 한 번이라도 검색했나. 화면을 처음 열 때만 자동으로 검색하려고 본다.
    public bool HasSearched(EAuctionKind kind) => GetState(kind).Request != null;

    // 이 축의 검색 결과를 마지막으로 받은 시각(로컬). 아직 없으면 null.
    public DateTime? GetSearchReceivedAt(EAuctionKind kind) => GetState(kind).ReceivedAt;

    // ─── 내 매물 ───
    public IReadOnlyList<AuctionListingInfo> MyListings => _myListings;

    // 내 매물을 마지막으로 받은 시각(로컬). 아직 없으면 null.
    public DateTime? MyListingsReceivedAt { get; private set; }

    // 안 본 내 매물 소식이 있나 — 경매 우편이 오면 켜지고, 내 매물 화면이 보이면 꺼진다(탭 배지).
    public bool HasMyListingsNews { get; private set; }

    // 이 매물이 내가 올린 것인가 — 검색 결과에서 [구매]를 잠그고 '내 매물'로 표시한다.
    // ※ 방금 등록한 것도 참이다(등록 응답의 ListingId를 바로 넣는다). 내 매물 목록이 오면 그것으로 다시 맞춘다.
    public bool IsMine(long listingId) => _myListingIds.Contains(listingId);

    // 내가 이 자원을 거래소에 판매 중인 수량. 'unitPrice'를 주면 그 단가의 것만 센다 (0이면 전부).
    // ※ 거래소 구매는 내 매물을 건너뛴다(서버) — 살 수 있는 수량 = 판매 중 수량 - 이 값.
    public long GetMyItemCount(int tid, long unitPrice = 0L)
    {
        long count = 0L;

        foreach (AuctionListingInfo listing in _myListings)
        {
            if (listing.Kind == EAuctionKind.Item && listing.Tid == tid && (unitPrice == 0L || listing.UnitPrice == unitPrice))
            {
                count += listing.Count;
            }
        }

        return count;
    }

    // 응답 이벤트 — 상태를 먼저 고친 뒤 결과 코드와 함께 쏜다. 받은 Presenter가 대기를 닫고 다시 그린다.
    public event Action<EResultCode>?               MarketItemsCompleted;
    public event Action<EResultCode>?               MarketPriceCompleted;
    public event Action<S_MarketBuyResponse>?       MarketBuyCompleted;   // 산 수량·낸 금액을 알림에 쓴다
    public event Action<EAuctionKind, EResultCode>? SearchCompleted;      // 축 · 결과 코드 — 자기 축이 아니면 무시한다
    public event Action<EResultCode>?               BuyCompleted;
    public event Action<EResultCode, long>?         RegisterCompleted;    // 결과 코드 · 낸 등록비
    public event Action<EResultCode>?               CancelCompleted;
    public event Action<EResultCode>?               MyListingsCompleted;
    public event Action?                            MyListingsNewsChanged; // 'HasMyListingsNews'가 바뀜 — 탭 배지

    // 참조 확보 → 구독 순서로 진행한다 (클라 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다.
    private void Start()
    {
        _network = NetworkManager.Instance;

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

        ServerPacketHandler.MarketItemsReceived       += OnMarketItemsReceived;
        ServerPacketHandler.MarketPriceReceived       += OnMarketPriceReceived;
        ServerPacketHandler.MarketBought              += OnMarketBought;
        ServerPacketHandler.AuctionSearched           += OnAuctionSearched;
        ServerPacketHandler.AuctionBuyResponded       += OnAuctionBuyResponded;
        ServerPacketHandler.AuctionRegisterResponded  += OnAuctionRegisterResponded;
        ServerPacketHandler.AuctionCancelResponded    += OnAuctionCancelResponded;
        ServerPacketHandler.AuctionMyListingsReceived += OnAuctionMyListingsReceived;
        ServerPacketHandler.MailArrived               += OnMailArrived;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed = false;

        ServerPacketHandler.MarketItemsReceived       -= OnMarketItemsReceived;
        ServerPacketHandler.MarketPriceReceived       -= OnMarketPriceReceived;
        ServerPacketHandler.MarketBought              -= OnMarketBought;
        ServerPacketHandler.AuctionSearched           -= OnAuctionSearched;
        ServerPacketHandler.AuctionBuyResponded       -= OnAuctionBuyResponded;
        ServerPacketHandler.AuctionRegisterResponded  -= OnAuctionRegisterResponded;
        ServerPacketHandler.AuctionCancelResponded    -= OnAuctionCancelResponded;
        ServerPacketHandler.AuctionMyListingsReceived -= OnAuctionMyListingsReceived;
        ServerPacketHandler.MailArrived               -= OnMailArrived;
    }

    #endregion

    #region 요청 — 거래소(자원)

    // 거래소 목록을 받는다. 'tids'가 비었으면 매물이 있는 자원 전체다 (MarketItemPresenter가 호출).
    //   tids : 이름 검색 결과 — 요청한 TID는 매물이 없어도 시세와 함께 온다
    public void RequestMarketItems(List<int>? tids)
    {
        _lastMarketTids = tids;

        _network.Send(new C_MarketItemsRequest { Tids = tids });
        ClientLogger.Info(ClientLogger.Send, $"거래소 목록 요청 — TID {tids?.Count ?? 0}개");
    }

    // 마지막 조건으로 거래소 목록을 다시 받는다 (구매 성공 뒤 — MarketItemPresenter가 호출).
    public void RefreshMarketItems() => RequestMarketItems(_lastMarketTids);

    // 한 자원의 가격대(단가별 판매 중 수량)를 받는다 (MarketItemPresenter가 호출).
    public void RequestMarketPrice(int tid)
    {
        _network.Send(new C_MarketPriceRequest { Tid = tid });
        ClientLogger.Info(ClientLogger.Send, $"거래소 가격대 요청 — TID {tid}");
    }

    // 한 자원을 'count'개, 개당 'maxUnitPrice' 이하로 최저가부터 산다 (MarketItemPresenter가 호출).
    // ※ 다 못 채우면 아무것도 사지 않는다(MarketNotEnough). 실제 낸 금액은 응답의 TotalPrice다.
    public void RequestMarketBuy(int tid, int count, long maxUnitPrice)
    {
        _network.Send(new C_MarketBuyRequest { Tid = tid, Count = count, MaxUnitPrice = maxUnitPrice });
        ClientLogger.Info(ClientLogger.Send, $"거래소 구매 요청 — TID {tid} x{count}, 단가 상한 {maxUnitPrice}");
    }

    #endregion

    #region 요청 — 경매장(장비 · 캐릭터)

    // 장비를 첫 페이지부터 검색한다 (AuctionSearchPresenter가 호출).
    //   tids         : 이름 검색 결과. null이면 이름 조건 없음
    //   kind         : 장비 분류. None이면 전체
    //   rarity       : 희귀도. None이면 전체 — 고르면 그 등급만 본다
    //   enchantGrade : 인챈트 등급 하한. None이면 조건 없음 — 장비 하나의 등급 하나로 거른다(칸마다가 아니다)
    public void SearchEquips(List<int>? tids, EquipKind kind, GlobalRarity rarity, GlobalRarity enchantGrade)
    {
        StartSearch(new C_AuctionSearchRequest
        {
            Kind            = EAuctionKind.Equip,
            Category        = (int)kind,
            Tids            = tids,
            MinRarity       = (int)rarity,
            MaxRarity       = (int)rarity,
            MinEnchantGrade = (int)enchantGrade,
            PageSize        = SearchPageSize,
        });
    }

    // 캐릭터를 첫 페이지부터 검색한다 (AuctionSearchPresenter가 호출). 분류는 없다(Category = 0).
    public void SearchCharacters(List<int>? tids, GlobalRarity rarity)
    {
        StartSearch(new C_AuctionSearchRequest
        {
            Kind      = EAuctionKind.Character,
            Tids      = tids,
            MinRarity = (int)rarity,
            MaxRarity = (int)rarity,
            PageSize  = SearchPageSize,
        });
    }

    // 같은 조건으로 다음 페이지를 받는다 — 마지막으로 본 매물이 커서다 (AuctionSearchPresenter의 [더 보기]).
    public void SearchNextPage(EAuctionKind kind)
    {
        SearchState state = GetState(kind);

        if (state.Request == null || state.Results.Count == 0)
        {
            return;
        }

        AuctionListingInfo last = state.Results[state.Results.Count - 1];

        _isNextPagePending = true;
        SendSearch(state.Request, last.UnitPrice, last.ListingId);
    }

    // 새 조건을 그 축에 걸고 첫 페이지를 보낸다 (SearchEquips · SearchCharacters에서 호출).
    private void StartSearch(C_AuctionSearchRequest request)
    {
        GetState(request.Kind).Request = request;

        _isNextPagePending = false;
        SendSearch(request, 0L, 0L);
    }

    // 매물 하나를 통째로 산다 (AuctionSearchPresenter가 호출).
    // ※ 목록에서 본 총액을 그대로 보낸다 — 그사이 가격이 바뀌었으면 서버가 AuctionPriceChanged로 거절한다.
    public void RequestBuy(AuctionListingInfo listing)
    {
        _network.Send(new C_AuctionBuyRequest { ListingId = listing.ListingId, ExpectedTotalPrice = listing.TotalPrice });
        ClientLogger.Info(ClientLogger.Send, $"경매 구매 요청 — 매물 {listing.ListingId}, {listing.TotalPrice}G");
    }

    // 검색 요청을 커서와 함께 보낸다 (StartSearch · SearchNextPage에서 호출). 첫 페이지는 커서가 둘 다 0이다.
    private void SendSearch(C_AuctionSearchRequest request, long cursorUnitPrice, long cursorListingId)
    {
        request.CursorUnitPrice = cursorUnitPrice;
        request.CursorListingId = cursorListingId;
        _pendingSearchKind      = request.Kind;

        _network.Send(request);
        ClientLogger.Info(ClientLogger.Send, $"경매 검색 요청 — {request.Kind} 분류 {request.Category}, 커서 ({cursorUnitPrice}, {cursorListingId})");
    }

    // 축의 검색 상태. 검색 축이 아닌 값(자원 등)은 장비 축으로 떨어뜨린다 — 부르는 쪽 배선 실수다.
    private SearchState GetState(EAuctionKind kind)
    {
        if (_searches.TryGetValue(kind, out SearchState state))
        {
            return state;
        }

        ClientLogger.Warn(ClientLogger.UI, $"검색 축이 아닌 종류 {kind} — 장비 축으로 본다.");

        return _searches[EAuctionKind.Equip];
    }

    #endregion

    #region 요청 — 등록 · 내 매물

    // 자원을 올린다 — 거래소에서 일부씩 팔린다 (AuctionRegisterPresenter가 호출).
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

    // 내 진행 중 매물을 받는다 (AuctionMyListingPresenter · 구매 화면을 열 때 · 등록 성공 뒤).
    // ※ 빈도 제한 밖이라 자주 불러도 된다. 구매 화면은 대기 없이 조용히 부른다 — 내 매물 표시용이다.
    public void RequestMyListings()
    {
        _network.Send(new C_AuctionMyListingsRequest());
        ClientLogger.Info(ClientLogger.Send, "내 매물 요청");
    }

    // 내 매물 소식을 봤다 — 배지를 끈다 (AuctionMyListingPresenter가 그릴 때 호출).
    public void ClearMyListingsNews()
    {
        if (!HasMyListingsNews)
        {
            return;
        }

        HasMyListingsNews = false;
        MyListingsNewsChanged?.Invoke();
    }

    // 판매를 취소한다 — 물건은 우편으로 돌아오고 등록비는 돌아오지 않는다 (AuctionMyListingPresenter가 호출).
    public void RequestCancel(long listingId)
    {
        _network.Send(new C_AuctionCancelRequest { ListingId = listingId });
        ClientLogger.Info(ClientLogger.Send, $"경매 취소 요청 — 매물 {listingId}");
    }

    #endregion

    #region 응답 처리 (ServerPacketHandler 구독)

    // 거래소 목록 — 성공이면 통째로 갈아 끼운다
    private void OnMarketItemsReceived(S_MarketItemsResponse res)
    {
        if (res.Result == EResultCode.Ok)
        {
            _marketItems.Clear();
            _marketItems.AddRange(res.Items ?? new List<MarketItemInfo>());
            HasMarketItems        = true;
            MarketItemsReceivedAt = DateTime.Now;
        }

        MarketItemsCompleted?.Invoke(res.Result);
    }

    // 거래소 가격대 — 성공이면 그 자원의 가격대로 갈아 끼운다
    private void OnMarketPriceReceived(S_MarketPriceResponse res)
    {
        if (res.Result == EResultCode.Ok)
        {
            PriceTid = res.Tid;
            _priceLevels.Clear();
            _priceLevels.AddRange(res.Levels ?? new List<MarketPriceLevelInfo>());
        }

        MarketPriceCompleted?.Invoke(res.Result);
    }

    // 거래소 구매 — 성공이면 본 가격대가 낡았으므로 비운다(다시 고르면 새로 받는다)
    private void OnMarketBought(S_MarketBuyResponse res)
    {
        if (res.Result == EResultCode.Ok)
        {
            PriceTid = 0;
            _priceLevels.Clear();
        }

        MarketBuyCompleted?.Invoke(res);
    }

    // 검색 — 보낸 축에 꽂는다. 다음 페이지면 이어 붙이고, 첫 페이지면 갈아 끼운다
    private void OnAuctionSearched(S_AuctionSearchResponse res)
    {
        EAuctionKind kind  = _pendingSearchKind;
        SearchState  state = GetState(kind);

        if (res.Result == EResultCode.Ok)
        {
            if (!_isNextPagePending)
            {
                state.Results.Clear();
            }

            state.Results.AddRange(res.Listings ?? new List<AuctionListingInfo>());
            state.HasMore    = res.HasMore;
            state.ReceivedAt = DateTime.Now;
        }

        _isNextPagePending = false;
        SearchCompleted?.Invoke(kind, res.Result);
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

        if (isGone)
        {
            foreach (SearchState state in _searches.Values)
            {
                state.Results.RemoveAll(listing => listing.ListingId == res.ListingId);
            }
        }

        BuyCompleted?.Invoke(res.Result);
    }

    // 등록 — 내 매물 ID에 넣고 결과를 알린다. 인벤토리에서 빼는 일은 'PlayerDataModel'이 같은 응답으로 한다
    //
    // ★ 성공하면 내 매물을 다시 받는다 — 응답에는 매물 ID만 있어 거래소 가격대의 '내 매물 N개'를 셀 재료가 없다.
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

    // 새 우편 — 경매 우편이 섞였으면 내 매물을 조용히 다시 받고 소식을 켠다.
    // 우편함 캐시는 'PlayerDataModel'이 같은 패킷으로 갱신한다.
    private void OnMailArrived(S_MailArrivedResponse res)
    {
        if (res.Mails == null || !res.Mails.Exists(mail => MyListingMailTids.Contains(mail.TemplateTid)))
        {
            return;
        }

        RequestMyListings();

        HasMyListingsNews = true;
        MyListingsNewsChanged?.Invoke();
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

    // 경매 단가 상한 — 하한 x 배수(가격 밴드 · 시세 조작 대응 — 기획 거래 3.4). 서버 'AuctionRules.MaxUnitPrice'와 같다.
    public static long MaxUnitPrice(int basePrice) => MinUnitPrice(basePrice) * PriceBandMultiplier;

    // 가격 밴드 배수 — 안내 문구의 'x10'에 쓴다.
    public static long PriceBandMultiplier => Constants.AuctionPriceBandMultiplier;

    // 경매 등록가 범위 문구 — "12 ~ 120 G". 등록 화면·인벤토리 툴팁이 같은 모양으로 말한다.
    public static string FormatBand(int basePrice) => $"{MinUnitPrice(basePrice):N0} ~ {MaxUnitPrice(basePrice):N0} G";

    // 등록비 비율(천분율) — 안내 문구의 '1%'에 쓴다.
    public static long ListingFeePermille => Constants.AuctionListingFeePermille;

    // 등록비 — 총액의 천분율, 최소 1. 서버 'AuctionRules.ListingFee'와 같은 식이다.
    public static long ListingFee(long totalPrice) => Math.Max(1L, totalPrice * Constants.AuctionListingFeePermille / 1000L);

    #endregion
}

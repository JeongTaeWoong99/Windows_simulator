using System.Collections.Generic;
using GameData;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 자원 구매 화면(거래소) — 경매장 [구매] 탭의 '자원' 축. 자원을 종류별로 보고, 골라서 수량으로 산다.
//
// ■ 거래소는 서버가 파는 상점이 아니다 — **플레이어들이 올린 자원 매물을 종류별로 묶어 보여 주는 창**이다.
//   자원은 같은 TID면 똑같아서 매물 하나하나를 고를 이유가 없다. 그래서 "N개를 개당 최대 P에"로 사면
//   서버가 싼 매물부터 여러 판매자에 걸쳐 채운다. 장비는 개체마다 인챈트가 달라 매물을 하나씩 고른다('AuctionSearchPresenter').
//
// ■ 동선
//   목록(최저가·판매 중 수량·최근가·전일 평균) → 줄의 [선택] → 가격대(단가별 수량) → "N개, 개당 최대 P" → [구매]
//   N개를 다 못 채우면 아무것도 사지 않는다(MarketNotEnough). 산 자원은 **우편**으로 온다.
//
// ■ 조회에 빈도 제한이 있다 (순간 5회 · 2초마다 1회 회복 — 목록·가격대·장비 검색이 한 통)
//   그래서 목록은 탭을 **처음** 열 때만 자동으로 받고, 이후엔 [검색]을 눌러야 새로 받는다.
//   탭을 오갈 때마다 받으면 금방 AuctionTooManyRequests가 뜬다.
//
// ■ 선택한 자원(_selectedTid)·정렬은 화면 상태다 — 서버 상태가 아니라서 모델에 두지 않는다.
// ■ 내 매물은 사지 못한다(서버가 건너뛴다) — 목록·가격대에 '내 매물 N개'로 보이고, 살 수 있는 수량에서 뺀다.
// ■ 수량은 살 수 있는 만큼(판매 중 - 내 매물, 1회 상한)을 넘게 넣으면 그 값으로 바로 고친다('AuctionInput').
public class MarketItemPresenter : MonoBehaviour
{
    [CenterHeader("검색")]
    [SerializeField, Tooltip("이름 검색 입력칸. 비우면 매물이 있는 자원 전체")]
    private TMP_InputField searchInput = null!;

    [SerializeField, Tooltip("검색 버튼 — 목록을 새로 받는다. OnClick은 코드가 연결한다")]
    private Button searchButton = null!;


    [CenterHeader("목록")]
    [SerializeField, Tooltip("자원 한 줄 프리팹")]
    private AuctionRowView rowPrefab = null!;

    [SerializeField, Tooltip("줄이 쌓이는 Content (VLG + ContentSizeFitter)")]
    private Transform rowParent = null!;

    [SerializeField, Tooltip("목록이 비었을 때만 보인다")]
    private GameObject emptyText = null!;

    [SerializeField, Tooltip("받은 시각 문구 — \"3분 전에 받음\". 목록은 [검색]을 눌러야 새로 받으므로 얼마나 낡았는지 보인다")]
    private TMP_Text receivedText = null!;

    [SerializeField, Tooltip("가격 정렬 버튼 — 낮은 가격순 ↔ 높은 가격순(최저가 기준). OnClick은 코드가 연결한다")]
    private Button sortButton = null!;

    [SerializeField, Tooltip("정렬 버튼 문구 — 지금 정렬을 적는다")]
    private TMP_Text sortText = null!;


    [CenterHeader("구매")]
    [SerializeField, Tooltip("고른 자원과 가격대 — 단가별 판매 중 수량")]
    private TMP_Text priceText = null!;

    [SerializeField, Tooltip("살 수량. Content Type은 Integer Number")]
    private TMP_InputField countInput = null!;

    [SerializeField, Tooltip("개당 최대 단가. Content Type은 Integer Number")]
    private TMP_InputField maxPriceInput = null!;

    [SerializeField, Tooltip("최대 지출 미리보기")]
    private TMP_Text buyHintText = null!;

    [SerializeField, Tooltip("구매 버튼. OnClick은 코드가 연결한다")]
    private Button buyButton = null!;

    private readonly List<AuctionRowContent> _contents = new List<AuctionRowContent>();

    private AuctionModel      _auction = null!;
    private PlayerDataModel   _data    = null!;
    private ServerWaitManager _wait    = null!;
    private UIManager         _ui      = null!;
    private AuctionRowList    _rows    = null!;

    // 고른 자원. 0이면 아직 안 골랐다.
    private int _selectedTid;

    // 지금 가격 정렬 — 화면 상태라 저장하지 않는다.
    private AuctionSortOrder _sortOrder = AuctionSortOrder.Ascending;

    // 진행 중인 대기의 손잡이 — 응답이 오면 결과를 보고한다.
    private ServerWaitHandle? _waitHandle;

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 배선 → 초기화 순서로 진행한다 (클라 공통 규약)
    private void Start()
    {
        this.RequireRef(searchInput,   nameof(searchInput));
        this.RequireRef(searchButton,  nameof(searchButton));
        this.RequireRef(rowPrefab,     nameof(rowPrefab));
        this.RequireRef(rowParent,     nameof(rowParent));
        this.RequireRef(emptyText,     nameof(emptyText));
        this.RequireRef(receivedText,  nameof(receivedText));
        this.RequireRef(sortButton,    nameof(sortButton));
        this.RequireRef(sortText,      nameof(sortText));
        this.RequireRef(priceText,     nameof(priceText));
        this.RequireRef(countInput,    nameof(countInput));
        this.RequireRef(maxPriceInput, nameof(maxPriceInput));
        this.RequireRef(buyHintText,   nameof(buyHintText));
        this.RequireRef(buyButton,     nameof(buyButton));

        _auction = Services.Get<AuctionModel>();
        _data    = Services.Get<PlayerDataModel>();
        _wait    = Services.Get<ServerWaitManager>();
        _ui      = Services.Get<UIManager>();
        _rows    = new AuctionRowList(rowPrefab, rowParent, OnRowSelected);

        Subscribe();

        searchButton.onClick.AddListener(OnSearchClicked);
        searchInput.onSubmit.AddListener(_ => OnSearchClicked());
        buyButton.onClick.AddListener(OnBuyClicked);
        sortButton.onClick.AddListener(OnSortClicked);
        countInput.onValueChanged.AddListener(_ => OnCountChanged());
        countInput.onEndEdit.AddListener(_ => OnCountEndEdit());
        maxPriceInput.onValueChanged.AddListener(_ => RefreshBuy());

        Refresh();
        RequestFirstList();
        RequestMyListings();

        _isReady = true;
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    private void OnEnable()
    {
        if (_isReady)
        {
            Subscribe();
            Refresh();
            RequestMyListings();
        }
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
    }

    #region 구독

    // 거래소 응답과 재화 변경 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed = true;

        _auction.MarketItemsCompleted += OnMarketItemsCompleted;
        _auction.MarketPriceCompleted += OnMarketPriceCompleted;
        _auction.MarketBuyCompleted   += OnMarketBuyCompleted;
        _auction.MyListingsCompleted  += OnMyListingsCompleted;
        _data.CurrencyChanged         += RefreshBuy;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed = false;

        _auction.MarketItemsCompleted -= OnMarketItemsCompleted;
        _auction.MarketPriceCompleted -= OnMarketPriceCompleted;
        _auction.MarketBuyCompleted   -= OnMarketBuyCompleted;
        _auction.MyListingsCompleted  -= OnMyListingsCompleted;
        _data.CurrencyChanged         -= RefreshBuy;
    }

    #endregion

    #region 요청

    // 목록을 한 번도 안 받았으면 받는다 (Start에서 호출 — 탭을 처음 연 순간).
    private void RequestFirstList()
    {
        if (_auction.HasMarketItems || !_data.IsLoggedIn)
        {
            return;
        }

        _auction.RequestMarketItems(null);
        BeginWait("거래소 목록");
    }

    // 내 매물을 조용히 받는다 — 대기 없이, 결과가 오면 '내 매물 N개' 표시만 다시 그린다 (Start · OnEnable).
    private void RequestMyListings()
    {
        if (_data.IsLoggedIn)
        {
            _auction.RequestMyListings();
        }
    }

    // 이름으로 목록을 새로 받는다 (searchButton OnClick · 입력칸 Enter).
    //
    // 맞는 이름이 없으면 보내지 않는다 — 빈 TID 목록은 서버가 "조건 없음"으로 읽어 전체를 돌려준다.
    private void OnSearchClicked()
    {
        if (_waitHandle != null)
        {
            return;
        }

        List<int>? tids = AuctionText.FindItemTids(searchInput.text);

        if (tids != null && tids.Count == 0)
        {
            _wait.RaiseNotice($"'{searchInput.text.Trim()}'(이)라는 자원이 없습니다.");

            return;
        }

        _auction.RequestMarketItems(tids);
        BeginWait("거래소 목록");
    }

    // 줄의 [선택] — 그 자원을 고르고 가격대를 받는다 (AuctionRowView.ActionClicked)
    private void OnRowSelected(long key)
    {
        if (_waitHandle != null)
        {
            return;
        }

        _selectedTid = (int)key;

        // 기본값은 1개 · 지금 최저가 — 가장 싸게 하나 사는 것이 가장 흔한 동선이다.
        MarketItemInfo? item = FindItem(_selectedTid);
        countInput.text    = "1";
        maxPriceInput.text = item != null && item.LowestUnitPrice > 0 ? item.LowestUnitPrice.ToString() : "";

        _auction.RequestMarketPrice(_selectedTid);
        BeginWait("가격대");
        Refresh();
    }

    // [가격 정렬] — 다음 정렬로 바꿔 다시 그린다. 요청은 보내지 않는다 (sortButton OnClick)
    private void OnSortClicked()
    {
        _sortOrder = AuctionSort.Next(_sortOrder);
        Refresh();
    }

    // 수량 입력 — 살 수 있는 만큼을 넘으면 그 값으로 고친다 (countInput.onValueChanged)
    private void OnCountChanged()
    {
        AuctionInput.ClampMax(countInput, GetMaxBuyCount());
        RefreshBuy();
    }

    // 수량 입력 끝 — 비었거나 0이면 1로 둔다 (countInput.onEndEdit)
    private void OnCountEndEdit()
    {
        if (_selectedTid != 0 && AuctionInput.ClampMin(countInput, 1L))
        {
            RefreshBuy();
        }
    }

    // [구매] — 확인을 받고 보낸다 (buyButton OnClick)
    private void OnBuyClicked()
    {
        if (_waitHandle != null || !TryReadBuy(out int count, out long maxPrice))
        {
            return;
        }

        int    tid  = _selectedTid;
        string name = GameDataLoader.GetItemName(tid);

        _ui.AskConfirm(
            $"{name} {count:N0}개를 개당 최대 {maxPrice:N0} G에 삽니다 (최대 {count * maxPrice:N0} G).\n" +
            "싼 매물부터 채우고, 다 못 채우면 아무것도 사지 않습니다.\n산 자원은 우편으로 옵니다.",
            () =>
            {
                _auction.RequestMarketBuy(tid, count, maxPrice);
                BeginWait("거래소 구매");
            });
    }

    #endregion

    #region 응답 (AuctionModel 구독)

    // 목록 도착 — 대기를 닫고 다시 그린다 (AuctionModel.MarketItemsCompleted 구독)
    private void OnMarketItemsCompleted(EResultCode code)
    {
        CloseWait(code);
        Refresh();
    }

    // 가격대 도착 (AuctionModel.MarketPriceCompleted 구독)
    private void OnMarketPriceCompleted(EResultCode code)
    {
        CloseWait(code);
        Refresh();
    }

    // 내 매물 도착 — '내 매물 N개' 표시와 살 수 있는 수량을 다시 맞춘다. 조용히 받은 것이라 대기는 없다 (AuctionModel.MyListingsCompleted 구독)
    private void OnMyListingsCompleted(EResultCode code)
    {
        Refresh();
    }

    // 구매 결과 — 성공이면 알리고 목록을 새로 받는다 (AuctionModel.MarketBuyCompleted 구독)
    private void OnMarketBuyCompleted(S_MarketBuyResponse res)
    {
        CloseWait(res.Result);

        if (res.Result == EResultCode.Ok)
        {
            _wait.RaiseNotice($"{GameDataLoader.GetItemName(res.Tid)} {res.Count:N0}개를 {res.TotalPrice:N0} G에 샀습니다.\n우편함에서 받아 주세요.");

            _auction.RefreshMarketItems();
            BeginWait("거래소 목록");
        }

        Refresh();
    }

    #endregion

    #region 대기

    // 요청 대기를 연다 — 버튼을 잠그고, 닫히면 다시 연다.
    private void BeginWait(string label)
    {
        _waitHandle = _wait.Begin(label, onClosed: OnWaitClosed);
        RefreshBuy();
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
        _waitHandle = null;
        RefreshBuy();
    }

    #endregion

    #region 표시 갱신

    // 목록·가격대·구매 칸을 지금 상태로 그린다.
    private void Refresh()
    {
        _contents.Clear();

        // 매물이 없는 줄(최저가 0)은 정렬 방향과 상관없이 뒤로 간다.
        foreach (MarketItemInfo item in AuctionSort.Apply(_auction.MarketItems, _sortOrder, i => i.AvailableCount > 0 ? i.LowestUnitPrice : 0L))
        {
            _contents.Add(ToRow(item));
        }

        _rows.Show(_contents);
        emptyText.SetActive(_contents.Count == 0);
        sortText.text     = AuctionSort.GetLabel(_sortOrder);
        receivedText.text = AuctionModel.FormatReceivedAgo(_auction.MarketItemsReceivedAt);

        priceText.text = BuildPriceText();
        RefreshBuy();
    }

    // 목록 한 줄 — 아이콘 · 이름(등급 색) · 판매 중 수량(내 매물 수)·최근가·전일 평균 · 최저가. 툴팁에 시세 전부.
    private AuctionRowContent ToRow(MarketItemInfo item)
    {
        long   mine      = _auction.GetMyItemCount(item.Tid);
        string dot       = UIRichText.Dot;
        string mineText  = mine > 0 ? " " + UIRichText.Paint($"(내 매물 {mine:N0})", UIThemeRole.Highlight) : "";
        string recent    = item.RecentUnitPrice   > 0 ? $"{item.RecentUnitPrice:N0}"   : "-";
        string yesterday = item.YesterdayAvgPrice > 0 ? $"{item.YesterdayAvgPrice:N0}" : "-";
        string lowest    = item.AvailableCount    > 0
            ? $"{UIRichText.Label("최저")} {UIRichText.Gold(item.LowestUnitPrice)}"
            : UIRichText.Label("매물 없음");

        return new AuctionRowContent
        {
            Key         = item.Tid,
            Icon        = ItemIconContent.ForItem(item.Tid, 0L),
            Title       = GameDataLoader.GetItemName(item.Tid),
            TitleColor  = RarityPalette.Get(GameDataLoader.GetItemRarity(item.Tid)),
            Info        = $"{UIRichText.Label("판매 중")} {item.AvailableCount:N0}개{mineText}{dot}{UIRichText.Label("최근")} {recent}{dot}{UIRichText.Label("전일 평균")} {yesterday}",
            Detail      = lowest,
            ActionLabel = item.Tid == _selectedTid ? "선택됨" : "선택",
            CanAct      = true,
            Tooltip     = () => AuctionText.BuildMarketItemTooltip(item, _auction.GetMyItemCount(item.Tid), _data.GetItemCount(item.Tid)),
        };
    }

    // 고른 자원의 가격대 문구 — 단가 · 수량 · 내 매물이 열을 맞춰 선다. 고르지 않았거나 아직 안 왔으면 안내 문구.
    // ※ 열 맞춤은 TMP '<pos>'(칸 폭의 %)다 — 글꼴 폭이 달라도 열이 흔들리지 않는다.
    private string BuildPriceText()
    {
        if (_selectedTid == 0)
        {
            return UIRichText.Label("목록에서 자원을 [선택]하면 여기에 단가별 판매 수량이 나옵니다.");
        }

        string name = $"<b>{GameDataLoader.GetItemName(_selectedTid)}</b>";

        if (_auction.PriceTid != _selectedTid)
        {
            return $"{name}\n{UIRichText.Label("가격대를 불러오지 못했습니다. 다시 [선택]해 주세요.")}";
        }

        if (_auction.PriceLevels.Count == 0)
        {
            return $"{name}\n{UIRichText.Label("판매 중인 매물이 없습니다.")}";
        }

        var lines = new List<string>
        {
            $"{name}  {UIRichText.Label("가격대 — 싼 매물부터 팔립니다")}",
            UIRichText.Label("  개당 단가<pos=40%>판매 수량<pos=68%>내 매물"),
        };

        foreach (MarketPriceLevelInfo level in _auction.PriceLevels)
        {
            // 내 매물이 섞인 칸은 몇 개가 내 것인지 붙인다 — 내 물건이 가격대 어디쯤 있는지 보인다.
            long   mine     = _auction.GetMyItemCount(_selectedTid, level.UnitPrice);
            string mineText = mine > 0 ? UIRichText.Paint($"{mine:N0}개", UIThemeRole.Highlight) : UIRichText.Label("-");

            lines.Add($"  {UIRichText.Gold(level.UnitPrice)}<pos=40%>{level.Count:N0}개<pos=68%>{mineText}");
        }

        return string.Join("\n", lines);
    }

    // 구매 칸의 미리보기와 버튼 잠금을 맞춘다 (입력 변경 · 재화 변경 · 대기 시작/종료).
    //
    // ※ 골드 판단은 표시용이다 — 서버는 "수량 x 단가 상한"을 잡아 두고 검사한다(NotEnoughCurrency).
    private void RefreshBuy()
    {
        long maxCount = GetMaxBuyCount();

        if (_selectedTid != 0 && maxCount < 1L)
        {
            buyHintText.text       = UIRichText.Paint("지금 살 수 있는 매물이 없습니다.", UIThemeRole.Negative)
                                   + "\n" + UIRichText.Small(UIRichText.Label("내가 올린 매물은 살 수 없습니다."));
            buyButton.interactable = false;

            return;
        }

        if (!TryReadBuy(out int count, out long maxPrice))
        {
            buyHintText.text       = _selectedTid == 0
                ? ""
                : UIRichText.Label($"수량 1 ~ {maxCount:N0}개, 개당 최대 단가를 넣으세요.");
            buyButton.interactable = false;

            return;
        }

        long   total  = count * maxPrice;
        bool   enough = _data.Gold >= total;
        string held   = UIRichText.Paint($"{_data.Gold:N0} G", enough ? UIThemeRole.TextMain : UIThemeRole.Negative);

        buyHintText.text = $"{UIRichText.Label("최대 지출")} {UIRichText.Gold(total)}{UIRichText.Dot}{UIRichText.Label("보유")} {held}{UIRichText.Dot}{UIRichText.Label("살 수 있는 수량")} {maxCount:N0}개\n"
                         + UIRichText.Small(UIRichText.Label("싼 매물부터 채웁니다 · 수량을 다 못 채우면 사지 않습니다 · 산 자원은 우편으로 옵니다"));
        buyButton.interactable = _waitHandle == null && enough;
    }

    #endregion

    #region 보조

    // 지금 살 수 있는 최대 수량 — min(1회 상한, 판매 중 - 내 매물). 자원을 안 골랐으면 1회 상한.
    //
    // 판매 중 수량은 가격대를 받았으면 그 합(더 새것), 아니면 목록 줄의 값이다.
    // ※ 표시용이다 — 그사이 누가 사 가면 서버가 MarketNotEnough로 거절하고 아무것도 사지 않는다.
    private long GetMaxBuyCount()
    {
        if (_selectedTid == 0)
        {
            return Constants.MarketMaxBuyCount;
        }

        long available = 0L;

        if (_auction.PriceTid == _selectedTid)
        {
            foreach (MarketPriceLevelInfo level in _auction.PriceLevels)
            {
                available += level.Count;
            }
        }
        else
        {
            available = FindItem(_selectedTid)?.AvailableCount ?? 0L;
        }

        long buyable = System.Math.Max(0L, available - _auction.GetMyItemCount(_selectedTid));

        return System.Math.Min(Constants.MarketMaxBuyCount, buyable);
    }

    // 구매 칸 입력을 읽는다. 자원을 안 골랐거나 값이 범위 밖이면 false.
    private bool TryReadBuy(out int count, out long maxPrice)
    {
        maxPrice = 0L;

        bool isCountValid = int.TryParse(countInput.text, out count)
                         && count >= 1
                         && count <= GetMaxBuyCount();

        return _selectedTid != 0
            && isCountValid
            && long.TryParse(maxPriceInput.text, out maxPrice)
            && maxPrice >= 1L;
    }

    // 목록에서 이 자원의 줄을 찾는다. 없으면 null.
    private MarketItemInfo? FindItem(int tid)
    {
        foreach (MarketItemInfo item in _auction.MarketItems)
        {
            if (item.Tid == tid)
            {
                return item;
            }
        }

        return null;
    }

    #endregion
}

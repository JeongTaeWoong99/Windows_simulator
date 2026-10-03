using System.Collections.Generic;
using GameData;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 개체 구매 화면 — 경매장 [구매] 탭의 '장비' · '캐릭터' 축. 개체 매물을 검색해 통째로 산다(입찰 없음).
//
// ■ 한 스크립트, 두 화면: 'searchKind'로 축을 정한다. 씬에는 축마다 이 Presenter가 하나씩 붙는다.
//   검색 상태(조건·결과·커서·받은 시각)는 'AuctionModel'이 축마다 따로 든다 — 두 화면이 서로의 [더 보기]를 잇지 않는다.
// ■ 검색 조건
//   장비   : 이름 · 분류(무기·장신구·보석) · 희귀도 · 인챈트 등급 하한
//   캐릭터 : 이름 · 희귀도 — 분류·인챈트 드롭다운은 비워 둔다(연결하지 않는다)
//   인챈트 등급은 장비 하나에 하나다(이슈 #46 — 칸마다 색이 다르지 않다). 옵션 다중 선택은 매물이 쌓인 뒤에 넣는다.
// ■ 페이지: 서버는 **단가 낮은 순으로 20개씩** 준다(최대 50). [더 보기]가 받은 것 중 마지막 매물(단가·ID)을 커서로
//   다음 20개를 이어 받고, 서버가 'HasMore'로 더 있는지 알린다('AuctionModel.SearchNextPage').
//   버튼 문구에 지금까지 받은 수를 적는다 — 전체가 몇 개인지는 서버가 주지 않는다.
// ■ 구매: 목록에서 본 총액을 그대로 보낸다. 그사이 가격이 바뀌면 AuctionPriceChanged로 거절된다 —
//   그때는 사유 알림에 "다시 검색해 주세요"가 뜬다. 산 개체는 **우편**으로 온다(인챈트·레벨·적성 포함).
// ■ 조회 빈도 제한 때문에 화면을 **처음** 열 때만 자동 검색한다('MarketItemPresenter'와 같다).
//   대신 받은 시각을 "n분 전에 받음"으로 적어 목록이 얼마나 낡았는지 보인다.
// ■ 내 매물은 '[내 매물]'로 표시하고 [구매]를 잠근다 — 어디쯤에 있는지 보이게 목록에서 빼지는 않는다.
//   판별 재료(내 매물 목록)는 화면을 열 때마다 조용히 받는다 — 빈도 제한 밖이다.
// ■ [가격 정렬]은 받은 결과의 **사본만** 정렬한다('AuctionSort'). [더 보기]는 서버 순서의 마지막 매물로 잇는다.
//   ⚠️ 높은 가격순은 **받은 페이지 안에서만** 맞다 — 더 받을 것이 남았으면 버튼에 '받은 것 중'을 적는다.
public class AuctionSearchPresenter : MonoBehaviour
{
    [CenterHeader("축")]
    [SerializeField, Tooltip("이 화면이 검색하는 매물 종류 — Equip 또는 Character")]
    private EAuctionKind searchKind = EAuctionKind.Equip;


    [CenterHeader("검색")]
    [SerializeField, Tooltip("이름 검색 입력칸. 비우면 이름 조건 없음")]
    private TMP_InputField searchInput = null!;

    [SerializeField, Tooltip("분류 드롭다운 — 전체·무기·장신구·보석. 장비 축에만 연결한다. 항목은 코드가 채운다")]
    private TMP_Dropdown? kindDropdown;

    [SerializeField, Tooltip("희귀도 드롭다운 — 전체·일반~신화. 항목은 코드가 채운다")]
    private TMP_Dropdown rarityDropdown = null!;

    [SerializeField, Tooltip("인챈트 등급 하한 드롭다운 — 무관·일반 이상~신화 이상. 장비 축에만 연결한다. 항목은 코드가 채운다")]
    private TMP_Dropdown? enchantDropdown;

    [SerializeField, Tooltip("검색 버튼. OnClick은 코드가 연결한다")]
    private Button searchButton = null!;


    [CenterHeader("목록")]
    [SerializeField, Tooltip("매물 한 줄 프리팹")]
    private AuctionRowView rowPrefab = null!;

    [SerializeField, Tooltip("줄이 쌓이는 Content (VLG + ContentSizeFitter)")]
    private Transform rowParent = null!;

    [SerializeField, Tooltip("목록이 비었을 때만 보인다")]
    private GameObject emptyText = null!;

    [SerializeField, Tooltip("받은 시각 문구 — \"3분 전에 받음\"")]
    private TMP_Text receivedText = null!;

    [SerializeField, Tooltip("다음 페이지 버튼 — 결과가 더 있을 때만 누를 수 있다. OnClick은 코드가 연결한다")]
    private Button moreButton = null!;

    [SerializeField, Tooltip("[더 보기] 버튼 문구 — 받은 수 · 더 있는지")]
    private TMP_Text moreText = null!;

    [SerializeField, Tooltip("가격 정렬 버튼 — 낮은 가격순 ↔ 높은 가격순(받은 페이지 안에서). OnClick은 코드가 연결한다")]
    private Button sortButton = null!;

    [SerializeField, Tooltip("정렬 버튼 문구 — 지금 정렬을 적는다")]
    private TMP_Text sortText = null!;

    // 드롭다운 항목 순서 = 이 배열 순서. 0번이 '전체'(None)다.
    private static readonly EquipKind[] KindOptions =
    {
        EquipKind.None, EquipKind.Weapon, EquipKind.Accessory, EquipKind.Gem,
    };

    private static readonly GlobalRarity[] RarityOptions =
    {
        GlobalRarity.None, GlobalRarity.Common, GlobalRarity.Uncommon, GlobalRarity.Rare,
        GlobalRarity.Epic, GlobalRarity.Legendary, GlobalRarity.Mythic,
    };

    // 인챈트 등급 하한 — 희귀도와 같은 등급 체계다. 0번이 '무관'(None).
    private static readonly GlobalRarity[] EnchantOptions = RarityOptions;

    private readonly List<AuctionRowContent> _contents = new List<AuctionRowContent>();

    private AuctionModel      _auction = null!;
    private PlayerDataModel   _data    = null!;
    private ServerWaitManager _wait    = null!;
    private UIManager         _ui      = null!;
    private AuctionRowList    _rows    = null!;

    // 진행 중인 대기의 손잡이 — 응답이 오면 결과를 보고한다.
    private ServerWaitHandle? _waitHandle;

    // 지금 가격 정렬 — 화면 상태라 저장하지 않는다.
    private AuctionSortOrder _sortOrder = AuctionSortOrder.Ascending;

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    private bool IsEquip => searchKind == EAuctionKind.Equip;

    // 알림·확인창에 쓰는 축 이름
    private string KindWord => IsEquip ? "장비" : "캐릭터";

    // 참조 확보 → 구독 → 배선 → 초기화 순서로 진행한다 (클라 공통 규약)
    private void Start()
    {
        this.RequireRef(searchInput,    nameof(searchInput));
        this.RequireRef(rarityDropdown, nameof(rarityDropdown));
        this.RequireRef(searchButton,   nameof(searchButton));
        this.RequireRef(rowPrefab,      nameof(rowPrefab));
        this.RequireRef(rowParent,      nameof(rowParent));
        this.RequireRef(emptyText,      nameof(emptyText));
        this.RequireRef(receivedText,   nameof(receivedText));
        this.RequireRef(moreButton,     nameof(moreButton));
        this.RequireRef(sortButton,     nameof(sortButton));
        this.RequireRef(sortText,       nameof(sortText));
        this.RequireRef(moreText,       nameof(moreText));

        if (IsEquip)
        {
            this.RequireRef(kindDropdown,    nameof(kindDropdown));
            this.RequireRef(enchantDropdown, nameof(enchantDropdown));
        }

        _auction = Services.Get<AuctionModel>();
        _data    = Services.Get<PlayerDataModel>();
        _wait    = Services.Get<ServerWaitManager>();
        _ui      = Services.Get<UIManager>();
        _rows    = new AuctionRowList(rowPrefab, rowParent, OnRowBuyClicked);

        FillDropdowns();
        Subscribe();

        searchButton.onClick.AddListener(OnSearchClicked);
        searchInput.onSubmit.AddListener(_ => OnSearchClicked());
        moreButton.onClick.AddListener(OnMoreClicked);
        sortButton.onClick.AddListener(OnSortClicked);

        Refresh();
        RequestMyListings();

        if (!_auction.HasSearched(searchKind) && _data.IsLoggedIn)
        {
            OnSearchClicked();
        }

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

    // 검색·구매 응답과 재화 변경 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed = true;

        _auction.SearchCompleted     += OnSearchCompleted;
        _auction.BuyCompleted        += OnBuyCompleted;
        _auction.MyListingsCompleted += OnMyListingsCompleted;
        _data.CurrencyChanged        += Refresh;
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
        _auction.MyListingsCompleted -= OnMyListingsCompleted;
        _data.CurrencyChanged        -= Refresh;
    }

    #endregion

    #region 초기화

    // 드롭다운 항목을 채운다 (Start에서 한 번). 이름은 공용 표시 이름을 쓴다. 연결 안 된 드롭다운은 건너뛴다.
    private void FillDropdowns()
    {
        var rarityNames = new List<string>();

        foreach (GlobalRarity rarity in RarityOptions)
        {
            rarityNames.Add(rarity == GlobalRarity.None ? "등급 전체" : RarityLabel.Get(rarity));
        }

        rarityDropdown.ClearOptions();
        rarityDropdown.AddOptions(rarityNames);

        if (kindDropdown != null)
        {
            var kindNames = new List<string>();

            foreach (EquipKind kind in KindOptions)
            {
                kindNames.Add(kind == EquipKind.None ? "분류 전체" : EquipLabel.GetKindName(kind));
            }

            kindDropdown.ClearOptions();
            kindDropdown.AddOptions(kindNames);
        }

        if (enchantDropdown != null)
        {
            var enchantNames = new List<string>();

            foreach (GlobalRarity grade in EnchantOptions)
            {
                enchantNames.Add(grade == GlobalRarity.None ? "인챈트 무관" : $"{RarityLabel.Get(grade)} 이상");
            }

            enchantDropdown.ClearOptions();
            enchantDropdown.AddOptions(enchantNames);
        }
    }

    #endregion

    #region 요청

    // 조건으로 첫 페이지를 검색한다 (searchButton OnClick · 입력칸 Enter · 화면을 처음 열 때).
    //
    // 맞는 이름이 없으면 보내지 않는다 — 빈 TID 목록은 서버가 "조건 없음"으로 읽어 전체를 돌려준다.
    private void OnSearchClicked()
    {
        if (_waitHandle != null)
        {
            return;
        }

        List<int>? tids = IsEquip ? AuctionText.FindEquipTids(searchInput.text) : AuctionText.FindCharacterTids(searchInput.text);

        if (tids != null && tids.Count == 0)
        {
            _wait.RaiseNotice($"'{searchInput.text.Trim()}'(이)라는 {KindWord}이(가) 없습니다.");

            return;
        }

        GlobalRarity rarity = RarityOptions[rarityDropdown.value];

        if (IsEquip)
        {
            EquipKind    kind    = kindDropdown    != null ? KindOptions[kindDropdown.value]       : EquipKind.None;
            GlobalRarity enchant = enchantDropdown != null ? EnchantOptions[enchantDropdown.value] : GlobalRarity.None;

            _auction.SearchEquips(tids, kind, rarity, enchant);
        }
        else
        {
            _auction.SearchCharacters(tids, rarity);
        }

        BeginWait("경매장 검색");
    }

    // 내 매물을 조용히 받는다 — 대기 없이, 결과가 오면 '[내 매물]' 표시만 다시 그린다 (Start · OnEnable).
    private void RequestMyListings()
    {
        if (_data.IsLoggedIn)
        {
            _auction.RequestMyListings();
        }
    }

    // [가격 정렬] — 다음 정렬로 바꿔 다시 그린다. 요청은 보내지 않는다 (sortButton OnClick)
    private void OnSortClicked()
    {
        _sortOrder = AuctionSort.Next(_sortOrder);
        Refresh();
    }

    // [더 보기] — 같은 조건으로 다음 페이지를 받는다 (moreButton OnClick)
    private void OnMoreClicked()
    {
        if (_waitHandle != null || !_auction.HasMoreResults(searchKind))
        {
            return;
        }

        _auction.SearchNextPage(searchKind);
        BeginWait("경매장 검색");
    }

    // 줄의 [구매] — 확인을 받고 보낸다 (AuctionRowView.ActionClicked)
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

        string name = AuctionText.GetName(listing.Kind, listing.Tid);

        _ui.AskConfirm($"{name}을(를) {listing.TotalPrice:N0} G에 삽니다.\n보유 {_data.Gold:N0} G · 구매 후 {_data.Gold - listing.TotalPrice:N0} G\n\n산 {KindWord}은(는) 우편으로 옵니다.", () =>
        {
            _auction.RequestBuy(listing);
            BeginWait("경매장 구매");
        });
    }

    #endregion

    #region 응답 (AuctionModel 구독)

    // 검색 결과 도착 — 다른 축의 응답은 무시한다 (AuctionModel.SearchCompleted 구독)
    private void OnSearchCompleted(EAuctionKind kind, EResultCode code)
    {
        if (kind != searchKind)
        {
            return;
        }

        CloseWait(code);
        Refresh();
    }

    // 내 매물 도착 — '[내 매물]' 표시를 다시 맞춘다. 조용히 받은 것이라 대기는 없다 (AuctionModel.MyListingsCompleted 구독)
    private void OnMyListingsCompleted(EResultCode code)
    {
        Refresh();
    }

    // 구매 결과 — 내가 건 대기일 때만 닫고 알린다. 목록에서 빼는 일은 모델이 했다 (AuctionModel.BuyCompleted 구독)
    // ※ 두 축 화면 중 꺼진 쪽은 구독이 풀려 있어 받지 않는다. 그래도 대기 손잡이로 내 요청인지 가린다.
    private void OnBuyCompleted(EResultCode code)
    {
        if (_waitHandle == null)
        {
            Refresh();

            return;
        }

        CloseWait(code);

        if (code == EResultCode.Ok)
        {
            _wait.RaiseNotice("구매했습니다. 우편함에서 받아 주세요.");
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
        _waitHandle = null;
        Refresh();
    }

    #endregion

    #region 표시 갱신

    // 목록과 [더 보기]·받은 시각·정렬 문구를 지금 상태로 그린다.
    //
    // [구매]는 세 축을 함께 본다 — 내 매물이면, 누가 구매 중(Reserved)이면, 골드가 모자라면 잠근다.
    // ※ 골드 판단은 표시용이다. 실제 거절은 서버가 한다(NotEnoughCurrency · AuctionOwnListing).
    private void Refresh()
    {
        IReadOnlyList<AuctionListingInfo> results = _auction.GetSearchResults(searchKind);
        bool                              hasMore = _auction.HasMoreResults(searchKind);

        _contents.Clear();

        foreach (AuctionListingInfo listing in AuctionSort.Apply(results, _sortOrder, l => l.TotalPrice))
        {
            bool isMine = _auction.IsMine(listing.ListingId);
            bool canBuy = _waitHandle == null
                       && !isMine
                       && listing.State != EAuctionListingState.Reserved
                       && _data.Gold >= listing.TotalPrice;

            _contents.Add(AuctionText.ToRow(listing, isMine ? "내 매물" : "구매", canBuy, isMine));
        }

        _rows.Show(_contents);
        emptyText.SetActive(_contents.Count == 0);
        receivedText.text       = AuctionModel.FormatReceivedAgo(_auction.GetSearchReceivedAt(searchKind));
        moreButton.interactable = _waitHandle == null && hasMore;
        moreText.text           = FormatMoreLabel(results.Count, hasMore);
        sortText.text           = AuctionSort.GetLabel(_sortOrder, hasMore);
    }

    #endregion

    #region 보조

    // [더 보기] 문구 — "더 보기 (20개 받음)" · 끝까지 받았으면 "전부 받음 (37개)".
    private static string FormatMoreLabel(int loaded, bool hasMore)
    {
        if (loaded == 0)
        {
            return "더 보기";
        }

        return hasMore ? $"더 보기 ({loaded:N0}개 받음)" : $"전부 받음 ({loaded:N0}개)";
    }

    // 이 축의 검색 결과에서 매물을 찾는다. 없으면 null.
    private AuctionListingInfo? FindListing(long listingId)
    {
        foreach (AuctionListingInfo listing in _auction.GetSearchResults(searchKind))
        {
            if (listing.ListingId == listingId)
            {
                return listing;
            }
        }

        return null;
    }

    #endregion
}

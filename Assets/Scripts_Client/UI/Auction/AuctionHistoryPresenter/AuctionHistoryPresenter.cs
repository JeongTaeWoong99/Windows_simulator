using System.Collections.Generic;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 완료 탭 — 경매장 거래 기록(판매 완료 · 구매 완료 · 기간 만료 · 취소 · 반환). 최근 30일까지 서버가 보관한다 (T-147 · T-124).
//
// ■ 탭을 열 때마다 첫 쪽을 새로 받고 "봤다"로 남긴다 — 탭 배지('HistoryUnseenCount')가 꺼진다.
//   그때 새로 들어와 있던 기록 수('HistoryNewCount')만큼 맨 위 줄 일시 앞에 점을 찍는다(구분이 '전체'일 때만 — 걸러 받으면 어느 줄이 새것인지 모른다).
// ■ 구분·기간을 바꾸면 서버에 다시 묻는다. 이름은 받은 목록을 거르기만 한다(서버에 묻지 않는다).
// ■ 쪽으로 나눈다 — 한 쪽 9줄('AuctionModel.RowsPerPage'), 표 아래 [◀][▶]로 넘긴다(2026-10-14 사용자 결정).
//   서버는 건수를 주지 않아 전체 쪽 수는 모른다 — [▶]는 뒤에 더 있을 때만 열린다.
// ■ 물건·대금·환급은 모두 우편으로 간다 — 이 화면은 기록만 보여 준다. '받는 곳' 열이 그 우편을 가리킨다.
public class AuctionHistoryPresenter : MonoBehaviour, IAuctionQueryTarget
{
    [CenterHeader("머리")]
    [SerializeField, Tooltip("'기록 N건'")]
    private TMP_Text countText = null!;

    [SerializeField, Tooltip("구분 — 전체 · 판매 완료 · 구매 완료 · 기간 만료 · 취소 · 반환")]
    private TMP_Dropdown typeDropdown = null!;

    [SerializeField, Tooltip("기간 — 오늘 · 최근 7일 · 최근 30일")]
    private TMP_Dropdown periodDropdown = null!;

    [SerializeField, Tooltip("받은 골드 + · 쓴 골드 − (지금 보이는 줄의 합)")]
    private TMP_Text sumText = null!;


    [CenterHeader("목록")]
    [SerializeField, Tooltip("기록 한 줄 프리팹 — 버튼 없음")]
    private AuctionTableRowView rowPrefab = null!;

    [SerializeField, Tooltip("줄이 쌓이는 Content (VLG + ContentSizeFitter)")]
    private Transform rowParent = null!;

    [SerializeField, Tooltip("목록 스크롤 — 쪽을 받으면 맨 위로 올린다")]
    private ScrollRect scrollRect = null!;

    [SerializeField, Tooltip("표 아래 쪽 넘김 줄 — [◀] 'n 쪽' [▶]")]
    private AuctionPagerView pager = null!;

    [SerializeField, Tooltip("보일 기록이 없을 때만 보인다")]
    private TMP_Text emptyText = null!;

    [SerializeField, Tooltip("표 아래 안내 — '물건·대금·환급은 모두 우편으로 갑니다' · 받은 시각 · 더 받는 중")]
    private TMP_Text noteText = null!;

    // 구분 드롭다운 순서 — 표시 이름과 서버 값.
    private static readonly (string Label, EAuctionHistoryType Type)[] Types =
    {
        ("전체",      EAuctionHistoryType.None),
        ("판매 완료", EAuctionHistoryType.Sold),
        ("구매 완료", EAuctionHistoryType.Bought),
        ("기간 만료", EAuctionHistoryType.Expired),
        ("취소",      EAuctionHistoryType.Cancelled),
        ("반환",      EAuctionHistoryType.Returned),
    };

    // 기간 드롭다운 순서 — 표시 이름과 일 수. 서버 보관 기간이 30일이라 그 이상은 없다.
    private static readonly (string Label, int Days)[] Periods =
    {
        ("오늘",      1),
        ("최근 7일",  7),
        ("최근 30일", 30),
    };

    // 처음 고르는 기간 — 최근 7일(목업).
    private const int DefaultPeriodIndex = 1;

    private readonly List<AuctionTableRowContent> _contents = new List<AuctionTableRowContent>();

    private AuctionModel        _auction = null!;
    private PlayerDataModel     _data    = null!;
    private ServerWaitManager   _wait    = null!;
    private AuctionTableRowList _rows    = null!;

    private string _query = "";

    // 진행 중인 쪽 대기의 손잡이.
    private ServerWaitHandle? _waitHandle;

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // ─── 공용 검색창 ───
    public string QueryPlaceholder => "기록에서 찾을 아이템 이름";
    public string QueryHint        => "치는 대로 아래 기록을 거릅니다";
    public string Query            => _query;

    public void OnQueryChanged(string text)
    {
        _query = text;

        if (_isReady)
        {
            Refresh();
        }
    }

    public void OnQuerySubmitted(string text) => OnQueryChanged(text);

    // 지금 쪽의 기록에서 이름이 맞는 것 — 같은 아이템은 한 번만.
    public void CollectSuggestions(string query, List<AuctionSuggestion> into, int max)
    {
        into.Clear();

        foreach (AuctionHistoryInfo entry in _auction.History)
        {
            AuctionText.AddListSuggestion(into, entry.Kind, entry.Tid, query, max);
        }
    }

    // 이름으로 거르는 일은 'OnQueryChanged'가 이미 했다.
    public void OnSuggestionPicked(AuctionSuggestion suggestion) { }

    // 참조 확보 → 구독 → 배선 → 초기화 순서로 진행한다 (클라 공통 규약)
    private void Start()
    {
        this.RequireRef(countText,      nameof(countText));
        this.RequireRef(typeDropdown,   nameof(typeDropdown));
        this.RequireRef(periodDropdown, nameof(periodDropdown));
        this.RequireRef(sumText,        nameof(sumText));
        this.RequireRef(rowPrefab,      nameof(rowPrefab));
        this.RequireRef(rowParent,      nameof(rowParent));
        this.RequireRef(scrollRect,     nameof(scrollRect));
        this.RequireRef(emptyText,      nameof(emptyText));
        this.RequireRef(noteText,       nameof(noteText));
        this.RequireRef(pager,          nameof(pager));

        _auction = Services.Get<AuctionModel>();
        _data    = Services.Get<PlayerDataModel>();
        _wait    = Services.Get<ServerWaitManager>();
        _rows    = new AuctionTableRowList(rowPrefab, rowParent, onPrimary: null);

        Subscribe();

        FillDropdowns();

        typeDropdown.onValueChanged.AddListener(_ => RequestFirstPage());
        periodDropdown.onValueChanged.AddListener(_ => RequestFirstPage());
        pager.PrevClicked += () => GoToPage(_auction.HistoryPage - 1);
        pager.NextClicked += () => GoToPage(_auction.HistoryPage + 1);

        Refresh();
        RequestFirstPage();

        _isReady = true;
    }

    // 껐다 켠 경우의 재구독 — 열 때마다 새로 받는다 (Unity 메시지)
    private void OnEnable()
    {
        if (_isReady)
        {
            Subscribe();
            Refresh();
            RequestFirstPage();
        }
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
    }

    #region 구독

    // 거래 기록 응답 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed = true;

        _auction.HistoryCompleted += OnHistoryCompleted;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed = false;

        _auction.HistoryCompleted -= OnHistoryCompleted;
    }

    #endregion

    #region 요청

    // 고른 구분·기간으로 첫 쪽을 받는다 (Start · OnEnable · 드롭다운 변경).
    private void RequestFirstPage()
    {
        if (_waitHandle != null || !_data.IsLoggedIn)
        {
            return;
        }

        _auction.RequestHistory(Types[typeDropdown.value].Type, Periods[periodDropdown.value].Days);

        _waitHandle = _wait.Begin("거래 기록", onClosed: OnWaitClosed);
        Refresh();
    }

    // 같은 조건으로 그 쪽을 받는다 (pager [◀][▶])
    private void GoToPage(int page)
    {
        if (_waitHandle != null || !_auction.RequestHistoryPage(page))
        {
            return;
        }

        _waitHandle = _wait.Begin("거래 기록", onClosed: OnWaitClosed);
        Refresh();
    }

    #endregion

    #region 응답 (AuctionModel 구독)

    // 기록 도착 — 대기를 닫고 맨 위로 (AuctionModel.HistoryCompleted 구독)
    private void OnHistoryCompleted(EResultCode code)
    {
        if (_waitHandle != null)
        {
            if (code == EResultCode.Ok)
            {
                _waitHandle.Succeed();
            }
            else
            {
                _waitHandle.Fail(ResultMessages.ToText(code));
            }

            scrollRect.verticalNormalizedPosition = 1f;
        }

        Refresh();
    }

    // 대기가 끝났다(성공·실패·타임아웃 공통) (ServerWaitManager.Begin의 onClosed)
    private void OnWaitClosed()
    {
        _waitHandle = null;
        Refresh();
    }

    #endregion

    #region 표시 갱신

    // 머리 · 합계 · 목록 · 아래 안내를 지금 상태로 그린다.
    private void Refresh()
    {
        _contents.Clear();

        bool markNew  = typeDropdown.value == 0 && _auction.HistoryPage == 0;
        long received = 0L;
        long spent    = 0L;

        IReadOnlyList<AuctionHistoryInfo> history = _auction.History;

        for (int i = 0; i < history.Count; i++)
        {
            AuctionHistoryInfo entry = history[i];

            if (!AuctionText.MatchesName(entry.Kind, entry.Tid, _query))
            {
                continue;
            }

            if (entry.Type == EAuctionHistoryType.Bought)
            {
                spent += entry.Amount;
            }
            else
            {
                received += entry.Amount;
            }

            _contents.Add(ToRow(entry, isNew: markNew && i < _auction.HistoryNewCount));
        }

        _rows.Show(_contents);

        emptyText.gameObject.SetActive(_contents.Count == 0);
        emptyText.text = history.Count == 0 ? "이 기간에 거래 기록이 없습니다" : "이름이 맞는 기록이 없습니다";

        // 서버가 건수를 주지 않는다 — 지금 쪽까지 받은 수만 알고, 뒤에 더 있으면 '이상'을 붙인다.
        int known = _auction.HistoryPage * AuctionModel.RowsPerPage + history.Count;

        countText.text = $"기록 {UIRichText.Paint(known.ToString("N0"), UIThemeRole.Highlight)}건" +
                         (_auction.HasMoreHistory ? UIRichText.Label(" 이상") : "");

        sumText.text = $"{UIRichText.Label("받은 골드")} {UIRichText.Paint($"+{received:N0} G", UIThemeRole.Positive)}" +
                       $"{UIRichText.Dot}{UIRichText.Label("쓴 골드")} {UIRichText.Paint($"−{spent:N0} G", UIThemeRole.Negative)}";

        string ago = AuctionModel.FormatReceivedAgo(_auction.HistoryReceivedAt);

        noteText.text = "물건 · 대금 · 환급은 모두 우편으로 갑니다" + (ago.Length > 0 ? UIRichText.Dot + UIRichText.Label(ago) : "");

        pager.Bind(_auction.HistoryPage, pageCount: 0,
                   canPrev: _waitHandle == null && _auction.HistoryPage > 0,
                   canNext: _waitHandle == null && _auction.HasMoreHistory,
                   note: "");

        typeDropdown.interactable   = _waitHandle == null;
        periodDropdown.interactable = _waitHandle == null;
    }

    // 기록 한 줄 — 열: 일시 · 구분 · (아이템) · 수량 · 금액 · 받는 곳.
    private static AuctionTableRowContent ToRow(AuctionHistoryInfo entry, bool isNew)
    {
        string date = AuctionText.FormatDateTime(entry.AtUnixMs);

        return new AuctionTableRowContent
        {
            Key        = entry.HistoryId,
            Icon       = AuctionText.ToIcon(entry.Kind, entry.Tid, entry.Count),
            Title      = AuctionText.GetName(entry.Kind, entry.Tid) + (entry.EnchantGrade > 0 ? $" +{entry.EnchantGrade}" : ""),
            TitleColor = RarityPalette.Get(AuctionText.GetRarity(entry.Kind, entry.Tid)),
            Sub        = $"{UIRichText.Label("단가")} {UIRichText.Gold(entry.UnitPrice)}",
            Cells      = new[]
            {
                isNew ? $"{UIRichText.Paint("●", UIThemeRole.Highlight)} {date}" : UIRichText.Label(date),
                PaintType(entry.Type),
                $"{entry.Count:N0}",
                FormatAmount(entry),
                UIRichText.Label(FormatDestination(entry.Type)),
            },
            Highlight = isNew,
        };
    }

    // 구분 — 판매·구매는 의미색, 나머지는 흐린 글씨.
    private static string PaintType(EAuctionHistoryType type) => type switch
    {
        EAuctionHistoryType.Sold    => UIRichText.Paint("판매 완료", UIThemeRole.Positive),
        EAuctionHistoryType.Bought  => UIRichText.Paint("구매 완료", UIThemeRole.Accent),
        EAuctionHistoryType.Expired => UIRichText.Label("기간 만료"),
        EAuctionHistoryType.Cancelled => UIRichText.Label("취소"),
        EAuctionHistoryType.Returned  => UIRichText.Paint("반환", UIThemeRole.Negative),
        _ => UIRichText.Label("-"),
    };

    // 금액 — 판매는 받은 대금(+, 수수료 표시), 구매는 낸 금액(−), 만료·반환은 돌려받은 등록비(+), 취소는 없음.
    private static string FormatAmount(AuctionHistoryInfo entry)
    {
        if (entry.Type == EAuctionHistoryType.Bought)
        {
            return UIRichText.Paint($"−{entry.Amount:N0} G", UIThemeRole.Negative);
        }

        if (entry.Amount <= 0L)
        {
            return UIRichText.Paint("—", UIThemeRole.TextDisabled);
        }

        string amount = UIRichText.Paint($"+{entry.Amount:N0} G", UIThemeRole.Positive);

        return entry.Fee > 0L ? $"{amount} {UIRichText.Small(UIRichText.Label($"수수료 {entry.Fee:N0}"))}" : amount;
    }

    // 받는 곳 — 무엇이 어디로 갔는지.
    private static string FormatDestination(EAuctionHistoryType type) => type switch
    {
        EAuctionHistoryType.Sold      => "대금 → 우편",
        EAuctionHistoryType.Bought    => "물건 → 우편",
        EAuctionHistoryType.Expired   => "물건 · 등록비 → 우편",
        EAuctionHistoryType.Cancelled => "물건 → 우편",
        EAuctionHistoryType.Returned  => "물건 · 등록비 → 우편",
        _                             => "-",
    };

    #endregion

    #region 보조

    // 드롭다운 항목을 채운다 (Start에서 한 번).
    private void FillDropdowns()
    {
        var types   = new List<string>();
        var periods = new List<string>();

        foreach (var (label, _) in Types)
        {
            types.Add(label);
        }

        foreach (var (label, _) in Periods)
        {
            periods.Add(label);
        }

        typeDropdown.ClearOptions();
        typeDropdown.AddOptions(types);
        typeDropdown.SetValueWithoutNotify(0);

        periodDropdown.ClearOptions();
        periodDropdown.AddOptions(periods);
        periodDropdown.SetValueWithoutNotify(DefaultPeriodIndex);
    }

    #endregion
}

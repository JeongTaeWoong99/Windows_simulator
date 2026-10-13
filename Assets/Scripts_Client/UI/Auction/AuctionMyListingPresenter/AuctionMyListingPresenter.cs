using System.Collections.Generic;
using GameData;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 판매 탭 — 내가 올린 매물(판매 중 · 구매 진행 중)을 보고 취소한다. [+ 판매 등록]으로 등록 창을 연다 (T-147).
//
// ■ 탭을 열 때마다 새로 받는다 — 이 조회는 빈도 제한에 들지 않고, 팔린 것은 푸시가 오지 않아서
//   (대금이 우편으로 올 뿐이다) 열 때 받지 않으면 이미 팔린 매물이 남아 보인다.
// ■ 구매 진행 중(Reserved)인 매물은 [취소]를 잠근다 — 서버도 거절한다.
// ■ 취소하면 물건은 **우편**으로 돌아오고 등록비는 돌려받지 못한다. 확인 창에서 먼저 알린다.
// ■ 공용 검색창은 받은 목록을 이름으로 거르기만 한다 — 서버에 묻지 않는다.
// ■ 판매 중이 상한(Constants.AuctionMaxActiveListings)이면 [+ 판매 등록]을 잠근다.
public class AuctionMyListingPresenter : MonoBehaviour, IAuctionQueryTarget
{
    [CenterHeader("머리")]
    [SerializeField, Tooltip("'판매 중 n / 20'")]
    private TMP_Text countText = null!;

    [SerializeField, Tooltip("등록 기간 · 등록비 · 판매 수수료 안내 한 줄")]
    private TMP_Text infoText = null!;

    [SerializeField, Tooltip("[+ 판매 등록]. OnClick은 코드가 연결한다")]
    private Button registerButton = null!;

    [SerializeField, Tooltip("판매 등록 창 (경매장 안 모달)")]
    private AuctionRegisterPresenter register = null!;


    [CenterHeader("목록")]
    [SerializeField, Tooltip("판매 중 한 줄 프리팹")]
    private AuctionTableRowView rowPrefab = null!;

    [SerializeField, Tooltip("줄이 쌓이는 Content (VLG + ContentSizeFitter)")]
    private Transform rowParent = null!;

    [SerializeField, Tooltip("보일 매물이 없을 때만 보인다")]
    private TMP_Text emptyText = null!;

    [SerializeField, Tooltip("표 아래 안내 — '팔리면 대금은 우편으로 옵니다' · 받은 시각")]
    private TMP_Text noteText = null!;

    private readonly List<AuctionTableRowContent> _contents = new List<AuctionTableRowContent>();

    private AuctionModel        _auction = null!;
    private PlayerDataModel     _data    = null!;
    private ServerWaitManager   _wait    = null!;
    private UIManager           _ui      = null!;
    private AuctionTableRowList _rows    = null!;

    private string _query = "";

    // 진행 중인 대기의 손잡이 — 응답이 오면 결과를 보고한다.
    private ServerWaitHandle? _waitHandle;

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // ─── 공용 검색창 ───
    public string QueryPlaceholder => "판매 중인 아이템 이름";
    public string QueryHint        => "치는 대로 아래 목록을 거릅니다";
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

    // 참조 확보 → 구독 → 배선 → 초기화 순서로 진행한다 (클라 공통 규약)
    private void Start()
    {
        this.RequireRef(countText,      nameof(countText));
        this.RequireRef(infoText,       nameof(infoText));
        this.RequireRef(registerButton, nameof(registerButton));
        this.RequireRef(register,       nameof(register));
        this.RequireRef(rowPrefab,      nameof(rowPrefab));
        this.RequireRef(rowParent,      nameof(rowParent));
        this.RequireRef(emptyText,      nameof(emptyText));
        this.RequireRef(noteText,       nameof(noteText));

        _auction = Services.Get<AuctionModel>();
        _data    = Services.Get<PlayerDataModel>();
        _wait    = Services.Get<ServerWaitManager>();
        _ui      = Services.Get<UIManager>();
        _rows    = new AuctionTableRowList(rowPrefab, rowParent, OnRowCancelClicked);

        Subscribe();

        registerButton.onClick.AddListener(() => register.OpenEmpty());

        infoText.text = $"{UIRichText.Label("등록 기간")} {Constants.AuctionListingHours}시간" +
                        $"{UIRichText.Dot}{UIRichText.Label("등록비")} {AuctionModel.FormatPermille(AuctionModel.ListingFeePermille)} (만료되면 돌려받음)" +
                        $"{UIRichText.Dot}{UIRichText.Label("판매 수수료")} {AuctionModel.FormatPermille(Constants.AuctionSaleFeePermille)}";

        Refresh();
        RequestListings();

        _isReady = true;
    }

    // 껐다 켠 경우의 재구독 — 열 때마다 새로 받는다 (Unity 메시지)
    private void OnEnable()
    {
        if (_isReady)
        {
            Subscribe();
            Refresh();
            RequestListings();
        }
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
    }

    #region 구독

    // 내 매물·취소·등록 응답 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed = true;

        _auction.MyListingsCompleted += OnMyListingsCompleted;
        _auction.CancelCompleted     += OnCancelCompleted;
        _auction.RegisterCompleted   += OnRegisterCompleted;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed = false;

        _auction.MyListingsCompleted -= OnMyListingsCompleted;
        _auction.CancelCompleted     -= OnCancelCompleted;
        _auction.RegisterCompleted   -= OnRegisterCompleted;
    }

    #endregion

    #region 요청

    // 내 매물을 새로 받는다 (Start · OnEnable).
    private void RequestListings()
    {
        if (_waitHandle != null || !_data.IsLoggedIn)
        {
            return;
        }

        _auction.RequestMyListings();
        BeginWait("내 매물");
    }

    // 줄의 [취소] — 확인을 받고 보낸다 (AuctionTableRowView.PrimaryClicked)
    private void OnRowCancelClicked(long listingId)
    {
        if (_waitHandle != null)
        {
            return;
        }

        AuctionListingInfo? listing = FindListing(listingId);

        if (listing == null)
        {
            return;
        }

        string name = AuctionText.GetName(listing.Kind, listing.Tid);

        _ui.AskConfirm($"{name} 판매를 취소합니다.\n남은 물건은 우편으로 돌아오고, 등록비는 돌려받지 못합니다.", () =>
        {
            _auction.RequestCancel(listingId);
            BeginWait("판매 취소");
        });
    }

    #endregion

    #region 응답 (AuctionModel 구독)

    // 내 매물 도착 (AuctionModel.MyListingsCompleted 구독)
    private void OnMyListingsCompleted(EResultCode code)
    {
        CloseWait(code);
        Refresh();
    }

    // 취소 결과 — 성공이면 우편으로 돌아온다고 알린다. 목록에서 빼는 일은 모델이 했다 (AuctionModel.CancelCompleted 구독)
    private void OnCancelCompleted(EResultCode code)
    {
        CloseWait(code);

        if (code == EResultCode.Ok)
        {
            _wait.RaiseNotice("판매를 취소했습니다. 물건은 우편함에서 받아 주세요.");
        }

        Refresh();
    }

    // 등록 창에서 올렸다 — 모델이 내 매물을 다시 받는다. 그동안 상한 표시만 맞춘다 (AuctionModel.RegisterCompleted 구독)
    private void OnRegisterCompleted(EResultCode code, long fee)
    {
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

    // 머리 문구와 목록을 지금 상태로 그린다 — 검색어가 있으면 이름으로 거른다.
    private void Refresh()
    {
        _contents.Clear();

        foreach (AuctionListingInfo listing in _auction.MyListings)
        {
            if (!AuctionText.MatchesName(listing, _query))
            {
                continue;
            }

            _contents.Add(AuctionText.ToSellRow(listing, canCancel: _waitHandle == null));
        }

        _rows.Show(_contents);

        int  total  = _auction.MyListings.Count;
        bool isFull = total >= Constants.AuctionMaxActiveListings;

        emptyText.gameObject.SetActive(_contents.Count == 0);
        emptyText.text = total == 0 ? "판매 중인 매물이 없습니다" : "이름이 맞는 매물이 없습니다";

        countText.text = $"판매 중 {UIRichText.Paint(total.ToString("N0"), isFull ? UIThemeRole.Negative : UIThemeRole.Highlight)} / {Constants.AuctionMaxActiveListings:N0}";

        string received = AuctionModel.FormatReceivedAgo(_auction.MyListingsReceivedAt);

        noteText.text = "팔리면 대금은 우편으로 옵니다" + (received.Length > 0 ? UIRichText.Dot + UIRichText.Label(received) : "");

        registerButton.interactable = !isFull;
    }

    #endregion

    #region 보조

    // 내 매물에서 이 매물을 찾는다. 없으면 null.
    private AuctionListingInfo? FindListing(long listingId)
    {
        foreach (AuctionListingInfo listing in _auction.MyListings)
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

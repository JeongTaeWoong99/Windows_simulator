using System.Collections.Generic;
using GameData;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 내 매물 화면 — 경매장 탭의 '내 매물' 하위 탭. 올린 것(판매 중 · 구매 진행 중)을 보고 취소한다.
//
// ■ 탭을 열 때마다 새로 받는다 — 이 조회는 빈도 제한에 들지 않고, 팔린 것은 푸시가 오지 않아서
//   (대금이 우편으로 올 뿐이다) 열 때 받지 않으면 이미 팔린 매물이 남아 보인다.
// ■ 구매 진행 중(Reserved)인 매물은 [취소]를 잠근다 — 서버도 거절한다.
// ■ 취소하면 물건은 **우편**으로 돌아오고 등록비는 돌려받지 못한다. 확인 창에서 먼저 알린다.
public class AuctionMyListingPresenter : MonoBehaviour
{
    [CenterHeader("머리")]
    [SerializeField, Tooltip("'판매 중 n / 상한' 문구")]
    private TMP_Text countText = null!;

    [SerializeField, Tooltip("새로고침 버튼. OnClick은 코드가 연결한다")]
    private Button refreshButton = null!;


    [CenterHeader("목록")]
    [SerializeField, Tooltip("매물 한 줄 프리팹")]
    private AuctionRowView rowPrefab = null!;

    [SerializeField, Tooltip("줄이 쌓이는 Content (VLG + ContentSizeFitter)")]
    private Transform rowParent = null!;

    [SerializeField, Tooltip("올린 매물이 없을 때만 보인다")]
    private GameObject emptyText = null!;

    private readonly List<AuctionRowContent> _contents = new List<AuctionRowContent>();

    private AuctionModel      _auction = null!;
    private PlayerDataModel   _data    = null!;
    private ServerWaitManager _wait    = null!;
    private UIManager         _ui      = null!;
    private AuctionRowList    _rows    = null!;

    // 진행 중인 대기의 손잡이 — 응답이 오면 결과를 보고한다.
    private ServerWaitHandle? _waitHandle;

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 배선 → 초기화 순서로 진행한다 (클라 공통 규약)
    private void Start()
    {
        this.RequireRef(countText,     nameof(countText));
        this.RequireRef(refreshButton, nameof(refreshButton));
        this.RequireRef(rowPrefab,     nameof(rowPrefab));
        this.RequireRef(rowParent,     nameof(rowParent));
        this.RequireRef(emptyText,     nameof(emptyText));

        _auction = Services.Get<AuctionModel>();
        _data    = Services.Get<PlayerDataModel>();
        _wait    = Services.Get<ServerWaitManager>();
        _ui      = Services.Get<UIManager>();
        _rows    = new AuctionRowList(rowPrefab, rowParent, OnRowCancelClicked);

        Subscribe();

        refreshButton.onClick.AddListener(RequestListings);

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

    // 내 매물·취소 응답 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed = true;

        _auction.MyListingsCompleted += OnMyListingsCompleted;
        _auction.CancelCompleted     += OnCancelCompleted;
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
    }

    #endregion

    #region 요청

    // 내 매물을 새로 받는다 (Start · OnEnable · refreshButton OnClick).
    private void RequestListings()
    {
        if (_waitHandle != null || !_data.IsLoggedIn)
        {
            return;
        }

        _auction.RequestMyListings();
        BeginWait("내 매물");
    }

    // 줄의 [취소] — 확인을 받고 보낸다 (AuctionRowView.ActionClicked)
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

    // 머리 문구와 목록을 지금 상태로 그린다.
    private void Refresh()
    {
        _contents.Clear();

        foreach (AuctionListingInfo listing in _auction.MyListings)
        {
            bool canCancel = _waitHandle == null && listing.State != EAuctionListingState.Reserved;

            // 전부 내 것이라 '[내 매물]' 표시·판매자 줄은 뺀다 — 이 탭에서는 소음이다.
            _contents.Add(AuctionText.ToRow(listing, "취소", canCancel, isMine: true, markMine: false));
        }

        _rows.Show(_contents);
        emptyText.SetActive(_contents.Count == 0);

        countText.text             = $"판매 중 {_contents.Count:N0} / {Constants.AuctionMaxActiveListings:N0}건";
        refreshButton.interactable = _waitHandle == null;
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

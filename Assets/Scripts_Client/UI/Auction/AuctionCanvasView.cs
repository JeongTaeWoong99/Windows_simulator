using MikaProtocol;
using UnityEngine;

// 경매장 — 3열 폭 전부 × 가운데 띠를 쓰는 화면 (T-147). 하단 메뉴 [경매장]으로 열고, 안쪽 [X]·Esc로 닫는다.
// 옛날엔 거래 열(지금 거점 열 'HubCanvasView')의 탭 하나였다.
//
// ■ 왜 '!Overlay Canvas' 안인가 — 화면 전체를 막아야 해서다('Overlay 규칙.md' ① 차단 범위).
//   열 캔버스는 Sorting Order 0인 형제라 그 안의 차단막은 다른 열에 닿지 않는다.
//   오버레이의 첫째 자식이다 — 경매장이 띄우는 확인·수량 팝업·툴팁·로딩·알림이 모두 그 위에 떠야 한다.
//
// ■ 왜 'CanvasGroup'이 아니라 'SetActive'인가 — 다시 켜 줄 손(UIManager)이 밖에 있다(② 생명주기).
//   안의 Presenter들이 열 때마다 'OnEnable'로 다시 받고, 닫을 때 구독을 끊는 동작을 그대로 쓴다(거래 열 시절과 같다).
//
// ■ 가운데 띠에 맞춘다 ('FitBodyToBand')
//   판('body')은 3열의 가운데 칸과 같은 높이·위치다. 위·아래 칸 높이는 위젯 위치에 따라 바뀌므로('WidgetPositionLayout')
//   열 때마다 맞춘다. 판 밖(위 상태 칸 · 아래 위젯 칸)은 루트의 차단막이 덮는다.
//
// ■ 탭 사이를 건너가는 길 — 화면끼리 서로를 쥐지 않고 여기를 거친다
//   검색 줄의 [시세] → 시세 탭 · 시세 탭의 [이 아이템 매물 보기] → 검색 탭 · [팔러 가기]·Ctrl+우클릭 → 판매 탭 + 등록 창.
//   받는 화면이 아직 한 번도 안 켜졌을 수 있다 — 각자 Start 전에 받은 요청을 지키므로 "요청 → 탭 열기" 순서로 부른다.
public class AuctionCanvasView : MonoBehaviour
{
    [CenterHeader("가운데 띠")]
    [SerializeField, Tooltip("경매장 판 — 3열의 가운데 칸과 같은 자리에 맞춘다. 앵커는 화면 전체로 늘려 둔다")]
    private RectTransform body = null!;

    [SerializeField, Tooltip("3열 배치 — 가운데 칸의 위·아래 여백을 여기서 읽는다 (!Horizontal Columns)")]
    private WidgetPositionLayout columnLayout = null!;

    [CenterHeader("탭 사이 길")]
    [SerializeField, Tooltip("탭 줄")]
    private AuctionTabPresenter auctionTabs = null!;

    [SerializeField, Tooltip("검색 탭 — 한 아이템의 매물을 바로 찾는다")]
    private AuctionSearchPresenter search = null!;

    [SerializeField, Tooltip("시세 탭 — 한 아이템의 시세를 연다")]
    private AuctionPricePresenter price = null!;

    [SerializeField, Tooltip("판매 등록 창 — 물건을 미리 골라 연다")]
    private AuctionRegisterPresenter register = null!;

    // 지금 열려 있는가
    public bool IsOpen => gameObject.activeSelf;

    // 경매장을 열고 닫는다 (UIManager가 호출).
    public void Show(bool on)
    {
        if (on)
        {
            FitBodyToBand();
        }

        gameObject.SetActive(on);
    }

    // 판매 탭 + 등록 창을 열고 이 물건을 골라 둔다 (인벤토리 격자 Ctrl+우클릭).
    //   kind : 자원·장비·캐릭터
    //   key  : 자원 TID · 장비·캐릭터 개체 번호
    public void OpenRegister(EAuctionKind kind, long key)
    {
        this.RequireRef(auctionTabs, nameof(auctionTabs));
        this.RequireRef(register,    nameof(register));

        Show(true);
        auctionTabs.ShowTab(AuctionTab.Sell);
        register.Open(kind, key, byTid: false);
    }

    // 판매 탭 + 등록 창을 열고 이 종류의 첫 물건을 골라 둔다 (시세 탭 [팔러 가기]).
    public void OpenRegisterFor(EAuctionKind kind, int tid)
    {
        this.RequireRef(auctionTabs, nameof(auctionTabs));
        this.RequireRef(register,    nameof(register));

        auctionTabs.ShowTab(AuctionTab.Sell);
        register.Open(kind, tid, byTid: true);
    }

    // 시세 탭을 열고 이 아이템의 시세를 받는다 (검색 줄 [시세] · 구매 확인 · 등록 창 [시세 보기]).
    public void OpenPrice(EAuctionKind kind, int tid)
    {
        this.RequireRef(auctionTabs, nameof(auctionTabs));
        this.RequireRef(price,       nameof(price));

        price.Open(kind, tid);
        auctionTabs.ShowTab(AuctionTab.Price);
    }

    // 검색 탭을 열고 이 아이템의 매물만 찾는다 (시세 탭 [이 아이템 매물 보기]).
    public void OpenSearchFor(EAuctionKind kind, int tid)
    {
        this.RequireRef(auctionTabs, nameof(auctionTabs));
        this.RequireRef(search,      nameof(search));

        search.SearchFor(kind, tid);
        auctionTabs.ShowTab(AuctionTab.Search);
    }

    // 판을 3열의 가운데 칸 자리에 맞춘다 (Show에서 호출).
    // ※ 판은 화면 전체로 늘린 앵커라 offset이 곧 위·아래 여백이다. 3열과 이 층은 같은 캔버스 좌표(1920 × 1080)를 쓴다.
    private void FitBodyToBand()
    {
        this.RequireRef(body,         nameof(body));
        this.RequireRef(columnLayout, nameof(columnLayout));

        if (!columnLayout.TryGetCenterBandInsets(out float top, out float bottom))
        {
            return; // 배치를 못 읽었으면 저장된 자리 그대로 — 원인은 'WidgetPositionLayout'이 이미 알린다
        }

        body.offsetMin = new Vector2(body.offsetMin.x, bottom);
        body.offsetMax = new Vector2(body.offsetMax.x, -top);
    }
}

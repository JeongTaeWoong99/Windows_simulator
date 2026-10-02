using MikaProtocol;
using UnityEngine;

// 거래 열 — 뽑기 · 경매장 · 창고 3탭이 들어간다('MarketTabPresenter'). 탭마다 화면이 따로다.
// 창고는 아직 '(기능 없음)' 자리만 있다.
//
// ⚠️ 여기 '창고'는 인벤토리가 아니다 — 인벤토리(옛 이름 창고/Storage)는 얻은 것이 바로 들어오는 왼쪽 열이고('InventoryCanvasView'),
//   창고는 두고두고 쓸 것을 맡겨 두는 곳이다.
//   거래·상점 수치는 기획이 정한다 → GameDesign/design/trade/README.md
//
// ■ 밖에서 경매 등록으로 바로 오기 ('OpenAuctionRegister')
//   인벤토리 칸 Shift+우클릭이 부른다. 열 → 경매장 탭 → 등록 하위 탭 → 그 물건을 미리 골라 둔다.
//   세 Presenter가 아직 한 번도 안 켜졌을 수 있다 — 각자 Start 전에 받은 요청을 지키므로 순서대로 부르기만 한다.
public class MarketCanvasView : MonoBehaviour
{
    [CenterHeader("경매 등록 바로가기")]
    [SerializeField, Tooltip("거래 열의 탭 줄 — 경매장 탭을 연다")]
    private MarketTabPresenter marketTabs = null!;

    [SerializeField, Tooltip("경매장의 하위 탭 줄 — 등록 탭을 연다")]
    private AuctionTabPresenter auctionTabs = null!;

    [SerializeField, Tooltip("경매 등록 화면 — 물건을 미리 골라 둔다")]
    private AuctionRegisterPresenter auctionRegister = null!;

    // 이 열을 열고 닫는다 (UIManager가 호출).
    public void Show(bool on)
    {
        gameObject.SetActive(on);
    }

    // 경매 등록 화면을 열고 이 물건을 골라 둔다 (인벤토리 격자 Shift+우클릭).
    //   kind : 자원·장비·캐릭터
    //   key  : 자원 TID · 장비·캐릭터 개체 번호
    public void OpenAuctionRegister(EAuctionKind kind, long key)
    {
        this.RequireRef(marketTabs,      nameof(marketTabs));
        this.RequireRef(auctionTabs,     nameof(auctionTabs));
        this.RequireRef(auctionRegister, nameof(auctionRegister));

        Show(true);
        marketTabs.ShowTab(MarketTab.Auction);
        auctionTabs.ShowTab(AuctionTab.Register);
        auctionRegister.Preselect(kind, key);
    }
}

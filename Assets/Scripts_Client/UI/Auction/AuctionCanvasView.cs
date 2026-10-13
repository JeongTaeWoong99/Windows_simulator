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
// ■ 밖에서 경매 등록으로 바로 오기 ('OpenRegister')
//   인벤토리 칸 Shift+우클릭이 부른다. 경매장 → 등록 하위 탭 → 그 물건을 미리 골라 둔다.
//   두 Presenter가 아직 한 번도 안 켜졌을 수 있다 — 각자 Start 전에 받은 요청을 지키므로 순서대로 부르기만 한다.
public class AuctionCanvasView : MonoBehaviour
{
    [CenterHeader("가운데 띠")]
    [SerializeField, Tooltip("경매장 판 — 3열의 가운데 칸과 같은 자리에 맞춘다. 앵커는 화면 전체로 늘려 둔다")]
    private RectTransform body = null!;

    [SerializeField, Tooltip("3열 배치 — 가운데 칸의 위·아래 여백을 여기서 읽는다 (!Horizontal Columns)")]
    private WidgetPositionLayout columnLayout = null!;


    [CenterHeader("경매 등록 바로가기")]
    [SerializeField, Tooltip("경매장의 하위 탭 줄 — 등록 탭을 연다")]
    private AuctionTabPresenter auctionTabs = null!;

    [SerializeField, Tooltip("경매 등록 화면 — 물건을 미리 골라 둔다")]
    private AuctionRegisterPresenter auctionRegister = null!;

    // 지금 열려 있는가
    public bool IsOpen => gameObject.activeSelf;

    // 경매장 위에 막는 오버레이가 떠 있는가 — Esc로 닫아도 되는지 본다('AuctionHeaderPresenter').
    //
    // ※ 오버레이 층은 뒤 형제일수록 앞에 그린다. 내 뒤 형제 중 클릭을 막고 있는 것(확인·수량 팝업 · 로딩 · 알림)이
    //   하나라도 있으면 덮인 것이다. 툴팁은 막지 않으니 걸리지 않는다.
    //   ⚠️ 로딩도 여기 걸린다 — 요청 중에 닫으면 안의 Presenter가 구독을 끊어 응답을 놓친다('Hub 규칙.md'의 사고).
    public bool IsCovered
    {
        get
        {
            Transform layer = transform.parent;

            for (int i = transform.GetSiblingIndex() + 1; i < layer.childCount; i++)
            {
                Transform sibling = layer.GetChild(i);

                if (!sibling.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var group = sibling.GetComponent<CanvasGroup>();

                if (group != null && group.blocksRaycasts)
                {
                    return true;
                }
            }

            return false;
        }
    }

    // 경매장을 열고 닫는다 (UIManager가 호출).
    public void Show(bool on)
    {
        if (on)
        {
            FitBodyToBand();
        }

        gameObject.SetActive(on);
    }

    // 경매 등록 화면을 열고 이 물건을 골라 둔다 (인벤토리 격자 Shift+우클릭).
    //   kind : 자원·장비·캐릭터
    //   key  : 자원 TID · 장비·캐릭터 개체 번호
    public void OpenRegister(EAuctionKind kind, long key)
    {
        this.RequireRef(auctionTabs,     nameof(auctionTabs));
        this.RequireRef(auctionRegister, nameof(auctionRegister));

        Show(true);
        auctionTabs.ShowTab(AuctionTab.Register);
        auctionRegister.Preselect(kind, key);
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

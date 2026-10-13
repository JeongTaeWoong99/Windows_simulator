using UnityEngine;
using UnityEngine.UI;

// 경매장 머리 줄 — 제목과 [X]. [X]와 Esc가 경매장을 닫는다 (T-147).
//
// ■ 왜 닫는 길이 따로 있나
//   경매장이 가운데 띠를 다 덮어 하단 메뉴의 [경매장] 버튼이 가려진다 — 연 버튼으로 다시 닫을 수 없다.
//
// ■ Esc는 경매장 위에 아무것도 막고 있지 않을 때만 닫는다 ('AuctionCanvasView.IsCovered')
//   확인·수량 팝업이 떠 있을 때 Esc가 뒤의 경매장을 닫으면 팝업만 홀로 남는다.
//   로딩 중에 닫으면 안의 Presenter가 구독을 끊어 응답을 놓친다 — 클릭은 로딩 차단막이 막지만 키는 못 막는다.
public class AuctionHeaderPresenter : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("닫기 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button closeButton = null!;

    [SerializeField, Tooltip("경매장 캔버스 — 위에 막는 오버레이가 떠 있는지 묻는다")]
    private AuctionCanvasView canvasView = null!;

    private UIManager       _ui   = null!;
    private KeyInputManager _keys = null!;

    // 참조 확보 → 배선 (클라 공통 규약. 구독할 이벤트가 없다)
    private void Start()
    {
        this.RequireRef(closeButton, nameof(closeButton));
        this.RequireRef(canvasView,  nameof(canvasView));

        _ui   = Services.Get<UIManager>();
        _keys = Services.Get<KeyInputManager>();

        closeButton.onClick.AddListener(Close);
    }

    // Esc를 본다 (Unity 메시지). 경매장이 열려 있을 때만 돈다 — 닫히면 이 오브젝트도 꺼진다.
    private void Update()
    {
        if (!_keys.WasClosePressed || canvasView.IsCovered)
        {
            return;
        }

        Close();
    }

    // 경매장을 닫는다 (closeButton OnClick · Esc)
    private void Close()
    {
        _ui.ShowAuction(false);
    }
}

using UnityEngine;

// 거점 화면 — 3열 폭 전부 × 가운데 띠를 쓰는 화면 (T-182 · T-183). 하단 메뉴 [거점]으로 열고, 안쪽 [X]로 닫는다.
// 경매장('AuctionCanvasView') · 아티팩트('ArtifactCanvasView')와 같은 틀이다 — 지도 위에 건물 5채가 서고, 건물을 누르면 그 창이 뜬다.
//
// ■ 왜 '!Overlay Canvas' 안인가 — 경매장과 같다(화면 전체를 막는다 · 'Overlay 규칙.md' ① 차단 범위).
//   셋은 함께 열리지 않는다('UIManager.ShowOutpost' · 'ShowAuction' · 'ShowArtifact'가 서로 닫는다).
//
// ■ 왜 'SetActive'인가 — 경매장과 같다. 안의 Presenter들이 열 때마다 'OnEnable'로 다시 그린다.
public class OutpostCanvasView : MonoBehaviour
{
    [CenterHeader("가운데 띠")]
    [SerializeField, Tooltip("거점 판 — 3열의 가운데 칸과 같은 자리에 맞춘다. 앵커는 화면 전체로 늘려 둔다")]
    private RectTransform body = null!;

    [SerializeField, Tooltip("3열 배치 — 가운데 칸의 위·아래 여백을 여기서 읽는다 (!Horizontal Columns)")]
    private WidgetPositionLayout columnLayout = null!;

    // 지금 열려 있는가 — 닫히는 중이면 false
    public bool IsOpen => PanelTransition.IsShown(gameObject);

    // 화면을 열고 닫는다 (UIManager가 호출). 닫힘은 연출이 끝난 뒤에 꺼진다('PanelTransition').
    public void Show(bool on)
    {
        if (on)
        {
            FitBodyToBand();
        }

        PanelTransition.SetActive(gameObject, on, body);
    }

    // 판을 3열의 가운데 칸 자리에 맞춘다 (Show에서 호출) — 'AuctionCanvasView.FitBodyToBand'와 같다.
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

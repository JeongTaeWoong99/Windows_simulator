using UnityEngine;
using UnityEngine.EventSystems;

// 인벤토리 칸을 끌어 다른 칸에 놓는 입력 (T-044).
//
// ■ 프리팹이 아니라 격자가 칸을 만들 때 코드로 붙인다
//   가챠 결과 팝업이 같은 칸 프리팹을 쓰는데, 거기서는 끌리면 안 된다('TooltipTrigger'와 같은 이유).
//   인벤토리 칸을 만드는 곳이 격자 하나뿐이라 붙이는 곳도 하나다.
// ■ 판단하지 않는다 — 입력을 격자에 넘길 뿐이다
//   끌어도 되는지(찾기 중 · 응답 대기 중), 놓은 자리가 어느 칸인지, 무엇을 보낼지는 격자가 정한다.
// ※ 칸은 풀로 돌지만 **프레임은 옮겨 다니지 않는다** — 그래서 프레임 번호를 만들 때 한 번만 받는다.
// ※ 끌기가 시작되면 EventSystem이 클릭을 취소한다 — 좌클릭(상자 개봉 · 큐브 창)과 부딪히지 않는다.
public class InventorySlotDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private InventoryGridPresenter? _grid;
    private int                     _frameIndex;

    // 이번 끌기를 격자가 받아들였나. 거절한 끌기는 이후 이동·놓기도 넘기지 않는다.
    private bool _isDragging;

    // 격자와 프레임 번호를 받는다 (격자가 칸을 만들 때 한 번 호출).
    public void Bind(InventoryGridPresenter grid, int frameIndex)
    {
        _grid       = grid;
        _frameIndex = frameIndex;
    }

    // 끌기 시작 — 격자가 받아들이면 그때부터 따라간다 (EventSystem 콜백)
    public void OnBeginDrag(PointerEventData eventData)
    {
        _isDragging = _grid != null && _grid.BeginSlotDrag(_frameIndex, eventData);
    }

    // 끄는 중 — 끌리는 칸 그림이 포인터를 따라간다 (EventSystem 콜백)
    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDragging)
        {
            return;
        }

        _grid!.MoveSlotDrag(eventData);
    }

    // 놓았다 — 놓은 자리를 격자가 판정해 이동을 요청한다 (EventSystem 콜백)
    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        _grid!.EndSlotDrag(_frameIndex, eventData);
    }

    // 끌던 중에 꺼졌다(탭 전환·창 닫기) — 놓기가 오지 않으므로 끌기를 잊는다 (Unity 메시지)
    // ※ 끌리는 칸 그림은 격자가 자기 'OnDisable'·'ShowTab'에서 거둔다.
    private void OnDisable()
    {
        _isDragging = false;
    }
}

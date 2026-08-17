using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 이 오브젝트를 타이틀바처럼 잡아 끌면 창을 이동한다 (메인 뷰 캔버스에 붙인다).
/// 실제 이동은 'WindowManager.BeginWindowDrag'가 OS 이동 루프에 위임한다.
///
/// ■ 왜 'IBeginDragHandler'인가 — 클릭/드래그 구분이 공짜다
///   드래그는 EventSystem의 이동 임계값을 넘겨야 발화한다. 그래서 단순 클릭은 아래 버튼으로
///   그대로 가고(오발 없음), 임계값을 넘겨 끌기 시작할 때만 창 이동이 걸린다.
///   별도 타이머·임계값 코드가 필요 없다.
///
/// ⚠️ 'IDragHandler'도 반드시 함께 구현한다 — 이게 없으면 드래그가 아예 안 걸린다.
///   EventSystem은 마우스를 누를 때 'GetEventHandler&lt;IDragHandler&gt;'로 드래그 대상('pointerDrag')을
///   정하고, 그 대상에게만 'OnBeginDrag'를 보낸다. 즉 'IBeginDragHandler'만 있으면 이 오브젝트가
///   애초에 드래그 대상으로 선택되지 않아 'OnBeginDrag'가 호출되지 않는다. 'OnDrag' 본문은 비어도
///   된다 — 실제 이동은 OS 모달 루프가 하므로 프레임마다 할 일이 없다.
///
/// ⚠️ 이벤트를 받으려면 이 캔버스에 'GraphicRaycaster'와, 자식에 raycast target Graphic이 있어야
///   한다. 자식이 맞은 뒤 이벤트가 이 루트로 버블링되어 여기서 발화한다. 빈(투명) 영역은 클릭
///   스루로 통과하므로 드래그도 걸리지 않는다.
/// </summary>
public class WindowDragArea : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    private WindowManager _window = null!;

    // 서비스 조회는 Start에서 (Awake·OnEnable은 등록 순서가 보장되지 않는다 — 폴더 구조.md 3-2)
    private void Start()
    {
        _window = Services.Get<WindowManager>();
    }

    // 드래그 시작 — 창 이동을 OS에 위임한다 (EventSystem 드래그 콜백)
    public void OnBeginDrag(PointerEventData eventData)
    {
        _window.BeginWindowDrag();
    }

    // 이 오브젝트가 드래그 대상('pointerDrag')으로 선택되게 하려고 IDragHandler를 구현한다.
    // 실제 창 이동은 OnBeginDrag가 넘긴 OS 모달 루프가 처리하므로 여기선 할 일이 없다.
    public void OnDrag(PointerEventData eventData) { }
}

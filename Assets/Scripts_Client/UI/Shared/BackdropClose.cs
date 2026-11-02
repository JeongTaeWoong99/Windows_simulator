using System;
using UnityEngine;
using UnityEngine.EventSystems;

// 차단막을 누르면 닫는다 — 창 뒤 어두운 바탕을 누르는 것이 [X]·[취소]와 같은 뜻이다.
// 차단막 오브젝트에 코드로 붙인다('Bind') — 창마다 씬에서 붙이지 않는다. 눌렀을 때 할 일은 부르는 쪽이 넘긴다.
//
// ■ 왜 'Button'이 아닌가
//   차단막의 자식(창 판)을 눌러도 클릭이 부모로 올라온다 — 판의 빈 곳을 눌렀는데 닫히면 안 된다.
//   그래서 누른 곳과 뗀 곳이 **둘 다 차단막 자신**일 때만 받는다('IsDirectClick').
//   입력창에서 글자를 긁다가 창 밖에서 떼도 닫히지 않는 것도 이 덕분이다.
//
// ■ 왼쪽 버튼만 받는다 — 게임 안에서 오른쪽 클릭은 뜻이 따로 있다.
public class BackdropClose : MonoBehaviour, IPointerClickHandler
{
    private Action? _onClick;

    // 이 차단막을 누르면 'onClick'을 부른다 (창의 Start·Awake에서 한 번).
    public static void Bind(GameObject backdrop, Action onClick)
    {
        BackdropClose close = backdrop.AddComponent<BackdropClose>();

        close._onClick = onClick;
    }

    // 이 클릭이 자식이 아니라 차단막 자신을 눌렀다 뗀 왼쪽 클릭인가
    // — 자기 'OnPointerClick'을 이미 가진 창('GachaResultPresenter')은 이것만 빌려 쓴다.
    public static bool IsDirectClick(PointerEventData eventData, GameObject backdrop)
    {
        return eventData.button == PointerEventData.InputButton.Left
            && eventData.pointerPressRaycast.gameObject == backdrop
            && eventData.pointerCurrentRaycast.gameObject == backdrop;
    }

    // 차단막 클릭 (EventSystem 콜백)
    public void OnPointerClick(PointerEventData eventData)
    {
        if (IsDirectClick(eventData, gameObject))
        {
            _onClick?.Invoke();
        }
    }
}

using UnityEngine;

// 상태 캔버스 — '@Main Column'의 위·아래 슬롯 중 위젯의 반대편에 들어간다
// ('WidgetPositionLayout'). 위치가 바뀌므로 "상단바"가 아니라 담는 것(상태)으로 이름을 붙였다.
// 이름·골드 표시와 화면 버튼은 자식의 'StatePresenter'가 맡는다.
public class StateCanvasView : MonoBehaviour
{
    // 이 캔버스를 열고 닫는다 (UIManager가 호출). 위젯과 함께 여닫힌다.
    // 닫힘은 연출이 끝난 뒤에 꺼진다('PanelTransition') — 열림 여부는 'IsShown'으로 읽는다.
    public void Show(bool on)
    {
        PanelTransition.SetActive(gameObject, on);
    }

    // 열려 있는가 — 닫히는 중이면 false
    public bool IsShown => PanelTransition.IsShown(gameObject);
}

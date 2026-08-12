using UnityEngine;

/// <summary>
/// 상태 캔버스 — '@Main Column'의 위·아래 슬롯 중 위젯의 반대편에 들어간다
/// ('WidgetPositionLayout'). 위치가 바뀌므로 "상단바"가 아니라 담는 것(상태)으로 이름을 붙였다.
/// 이름·골드 표시와 화면 버튼은 자식의 'StatePresenter'가 맡는다.
/// </summary>
public class StateCanvasView : MonoBehaviour
{
    /// <summary>이 캔버스를 열고 닫는다 (UIManager가 호출). 위젯과 함께 여닫힌다.</summary>
    public void Show(bool on)
    {
        gameObject.SetActive(on);
    }
}

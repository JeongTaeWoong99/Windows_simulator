using UnityEngine;

/// <summary>
/// 상태 캔버스 — <c>@Main Column</c>의 위·아래 슬롯 중 <b>위젯의 반대편</b>에 들어간다
/// (<c>WidgetPositionLayout</c>). 위치가 바뀌므로 "상단바"가 아니라 담는 것(상태)으로 이름을 붙였다.
///
/// <para>
/// ■ 손잡이일 뿐이다<br/>
/// 다른 캔버스(<c>StorageCanvasView</c>·<c>MarketCanvasView</c>)와 같이 여닫기만 한다.
/// 이름·골드 표시와 화면 버튼은 자식의 <see cref="StatePresenter"/>가 맡는다.
/// </para>
///
/// <para>
/// ⚠️ <b>예전에는 이 클래스가 내용까지 그렸다.</b> 캔버스가 담는 건 패널이고 내용은 패널이 그린다는
/// 규칙을 혼자 어기고 있어서, <c>State Panel</c>을 한 겹 넣고 갈랐다.
/// → <c>UI 스크립트 규칙.md</c> §3
/// </para>
/// </summary>
public class StateCanvasView : MonoBehaviour
{
    /// <summary>이 캔버스를 열고 닫는다 (UIManager가 호출). 위젯과 함께 여닫힌다.</summary>
    public void Show(bool on)
    {
        gameObject.SetActive(on);
    }
}

using UnityEngine;

/// <summary>
/// 상주 위젯 캔버스 — <b>바탕화면에 항상 떠 있는 유일한 것</b>이다. 3열 큰 창은 필요할 때만 연다.
///
/// <para>
/// ■ 그래서 여닫는 API가 없다<br/>
/// 다른 캔버스와 달리 <c>Show</c>를 두지 않았다. 이걸 끄면 게임이 화면에서 사라진다 —
/// 끄고 켜는 대상이 아니라 <b>기준점</b>이다. 위젯은 <c>@Main Column</c>의 위·아래 슬롯 중 한 칸에
/// 들어가고, 반대편 칸에 상태 캔버스가 들어간다(<c>WidgetPositionLayout</c>).
/// </para>
///
/// <para>
/// ■ 내용은 <see cref="WidgetPresenter"/>가 그린다<br/>
/// 예전엔 이 클래스가 열기/닫기 버튼을 직접 잡았다. 캔버스가 담는 건 패널이고 내용은 패널이
/// 그린다는 규칙을 어긴 것이라, <c>Widget Panel</c>을 한 겹 넣고 갈랐다.
/// → <c>UI 스크립트 규칙.md</c> §3
/// </para>
/// </summary>
public class WidgetCanvasView : MonoBehaviour
{
    // 여닫지 않는 유일한 캔버스라 Show(bool) 도 없다. 이 클래스는 하이어라키에서
    // "여기가 위젯 캔버스다"를 표시하고, 나중에 캔버스 단위 설정이 생기면 그때 채운다.
}

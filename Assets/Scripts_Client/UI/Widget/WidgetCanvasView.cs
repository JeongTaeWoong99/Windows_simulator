using UnityEngine;

/// <summary>
/// 상주 위젯 캔버스 — 바탕화면에 항상 떠 있는 유일한 것이다. 3열 큰 창은 필요할 때만 연다.
///
/// ⚠️ 이 캔버스만 'Show'가 없다 — 끄면 게임이 화면에서 사라진다. 여닫는 대상이 아니라 기준점이다.
/// 위젯은 '@Main Column'의 위·아래 슬롯 중 한 칸에 들어가고 반대편이 상태 캔버스다
/// ('WidgetPositionLayout'). 내용은 자식의 'WidgetPresenter'가 그린다.
/// </summary>
public class WidgetCanvasView : MonoBehaviour
{
    // 여닫지 않는 유일한 캔버스라 Show(bool) 도 없다. 이 클래스는 하이어라키에서
    // "여기가 위젯 캔버스다"를 표시하고, 나중에 캔버스 단위 설정이 생기면 그때 채운다.
}

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 위젯이 놓일 창 안 6칸(가로 3 × 세로 2). ScreenAnchor 와 같은 나열 순서를 써서
/// index % 3 = 가로(0=왼쪽,1=가운데,2=오른쪽), index / 3 = 세로(0=위,1=아래)로 계산할 수 있다.
///
/// ※ 창을 데스크톱 어디에 두는가(ScreenAnchor 9분할)와는 <b>다른 축</b>이다. 둘을 섞지 않는다.
/// </summary>
public enum WidgetPosition
{
    UpperLeft, UpperCenter, UpperRight,
    LowerLeft, LowerCenter, LowerRight,
}

/// <summary>
/// 위젯 위치(6칸)에 맞춰 3열 순서와 위젯/상태 패널의 위·아래 슬롯을 배치한다.
///
/// ■ "상단바"가 아니라 "상태 패널"인 이유
///   위젯이 위 칸으로 가면 이 패널은 <b>아래로 내려간다.</b> 위치를 이름에 담으면 절반은 거짓이 된다.
///   담는 것(계정 레벨·골드·시스템 아이콘 = 상태)으로 이름을 붙인다.
///
/// ■ 좌표를 계산하지 않는다
///   위치를 anchoredPosition 으로 옮기지 않고 <b>형제 순서(sibling index)만 바꾼다.</b>
///   실제 배치는 HorizontalLayoutGroup·VerticalLayoutGroup 이 계산하므로,
///   창 배율이 바뀌어도 좌표를 다시 잡을 필요가 없다.
///
/// ■ 3열 순서 규칙 — 작업슬롯과 창고는 항상 붙어 있다
///   거래를 작업슬롯에서 <b>가장 먼 끝</b>에 두면 창고가 자동으로 사이에 남는다.
///   창고는 작업슬롯에 끌어다 넣는 재료라 드래그 거리가 곧 조작 비용이고,
///   거래는 한 번 갔다 오면 되는 곳이라 멀어도 손해가 적다.
///   → GameDesign/기획/게임UI/README.md 2.1
///
/// ■ ExecuteAlways
///   재생하지 않고 인스펙터에서 6칸을 바꿔 보며 확인하려고 에디터에서도 돈다.
/// </summary>
[ExecuteAlways]
public class WidgetPositionLayout : MonoBehaviour
{
    // ※ 전부 인스펙터 필수 참조다. nullable 경고를 피하려 = null! 로 두고, 미연결은 Apply 에서 경고로 드러낸다.
    //   (WindowPanelUI 처럼 예외를 던지지 않는 이유 — 이 컴포넌트는 배선 도중인 에디터에서도 돌기 때문이다)
    [CenterHeader("< 열 참조 >")]
    [SerializeField] private RectTransform columns           = null!; // HorizontalLayoutGroup 을 가진 3열의 부모
    [SerializeField] private RectTransform storageColumn     = null!; // 창고 열
    [SerializeField] private RectTransform workstationColumn = null!; // 작업슬롯 열 — 위젯의 가로 칸을 따라간다
    [SerializeField] private RectTransform marketColumn      = null!; // 거래 열

    [CenterHeader("< 작업슬롯 열의 위·아래 슬롯 >")]
    [SerializeField] private RectTransform widgetPanel = null!; // 위젯 — 6칸의 세로가 이 패널의 슬롯을 정한다
    [SerializeField] private RectTransform statePanel  = null!; // 상태 패널(계정 레벨·골드·시스템 아이콘) — 항상 위젯의 반대편 슬롯

    [CenterHeader("< 위젯 위치 >")]
    [SerializeField] private WidgetPosition position = WidgetPosition.LowerCenter;

    public WidgetPosition Position => position;

    // 배선이 끝난 뒤 씬을 열거나 재생을 시작하면 현재 위치를 반영한다 (Unity 메시지)
    private void OnEnable()
    {
        Apply();
    }

#if UNITY_EDITOR
    // 인스펙터에서 위치를 바꾸면 즉시 반영한다 (Unity 메시지)
    private void OnValidate()
    {
        // OnValidate 안에서 계층을 바꾸면 Unity 가 경고를 낸다 — 다음 에디터 틱으로 미룬다.
        UnityEditor.EditorApplication.delayCall += ApplyIfAlive;
    }

    // delayCall 사이에 오브젝트가 사라졌을 수 있어 살아 있을 때만 적용한다 (OnValidate 의 지연 콜백)
    private void ApplyIfAlive()
    {
        if (this == null)
            return;

        Apply();
    }
#endif

    /// <summary>현재 <see cref="position"/>을 3열 순서와 위·아래 슬롯에 반영한다.</summary>
    public void Apply()
    {
        if (!HasAllReferences())
            return;

        ApplyColumnOrder();
        ApplyVerticalSlot();

        LayoutRebuilder.MarkLayoutForRebuild(columns);
    }

    // 가로 칸(왼쪽·가운데·오른쪽)에 맞춰 세 열의 순서를 정한다
    private void ApplyColumnOrder()
    {
        int workstationIndex = (int)position % 3;                  // 작업슬롯은 위젯의 가로 칸 그대로
        int marketIndex      = workstationIndex == 2 ? 0 : 2;      // 거래는 작업슬롯에서 가장 먼 끝
                                                                   // (가운데면 양끝이 같으므로 기본 배치 — 거래 오른쪽)
        RectTransform[] order = new RectTransform[3];
        order[workstationIndex] = workstationColumn;
        order[marketIndex]      = marketColumn;

        for (int i = 0; i < order.Length; i++)
        {
            if (order[i] == null)
                order[i] = storageColumn; // 남은 한 칸이 창고 — 언제나 작업슬롯 옆이 된다
        }

        for (int i = 0; i < order.Length; i++)
        {
            order[i].SetSiblingIndex(i);
        }
    }

    // 세로 칸(위·아래)에 맞춰 위젯과 상태 패널을 서로 반대편 슬롯에 넣는다
    private void ApplyVerticalSlot()
    {
        bool isUpper   = (int)position / 3 == 0;
        int  lastIndex = workstationColumn.childCount - 1;

        widgetPanel.SetSiblingIndex(isUpper ? 0 : lastIndex);
        statePanel.SetSiblingIndex(isUpper ? lastIndex : 0);
    }

    // 필수 참조가 전부 연결됐는지 확인한다 (Apply 진입 가드)
    private bool HasAllReferences()
    {
        RectTransform[] references = { columns, storageColumn, workstationColumn, marketColumn, widgetPanel, statePanel };

        int linked = 0;
        foreach (var reference in references)
        {
            if (reference != null)
                linked++;
        }

        // 일부만 연결된 상태만 경고한다 — 전부 비어 있으면 컴포넌트를 막 붙여 배선 전인 정상 상황이다.
        if (linked > 0 && linked < references.Length)
            ClientLog.Warn(ClientLog.UI, "인스펙터 참조가 일부만 연결돼 배치를 건너뛴다.", this);

        return linked == references.Length;
    }
}

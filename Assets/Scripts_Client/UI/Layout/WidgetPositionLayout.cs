using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 위젯이 놓일 창 안 6칸(가로 3 × 세로 2). ScreenAnchor 와 같은 나열 순서를 써서
/// index % 3 = 가로(0=왼쪽,1=가운데,2=오른쪽), index / 3 = 세로(0=위,1=아래)로 계산할 수 있다.
///
/// ※ 창을 데스크톱 어디에 두는가(ScreenAnchor 9분할)와는 다른 축이다. 둘을 섞지 않는다.
/// </summary>
public enum WidgetPosition
{
    UpperLeft, UpperCenter, UpperRight,
    LowerLeft, LowerCenter, LowerRight,
}

/// <summary>
/// 위젯 위치(6칸)에 맞춰 3열 순서와 위젯/상태 패널의 위·아래 슬롯을 배치한다.
///
/// ■ 좌표를 계산하지 않는다
///   위치를 anchoredPosition 으로 옮기지 않고 형제 순서(sibling index)와 자식 정렬만 바꾼다.
///   실제 배치는 HorizontalLayoutGroup·VerticalLayoutGroup 이 계산하므로,
///   창 배율이 바뀌어도 좌표를 다시 잡을 필요가 없다.
///
/// ■ 가로 정렬은 건드리지 않는다
///   여닫을 때 Column 이 아니라 그 안의 Canvas 만 끄기 때문이다. 열 3개와 (Layout) 스페이서가
///   항상 남아 있어 3열 묶음의 폭이 변하지 않고, 위젯이 6칸 자리에서 움직이지 않는다.
///   Column 을 끄면 남은 열들이 가운데로 다시 몰려 위젯 가로 칸이 무의미해진다 — 그래서 안 끈다.
///
/// ■ 3열 순서 규칙 — 작업슬롯과 창고는 항상 붙어 있다
///   거래를 작업슬롯에서 가장 먼 끝에 두면 창고가 자동으로 사이에 남는다.
///   → GameDesign/design/ui/README.md 2.1
///
/// ■ ExecuteAlways
///   재생하지 않고 인스펙터에서 6칸을 바꿔 보며 확인하려고 에디터에서도 돈다.
///
/// ⚠️ 항상 켜져 있는 오브젝트에 붙인다 ('!Horizental Columns').
///   배치를 'OnEnable'에서 적용하므로, 여닫히는 캔버스에 붙이면 그게 꺼져 있는 동안
///   3열 순서와 위젯 위치가 아예 반영되지 않는다. 기본 상태로 꺼져 있는 캔버스라면 한 번도 안 돈다.
///   (실제로 '#Setting Canvas'에 붙어 있다가 그 캔버스가 토글 대상이 되면서 드러난 문제다)
/// </summary>
[ExecuteAlways]
public class WidgetPositionLayout : MonoBehaviour
{
    // ※ 전부 인스펙터 필수 참조다. nullable 경고를 피하려 = null! 로 두고, 미연결은 Apply 에서 경고로 드러낸다.
    //   (SettingPresenter 처럼 예외를 던지지 않는 이유 — 이 컴포넌트는 배선 도중인 에디터에서도 돌기 때문이다)
    [CenterHeader("열 참조")]
    [SerializeField] private RectTransform columns           = null!; // HorizontalLayoutGroup 을 가진 3열의 부모
    [SerializeField] private RectTransform storageColumn     = null!; // 창고 열
    [SerializeField] private RectTransform workstationColumn = null!; // 작업슬롯 열 — 위젯의 가로 칸을 따라간다
    [SerializeField] private RectTransform marketColumn      = null!; // 거래 열

    [CenterHeader("작업슬롯 열의 위·아래 슬롯")]
    [SerializeField] private RectTransform widgetPanel = null!; // 위젯 — 6칸의 세로가 이 패널의 슬롯을 정한다
    [SerializeField] private RectTransform statePanel  = null!; // 상태 패널(계정 레벨·골드·시스템 아이콘) — 항상 위젯의 반대편 슬롯

    [CenterHeader("위젯 위치")]
    [SerializeField] private WidgetPosition position = WidgetPosition.LowerCenter;

    // ※ 정렬을 바꿀 대상은 따로 배선하지 않는다 — 위 열 참조 3개에서 VerticalLayoutGroup을 직접 꺼낸다.
    //   예전엔 배열로 따로 받았는데, 인스펙터를 비워 두면 아무 일도 안 일어나면서 원인이 안 보였다.
    //   같은 오브젝트를 두 번 배선할 이유가 없다.
    [CenterHeader("위·아래에 따라 바꿀 열의 자식 정렬")]
    [SerializeField, Tooltip("위젯이 '위' 칸일 때 열의 자식 정렬 — 내용을 위로 붙여 위젯이 창 위 가장자리에 온다")]
    private TextAnchor upperAlignment = TextAnchor.UpperCenter;

    [SerializeField, Tooltip("위젯이 '아래' 칸일 때 열의 자식 정렬 — 내용을 아래로 붙인다")]
    private TextAnchor lowerAlignment = TextAnchor.LowerCenter;

    public WidgetPosition Position => position;

    /// <summary>
    /// 위젯 위치를 바꾸고 즉시 반영한다 (설정 드롭다운이 호출). 재생 중이면 저장까지 한다.
    /// </summary>
    public void SetPosition(WidgetPosition value)
    {
        position = value;
        Apply();

        // ⚠️ 저장은 재생 중에만 — 이유는 LoadSavedPosition 주석 참조.
        if (Application.isPlaying)
            WindowSettings.SaveInt(WindowSettings.WidgetPositionKey, (int)value);
    }

    // 배선이 끝난 뒤 씬을 열거나 재생을 시작하면 현재 위치를 반영한다 (Unity 메시지)
    private void OnEnable()
    {
        LoadSavedPosition();
        Apply();
    }

    /// <summary>
    /// 캔버스(=창의 렌더 영역) 크기가 바뀌면 배치를 다시 태운다 (Unity 메시지).
    ///
    /// 이 오브젝트는 캔버스에 stretch 로 붙어 있어 창이 리사이즈되면 이 콜백이 온다.
    /// 창 크기 프리셋 변경 · 타이틀바 토글 · 배율이 다른 모니터로 드래그가 전부 여기로 모인다.
    ///
    /// ■ 왜 필요한가
    ///   렌더 영역이 잠깐이라도 16:9가 아니면 그 폭으로 계산된 열 폭이 남을 수 있다.
    ///   'WindowManager' 쪽에서 그 과도 상태를 없앴지만, 여기서 한 번 더 다시 계산해
    ///   <b>어떤 모니터·배율·창 크기에서도 열 폭이 스스로 복구되게</b> 한다.
    ///
    /// ■ 'WindowManager'를 참조하지 않는다
    ///   Unity 콜백만으로 자립하므로 Managers ↔ UI 역방향 의존이 생기지 않는다.
    ///
    /// ⚠️ <b>여기서 'Apply()'를 직접 부르면 안 된다.</b> 이 콜백은 UGUI 가 레이아웃 패스를
    ///   도는 도중(HorizontalLayoutGroup 이 자식 크기를 바꾸는 순간)에도 날아온다.
    ///   그 안에서 같은 서브트리를 다시 태우면 바깥 패스가 자식을 순회하던 중에 폭이 갈아엎어져
    ///   <b>일부 열만 새 값, 나머지는 옛 값</b>으로 남는다 — 실제로 열이 서로 침범했다 (A-1 회귀).
    ///   그래서 예약만 하고 실제 적용은 'LateUpdate'(레이아웃 패스 밖)에서 한다.
    /// </summary>
    private void OnRectTransformDimensionsChange()
    {
        // 콜백은 Awake 이전에도 올 수 있다 — 참조가 아직 없으면 Apply 가 알아서 빠진다.
        _pendingApply = true;
    }

    /// <summary>
    /// 저장된 위치를 읽어 온다. 없으면 인스펙터 값을 그대로 쓴다 (공장 초기값).
    ///
    /// ⚠️ 재생 중일 때만 읽는다. 이 컴포넌트는 '[ExecuteAlways]'라 에디터에서도 도는데,
    /// 거기서 'PlayerPrefs'를 읽으면 인스펙터로 6칸을 바꿔 보는 순간 저장값이 그것을 덮어써
    /// 미리보기가 망가진다. 에디터에서는 인스펙터가 진실이고, 빌드에서는 저장값이 진실이다.
    /// </summary>
    private void LoadSavedPosition()
    {
        if (!Application.isPlaying)
            return;

        int saved = WindowSettings.LoadInt(WindowSettings.WidgetPositionKey, (int)position);
        position  = (WidgetPosition)Mathf.Clamp(saved, 0, (int)WidgetPosition.LowerRight);
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

    // 리빌드 중에 들어온 재진입을 막는 빗장 (Apply 자기 재귀 방지)
    private bool _applying;

    // 다음 LateUpdate 에서 Apply 를 돌려야 하는가 (OnRectTransformDimensionsChange 가 세운다)
    private bool _pendingApply;

    // 자가 복구 재시도는 1회만 — 두 번째도 어긋나면 로그만 남기고 포기한다
    private bool _guardRetried;

    /// <summary>
    /// 예약된 배치 적용과 두 가드를 <b>레이아웃 패스 밖에서</b> 처리한다 (Unity 메시지).
    ///
    /// 'LateUpdate' 는 UGUI 의 캔버스 갱신('Canvas.willRenderCanvases')보다 앞이므로,
    /// 여기서 dirty 를 찍어 두면 같은 프레임 안에 정상적으로 한 번 리빌드된다.
    ///
    /// ⚠️ 가드는 <b>주기적으로</b> 돈다. 예전엔 'Apply()' 다음 프레임에만 검사했는데,
    ///   그러면 초기화·설정 변경 때만 돌아 <b>나중에 어긋나는 사고를 통째로 놓친다</b>.
    ///   실제로 A-1 이 그렇게 새어 나갔다 — 로그에는 정상값만 남고 화면만 깨져 있었다.
    /// </summary>
    private void LateUpdate()
    {
        if (_pendingApply)
        {
            _pendingApply = false;
            Apply();
        }

        if (Time.frameCount % GuardInterval == 0)
        {
            VerifyColumnWidths();
            VerifyNoOverflow();
        }
    }

    /// <summary>
    /// 현재 'position'을 3열 순서와 위·아래 슬롯에 반영한다.
    ///
    /// ⚠️ 리빌드는 <b>예약</b>한다('ForceRebuildLayoutImmediate'가 아니다).
    /// 즉시 리빌드는 이 메서드가 레이아웃 패스 안에서 불릴 때 재진입이 되어 열 폭이 반쯤만
    /// 갱신된다 — 'OnRectTransformDimensionsChange' 주석 참조. 형제 순서 변경
    /// ('SetSiblingIndex')은 UGUI 가 알아서 부모를 dirty 로 만들지만, 정렬만 바뀐 경우까지
    /// 확실히 덮으려고 명시적으로 한 번 더 찍는다.
    /// </summary>
    public void Apply()
    {
        if (_applying || !HasAllReferences())
            return;

        _applying = true;
        try
        {
            ApplyColumnOrder();
            ApplyVerticalSlot();

            LayoutRebuilder.MarkLayoutForRebuild(columns);
        }
        finally
        {
            _applying = false;
        }
    }

    /// <summary>
    /// 세 열의 폭이 서로 같은지 검사하고, 어긋났으면 경고를 남긴 뒤 <b>한 번만</b> 다시 태운다.
    ///
    /// ■ 넘침 가드('VerifyNoOverflow')와 보는 것이 다르다
    ///   열 셋이 서로 다른 폭이어도 <b>합이 부모 안에 들어가면 넘침으로는 안 잡힌다.</b>
    ///   반대로 넘침은 열이 균등해도 그 아래에서 난다. 두 가드는 겹치지 않는다.
    ///
    /// ■ 폭·순서를 하나도 못박지 않는다
    ///   "셋이 서로 같은가"만 본다. 그래서 설정으로 열 순서가 바뀌든, 크기 프리셋으로 창이
    ///   커지든 작아지든 그대로 성립한다. 기대값을 계산해 비교하면 그 계산이 또 하나의
    ///   틀릴 수 있는 곳이 된다.
    ///
    /// ■ 재시도가 1회인 이유
    ///   원인이 리빌드 타이밍이면 한 번이면 붙는다. 두 번째도 어긋나면 다른 원인이므로
    ///   계속 태워 봐야 매 프레임 리빌드만 돌 뿐이다. 그때는 로그가 진단의 입구가 된다.
    /// </summary>
    private void VerifyColumnWidths()
    {
        // 에디터 미리보기에서는 검사하지 않는다 — 배선/해상도가 확정되지 않은 상태라 경고만 시끄럽다.
        if (!Application.isPlaying || !HasAllReferences())
            return;

        float storage     = storageColumn.rect.width;
        float workstation = workstationColumn.rect.width;
        float market      = marketColumn.rect.width;

        float max = Mathf.Max(storage, Mathf.Max(workstation, market));
        float min = Mathf.Min(storage, Mathf.Min(workstation, market));

        if (max - min <= 1f)
        {
            _guardRetried = false; // 정상으로 돌아왔으니 다음 사고를 위해 재시도권을 돌려준다
            return;
        }

        string detail = $"창고 {storage:F1} · 작업 {workstation:F1} · 거래 {market:F1} (차이 {max - min:F1}px)";

        if (_guardRetried)
        {
            ClientLogger.Warn(ClientLogger.UI, $"열 폭이 재시도 후에도 어긋난다 — {detail}", this);
            return;
        }

        _guardRetried = true;
        _pendingApply = true; // 다음 LateUpdate 에서 한 번 더 태운다
        ClientLogger.Warn(ClientLogger.UI, $"열 폭이 어긋나 다시 배치한다 — {detail}", this);
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

        ApplyChildAlignment(isUpper);
    }

    /// <summary>
    /// 위젯이 어느 칸이냐에 따라 3열의 자식 정렬을 뒤집는다 ('ApplyVerticalSlot'에서 호출).
    ///
    /// 내용이 열 높이를 다 채우지 않을 때 남는 공간이 어디로 가는지를 정하는 값이다.
    /// 위젯이 위 칸이면 내용을 위로 붙여 위젯이 창 위 가장자리에 오고, 아래 칸이면 반대다.
    /// 값 자체는 인스펙터에서 바꿀 수 있다 — 열 구성이 달라지면 원하는 조합도 달라진다.
    ///
    /// 세 열을 전부 바꾼다 — 작업슬롯 열만 뒤집으면 창고·거래의 배너 줄 높이가 어긋난다
    /// (기획 2장 "세 창의 정렬 규칙": 세 배너 줄이 같은 높이에 와야 한다).
    /// </summary>
    private void ApplyChildAlignment(bool isUpper)
    {
        TextAnchor alignment = isUpper ? upperAlignment : lowerAlignment;

        SetColumnAlignment(storageColumn,     alignment);
        SetColumnAlignment(workstationColumn, alignment);
        SetColumnAlignment(marketColumn,      alignment);
    }

    // 열의 VerticalLayoutGroup 정렬을 바꾼다. 그룹이 없는 열은 건너뛴다 (ApplyChildAlignment에서 호출)
    private static void SetColumnAlignment(RectTransform column, TextAnchor alignment)
    {
        if (column == null)
            return;

        var group = column.GetComponent<VerticalLayoutGroup>();
        if (group != null)
            group.childAlignment = alignment;
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
            ClientLogger.Warn(ClientLogger.UI, "인스펙터 참조가 일부만 연결돼 배치를 건너뛴다.", this);

        return linked == references.Length;
    }

    // 가드 검사 주기(프레임). 매 프레임 돌 이유가 없다
    private const int GuardInterval = 10;

    // 마지막으로 남긴 넘침 내용 — 같은 상태가 이어지면 다시 찍지 않는다 (로그 폭주 방지)
    private string _lastOverflowDump = "";

    /// <summary>
    /// 3열과 그 자손을 훑어 <b>부모보다 넓은 노드</b>가 있으면 경고한다 ('LateUpdate'가 주기 호출).
    ///
    /// ■ 왜 열만 보면 부족한가
    ///   A-1 은 열 3개는 멀쩡한데 <b>그 아래 한 노드</b>가 넘쳐서 난 사고였다.
    ///   열만 보는 가드는 끝까지 아무 말도 하지 못했고, 로그에는 정상값만 남았다.
    ///
    /// ■ 기대값을 계산하지 않는다
    ///   "자식이 부모보다 넓은가"만 본다. 열 순서·창 크기가 런타임에 바뀌어도 그대로 성립한다.
    ///
    /// ■ 고치지 않고 알리기만 한다
    ///   넘침의 원인은 대개 그 노드가 부모보다 큰 min 을 요구하는 것이라 여기서 다시 태워 봐야
    ///   같은 결과가 나온다. 대신 <b>어느 노드가 무엇을 요구해서</b> 넘쳤는지를 남겨 진단의 입구가 된다.
    /// </summary>
    private void VerifyNoOverflow()
    {
        if (!Application.isPlaying || !HasAllReferences())
            return;

        var found = new System.Text.StringBuilder();
        CollectOverflow(storageColumn,     found);
        CollectOverflow(workstationColumn, found);
        CollectOverflow(marketColumn,      found);

        // 3열 묶음이 부모를 넘는지도 함께 본다 — 넘치면 HorizontalLayoutGroup 이 열을 밖으로 민다
        var   group   = columns.GetComponent<HorizontalLayoutGroup>();
        float spacing = group != null ? group.spacing * 2f : 0f;
        float padding = group != null ? group.padding.horizontal : 0f;
        float used    = storageColumn.rect.width + workstationColumn.rect.width + marketColumn.rect.width
                      + spacing + padding;

        if (used - columns.rect.width > 1f)
            found.Append($"\n  [열 묶음] 3열+간격+여백 {used:F1} > columns {columns.rect.width:F1} " +
                         $"({used - columns.rect.width:F1}px 초과)");

        if (found.Length == 0)
        {
            if (_lastOverflowDump.Length > 0)
                ClientLogger.Warn(ClientLogger.UI, "레이아웃 넘침이 해소됐다.", this);

            _lastOverflowDump = "";
            return;
        }

        string dump = found.ToString();
        if (dump == _lastOverflowDump)
            return;

        _lastOverflowDump = dump;
        ClientLogger.Warn(ClientLogger.UI, $"자식이 부모보다 넓다 — columns={columns.rect.width:F1}{dump}", this);
    }

    // 부모보다 1px 넘게 넓은 자손을 재귀로 모은다 (VerifyNoOverflow 에서 호출)
    private static void CollectOverflow(RectTransform node, System.Text.StringBuilder found)
    {
        for (int i = 0; i < node.childCount; i++)
        {
            var child = node.GetChild(i) as RectTransform;

            // 꺼진 가지는 그리지도 않고 값도 낡아 있다 — 건너뛴다
            if (child == null || !child.gameObject.activeInHierarchy)
                continue;

            float excess = child.rect.width - node.rect.width;
            if (excess > 1f)
                found.Append($"\n  {NodePath(child)} w={child.rect.width:F1} > 부모 {node.rect.width:F1} " +
                             $"({excess:F1}px 초과) {Claims(child)}");

            CollectOverflow(child, found);
        }
    }

    // 노드가 레이아웃에 주장하는 폭과, 그 값을 만드는 컴포넌트들 (CollectOverflow 에서 호출)
    private static string Claims(RectTransform node)
    {
        var names = new System.Text.StringBuilder();

        foreach (var component in node.GetComponents<Component>())
        {
            if (component is not ILayoutElement)
                continue;

            if (names.Length > 0)
                names.Append(", ");

            names.Append(component.GetType().Name);

            // ctrlW 가 꺼진 그룹은 자식의 '현재 크기'를 곧 min 으로 삼아 한 번 넓어지면 못 돌아온다
            if (component is HorizontalOrVerticalLayoutGroup layoutGroup)
                names.Append($"(ctrlW={layoutGroup.childControlWidth} expW={layoutGroup.childForceExpandWidth})");
        }

        return $"[min={LayoutUtility.GetMinWidth(node):F1} pref={LayoutUtility.GetPreferredWidth(node):F1} " +
               $"flex={LayoutUtility.GetFlexibleWidth(node):F1}] " +
               $"{(names.Length > 0 ? names.ToString() : "레이아웃 컴포넌트 없음")}";
    }

    // 어느 노드인지 알아볼 수 있을 만큼만 계층 경로를 만든다 (CollectOverflow 에서 호출)
    private static string NodePath(Transform node)
    {
        string path = node.name;

        Transform parent = node.parent;
        for (int depth = 0; depth < 4 && parent != null; depth++, parent = parent.parent)
        {
            path = parent.name + "/" + path;
        }

        return path;
    }
}

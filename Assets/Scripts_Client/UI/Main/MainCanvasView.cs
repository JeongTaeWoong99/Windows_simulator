using TMPro;
using UnityEngine;

/// <summary>
/// 메인 캔버스 — <c>@Main Column</c>의 가운데 칸. <b>여러 화면이 이 안에서 자리를 나눠 쓴다.</b>
///
/// <para>
/// ■ 머리와 발은 고정, 몸통만 갈아 끼운다<br/>
/// <code>
/// #Main Canvas (MAIN VIEW)                    여닫기 + 제목 (이 클래스)
/// ├─ Title                                    ← 항상  (제목만 바뀐다)
/// ├─ WorkStation List Presenter    (↓ SUB VIEW)   ┐
/// ├─ WorkStation Select Presenter  (↓ SUB VIEW)   │ 셋 중 하나만
/// ├─ Setting Presenter             (↓ SUB VIEW)   ┘
/// └─ Menu Presenter                (↓ SUB VIEW)   ← 항상
/// </code>
/// 무엇을 띄울지는 <c>UIManager</c>가 <see cref="MainScreen"/>으로 정한다.
/// 상태 패널의 버튼과 목록의 칸 클릭이 그걸 부른다.
/// </para>
///
/// <para>
/// ■ 왜 화면마다 캔버스를 두지 않았나<br/>
/// 예전엔 <c>#WorkStation Canvas</c>·<c>#Setting Canvas</c>가 <c>@Main Column</c>의 형제로 나란히 있었다.
/// 그러면 <b>화면을 하나 붙일 때마다 캔버스가 하나씩 늘어난다</b> — 각각
/// <c>Canvas</c>·<c>GraphicRaycaster</c>·<c>LayoutElement</c> 높이를 따로 맞춰야 하고,
/// 하나만 어긋나도 크기가 틀어진다(실제로 <c>#Setting Canvas</c>가 옛 높이 415를 들고 와 그랬다).
/// <b>캔버스를 하나로 모으면 그 셋을 한 번만 맞추면 된다.</b>
/// </para>
///
/// <para>
/// ■ 세로 3단이라 여기에 레이아웃 그룹을 둔다<br/>
/// <c>VerticalLayoutGroup</c>으로 <c>Title</c>(고정 50) · 몸통(나머지 전부) · <c>Menu</c>(고정 100)를 쌓는다.
/// 갈아 끼워지는 세 화면은 <b>전부 <c>preferredHeight 0 · flexibleHeight 1</c></b>이라 어느 것이 켜지든
/// 같은 자리에 같은 크기로 들어간다 — <b>꺼진 패널은 레이아웃에서 아예 빠지므로 겹치지 않는다.</b>
/// 내부 배치는 각 패널이 자기 레이아웃 그룹으로 한다.
/// </para>
///
/// <para>
/// ⚠️ <b>@Main Column 의 세 칸은 전부 높이가 고정이다</b> — 90 · 900 · 90 = 1080 = 컬럼 높이.
/// <c>LayoutElement</c>에 <c>preferredHeight</c>를 주고 <b><c>flexibleHeight</c>는 0으로 둔다.</b>
/// 하나라도 <c>flexibleHeight = 1</c>이면 <b>형제가 꺼질 때 그 자리를 혼자 빨아들인다</b> —
/// 전부 닫아 위젯만 남겼을 때 위젯이 화면 전체로 늘어난다.
/// → <c>UI 스크립트 규칙.md</c> §7-2
/// </para>
/// </summary>
public class MainCanvasView : MonoBehaviour
{
    // ※ 캔버스 View가 위젯을 쥐는 유일한 예외다.
    //   Title은 세 화면이 함께 쓰는 머리라 어느 패널에도 속하지 않는다 — 한 패널의 Presenter에 맡기면
    //   그 패널이 꺼질 때 함께 죽어서, 정작 다른 화면으로 넘어간 순간 제목을 못 바꾼다.
    //   Model을 구독하지 않고 위에서 밀어 넣은 값만 그리므로 역할은 여전히 View다.
    [CenterHeader("참조")]
    [SerializeField, Tooltip("캔버스 머리의 제목 — Title > Ttitle Text (TMP)")]
    private TMP_Text titleText = null!;

    // 참조 확보만 한다 (클라 공통 규약. 구독할 이벤트도 배선할 입력도 없다)
    private void Start()
    {
        this.RequireRef(titleText, nameof(titleText));
    }

    /// <summary>
    /// 이 캔버스를 통째로 열고 닫는다.
    ///
    /// <para>
    /// ※ 안의 화면을 고르는 건 <c>UIManager.ShowMainScreen</c>이다. 이건 캔버스 자체를 끄는 손잡이라
    /// 다른 캔버스(<c>StorageCanvasView</c> 등)와 모양을 맞춰 둔다.
    /// </para>
    /// </summary>
    public void Show(bool on)
    {
        gameObject.SetActive(on);
    }

    /// <summary>
    /// 머리의 제목을 바꾼다 (<c>UIManager.ShowMainScreen</c>이 화면을 갈아 끼울 때 호출).
    /// 문구는 <c>UI Manager</c>의 <c>Main Screens</c>에 화면별로 적혀 있다.
    /// </summary>
    public void SetTitle(string title)
    {
        titleText.text = title;
    }
}

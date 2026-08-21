using TMPro;
using UnityEngine;

/// <summary>
/// 메인 캔버스 — '@Main Column'의 가운데 칸. 여러 화면(목록·선택·설정)이 이 안에서 자리를 나눠 쓰고
/// 'Title'과 'Menu Presenter'는 항상 남는다. 무엇을 띄울지는 'UIManager'가 'MainScreen'으로 정한다.
///
/// ⚠️ '@Main Column'의 세 칸은 전부 높이가 <b>숫자로</b> 고정이다 — 60 · 900 · 120 = 1080 = 컬럼 높이.
/// 하나라도 'flexibleHeight = 1'이면 형제가 꺼질 때 그 자리를 혼자 빨아들여,
/// 전부 닫아 위젯만 남겼을 때 위젯이 화면 전체로 늘어난다.
///
/// ★ 여기(가운데)의 900만 사람이 정한다. <b>위·아래 60·120은 'WidgetPositionLayout'이
/// 나머지를 2:1로 나눠 써 넣는다</b> — 이 값을 바꾸면 사이드 둘이 알아서 따라온다.
/// 사이드를 인스펙터에서 직접 고쳐 봐야 다음 배치에서 덮어써진다.
/// → 계층·레이아웃 규격은 'UI 규칙.md' 7-2장·8장
/// </summary>
public class MainCanvasView : MonoBehaviour
{
    // ※ 캔버스 View가 위젯을 쥐는 유일한 예외다 (UI 규칙.md 6장).
    //   Title은 세 화면이 함께 쓰는 머리라, 한 패널의 Presenter에 맡기면 그 패널이 꺼질 때
    //   함께 죽어서 정작 다른 화면으로 넘어간 순간 제목을 못 바꾼다.
    [CenterHeader("참조")]
    [SerializeField, Tooltip("캔버스 머리의 제목 — Title > Ttitle Text (TMP)")]
    private TMP_Text titleText = null!;

    // 참조 확보만 한다 (클라 공통 규약. 구독할 이벤트도 배선할 입력도 없다)
    private void Start()
    {
        this.RequireRef(titleText, nameof(titleText));
    }

    /// <summary>
    /// 이 캔버스를 통째로 열고 닫는다. 안의 화면을 고르는 건 'UIManager.ShowMainScreen'이다.
    /// </summary>
    public void Show(bool on)
    {
        gameObject.SetActive(on);
    }

    /// <summary>
    /// 머리의 제목을 바꾼다 ('UIManager.ShowMainScreen'이 화면을 갈아 끼울 때 호출).
    /// 문구는 'UI Manager'의 'Main Screens'에 화면별로 적혀 있다.
    /// </summary>
    public void SetTitle(string title)
    {
        titleText.text = title;
    }
}

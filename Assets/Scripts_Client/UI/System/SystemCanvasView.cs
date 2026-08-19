using UnityEngine;

/// <summary>
/// 시스템 오버레이 열 — 로딩 표시와 알림(Notice)이 올라앉는 최상위 캔버스.
/// 로그인·게임 화면보다 앞에 떠야 해서 모든 메인 뷰보다 큰 Sorting Order를 준다.
///
/// ⚠️ 이 캔버스는 다른 UI보다 앞에 나와야 해서 'Override Sorting'과
/// 자기 'GraphicRaycaster'가 필요하다 (근거는 'UI 규칙.md' 7장).
///
/// 로딩/알림의 실제 표시·숨김은 자식의 'LoadingPresenter'·'NoticePresenter'가
/// 'ServerWaitManager' 이벤트를 구독해 스스로 한다 — 이 껍데기는 자리만 잡는다.
///
/// ⏸ 이 캔버스는 상주(항상 켜짐)라 'UIManager'가 여닫지 않는다. 그래서 아래 'Show'는 지금 호출처가 없다 —
/// 다른 'XxxCanvasView'와 API를 맞춘 예약이다(언젠가 오버레이 전체를 끌 일이 생기면 그때 쓴다).
/// </summary>
public class SystemCanvasView : MonoBehaviour
{
    /// <summary>이 열을 열고 닫는다 (예약 — 현재 호출처 없음, 오버레이는 상주한다).</summary>
    public void Show(bool on)
    {
        gameObject.SetActive(on);
    }
}

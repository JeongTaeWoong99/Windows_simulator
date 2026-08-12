using UnityEngine;

/// <summary>
/// 로그인 열 — 게임에 들어오면 가장 먼저, 그리고 이것만 보이는 화면.
/// 아이디 입력과 요청 전송은 자식의 'LoginPresenter'가 맡는다.
///
/// ⚠️ 이 캔버스는 다른 UI보다 앞에 나와야 해서 'Override Sorting'과
/// 자기 'GraphicRaycaster'가 필요하다 (근거는 'UI 규칙.md' 7장).
/// </summary>
public class LoginCanvasView : MonoBehaviour
{
    /// <summary>이 열을 열고 닫는다 (UIManager가 호출).</summary>
    public void Show(bool on)
    {
        gameObject.SetActive(on);
    }
}

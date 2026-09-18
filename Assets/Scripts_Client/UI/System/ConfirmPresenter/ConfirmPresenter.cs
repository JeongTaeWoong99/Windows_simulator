using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 확인 팝업 — 문구를 띄우고 [확인]을 누르면 콜백을 부른다. [취소]면 부르지 않는다.
//
// ■ 지금 누가 쓰나
// 잠긴 작업슬롯의 해금("N 골드가 필요합니다. 해금하시겠습니까?")뿐이다.
// 해금 전용이 아니라 **예/아니오를 묻는 자리면 어디서든** 쓰라고 이 이름·이 캔버스에 둔다.
//
// ■ 'AmountInputPresenter'와 같은 모양이다
// 화면 전체를 막아야 해서 '!System Canvas'에 살고, 상주라 'CanvasGroup'으로 여닫으며,
// 답을 돌려주는 왕복이라 'UIManager.AskConfirm'이 중개한다('System 규칙.md').
public class ConfirmPresenter : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("자기 CanvasGroup. 이 오브젝트를 끄지 않고 alpha·blocksRaycasts로 여닫는다")]
    private CanvasGroup group = null!;

    [SerializeField, Tooltip("묻는 문구")]
    private TMP_Text messageText = null!;

    [SerializeField, Tooltip("확인 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button confirmButton = null!;

    [SerializeField, Tooltip("취소 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button cancelButton = null!;

    // 확인을 눌렀을 때 부를 곳. 취소·닫힘이면 부르지 않는다.
    private Action? _onConfirm;

    // 참조 확보 → 배선 → 초기화 순서로 진행한다 (클라 공통 규약. 구독할 이벤트가 없다)
    private void Start()
    {
        this.RequireRef(group,         nameof(group));
        this.RequireRef(messageText,   nameof(messageText));
        this.RequireRef(confirmButton, nameof(confirmButton));
        this.RequireRef(cancelButton,  nameof(cancelButton));

        confirmButton.onClick.AddListener(OnConfirmClicked);
        cancelButton.onClick.AddListener(OnCancelClicked);

        Close(); // 시작은 닫힘 — 씬에 열린 채 저장됐어도 여기서 정리된다
    }

    #region 여닫기

    // 묻는다 ('UIManager.AskConfirm'이 호출).
    //   message   : 묻는 문구
    //   onConfirm : 확인을 눌렀을 때 받을 곳. 취소면 불리지 않는다
    public void Open(string message, Action onConfirm)
    {
        _onConfirm       = onConfirm;
        messageText.text = message;

        SetVisible(true);
    }

    // 팝업을 닫고 대기 중인 콜백을 버린다 (확인·취소·열 정리).
    //
    // ★ public인 이유 — 상주라 'OnDisable'이 오지 않는다. 'UIManager.CloseAllExceptWidget'이 부른다.
    public void Close()
    {
        _onConfirm = null;

        SetVisible(false);
    }

    // 보이기·차단을 함께 켜고 끈다 (Open · Close에서 호출). 오브젝트는 끄지 않는다.
    private void SetVisible(bool on)
    {
        group.alpha          = on ? 1f : 0f;
        group.blocksRaycasts = on;
        group.interactable   = on;
    }

    #endregion

    #region 입력

    // 확인 (confirmButton OnClick에 코드로 연결)
    //
    // ※ 콜백을 부르기 전에 닫는다. 받은 쪽이 곧바로 알림·다른 팝업을 열 수 있다.
    private void OnConfirmClicked()
    {
        Action? callback = _onConfirm;

        Close();

        callback?.Invoke();
    }

    // 취소 — 아무것도 하지 않고 닫는다 (cancelButton OnClick에 코드로 연결)
    private void OnCancelClicked()
    {
        Close();
    }

    #endregion
}

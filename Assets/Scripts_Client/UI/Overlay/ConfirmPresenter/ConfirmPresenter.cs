using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 확인 팝업 — 문구를 띄우고 [확인]을 누르면 콜백을 부른다. [취소]면 부르지 않는다.
//
// ■ 지금 누가 쓰나
// 예/아니오를 묻는 자리 전부 — 작업슬롯 해금 · 경매 구매/등록/취소 등.
// 값이 여럿인 확인(경매 구매·등록)은 문구는 짧게 두고, 값 표는 아래 어두운 상자('detail')에 따로 적는다.
//
// ■ 'AmountInputPresenter'와 같은 모양이다
// 화면 전체를 막아야 해서 '!Overlay Canvas'에 살고, 상주라 'CanvasGroup'으로 여닫으며,
// 답을 돌려주는 왕복이라 'UIManager.AskConfirm'이 중개한다('Overlay 규칙.md').
public class ConfirmPresenter : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("자기 CanvasGroup. 이 오브젝트를 끄지 않고 alpha·blocksRaycasts로 여닫는다")]
    private CanvasGroup group = null!;

    [SerializeField, Tooltip("가운데 판 — 열 때 살짝 커지며 나타난다. 비우면 팝업 전체가 커진다")]
    private RectTransform? panel;

    [SerializeField, Tooltip("묻는 문구")]
    private TMP_Text messageText = null!;

    [SerializeField, Tooltip("문구 아래 어두운 상자 — 상세(가격·골드 표)가 있을 때만 켠다")]
    private GameObject detailBox = null!;

    [SerializeField, Tooltip("상자 안 상세 글씨 — 밝은 글씨 · 금액은 노랑")]
    private TMP_Text detailText = null!;

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
        this.RequireRef(detailBox,     nameof(detailBox));
        this.RequireRef(detailText,    nameof(detailText));
        this.RequireRef(confirmButton, nameof(confirmButton));
        this.RequireRef(cancelButton,  nameof(cancelButton));

        confirmButton.onClick.AddListener(OnConfirmClicked);
        cancelButton.onClick.AddListener(OnCancelClicked);
        BackdropClose.Bind(gameObject, OnCancelClicked); // 차단막을 누르면 취소

        SetVisible(false, instant: true); // 시작은 닫힘 — 씬에 열린 채 저장됐어도 여기서 정리된다
    }

    #region 여닫기

    // 묻는다 ('UIManager.AskConfirm'이 호출).
    //   message   : 묻는 문구
    //   onConfirm : 확인을 눌렀을 때 받을 곳. 취소면 불리지 않는다
    //   detail    : 어두운 상자에 적을 표 — 글씨만 길게 이어지면 무엇이 값인지 안 보였다(2026-11-15 사용자: 경매 구매 확인). 비우면 상자를 숨긴다
    public void Open(string message, Action onConfirm, string? detail = null)
    {
        _onConfirm       = onConfirm;
        messageText.text = message;
        detailText.text  = detail ?? "";
        detailBox.SetActive(!string.IsNullOrEmpty(detail));

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
    // 연출은 'PanelTransition' — 차단·입력은 여는 즉시 켜고 닫는 즉시 끈다.
    private void SetVisible(bool on, bool instant = false)
    {
        PanelTransition.SetVisible(group, on, panel, instant);
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

    // 취소 — 아무것도 하지 않고 닫는다 (cancelButton OnClick · 차단막에 코드로 연결)
    private void OnCancelClicked()
    {
        Close();
    }

    #endregion
}

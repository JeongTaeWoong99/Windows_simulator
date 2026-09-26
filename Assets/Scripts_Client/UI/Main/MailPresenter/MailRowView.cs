using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 우편함 목록의 한 줄. 우편 한 통을 보여 주고 [받기] 또는 [삭제] 버튼 하나를 갖는다.
//
// 버튼은 하나다 — 안 받은 우편은 받기만, 받은 우편은 지우기만 할 수 있어서 둘이 동시에 뜨는 일이 없다.
// 안 받은 우편에 삭제 버튼을 두지 않는 것은 기획(우편 1장 12번)이다.
// 제목·첨부 문구는 'MailPresenter'가 완성해서 넘긴다 (종속 View 규약은 'UI 규칙.md').
public class MailRowView : MonoBehaviour
{
    // 받은 우편은 흐리게 — "이미 처리한 줄"이 한눈에 갈린다.
    private static readonly Color UnclaimedTitleColor = Color.white;
    private static readonly Color ClaimedTitleColor   = new Color(1f, 1f, 1f, 0.45f);

    [CenterHeader("참조")]
    [SerializeField, Tooltip("우편 제목 (템플릿의 Title)")]
    private TMP_Text titleText = null!;

    [SerializeField, Tooltip("발신자 · 도착 시각 (받은 우편이면 '받음'이 붙는다)")]
    private TMP_Text infoText = null!;

    [SerializeField, Tooltip("첨부 요약 — '골드 1,000 · 나무 x10'")]
    private TMP_Text attachmentText = null!;

    [SerializeField, Tooltip("받기/삭제 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button actionButton = null!;

    [SerializeField, Tooltip("버튼 문구 — '받기' 또는 '삭제'")]
    private TMP_Text actionText = null!;

    // 안 받은 우편의 [받기]를 눌렀다 ('MailPresenter'가 구독).
    public event Action<long>? ClaimClicked;

    // 받은 우편의 [삭제]를 눌렀다 ('MailPresenter'가 구독).
    public event Action<long>? DeleteClicked;

    private long _mailId;
    private bool _isClaimed;

    // 자기 버튼만 배선한다 — 서비스를 조회하지 않으므로 Awake로 충분하다 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(titleText,      nameof(titleText));
        this.RequireRef(infoText,       nameof(infoText));
        this.RequireRef(attachmentText, nameof(attachmentText));
        this.RequireRef(actionButton,   nameof(actionButton));
        this.RequireRef(actionText,     nameof(actionText));

        actionButton.onClick.AddListener(OnActionClicked);
    }

    // 이 줄이 그릴 우편을 정한다 ('MailPresenter'가 호출).
    //   mailId     : 받기·삭제 요청에 그대로 실린다
    //   title      : 완성된 제목
    //   info       : 완성된 "발신자 · 도착 시각" 문구
    //   attachment : 완성된 첨부 요약
    //   isClaimed  : 받은 우편인가 — 버튼이 [받기]와 [삭제] 중 무엇이 될지를 정한다
    public void Bind(long mailId, string title, string info, string attachment, bool isClaimed)
    {
        _mailId    = mailId;
        _isClaimed = isClaimed;

        titleText.text      = title;
        titleText.color     = isClaimed ? ClaimedTitleColor : UnclaimedTitleColor;
        infoText.text       = info;
        attachmentText.text = attachment;
        actionText.text     = isClaimed ? "삭제" : "받기";
    }

    // 요청을 기다리는 동안 버튼을 잠근다 ('MailPresenter'가 호출) — 같은 우편을 두 번 보내지 않게.
    public void SetInteractable(bool on) => actionButton.interactable = on;

    // 줄을 비운다. 오브젝트는 살려 두고 재사용 풀로 되돌린다.
    public void Clear()
    {
        _mailId             = 0L;
        titleText.text      = "";
        infoText.text       = "";
        attachmentText.text = "";
    }

    // 버튼 눌림 — 지금 상태에 맞는 이벤트 하나만 쏜다 (actionButton.onClick)
    private void OnActionClicked()
    {
        if (_isClaimed)
        {
            DeleteClicked?.Invoke(_mailId);
        }
        else
        {
            ClaimClicked?.Invoke(_mailId);
        }
    }
}

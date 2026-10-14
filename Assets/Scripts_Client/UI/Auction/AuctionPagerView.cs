using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 경매장 표 아래 쪽 넘김 줄 — [◀] 'n / N 쪽' [▶] + 오른쪽 안내 글자.
// 검색 · 판매 · 완료 표가 하나씩 갖는다. 무엇을 받을지는 모른다 — 눌리면 위로 알리기만 한다 (종속 View 규약은 'UI 규칙.md').
//
// ■ 쭉 내려 이어 받지 않고 쪽으로 나눈다 (2026-10-14 사용자 결정)
//   한 화면에 보이는 줄 수('AuctionModel.RowsPerPage')만 그린다 — 줄을 계속 쌓으면 받은 만큼 줄 오브젝트가 늘어난다.
public class AuctionPagerView : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("[◀] 앞 쪽. OnClick은 코드가 연결한다")]
    private Button prevButton = null!;

    [SerializeField, Tooltip("[▶] 다음 쪽. OnClick은 코드가 연결한다")]
    private Button nextButton = null!;

    [SerializeField, Tooltip("'n / N 쪽'")]
    private TMP_Text pageText = null!;

    [SerializeField, Tooltip("오른쪽 안내 — 건수 · 받는 중 · 우편 안내 등")]
    private TMP_Text noteText = null!;

    // 쪽 버튼이 눌렸다 (표를 가진 Presenter가 구독).
    public event Action? PrevClicked;
    public event Action? NextClicked;

    // 자기 버튼만 배선한다 — 서비스를 조회하지 않으므로 Awake로 충분하다 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(prevButton, nameof(prevButton));
        this.RequireRef(nextButton, nameof(nextButton));
        this.RequireRef(pageText,   nameof(pageText));
        this.RequireRef(noteText,   nameof(noteText));

        prevButton.onClick.AddListener(() => PrevClicked?.Invoke());
        nextButton.onClick.AddListener(() => NextClicked?.Invoke());
    }

    // 쪽 표시를 정한다.
    //   page      : 0부터
    //   pageCount : 전체 쪽 수. 모르면 0 — 'n 쪽'만 적는다(완료 탭은 서버가 건수를 주지 않는다)
    public void Bind(int page, int pageCount, bool canPrev, bool canNext, string note)
    {
        pageText.text = pageCount > 0 ? $"{page + 1} / {pageCount} 쪽" : $"{page + 1} 쪽";
        noteText.text = note;

        prevButton.interactable = canPrev;
        nextButton.interactable = canNext;
    }

    // 쪽 수 — 'count'건을 한 쪽 'perPage'줄로 나눈다. 0건이어도 1쪽.
    public static int PageCount(int count, int perPage) => Math.Max(1, (count + perPage - 1) / perPage);
}

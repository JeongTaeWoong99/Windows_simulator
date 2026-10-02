using TMPro;
using UnityEngine;

// 특성 표의 한 줄 — 왼쪽 산업 이름 + 칸 셋(개척 · 속도 · 산출량).
//
// 줄은 무엇을 그릴지 모른다 — 이름은 'Bind'로 받고, 칸은 'TraitPresenter'가 'GetCell'로 꺼내 직접 묶는다.
// (종속 View 규약은 'UI 규칙.md'의 "종속 View 쪽 규약")
//
// ※ 왼쪽 이름 열이 칸보다 좁아야 해서 'GridLayoutGroup'(칸 크기가 하나)으로는 못 그린다 —
//   줄마다 'HorizontalLayoutGroup'이고, 머리 줄(개척·속도·산출량)도 같은 폭 규칙으로 맞춘다.
public class TraitRowView : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("왼쪽 산업 이름 (예: '농사' · 공통 줄은 '공통')")]
    private TMP_Text labelText = null!;

    [SerializeField, NonReorderable, Tooltip("칸들 — 표의 열 순서(개척 · 속도 · 산출량)대로 넣는다")]
    private TraitNodeView[] cells = new TraitNodeView[0];

    // 칸 수 — 'TraitPresenter'가 열 수와 맞는지 본다.
    public int CellCount => cells.Length;

    // 필수 참조 검증 — 서비스를 조회하지 않으므로 Awake로 충분하다 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(labelText, nameof(labelText));
    }

    // 줄 이름을 정한다 ('TraitPresenter'가 호출).
    public void Bind(string label)
    {
        labelText.text = label;
    }

    // 'column'번째 칸 ('TraitPresenter'가 호출).
    public TraitNodeView GetCell(int column) => cells[column];
}

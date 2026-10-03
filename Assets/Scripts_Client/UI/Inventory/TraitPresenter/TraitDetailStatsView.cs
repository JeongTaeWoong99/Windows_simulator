using TMPro;
using UnityEngine;

// 특성 정보 영역의 "지금 ▶ 다음" 비교 상자 둘.
//
// 이 View는 레벨도 효과 공식도 모른다 — 완성된 문구만 'Bind'로 받는다.
// (종속 View 규약은 'UI 규칙.md'의 "종속 View 쪽 규약")
//
// ■ 왜 "항목 · 값" 한 줄(EfficiencyRowView)을 안 쓰나 (2026-10-02 사용자 피드백)
// 효과 "속도 +0% ▶ 속도 +8%"가 값 칸에 안 들어가 '…'로 잘렸다. 상자마다 값 하나만 적어 잘릴 일이 없다.
//
// ※ 조건(계정 Lv · 포인트)은 여기 두지 않는다 — 변화와 성격이 달라 상자 모양을 같이 쓰면 한 표처럼 읽혔다.
//   'TraitPresenter'가 버튼 바로 위 글자 한 줄로 따로 그린다(2026-10-03).
public class TraitDetailStatsView : MonoBehaviour
{
    [CenterHeader("비교 상자")]
    [SerializeField, Tooltip("'지금 (Lv0)' · 최대 레벨이면 '현재 효과'")]
    private TMP_Text nowTitleText = null!;

    [SerializeField, Tooltip("지금 효과 (예: '속도 +0%')")]
    private TMP_Text nowValueText = null!;

    [SerializeField, Tooltip("두 상자 사이 '▶' — 최대 레벨이면 꺼진다")]
    private GameObject arrow = null!;

    [SerializeField, Tooltip("다음 레벨 상자 — 최대 레벨이면 꺼진다")]
    private GameObject nextBox = null!;

    [SerializeField, Tooltip("'다음 (Lv1)'")]
    private TMP_Text nextTitleText = null!;

    [SerializeField, Tooltip("다음 레벨 효과 (예: '속도 +8%')")]
    private TMP_Text nextValueText = null!;

    // 필수 참조 검증 — 서비스를 조회하지 않으므로 Awake로 충분하다 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(nowTitleText,  nameof(nowTitleText));
        this.RequireRef(nowValueText,  nameof(nowValueText));
        this.RequireRef(arrow,         nameof(arrow));
        this.RequireRef(nextBox,       nameof(nextBox));
        this.RequireRef(nextTitleText, nameof(nextTitleText));
        this.RequireRef(nextValueText, nameof(nextValueText));
    }

    // 더 올릴 수 있는 특성을 그린다 ('TraitPresenter'가 호출) — 지금 상자 ▶ 다음 상자.
    public void BindNext(string nowTitle, string nowValue, string nextTitle, string nextValue)
    {
        nowTitleText.text  = nowTitle;
        nowValueText.text  = nowValue;
        nextTitleText.text = nextTitle;
        nextValueText.text = nextValue;

        arrow.SetActive(true);
        nextBox.SetActive(true);
    }

    // 최대 레벨인 특성을 그린다 ('TraitPresenter'가 호출) — 지금 상자 하나만 남긴다.
    //
    // ※ '최대 레벨' 표시는 여기 두지 않는다 — 큰 상자로 띄우니 보기 싫다는 피드백(2026-10-02).
    //   이름 줄의 'Lv 5 / 5 · 최대'가 말한다.
    public void BindMax(string nowTitle, string nowValue)
    {
        nowTitleText.text = nowTitle;
        nowValueText.text = nowValue;

        arrow.SetActive(false);
        nextBox.SetActive(false);
    }
}

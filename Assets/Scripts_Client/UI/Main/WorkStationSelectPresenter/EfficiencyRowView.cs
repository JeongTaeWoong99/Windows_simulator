using TMPro;
using UnityEngine;

// 효율 계산의 한 줄 — 왼쪽 항목 이름, 오른쪽 값, 값 아래 안내 문구(필요할 때만).
//
// 줄 수는 늘어난다 — 장비 · 특성 · 액티브 부스트가 붙으면 항목이 하나씩 생긴다(일감 'T-055').
// 그래서 씬에 줄을 박지 않고 프리팹 풀로 둔다. 무엇을 몇 줄 그릴지는 'WorkStationSelectPresenter'가 정한다.
// (종속 View 규약은 'UI 규칙.md'의 "종속 View 쪽 규약")
public class EfficiencyRowView : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("항목 이름 (예: 적성 기본값)")]
    private TMP_Text labelText = null!;

    [SerializeField, Tooltip("값 (예: 2.45배)")]
    private TMP_Text valueText = null!;

    [SerializeField, Tooltip("값에 붙는 안내 문구. 할 말이 없으면 꺼진다")]
    private TMP_Text noteText = null!;

    // 필수 참조 검증 — 서비스를 조회하지 않으므로 Awake로 충분하다 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(labelText, nameof(labelText));
        this.RequireRef(valueText, nameof(valueText));
        this.RequireRef(noteText,  nameof(noteText));
    }

    // 항목 이름과 값을 그린다. 안내 문구는 지운다 ('WorkStationSelectPresenter'가 호출)
    public void Bind(string label, string value)
    {
        labelText.text = label;
        valueText.text = value;
        SetNote("");
    }

    // 값 아래 안내 문구를 띄운다. 빈 문자열이면 줄을 끈다 — 빈 줄이 높이를 차지하지 않게 한다.
    public void SetNote(string note)
    {
        noteText.text = note;
        noteText.gameObject.SetActive(note.Length > 0);
    }

    // 줄을 비운다. 오브젝트는 살려 두고 재사용 풀로 되돌린다.
    public void Clear()
    {
        Bind("", "");
    }
}

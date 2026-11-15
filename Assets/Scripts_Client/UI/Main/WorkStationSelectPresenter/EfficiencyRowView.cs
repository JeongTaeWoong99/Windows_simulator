using GameData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    // 효율 계산 줄은 색을 쓰지 않는다 — 산업 레벨 정보의 자원 줄만 'SetRarity'로 칠한다.
    [SerializeField, Tooltip("줄 바탕 — 프리팹 루트의 Image. 평소 색은 그대로 두고 'SetRarity'일 때만 칠해진다")]
    private Image backgroundImage = null!;

    [SerializeField, Tooltip("줄에 올리면 뜨는 설명 툴팁 — 이 항목이 무슨 뜻인지(2026-11-15). 바탕 Image가 레이를 받아야 뜬다")]
    private TooltipTrigger tooltip = null!;

    // 씬에 찍힌 기본 바탕색. 'Clear'가 여기로 되돌린다 — 풀에서 재사용될 때 이전 등급 색이 남지 않게.
    private Color _defaultColor;

    // 필수 참조 검증 — 서비스를 조회하지 않으므로 Awake로 충분하다 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(labelText, nameof(labelText));
        this.RequireRef(valueText, nameof(valueText));
        this.RequireRef(noteText,  nameof(noteText));
        this.RequireRef(backgroundImage, nameof(backgroundImage));
        this.RequireRef(tooltip,         nameof(tooltip));

        _defaultColor = backgroundImage.color;
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

    // 줄에 올리면 뜰 설명을 단다. 빈 문자열이면 툴팁을 끈다 ('WorkStationSelectPresenter'가 Bind 뒤에 호출).
    //   title : 툴팁 제목 — 보통 항목 이름
    //   body  : 이 항목이 무슨 뜻인지 한두 문장
    public void SetTooltip(string title, string body)
    {
        tooltip.SetProvider(body.Length > 0 ? () => new TooltipContent(title).Row(body, "") : null);
    }

    // 줄무늬 — 홀수 줄 바탕을 조금 밝힌다. 툴팁 줄무늬와 같은 생각(2026-11-15 사용자: 효율 계산이 다 같은 갈색이라 구분이 안 됐다).
    private const float StripeLighten = 0.12f;

    // 번갈아 칠한다 (효율 계산 줄만 호출 — 'WorkStationSelectPresenter.RefreshEfficiency').
    public void SetStripe(bool odd)
    {
        backgroundImage.color = odd ? Color.Lerp(_defaultColor, Color.white, StripeLighten) : _defaultColor;
    }

    // 줄 바탕을 등급 색으로 칠한다 (산업 레벨 정보의 자원 줄만 호출).
    //   rarity : 아이템 종류(TID)로 읽은 등급. 인벤토리 칸과 같은 표('RarityPalette')다
    public void SetRarity(GlobalRarity rarity)
    {
        backgroundImage.color = RarityPalette.Get(rarity);
    }

    // 줄을 비운다. 오브젝트는 살려 두고 재사용 풀로 되돌린다.
    // 바탕색도 씬 기본값으로 되돌린다 — 안 되돌리면 효율 계산 줄이 이전 자원의 등급 색을 쓴다.
    public void Clear()
    {
        Bind("", "");
        SetTooltip("", "");
        backgroundImage.color = _defaultColor;
    }
}

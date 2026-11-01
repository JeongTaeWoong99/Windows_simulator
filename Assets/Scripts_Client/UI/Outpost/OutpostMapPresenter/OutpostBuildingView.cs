using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 지도 위 건물 한 채 — 그림 · 이름판(이름 + 레벨) · 분류 · 표시 둘(▲ 올릴 수 있음 · ● 남은 포인트). 눌리면 'Clicked'만 쏜다.
//
// 이 칸은 비용도 포인트 계산도 모른다 — 완성된 값만 'Bind'로 받는다 (종속 View 규약은 'UI 규칙.md').
//
// ■ 어느 건물인지는 씬이 정한다
// 자리(앵커)와 그림이 건물마다 다르고, 표에는 자리 칼럼이 없다 — 칸마다 인스펙터에 TID를 적어 둔다.
public class OutpostBuildingView : MonoBehaviour
{
    [CenterHeader("건물")]
    [SerializeField, Tooltip("이 칸이 그리는 건물 — BuildingTable.BuildingTID")]
    private int buildingTid;

    [CenterHeader("참조")]
    [SerializeField, Tooltip("이 건물을 고르는 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button button = null!;

    [SerializeField, Tooltip("건물 이름")]
    private TMP_Text nameText = null!;

    [SerializeField, Tooltip("건물 레벨 — 'Lv3'")]
    private TMP_Text levelText = null!;

    [SerializeField, Tooltip("분류 — 편의 · 산업 · 거래 · 인력 · 수집")]
    private TMP_Text categoryText = null!;

    [SerializeField, Tooltip("지금 올릴 수 있을 때 켜지는 표시")]
    private GameObject upgradeMark = null!;

    [SerializeField, Tooltip("남은 포인트 표시 — 포인트가 있을 때만 켜진다")]
    private GameObject pointMark = null!;

    [SerializeField, Tooltip("남은 포인트 글자 — '● 3'")]
    private TMP_Text pointText = null!;

    // 이 건물을 눌렀다 ('OutpostMapPresenter'가 구독)
    public event Action<OutpostBuildingView>? Clicked;

    public int BuildingTid => buildingTid;

    // 자기 버튼만 배선한다 — 서비스를 조회하지 않으므로 Awake로 충분하다 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(button,       nameof(button));
        this.RequireRef(nameText,     nameof(nameText));
        this.RequireRef(levelText,    nameof(levelText));
        this.RequireRef(categoryText, nameof(categoryText));
        this.RequireRef(upgradeMark,  nameof(upgradeMark));
        this.RequireRef(pointMark,    nameof(pointMark));
        this.RequireRef(pointText,    nameof(pointText));

        button.onClick.AddListener(() => Clicked?.Invoke(this));
    }

    // 완성값을 그린다 ('OutpostMapPresenter'가 호출).
    public void Bind(string displayName, string category, int level, bool canUpgrade, int points)
    {
        nameText.text     = displayName;
        levelText.text    = $"Lv{level}";
        categoryText.text = category;

        upgradeMark.SetActive(canUpgrade);
        pointMark.SetActive(points > 0);
        pointText.text = Lang.Format("UI.Outpost.PointLeft", points);
    }
}

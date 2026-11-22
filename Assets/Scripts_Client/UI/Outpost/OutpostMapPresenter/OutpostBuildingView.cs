using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 지도 위 건물 한 채 — 그림 · 이름판(이름 + 레벨 + 분류) · 이름판 귀퉁이 빨간 점. 눌리면 'Clicked'만 쏜다.
// 그림은 레벨 단계('OutpostArtStage.BuildingTier')마다 갈아 끼운다 — 빈 터 · 일하는 자리 · 작은 집 · 집 · 큰 건물 · 완성.
//
// ■ 빨간 점 하나로 알린다 (2026-11-22 사용자 — ▲ · ● 표시를 없애고 레드닷으로 통일)
//   올릴 수 있거나 찍을 수 있는 특성이 있으면 이름판 오른쪽 위 귀퉁이에 켠다.
//
// ■ 이름판은 그림 가운데 아래에 둔다
//   단계마다 그림이 캔버스 안에서 치우친 정도가 달라(문 자리는 고정) — 생성기가 그림의 가로 가운데를 **스프라이트 피벗 x**에 적어 둔다.
//   이름판은 그 피벗을 읽어 옮긴다. 그림을 다시 뽑으면 임포트 때 피벗도 다시 넣는다('Outpost 규칙.md' 그림 단계).
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

    [SerializeField, NonReorderable, Tooltip("단계 그림 6장 — 빈 터 · 천막 · 오두막 · 집 · 큰 건물 · 완성 (building_<TID>_0~5)")]
    private Sprite[] tierSprites = new Sprite[0];

    [CenterHeader("참조")]
    [SerializeField, Tooltip("이 건물을 고르는 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button button = null!;

    [SerializeField, Tooltip("건물 그림 — 단계 그림을 갈아 끼운다")]
    private Image art = null!;

    [SerializeField, Tooltip("건물 이름")]
    private TMP_Text nameText = null!;

    [SerializeField, Tooltip("건물 레벨 — 'Lv3'")]
    private TMP_Text levelText = null!;

    [SerializeField, Tooltip("분류 — 편의 · 산업 · 거래 · 인력 · 수집")]
    private TMP_Text categoryText = null!;

    [SerializeField, Tooltip("이름판 — 그림 가운데 아래로 옮긴다")]
    private RectTransform namePlate = null!;

    [SerializeField, Tooltip("이름판 귀퉁이 빨간 점 — 올리거나 찍을 수 있을 때")]
    private GameObject noticeDot = null!;

    // 이 건물을 눌렀다 ('OutpostMapPresenter'가 구독)
    public event Action<OutpostBuildingView>? Clicked;

    public int BuildingTid => buildingTid;

    // 자기 버튼만 배선한다 — 서비스를 조회하지 않으므로 Awake로 충분하다 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(button,       nameof(button));
        this.RequireRef(art,          nameof(art));

        if (tierSprites.Length != OutpostArtStage.TierCount)
        {
            throw new InvalidOperationException($"{name}: 'tierSprites'에 그림 {OutpostArtStage.TierCount}장을 넣을 것 (지금 {tierSprites.Length}장).");
        }

        this.RequireRef(nameText,     nameof(nameText));
        this.RequireRef(levelText,    nameof(levelText));
        this.RequireRef(categoryText, nameof(categoryText));
        this.RequireRef(namePlate,    nameof(namePlate));
        this.RequireRef(noticeDot,    nameof(noticeDot));

        button.onClick.AddListener(() => Clicked?.Invoke(this));
    }

    // 완성값을 그린다 ('OutpostMapPresenter'가 호출).
    //   hasNotice : 올리거나 찍을 수 있다 — 빨간 점
    public void Bind(string displayName, string category, int level, bool hasNotice)
    {
        nameText.text     = displayName;
        levelText.text    = $"Lv{level}";
        categoryText.text = category;

        Sprite sprite = tierSprites[OutpostArtStage.BuildingTier(level)];
        art.sprite    = sprite;

        // 이름판 x — 그림 가운데(스프라이트 피벗 x)
        float center = sprite.pivot.x / sprite.rect.width;
        namePlate.anchoredPosition = new Vector2((center - 0.5f) * ((RectTransform)transform).rect.width, namePlate.anchoredPosition.y);

        noticeDot.SetActive(hasNotice);
    }
}

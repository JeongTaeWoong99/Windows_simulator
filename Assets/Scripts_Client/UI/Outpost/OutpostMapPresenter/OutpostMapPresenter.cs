using GameData;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 마을 지도 — 건물 5채를 그리고, 건물을 누르면 옆에 작은 메뉴([업그레이드] · [특성])를 띄운다.
// 메뉴의 버튼이 건물 창('OutpostBuildingPresenter')을 연다. 지도 바닥을 누르면 메뉴가 닫힌다.
//
// ■ 빨간 점 하나 — 지금 할 수 있는 일이 있다 (2026-11-22 사용자 — ▲ · ● 표시를 레드닷으로 통일)
// 올릴 수 있다('PlayerDataModel.CheckBuildingUpgrade') 또는 찍을 수 있는 노드가 있다('CanLearnAnyTrait').
// 포인트만 남고 찍을 노드가 없으면 켜지 않는다 — 눌러도 할 일이 없는 재촉이 된다.
//
// ■ 바탕은 건물 레벨 합으로 자란다 ('OutpostArtStage.VillageStage')
// 지형(개울 · 숲)은 다섯 장이 같고 길 · 광장 · 울타리 · 소품만 달라진다 — 건물 자리가 고정이라 바탕만 갈아 끼운다.
public class OutpostMapPresenter : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, NonReorderable, Tooltip("지도 위 건물 칸 — 칸마다 BuildingTID를 적어 둔다")]
    private OutpostBuildingView[] buildings = new OutpostBuildingView[0];

    [SerializeField, Tooltip("지도 바닥 버튼 — 누르면 메뉴를 닫는다. OnClick은 코드가 연결한다")]
    private Button groundButton = null!;

    [SerializeField, Tooltip("지도 바닥 그림 — 마을 단계 바탕을 갈아 끼운다")]
    private Image groundImage = null!;

    [SerializeField, NonReorderable, Tooltip("마을 단계 바탕 5장 — ground_1~5")]
    private Sprite[] groundStages = new Sprite[0];

    [SerializeField, Tooltip("건물 창 — 메뉴의 버튼이 연다")]
    private OutpostBuildingPresenter buildingWindow = null!;

    [CenterHeader("건물 메뉴")]
    [SerializeField, Tooltip("건물 옆에 뜨는 작은 메뉴. 평소 꺼져 있다")]
    private RectTransform menuPanel = null!;

    [SerializeField, Tooltip("메뉴 — 건물 이름")]
    private TMP_Text menuNameText = null!;

    [SerializeField, Tooltip("메뉴 — 건물 레벨")]
    private TMP_Text menuLevelText = null!;

    [SerializeField, Tooltip("메뉴 — 이 건물이 무엇을 좋게 하는지 한 줄")]
    private TMP_Text menuDescText = null!;

    [SerializeField, Tooltip("메뉴 — [업그레이드]. OnClick은 코드가 연결한다")]
    private Button menuUpgradeButton = null!;

    [SerializeField, Tooltip("메뉴 — [특성]. OnClick은 코드가 연결한다")]
    private Button menuTraitButton = null!;

    [SerializeField, Tooltip("메뉴 — [특성] 글자. 남은 포인트를 붙인다")]
    private TMP_Text menuTraitLabel = null!;

    [SerializeField, Tooltip("메뉴 — [업그레이드] 빨간 점. 지금 올릴 수 있을 때")]
    private GameObject menuUpgradeDot = null!;

    [SerializeField, Tooltip("메뉴 — [특성] 빨간 점. 지금 찍을 수 있는 노드가 있을 때")]
    private GameObject menuTraitDot = null!;

    private PlayerDataModel _data = null!;

    private int  _selectedTid; // 메뉴를 띄운 건물. 0이면 메뉴가 닫혀 있다
    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 배선 → 초기화 순서로 진행한다 (클라 공통 규약)
    private void Start()
    {
        this.RequireRef(groundButton,      nameof(groundButton));
        this.RequireRef(groundImage,       nameof(groundImage));

        if (groundStages.Length != OutpostArtStage.StageCount)
        {
            throw new System.InvalidOperationException($"{name}: 'groundStages'에 그림 {OutpostArtStage.StageCount}장을 넣을 것 (지금 {groundStages.Length}장).");
        }

        this.RequireRef(buildingWindow,    nameof(buildingWindow));
        this.RequireRef(menuPanel,         nameof(menuPanel));
        this.RequireRef(menuNameText,      nameof(menuNameText));
        this.RequireRef(menuLevelText,     nameof(menuLevelText));
        this.RequireRef(menuUpgradeDot,    nameof(menuUpgradeDot));
        this.RequireRef(menuTraitDot,      nameof(menuTraitDot));
        this.RequireRef(menuDescText,      nameof(menuDescText));
        this.RequireRef(menuUpgradeButton, nameof(menuUpgradeButton));
        this.RequireRef(menuTraitButton,   nameof(menuTraitButton));
        this.RequireRef(menuTraitLabel,    nameof(menuTraitLabel));

        _data = Services.Get<PlayerDataModel>();

        Subscribe();

        foreach (OutpostBuildingView building in buildings)
        {
            building.Clicked += OnBuildingClicked;
        }

        groundButton.onClick.AddListener(CloseMenu);
        menuUpgradeButton.onClick.AddListener(() => OpenWindow(OutpostBuildingPresenter.Tab.Upgrade));
        menuTraitButton.onClick.AddListener(() => OpenWindow(OutpostBuildingPresenter.Tab.Trait));

        Redraw();

        _isReady = true;
    }

    // 껐다 켠 경우 — 메뉴를 닫고 시작한다. 닫혀 있는 동안 온 변경은 캐시에서 다시 그린다 (Unity 메시지)
    private void OnEnable()
    {
        if (!_isReady)
        {
            return;
        }

        Subscribe();
        CloseMenu();
        buildingWindow.Close();
        Redraw();
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
    }

    #region 구독

    // 표시를 바꾸는 변경 전부 — 레벨 · 포인트 · 골드 · 자원 · 계정 레벨 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed = true;

        _data.BuildingsChanged    += Redraw;
        _data.TraitsChanged       += Redraw;
        _data.CurrencyChanged     += Redraw;
        _data.InventoryChanged    += Redraw;
        _data.AccountLevelChanged += Redraw;
        Lang.Changed              += Redraw;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed = false;

        _data.BuildingsChanged    -= Redraw;
        _data.TraitsChanged       -= Redraw;
        _data.CurrencyChanged     -= Redraw;
        _data.InventoryChanged    -= Redraw;
        _data.AccountLevelChanged -= Redraw;
        Lang.Changed              -= Redraw;
    }

    #endregion

    // 건물 칸과 열린 메뉴를 다시 그린다 (구독 · Start · OnEnable)
    private void Redraw()
    {
        int totalLevel = 0;

        foreach (OutpostBuildingView building in buildings)
        {
            int tid = building.BuildingTid;
            totalLevel += _data.GetBuildingLevel(tid);

            if (!GameDataLoader.TryGetBuilding(tid, out var row))
            {
                continue;
            }

            building.Bind(Loc.Get(row.NameLocKey), Lang.Get($"UI.Outpost.Category.{tid}"), _data.GetBuildingLevel(tid),
                          _data.CheckBuildingUpgrade(tid) == EResultCode.Ok || _data.CanLearnAnyTrait(tid));
        }

        groundImage.sprite = groundStages[OutpostArtStage.VillageStage(totalLevel)];

        RedrawMenu();
    }

    // 표시·메뉴에 적을 남은 포인트. 트리 노드가 없는 건물은 0 — 맨 위 머리말의 이유.
    private int GetVisiblePoints(int buildingTid)
        => GameDataLoader.HasBuildingNodes(buildingTid) ? Mathf.Max(0, _data.GetBuildingPoints(buildingTid)) : 0;

    // 메뉴 내용을 그린다 (Redraw · 건물 클릭)
    private void RedrawMenu()
    {
        if (_selectedTid == 0 || !GameDataLoader.TryGetBuilding(_selectedTid, out var row))
        {
            menuPanel.gameObject.SetActive(false);

            return;
        }

        int points = GetVisiblePoints(_selectedTid);

        menuNameText.text   = Loc.Get(row.NameLocKey);
        menuLevelText.text  = $"Lv{_data.GetBuildingLevel(_selectedTid)}";
        menuDescText.text   = Lang.Get($"UI.Outpost.Desc.{_selectedTid}");
        menuTraitLabel.text = points > 0
            ? $"{Lang.Get("UI.Outpost.Traits")} {Lang.Format("UI.Outpost.PointLeft", points)}"
            : Lang.Get("UI.Outpost.Traits");

        menuUpgradeDot.SetActive(_data.CheckBuildingUpgrade(_selectedTid) == EResultCode.Ok);
        menuTraitDot.SetActive(_data.CanLearnAnyTrait(_selectedTid));

        menuPanel.gameObject.SetActive(true);
    }

    // 건물을 눌렀다 — 메뉴를 그 옆에 띄운다. 같은 건물을 다시 누르면 닫는다 (OutpostBuildingView.Clicked 구독).
    private void OnBuildingClicked(OutpostBuildingView building)
    {
        if (_selectedTid == building.BuildingTid)
        {
            CloseMenu();

            return;
        }

        _selectedTid = building.BuildingTid;

        PlaceMenuBeside((RectTransform)building.transform);
        RedrawMenu();
    }

    // 메뉴를 건물 옆에 놓는다 — 오른쪽 건물은 왼쪽으로, 아래쪽 건물은 위로 띄워 판 안에 남긴다.
    private void PlaceMenuBeside(RectTransform building)
    {
        bool isRightSide = building.anchorMin.x > 0.55f;
        bool isLowSide   = building.anchorMin.y < 0.45f;

        menuPanel.anchorMin = building.anchorMin;
        menuPanel.anchorMax = building.anchorMin;
        menuPanel.pivot     = new Vector2(isRightSide ? 1f : 0f, isLowSide ? 0f : 1f);

        float gapX = building.rect.width * 0.5f + 8f;

        menuPanel.anchoredPosition = new Vector2(isRightSide ? -gapX : gapX, isLowSide ? -40f : 40f);
    }

    private void CloseMenu()
    {
        _selectedTid = 0;

        RedrawMenu();
    }

    // 메뉴의 버튼 — 건물 창을 그 탭으로 연다
    private void OpenWindow(OutpostBuildingPresenter.Tab tab)
    {
        int tid = _selectedTid;

        CloseMenu();
        buildingWindow.Open(tid, tab);
    }
}

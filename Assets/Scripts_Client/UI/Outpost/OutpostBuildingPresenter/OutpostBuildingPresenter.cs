using GameData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 건물 창 — 지도 위에 뜨는 판. 머리 줄(이름 · 레벨 · 분류 · 탭 둘 · [X]) + 탭 화면(업그레이드 · 특성).
// 어느 건물 · 어느 탭인지만 정하고, 내용은 탭 화면이 그린다 — 탭 화면은 이 Presenter의 자식이 아니라 형제다('UI 규칙.md').
//
// ■ 특성 탭은 트리 노드가 있는 건물에서만 열린다
// 1차는 작업장 트리뿐이다(T-183). 다른 건물은 업그레이드 탭에 "특성은 준비 중"을 적고, 특성 탭 대신 그 안내로 데려간다.
public class OutpostBuildingPresenter : MonoBehaviour
{
    public enum Tab
    {
        Upgrade,
        Trait,
    }

    [CenterHeader("참조")]
    [SerializeField, Tooltip("창 전체 — 머리 줄(이 Presenter)과 두 페이지가 형제로 들어 있다. 평소 꺼져 있다")]
    private GameObject window = null!;

    [SerializeField, Tooltip("건물 이름")]
    private TMP_Text nameText = null!;

    [SerializeField, Tooltip("건물 레벨 — 'Lv3'")]
    private TMP_Text levelText = null!;

    [SerializeField, Tooltip("분류")]
    private TMP_Text categoryText = null!;

    [SerializeField, Tooltip("[업그레이드] 탭 버튼. OnClick은 코드가 연결한다")]
    private Button upgradeTabButton = null!;

    [SerializeField, Tooltip("[특성] 탭 버튼. OnClick은 코드가 연결한다")]
    private Button traitTabButton = null!;

    [SerializeField, Tooltip("닫기 버튼. OnClick은 코드가 연결한다")]
    private Button closeButton = null!;

    [CenterHeader("탭 화면")]
    [SerializeField, Tooltip("업그레이드 화면")]
    private OutpostUpgradePresenter upgradePage = null!;

    [SerializeField, Tooltip("특성 화면")]
    private TraitPresenter traitPage = null!;

    [CenterHeader("색")]
    [SerializeField, Tooltip("고른 탭")]
    private UIThemeRole tabSelectedRole = UIThemeRole.ButtonSelected;

    [SerializeField, Tooltip("고르지 않은 탭")]
    private UIThemeRole tabRole = UIThemeRole.Button;

    private PlayerDataModel _data = null!;

    private int  _buildingTid;
    private bool _isWired;

    // 건물 창을 연다 ('OutpostMapPresenter'가 호출).
    //
    // ※ 처음 열 때 배선한다 — 평소 꺼져 있어 'Start'가 첫 표시 뒤에야 돈다. 그 전에 탭을 골라야 한 프레임이라도 빈 창을 보이지 않는다.
    public void Open(int buildingTid, Tab tab)
    {
        Wire();

        _buildingTid = buildingTid;

        window.SetActive(true);

        if (tab == Tab.Trait && !GameDataLoader.HasBuildingNodes(buildingTid))
        {
            tab = Tab.Upgrade; // 트리가 없다 — 업그레이드 탭의 안내가 왜인지 말한다
        }

        ShowTab(tab);
        Redraw();
    }

    // 버튼과 데이터 구독을 한 번만 건다 (Open에서 호출). 버튼은 이 창이 닫혀 있어도 쥐고 있어도 된다.
    private void Wire()
    {
        if (_isWired)
        {
            return;
        }

        _isWired = true;

        this.RequireRef(window,           nameof(window));
        this.RequireRef(nameText,         nameof(nameText));
        this.RequireRef(levelText,        nameof(levelText));
        this.RequireRef(categoryText,     nameof(categoryText));
        this.RequireRef(upgradeTabButton, nameof(upgradeTabButton));
        this.RequireRef(traitTabButton,   nameof(traitTabButton));
        this.RequireRef(closeButton,      nameof(closeButton));
        this.RequireRef(upgradePage,      nameof(upgradePage));
        this.RequireRef(traitPage,        nameof(traitPage));

        _data = Services.Get<PlayerDataModel>();

        upgradeTabButton.onClick.AddListener(() => ShowTab(Tab.Upgrade));
        traitTabButton.onClick.AddListener(() => ShowTab(Tab.Trait));
        closeButton.onClick.AddListener(Close);
        BackdropClose.Bind(window, Close); // 창 뒤 어두운 바탕('Building Window' 자신)
    }

    // 켜져 있는 동안만 머리 줄을 구독한다 (Unity 메시지)
    private void OnEnable()
    {
        if (!_isWired)
        {
            return;
        }

        _data.BuildingsChanged += Redraw;
        Lang.Changed           += Redraw;
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        if (!_isWired)
        {
            return;
        }

        _data.BuildingsChanged -= Redraw;
        Lang.Changed           -= Redraw;
    }

    public bool IsOpen => window.activeSelf;

    // 창을 닫는다 — [X] · 창 뒤 바탕 · 화면이 닫힐 때('OutpostMapPresenter' — 다시 열었을 때 지도부터 보이게)
    public void Close()
    {
        window.SetActive(false);
    }

    private void ShowTab(Tab tab)
    {
        bool isTrait = tab == Tab.Trait;

        if (isTrait)
        {
            traitPage.Show(_buildingTid);
            upgradePage.Hide();
        }
        else
        {
            upgradePage.Show(_buildingTid);
            traitPage.Hide();
        }

        PaintTab(upgradeTabButton, !isTrait);
        PaintTab(traitTabButton, isTrait);

        traitTabButton.gameObject.SetActive(GameDataLoader.HasBuildingNodes(_buildingTid));
    }

    // 머리 줄을 그린다 (Open · 구독)
    private void Redraw()
    {
        if (!GameDataLoader.TryGetBuilding(_buildingTid, out var row))
        {
            return;
        }

        nameText.text     = Loc.Get(row.NameLocKey);
        levelText.text    = $"Lv{_data.GetBuildingLevel(_buildingTid)}";
        categoryText.text = Lang.Get($"UI.Outpost.Category.{_buildingTid}");
    }

    // 탭 버튼 색 — 'Selectable'이 'Image.color'를 덮으므로 'ColorBlock'을 칠한다('TraitNodeView.ApplyState'와 같은 이유).
    private void PaintTab(Button button, bool isSelected)
    {
        Color      color  = UIThemePalette.Of(isSelected ? tabSelectedRole : tabRole);
        ColorBlock colors = button.colors;

        colors.normalColor      = color;
        colors.highlightedColor = color;
        colors.pressedColor     = color;
        colors.selectedColor    = color;

        button.colors = colors;
    }
}

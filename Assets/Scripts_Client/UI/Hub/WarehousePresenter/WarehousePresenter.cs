using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 거점 열 창고 탭의 화면 — 종류별 3탭(자원·캐릭터·장비) + 정렬(방향 ▼▲ · 기준) + 창고 격자 (T-126).
//
// ■ 격자는 인벤토리 격자와 같은 컴포넌트다
//   'InventoryGridPresenter'를 'container = Warehouse'로 둔 것이라 칸·스크롤·끌기·정렬이 그대로다.
//   인벤토리 격자와 서로 끌어 넣고 꺼낸다 — 판정과 요청은 격자가 한다('InventoryGridPresenter'의 "다른 격자에 놓으면" 주석).
//
// ■ 탭은 인벤토리 탭과 늘 같이 바뀐다 (2026-10-06 사용자 결정 → 이슈 #58)
//   맞닿은 두 격자가 같은 종류여야 끌어 넘길 수 있다. 전환의 주인은 'InventoryTabPresenter' 하나다 —
//   여기 탭 버튼은 그쪽에 요청하고('RequestTab'), 그쪽이 바뀌면 'TabChanged'로 따라온다.
//   두 곳이 각자 탭을 들면 한쪽만 바뀌는 순간이 생긴다.
//   - 인벤토리가 특성 탭이면 창고는 보던 탭에 머문다 — 특성에는 창고가 없다.
//
// ※ 화면(page)이 꺼졌다 켜진다('HubTabPresenter'). 꺼져 있는 동안은 따라가지 않고, 켜질 때 지금 탭을 읽어 맞춘다.
public class WarehousePresenter : MonoBehaviour
{
    // 탭 하나 — 버튼과 그 버튼이 여는 탭. 인스펙터에서 짝지어 넣는다.
    [Serializable]
    private struct TabEntry
    {
        [Tooltip("이 줄이 어느 탭인가 (자원·캐릭터·장비 — 특성은 넣지 않는다)")]
        public InventoryTab tab;

        [Tooltip("그 탭의 버튼 (씬 왼쪽부터 자원·캐릭터·장비)")]
        public Button button;
    }

    // ※ NonReorderable — 목록 순서를 화면·enum과 나란히 두고, [CenterHeader]가 건너뛰어지지 않게 한다('UI 규칙.md').
    [CenterHeader("참조")]
    [SerializeField, NonReorderable, Tooltip("탭 버튼들. 자원·캐릭터·장비마다 한 줄씩, 화면과 같은 순서로 넣는다")]
    private TabEntry[] tabs = new TabEntry[0];

    [SerializeField, Tooltip("창고 격자 — 이 화면 안의 Grid Presenter (Container = Warehouse)")]
    private InventoryGridPresenter grid = null!;

    [SerializeField, Tooltip("인벤토리 열의 탭 줄 — 탭을 함께 바꾼다")]
    private InventoryTabPresenter inventoryTabs = null!;

    [SerializeField, Tooltip("정렬 방향 버튼 — 누르면 ▼↔▲를 뒤집고 다시 줄 세운다. OnClick은 코드가 연결한다")]
    private Button sortButton = null!;

    [SerializeField, Tooltip("정렬 방향 화살표 — ▼(높은 순) · ▲(낮은 순)")]
    private TMP_Text sortArrowText = null!;

    [SerializeField, Tooltip("정렬 기준 — 등급순 · 이름순 · 수량순(자원 탭만). 목록은 코드가 채운다")]
    private TMP_Dropdown sortKeyDropdown = null!;


    [CenterHeader("선택 표시")]
    [SerializeField, Tooltip("지금 열려 있는 탭의 버튼 색")]
    private UIThemeRole selectedRole = UIThemeRole.ButtonSelected;

    [SerializeField, Tooltip("열려 있지 않은 탭의 버튼 색")]
    private UIThemeRole normalRole = UIThemeRole.Button;

    // 정렬 기준 드롭다운의 문구. 'InventorySortKey' 순서와 같다('InventoryToolPresenter'와 같은 문구).
    private static readonly string[] SortKeyLabels = { "등급순", "이름순", "수량순" };

    // 지금 창고가 보이는 탭. 특성 탭은 되지 않는다.
    private InventoryTab _tab = InventoryTab.Resource;

    // 정렬 방향·기준 — 인벤토리와 따로 든다(맡겨 둔 것과 들고 다니는 것은 보고 싶은 순서가 다를 수 있다).
    private InventorySortOrder _order   = InventorySortOrder.Descending;
    private InventorySortKey   _sortKey = InventorySortKey.Rarity;

    // 드롭다운 줄 번호 → 기준. 탭마다 쓸 수 있는 기준이 달라 줄 번호와 enum이 어긋난다.
    private readonly List<InventorySortKey> _sortKeyOptions = new List<InventorySortKey>();

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 배선 → 초기화 순서로 진행한다 (클라 공통 규약)
    private void Start()
    {
        this.RequireRef(grid,          nameof(grid));
        this.RequireRef(inventoryTabs, nameof(inventoryTabs));
        this.RequireRef(sortButton,      nameof(sortButton));
        this.RequireRef(sortArrowText,   nameof(sortArrowText));
        this.RequireRef(sortKeyDropdown, nameof(sortKeyDropdown));

        BindButtons();
        RefreshArrow();

        _isReady = true;

        Subscribe();
        Follow(inventoryTabs.CurrentTab);
    }

    // 껐다 켠 경우 — 꺼져 있던 사이 인벤토리 탭이 바뀌었을 수 있다 (Unity 메시지)
    private void OnEnable()
    {
        if (!_isReady)
        {
            return;
        }

        Subscribe();
        Follow(inventoryTabs.CurrentTab);
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
    }

    #region 구독

    // 인벤토리 탭 전환 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed = true;
        inventoryTabs.TabChanged += Follow;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed = false;
        inventoryTabs.TabChanged -= Follow;
    }

    #endregion

    #region 탭

    // 탭 버튼 · [정렬] 버튼을 배선한다 (Start에서 호출).
    private void BindButtons()
    {
        foreach (TabEntry entry in tabs)
        {
            if (entry.button == null)
            {
                continue;
            }

            // 반복 변수를 그대로 넘기면 모든 콜백이 마지막 값을 본다. 복사본을 캡처한다.
            InventoryTab tab = entry.tab;
            entry.button.onClick.AddListener(() => OnTabClicked(tab));
        }

        sortButton.onClick.AddListener(OnSortClicked);
        sortKeyDropdown.onValueChanged.AddListener(OnSortKeyChanged);
    }

    // 탭을 눌렀다 — 인벤토리 탭에 요청한다. 바뀌면 'TabChanged'로 돌아와 여기도 바뀐다 (탭 버튼 OnClick에 코드로 연결)
    // ※ 인벤토리 쪽이 떠나기를 막으면(큐브 창 결과 고르기 전) 둘 다 그대로다 — 선택 색만 되돌린다.
    private void OnTabClicked(InventoryTab tab)
    {
        inventoryTabs.RequestTab(tab);
        RefreshSelection();
    }

    // 인벤토리 탭을 따라간다 (InventoryTabPresenter.TabChanged 구독 · Start · OnEnable)
    // 특성 탭이면 보던 탭에 머문다 — 특성에는 창고가 없다.
    private void Follow(InventoryTab tab)
    {
        if (tab != InventoryTab.Trait)
        {
            _tab = tab;
        }

        grid.ShowTab(_tab);
        RefreshSelection();
        RebuildSortKeyOptions();
    }

    // 지금 탭의 버튼만 선택 색으로 칠한다 (Follow · OnTabClicked에서 호출).
    // ⚠️ Image.color를 직접 건드리지 않는다 — 버튼의 Transition이 ColorTint라 다음 상태 변화에 덮어써진다('InventoryTabPresenter'와 같다).
    private void RefreshSelection()
    {
        foreach (TabEntry entry in tabs)
        {
            if (entry.button == null)
            {
                continue;
            }

            Color target = UIThemePalette.Of(entry.tab == _tab ? selectedRole : normalRole);

            ColorBlock colors = entry.button.colors;
            colors.normalColor   = target;
            colors.selectedColor = target;
            entry.button.colors  = colors;
        }
    }

    #endregion

    #region 정렬

    // 정렬은 인벤토리 도구 줄과 똑같이 동작한다('InventoryToolPresenter' 정렬 구역) — 규칙은 서버가 쥔다.

    // 정렬 방향을 뒤집고 지금 탭을 다시 줄 세운다 (sortButton OnClick에 코드로 연결)
    private void OnSortClicked()
    {
        _order = _order == InventorySortOrder.Descending
            ? InventorySortOrder.Ascending
            : InventorySortOrder.Descending;

        RefreshArrow();
        grid.SortCurrent(_sortKey, _order);
    }

    // 정렬 기준을 골랐다 — 지금 방향 그대로 다시 줄 세운다 (sortKeyDropdown onValueChanged에 코드로 연결)
    private void OnSortKeyChanged(int index)
    {
        if (index < 0 || index >= _sortKeyOptions.Count)
        {
            return;
        }

        _sortKey = _sortKeyOptions[index];
        grid.SortCurrent(_sortKey, _order);
    }

    // 이 탭이 쓸 수 있는 기준만 드롭다운에 세운다 (Follow에서 호출).
    // 쓸 수 없는 기준은 감춘다(캐릭터·장비의 수량순). 지금 기준을 못 쓰면 등급으로 돌린다 — 줄 세우기는 하지 않는다.
    private void RebuildSortKeyOptions()
    {
        _sortKeyOptions.Clear();

        var labels = new List<string>();

        foreach (InventorySortKey key in (InventorySortKey[])Enum.GetValues(typeof(InventorySortKey)))
        {
            if (key == InventorySortKey.Count && !grid.CurrentSupportsSortKey(key))
            {
                continue;
            }

            _sortKeyOptions.Add(key);
            labels.Add(SortKeyLabels[(int)key]);
        }

        if (!_sortKeyOptions.Contains(_sortKey))
        {
            _sortKey = InventorySortKey.Rarity;
        }

        sortKeyDropdown.ClearOptions();
        sortKeyDropdown.AddOptions(labels);
        sortKeyDropdown.SetValueWithoutNotify(_sortKeyOptions.IndexOf(_sortKey));
        sortKeyDropdown.RefreshShownValue();
    }

    // 화살표를 지금 방향으로 맞춘다 (Start · 방향을 바꿀 때). ▼가 높은 순이다.
    private void RefreshArrow()
    {
        sortArrowText.text = _order == InventorySortOrder.Descending ? "▼" : "▲";
    }

    #endregion
}

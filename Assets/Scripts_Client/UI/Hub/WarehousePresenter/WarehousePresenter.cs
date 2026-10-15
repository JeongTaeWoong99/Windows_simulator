using System;
using UnityEngine;
using UnityEngine.UI;

// 거점 열 창고 탭의 화면 — 종류별 3탭(자원·캐릭터·장비) + [정렬] + 창고 격자 (T-126).
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

    [SerializeField, Tooltip("[정렬] 버튼 — 지금 탭을 등급순으로 줄 세운다(빈 칸도 메운다)")]
    private Button sortButton = null!;


    [CenterHeader("선택 표시")]
    [SerializeField, Tooltip("지금 열려 있는 탭의 버튼 색")]
    private UIThemeRole selectedRole = UIThemeRole.ButtonSelected;

    [SerializeField, Tooltip("열려 있지 않은 탭의 버튼 색")]
    private UIThemeRole normalRole = UIThemeRole.Button;

    // 지금 창고가 보이는 탭. 특성 탭은 되지 않는다.
    private InventoryTab _tab = InventoryTab.Resource;

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 배선 → 초기화 순서로 진행한다 (클라 공통 규약)
    private void Start()
    {
        this.RequireRef(grid,          nameof(grid));
        this.RequireRef(inventoryTabs, nameof(inventoryTabs));
        this.RequireRef(sortButton,    nameof(sortButton));

        BindButtons();

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

    // [정렬] — 지금 탭을 등급 높은 순으로 줄 세운다. 규칙은 서버가 쥔다(인벤토리 [정렬]과 같다) (sortButton OnClick에 코드로 연결)
    // ※ 기준·방향을 고르는 줄은 두지 않는다 — 맡겨 두는 곳이라 정리 한 번이면 된다. 필요해지면 인벤토리 도구 줄을 닮게 늘린다.
    private void OnSortClicked()
    {
        grid.SortCurrent(InventorySortKey.Rarity, InventorySortOrder.Descending);
    }

    #endregion
}

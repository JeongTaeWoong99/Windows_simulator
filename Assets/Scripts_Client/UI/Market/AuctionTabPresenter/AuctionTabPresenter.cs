using System;
using UnityEngine;
using UnityEngine.UI;

// 경매장 탭 안의 하위 탭. 값의 순서는 **화면의 탭 순서와 같게 유지한다**.
//
// ⚠️ 씬에는 이 enum이 int로 저장돼 있다 — 중간에 끼우거나 재정렬하면 씬 배선을 함께 고친다('MarketTab'과 같은 규칙).
//   2026-09-30 거래소·장비·등록·내 매물(4개) → 구매·등록·내 매물(3개)로 바꾸며 씬을 다시 배선했다.
public enum AuctionTab
{
    // 구매 — 자원·장비·캐릭터 축을 골라 산다('AuctionBuyTabPresenter'). 기본 탭이다.
    Buy,

    // 등록 — 인벤토리의 자원·장비를 골라 올린다('AuctionRegisterPresenter').
    Register,

    // 내 매물 — 올린 것을 보고 취소한다('AuctionMyListingPresenter').
    MyListings,
}

// 경매장 탭의 하위 탭 줄 — 구매 · 등록 · 내 매물 (씬 왼쪽부터).
//
// 파는 축(자원·장비·캐릭터)이 셋이지만 **하는 일**(사기·올리기·관리)로 탭을 나눈다 — 축으로 나누면 축이 늘 때마다 탭이 는다.
// 축은 [구매] 안의 줄('AuctionBuyTabPresenter')과 [등록]의 종류 드롭다운이 고른다.
//
// 탭마다 화면(Presenter)이 따로 있고 지금 탭의 화면만 켠다. 모양은 'MarketTabPresenter'와 같다.
// 화면들은 이 오브젝트의 자식이 아니라 형제다 — Presenter 안에 Presenter를 두지 않는다('UI 규칙.md').
//
// ⚠️ 화면은 꺼졌다 켜진다 — 각 Presenter는 'OnEnable'/'OnDisable'로 구독을 잇고 끊는다.
//   요청 중 탭을 바꾸는 일은 대기 차단이 막는다('Market 규칙.md').
public class AuctionTabPresenter : MonoBehaviour
{
    // 경매장 탭을 처음 열었을 때의 하위 탭.
    private const AuctionTab DefaultTab = AuctionTab.Buy;

    // 탭 하나 — 버튼과 그 버튼이 여는 화면. 인스펙터에서 짝지어 넣는다.
    [Serializable]
    private struct TabEntry
    {
        [Tooltip("이 줄이 어느 탭인가")]
        public AuctionTab tab;

        [Tooltip("그 탭의 버튼 (씬 왼쪽부터 구매·등록·내 매물)")]
        public Button button;

        [Tooltip("그 탭을 누르면 켜지는 화면. 나머지 탭의 화면은 꺼진다")]
        public GameObject page;
    }

    // ※ NonReorderable — 목록 순서를 화면·enum과 나란히 두고, [CenterHeader]가 건너뛰어지지 않게 한다('UI 규칙.md').
    [CenterHeader("참조")]
    [SerializeField, NonReorderable, Tooltip("탭 버튼과 화면. AuctionTab 값마다 정확히 한 줄씩, 화면과 같은 순서로 넣는다")]
    private TabEntry[] tabs = new TabEntry[0];


    [CenterHeader("선택 표시")]
    [SerializeField, Tooltip("지금 열려 있는 탭의 버튼 색")]
    private UIThemeRole selectedRole = UIThemeRole.ButtonSelected;

    [SerializeField, Tooltip("열려 있지 않은 탭의 버튼 색")]
    private UIThemeRole normalRole = UIThemeRole.Button;

    // 지금 열려 있는 하위 탭. 경매장 탭을 닫아도 유지된다.
    public AuctionTab CurrentTab { get; private set; } = DefaultTab;

    // 참조 확보 → 배선 → 초기화 순서로 진행한다 (클라 공통 규약)
    private void Start()
    {
        ValidateTabs();
        BindButtons();
        ShowTab(DefaultTab);
    }

    #region 초기화

    // 인스펙터 배선이 'AuctionTab'과 맞는지 본다 (Start에서 한 번).
    // 빠진 탭은 눌러도 아무 일이 없어 고장처럼 보인다 — 원인이 인스펙터라는 걸 먼저 알린다.
    private void ValidateTabs()
    {
        foreach (AuctionTab tab in Enum.GetValues(typeof(AuctionTab)))
        {
            int count = 0;

            foreach (TabEntry entry in tabs)
            {
                if (entry.tab == tab && entry.button != null && entry.page != null)
                {
                    count++;
                }
            }

            if (count != 1)
            {
                ClientLogger.Error(ClientLogger.UI,
                    $"경매장 하위 탭 '{tab}'의 버튼·화면 짝이 {count}개다 (정확히 1개여야 한다). " +
                    "Auction Tab Presenter의 Tabs를 확인할 것.", this);
            }
        }
    }

    // 탭 버튼을 배선한다 (Start에서 호출).
    private void BindButtons()
    {
        foreach (TabEntry entry in tabs)
        {
            if (entry.button == null)
            {
                continue;
            }

            // 반복 변수를 그대로 넘기면 모든 콜백이 마지막 값을 본다. 복사본을 캡처한다.
            AuctionTab tab = entry.tab;
            entry.button.onClick.AddListener(() => ShowTab(tab));
        }
    }

    #endregion

    #region 탭 전환

    // 그 탭의 화면만 켜고 버튼 선택 표시를 맞춘다 (Start · 탭 버튼).
    // ⚠️ Image.color를 직접 건드리지 않는다 — 색의 주인은 ColorBlock이다('MarketTabPresenter'와 같다).
    private void ShowTab(AuctionTab tab)
    {
        CurrentTab = tab;

        foreach (TabEntry entry in tabs)
        {
            bool on = entry.tab == tab;

            if (entry.page != null)
            {
                entry.page.SetActive(on);
            }

            if (entry.button == null)
            {
                continue;
            }

            Color target = UIThemePalette.Of(on ? selectedRole : normalRole);

            ColorBlock colors = entry.button.colors;
            colors.normalColor   = target;
            colors.selectedColor = target;
            entry.button.colors  = colors;
        }
    }

    #endregion
}

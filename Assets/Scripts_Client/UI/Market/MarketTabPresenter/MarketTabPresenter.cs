using System;
using UnityEngine;
using UnityEngine.UI;

// 거래 열의 탭. 값의 순서는 **화면의 탭 순서와 같게 유지한다** — 코드만 읽어도 화면이 그려져야 한다.
//
// ⚠️ 씬에는 이 enum이 int로 저장돼 있다 — 중간에 끼우거나 재정렬하면 씬 배선을 함께 고친다.
//   늘리기만 할 때는 끝에 붙이면 씬을 안 건드려도 된다('InventoryTab'과 같은 규칙).
public enum MarketTab
{
    // 뽑기 — 골드로 자원·캐릭터를 뽑는다('GachaPresenter'). 지금 유일하게 동작하는 탭이라 기본 탭이다.
    Gacha,

    // 경매장 — 거래소·장비·등록·내 매물 하위 탭 넷('AuctionTabPresenter'). T-096.
    Auction,

    // 창고 — 자원·장비·캐릭터를 두고두고 맡겨 두는 곳(인벤토리는 얻은 것이 바로 들어오는 곳). 서버 패킷 전이라 자리만 있다.
    // ※ 인벤토리(옛 이름 창고/Storage)와 헷갈리지 않게 코드 이름은 'Warehouse'다.
    Warehouse,
}

// 거래 열의 탭 줄 — 뽑기 · 경매장 · 창고 3탭 (씬 왼쪽부터).
//
// 탭마다 화면(page)이 따로 있고, 지금 탭의 화면만 켠다. 인벤토리 탭과 달리 격자 하나를
// 갈아 끼우는 구조가 아니다 — 세 화면이 내용도 모양도 다르다.
//
// ■ 아직 기능이 없는 탭도 누를 수 있다
//   화면에 '(기능 없음)'이 적혀 있어 고장과 구분된다(설정 화면의 '(기능 없음)' 줄과 같은 표기).
//   기능을 붙일 때는 그 page 안에 Presenter를 넣고 표시 글씨를 지우면 된다 — 이 파일은 안 바뀐다.
//
// ⚠️ page는 꺼졌다 켜진다 — page 안의 Presenter는 'OnEnable'/'OnDisable'로 구독을 잇고 끊어야 한다
//   ('GachaPresenter'가 이미 그렇게 한다). 요청 중 탭을 바꾸는 일은 대기 차단이 막는다('Market 규칙.md').
public class MarketTabPresenter : MonoBehaviour
{
    // 거래 열을 처음 열었을 때의 탭.
    private const MarketTab DefaultTab = MarketTab.Gacha;

    // 탭 하나 — 버튼과 그 버튼이 여는 화면. 인스펙터에서 짝지어 넣는다.
    [Serializable]
    private struct TabEntry
    {
        [Tooltip("이 줄이 어느 탭인가")]
        public MarketTab tab;

        [Tooltip("그 탭의 버튼 (씬 왼쪽부터 뽑기·경매장·창고)")]
        public Button button;

        [Tooltip("그 탭을 누르면 켜지는 화면. 나머지 탭의 화면은 꺼진다")]
        public GameObject page;
    }

    // ※ NonReorderable — 목록 순서를 화면·enum과 나란히 두고, [CenterHeader]가 건너뛰어지지 않게 한다('UI 규칙.md').
    [CenterHeader("참조")]
    [SerializeField, NonReorderable, Tooltip("탭 버튼과 화면. MarketTab 값마다 정확히 한 줄씩, 화면과 같은 순서로 넣는다")]
    private TabEntry[] tabs = new TabEntry[0];


    [CenterHeader("선택 표시")]
    [SerializeField, Tooltip("지금 열려 있는 탭의 버튼 색")]
    private UIThemeRole selectedRole = UIThemeRole.ButtonSelected;

    [SerializeField, Tooltip("열려 있지 않은 탭의 버튼 색")]
    private UIThemeRole normalRole = UIThemeRole.Button;

    // 지금 열려 있는 탭. 거래 열을 닫아도 유지된다 — 다시 열면 보던 탭이 그대로 있다.
    public MarketTab CurrentTab { get; private set; } = DefaultTab;

    private bool _hasRequestedTab; // Start 전에 밖에서 탭을 골랐나 — Start가 기본 탭으로 덮지 않게 한다

    // 참조 확보 → 배선 → 초기화 순서로 진행한다 (클라 공통 규약)
    // ※ 밖에서 'ShowTab'을 Start보다 먼저 불렀으면 그 탭을 지킨다(처음 여는 순간의 인벤토리 [경매 등록]).
    private void Start()
    {
        ValidateTabs();
        BindButtons();
        ShowTab(_hasRequestedTab ? CurrentTab : DefaultTab);
    }

    #region 초기화

    // 인스펙터 배선이 'MarketTab'과 맞는지 본다 (Start에서 한 번).
    // 빠진 탭은 눌러도 아무 일이 없어 고장처럼 보인다 — 원인이 인스펙터라는 걸 먼저 알린다.
    private void ValidateTabs()
    {
        foreach (MarketTab tab in Enum.GetValues(typeof(MarketTab)))
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
                    $"거래 탭 '{tab}'의 버튼·화면 짝이 {count}개다 (정확히 1개여야 한다). " +
                    "Market Tab Presenter의 Tabs를 확인할 것.", this);
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
            MarketTab tab = entry.tab;
            entry.button.onClick.AddListener(() => ShowTab(tab));
        }
    }

    #endregion

    #region 탭 전환

    // 그 탭의 화면만 켜고 버튼 선택 표시를 맞춘다 (Start · 탭 버튼 · 'MarketCanvasView.OpenAuctionRegister').
    //
    // ⚠️ Image.color를 직접 건드리지 않는다 — 버튼의 Transition이 ColorTint라 다음 상태 변화에
    //   덮어써진다. 색의 주인은 ColorBlock이다('InventoryTabPresenter'와 같다).
    public void ShowTab(MarketTab tab)
    {
        CurrentTab       = tab;
        _hasRequestedTab = true;

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

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 경매장 탭. 값의 순서는 **화면의 탭 순서와 같게 유지한다**.
//
// ⚠️ 씬에는 이 enum이 int로 저장돼 있다 — 중간에 끼우거나 재정렬하면 씬 배선을 함께 고친다('GuildTab'과 같은 규칙).
//   2026-10-14 구매·등록·내 매물 → 검색·시세·판매·완료로 바꾸며 씬을 다시 배선했다(T-147 2단계 · 목업).
public enum AuctionTab
{
    // 검색 — 왼쪽 옵션(분류·등급·상세 옵션·단가) + 오른쪽 결과('AuctionSearchPresenter'). 기본 탭이다.
    Search,

    // 시세 — 한 아이템의 최저가·판매 중·최근가·전일 평균 · 14일 그래프 · 단가별 수량('AuctionPricePresenter').
    Price,

    // 판매 — 판매 중 목록 · [+ 판매 등록] 창('AuctionMyListingPresenter' · 'AuctionRegisterPresenter').
    Sell,

    // 완료 — 판매 완료·구매 완료·기간 만료·취소 기록('AuctionHistoryPresenter').
    History,
}

// 경매장 탭 줄 — 검색 · 시세 · 판매 · 완료 (씬 왼쪽부터). 화면 폭을 넷으로 나누고 칸마다 아이콘 + 글자.
//
// 탭마다 화면(Presenter)이 따로 있고 지금 탭의 화면만 켠다. 모양은 'GuildTabPresenter'와 같다.
// 화면들은 이 오브젝트의 자식이 아니라 형제다 — Presenter 안에 Presenter를 두지 않는다('UI 규칙.md').
//
// ⚠️ 화면은 꺼졌다 켜진다 — 각 Presenter는 'OnEnable'/'OnDisable'로 구독을 잇고 끊는다.
//   요청 중 탭을 바꾸는 일은 대기 차단이 막는다('Auction 규칙.md').
//
// ■ 완료 배지: 안 본 거래 기록 수('AuctionModel.HistoryUnseenCount')를 [완료] 버튼에 단다 — 완료 탭을 열면 모델이 0으로 만든다.
// ■ 'TabChanged': 공용 검색창('AuctionQueryPresenter')이 지금 탭에 맞춰 안내·검색어를 바꾼다.
public class AuctionTabPresenter : MonoBehaviour
{
    // 경매장을 처음 열었을 때의 탭.
    private const AuctionTab DefaultTab = AuctionTab.Search;

    // 배지에 적는 최대 수 — 넘으면 "99+".
    private const int MaxBadgeCount = 99;

    // 탭 하나 — 버튼과 그 버튼이 여는 화면. 인스펙터에서 짝지어 넣는다.
    [Serializable]
    private struct TabEntry
    {
        [Tooltip("이 줄이 어느 탭인가")]
        public AuctionTab tab;

        [Tooltip("그 탭의 버튼 (씬 왼쪽부터 검색·시세·판매·완료)")]
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


    [CenterHeader("배지")]
    [SerializeField, Tooltip("[완료] 버튼 위의 배지 — 안 본 거래 기록이 있을 때만 켜진다")]
    private GameObject historyBadge = null!;

    [SerializeField, Tooltip("배지 안의 수")]
    private TMP_Text historyBadgeText = null!;

    private AuctionModel _auction = null!;

    private bool _isSubscribed;
    private bool _isReady;         // Start 완료 여부 — OnEnable 재구독 가드
    private bool _hasRequestedTab; // Start 전에 밖에서 탭을 골랐나 — Start가 기본 탭으로 덮지 않게 한다

    // 지금 열려 있는 탭. 경매장을 닫아도 유지된다.
    public AuctionTab CurrentTab { get; private set; } = DefaultTab;

    // 탭이 바뀌었다(같은 탭을 다시 골라도 쏜다) — 공용 검색창이 구독한다.
    public event Action<AuctionTab>? TabChanged;

    // 참조 확보 → 구독 → 배선 → 초기화 순서로 진행한다 (클라 공통 규약)
    // ※ 밖에서 'ShowTab'을 Start보다 먼저 불렀으면 그 탭을 지킨다(처음 여는 순간의 Ctrl+우클릭 등록).
    private void Start()
    {
        this.RequireRef(historyBadge,     nameof(historyBadge));
        this.RequireRef(historyBadgeText, nameof(historyBadgeText));

        _auction = Services.Get<AuctionModel>();

        ValidateTabs();
        Subscribe();
        BindButtons();
        ShowTab(_hasRequestedTab ? CurrentTab : DefaultTab);
        RefreshBadge();

        _isReady = true;
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    private void OnEnable()
    {
        if (_isReady)
        {
            Subscribe();
            RefreshBadge();
        }
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
    }

    #region 구독

    // 거래 기록 배지 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed = true;

        _auction.HistoryBadgeChanged += RefreshBadge;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed = false;

        _auction.HistoryBadgeChanged -= RefreshBadge;
    }

    #endregion

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
                    $"경매장 탭 '{tab}'의 버튼·화면 짝이 {count}개다 (정확히 1개여야 한다). " +
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

    // 그 탭의 화면만 켜고 버튼 선택 표시를 맞춘다 (Start · 탭 버튼 · 밖에서 여는 길 'AuctionCanvasView').
    // ⚠️ Image.color를 직접 건드리지 않는다 — 색의 주인은 ColorBlock이다('GuildTabPresenter'와 같다).
    public void ShowTab(AuctionTab tab)
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

        TabChanged?.Invoke(tab);
    }

    // 그 탭의 화면 오브젝트. 배선이 빠졌으면 null.
    public GameObject? GetPage(AuctionTab tab)
    {
        foreach (TabEntry entry in tabs)
        {
            if (entry.tab == tab)
            {
                return entry.page;
            }
        }

        return null;
    }

    #endregion

    #region 배지

    // [완료] 배지를 켜고 끈다 (Start · OnEnable · AuctionModel.HistoryBadgeChanged 구독)
    private void RefreshBadge()
    {
        int count = _auction.HistoryUnseenCount;

        historyBadge.SetActive(count > 0);
        historyBadgeText.text = count > MaxBadgeCount ? $"{MaxBadgeCount}+" : count.ToString();
    }

    #endregion
}

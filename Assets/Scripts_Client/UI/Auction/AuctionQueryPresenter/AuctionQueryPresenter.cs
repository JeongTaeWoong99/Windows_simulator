using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 공용 검색창을 쓰는 탭 화면 — 'AuctionQueryPresenter'가 지금 탭의 화면에 입력을 넘긴다.
// 검색 탭은 매물 이름, 시세 탭은 볼 아이템, 판매 탭은 판매 중 매물, 완료 탭은 기록에서 찾는다.
public interface IAuctionQueryTarget
{
    // 입력칸이 비었을 때 보이는 안내 — "아이템 이름 (일부만 쳐도 된다)"
    string QueryPlaceholder { get; }

    // 입력칸 옆의 안내 — "[검색] — 분류·옵션과 함께 조회한다"
    string QueryHint { get; }

    // 이 탭이 기억하는 검색어 — 탭을 오가면 입력칸에 되살린다.
    string Query { get; }

    // 칠 때마다 — 판매·완료 탭은 받은 목록을 바로 거른다. 검색·시세 탭은 적어 두기만 한다.
    void OnQueryChanged(string text);

    // [검색] — 검색 탭은 조회, 시세 탭은 그 이름의 시세를 연다.
    void OnQuerySubmitted(string text);
}

// 경매장 공용 검색창 — 탭 줄 위에 하나만 두고 네 탭이 함께 쓴다(목업 · T-147).
//
// ■ 이 Presenter는 입력을 **지금 탭의 화면에 넘기기만** 한다 — 무엇을 할지는 그 화면('IAuctionQueryTarget')이 정한다.
//   탭이 바뀌면 그 화면의 안내 문구·검색어로 입력칸을 바꿔 끼운다.
// ■ Enter로 검색하지 않는다 — 인게임 키는 Shift+우클릭·Esc뿐이다(2026-10-11 사용자 결정 · 'Managers 규칙.md').
//   목업의 "이름 + Enter"는 [검색] 버튼으로 한다.
public class AuctionQueryPresenter : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("검색 입력칸 (폭 440)")]
    private TMP_InputField queryInput = null!;

    [SerializeField, Tooltip("[검색] 버튼. OnClick은 코드가 연결한다")]
    private Button searchButton = null!;

    [SerializeField, Tooltip("입력칸 옆 안내 — 지금 탭에서 [검색]이 무엇을 하는지")]
    private TMP_Text hintText = null!;

    [SerializeField, Tooltip("탭 줄 — 지금 탭과 그 화면을 묻는다")]
    private AuctionTabPresenter tabs = null!;

    private IAuctionQueryTarget? _target;

    // 참조 확보 → 구독 → 배선 → 초기화 순서로 진행한다 (클라 공통 규약)
    private void Start()
    {
        this.RequireRef(queryInput,   nameof(queryInput));
        this.RequireRef(searchButton, nameof(searchButton));
        this.RequireRef(hintText,     nameof(hintText));
        this.RequireRef(tabs,         nameof(tabs));

        tabs.TabChanged += OnTabChanged;

        queryInput.onValueChanged.AddListener(OnValueChanged);
        searchButton.onClick.AddListener(OnSearchClicked);

        OnTabChanged(tabs.CurrentTab);
    }

    // 탭 줄 구독 해제 — 둘은 같은 캔버스 안에서 함께 켜지고 꺼지므로 파괴될 때만 끊는다 (Unity 메시지)
    private void OnDestroy()
    {
        if (tabs != null)
        {
            tabs.TabChanged -= OnTabChanged;
        }
    }

    // 지금 탭의 화면으로 바꿔 끼운다 — 안내 문구 · 검색어 (AuctionTabPresenter.TabChanged 구독 · Start)
    private void OnTabChanged(AuctionTab tab)
    {
        GameObject? page = tabs.GetPage(tab);

        _target = page != null ? page.GetComponent<IAuctionQueryTarget>() : null;

        if (_target == null)
        {
            ClientLogger.Warn(ClientLogger.UI, $"경매장 탭 '{tab}'의 화면이 검색창을 받지 않는다 — IAuctionQueryTarget이 없다.", this);

            return;
        }

        if (queryInput.placeholder is TMP_Text placeholder)
        {
            placeholder.text = _target.QueryPlaceholder;
        }

        hintText.text = _target.QueryHint;
        queryInput.SetTextWithoutNotify(_target.Query);
    }

    // 칠 때마다 지금 탭에 넘긴다 (queryInput.onValueChanged)
    private void OnValueChanged(string text)
    {
        _target?.OnQueryChanged(text);
    }

    // [검색] — 지금 탭에 넘긴다 (searchButton OnClick)
    private void OnSearchClicked()
    {
        _target?.OnQuerySubmitted(queryInput.text);
    }
}

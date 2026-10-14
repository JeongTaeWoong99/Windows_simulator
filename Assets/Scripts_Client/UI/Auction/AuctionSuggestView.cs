using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 검색 후보 한 줄에 그릴 완성값 — 입력칸을 가진 Presenter가 만들어 'AuctionSuggestView.Show'에 넘긴다.
public struct AuctionSuggestion
{
    public EAuctionKind Kind;
    public int          Tid;
    public long         Key;       // 개체 번호(등록 창의 장비·캐릭터) — 아니면 0
    public string       Name;      // 고르면 입력칸에 들어갈 이름
    public Color        NameColor; // 등급 색
    public string       Sub;       // 오른쪽 흐린 글자 — 종류 · 분류 · 보유
}

// 입력칸 아래 검색 후보 목록 — 치는 대로 이름이 맞는 것을 띄우고, 고르면 위로 알린다 (2026-10-14 사용자 요청).
// 경매장 공용 검색창과 등록 창의 '이름으로 찾기'가 하나씩 갖는다. 무엇을 띄울지는 그 Presenter가 정한다 (종속 View).
//
// ■ 줄은 씬에 미리 만들어 둔 'rows'만 쓴다 — 후보가 많아도 그 수까지만 보인다(만들지도 부수지도 않는다).
// ■ 입력칸에서 포커스가 빠지면 닫는다. 단 **후보 줄을 누르는 중이면 닫지 않는다** —
//   누르는 순간 포커스가 줄로 옮겨 가 입력칸의 Deselect가 클릭보다 먼저 온다. 한 프레임 뒤 포커스가 어디 있는지 보고 정한다.
// ■ 씬에는 꺼 둔 채 둔다 — 처음 띄울 때 Awake가 돈다(그 전에는 닫을 것도 없다).
// ■ 다른 패널 위에 떠야 한다 — 자기 Canvas(override sorting)를 둔다. 레이아웃에서는 빠진다(LayoutElement.ignoreLayout).
//   ⚠️ 정렬 덮어쓰기는 캔버스가 켜져 있을 때만 먹는다 — 꺼진 채 저장된 씬 값은 풀려 있어 탭 버튼 뒤에 깔렸다. 켤 때마다 건다.
public class AuctionSuggestView : MonoBehaviour
{
    // 후보 한 줄 — 누르는 버튼과 두 글자.
    [Serializable]
    private struct Row
    {
        public Button   button;
        public TMP_Text nameText;
        public TMP_Text subText;
    }

    [CenterHeader("참조")]
    [SerializeField, Tooltip("이 목록을 띄우는 입력칸 — 포커스가 빠지면 닫는다")]
    private TMP_InputField input = null!;

    [SerializeField, NonReorderable, Tooltip("후보 줄 — 위부터. 이 수까지만 보인다. OnClick은 코드가 연결한다")]
    private Row[] rows = new Row[0];

    // 다른 패널 위에 뜨는 정렬 순서 — 오버레이 캔버스(100) 바로 위.
    private const int SortingOrder = 101;

    // 줄이 눌렸다 — 그 후보를 싣는다 (입력칸을 가진 Presenter가 구독).
    public event Action<AuctionSuggestion>? Picked;

    // 보일 수 있는 줄 수 — 후보를 이만큼만 모으면 된다.
    public int Capacity => rows.Length;

    private readonly List<AuctionSuggestion> _shown = new List<AuctionSuggestion>();

    private Canvas _canvas = null!;

    // 자기 줄·입력칸만 배선한다 — 서비스를 조회하지 않으므로 Awake로 충분하다 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(input, nameof(input));

        _canvas = GetComponent<Canvas>();
        this.RequireRef(_canvas, nameof(_canvas));

        for (int i = 0; i < rows.Length; i++)
        {
            int index = i; // 반복 변수를 그대로 잡으면 모든 콜백이 마지막 값을 본다

            this.RequireRef(rows[i].button,   $"rows[{i}].button");
            this.RequireRef(rows[i].nameText, $"rows[{i}].nameText");
            this.RequireRef(rows[i].subText,  $"rows[{i}].subText");

            rows[i].button.onClick.AddListener(() => OnRowClicked(index));
        }

        input.onDeselect.AddListener(_ => CloseIfFocusLeftAsync(destroyCancellationToken).Forget());
    }

    // 후보를 띄운다 — 비었으면 닫는다.
    public void Show(IReadOnlyList<AuctionSuggestion> suggestions)
    {
        _shown.Clear();

        for (int i = 0; i < suggestions.Count && i < rows.Length; i++)
        {
            _shown.Add(suggestions[i]);
        }

        if (_shown.Count == 0)
        {
            Hide();

            return;
        }

        for (int i = 0; i < rows.Length; i++)
        {
            bool on = i < _shown.Count;

            rows[i].button.gameObject.SetActive(on);

            if (!on)
            {
                continue;
            }

            rows[i].nameText.text  = _shown[i].Name;
            rows[i].nameText.color = _shown[i].NameColor;
            rows[i].subText.text   = _shown[i].Sub;
        }

        gameObject.SetActive(true);

        _canvas.overrideSorting = true;
        _canvas.sortingOrder    = SortingOrder;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    // 줄을 눌렀다 — 닫고 위로 알린다 (rows[i].button OnClick)
    private void OnRowClicked(int index)
    {
        if (index >= _shown.Count)
        {
            return;
        }

        AuctionSuggestion picked = _shown[index];

        Hide();
        Picked?.Invoke(picked);
    }

    // 입력칸의 포커스가 빠졌다 — 한 프레임 뒤에도 포커스가 이 목록 안에 없으면 닫는다 (input.onDeselect)
    private async UniTask CloseIfFocusLeftAsync(CancellationToken ct)
    {
        await UniTask.NextFrame(ct);

        GameObject? focused = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

        if (focused == null || (!focused.transform.IsChildOf(transform) && focused != input.gameObject))
        {
            Hide();
        }
    }
}

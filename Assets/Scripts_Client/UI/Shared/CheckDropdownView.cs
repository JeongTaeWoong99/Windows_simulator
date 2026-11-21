using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 여러 개를 골라 두는 드롭다운 — 머리를 누르면 체크 목록이 펼쳐지고, 줄을 눌러도 닫히지 않는다 (2026-10-15 · 인벤토리 일괄 담기 [제외]).
// 무엇을 띄우고 체크를 어디에 저장할지는 부르는 Presenter가 정한다 (종속 View).
//
// ■ 'TMP_Dropdown'을 쓰지 않는 이유 — 하나만 고르고, 고르는 순간 닫힌다. 여러 개를 연달아 켜고 끌 수 없다.
//   모양은 같게 둔다 — 씬의 목록·줄은 다른 드롭다운의 'Template'을 복사해 만들었다.
// ■ 줄은 처음 'SetOptions'에서 템플릿을 복제해 만든다 — 항목은 표 데이터에서 오고 거의 바뀌지 않는다(바뀌면 그 수만큼 더 만든다).
// ■ 바깥을 누르면 닫는다 — 펼칠 때 화면 전체를 덮는 투명 막(blocker)을 목록 바로 밑에 깐다('TMP_Dropdown'과 같은 방식).
//   막이 클릭을 삼키므로 닫는 클릭이 아래 칸까지 가지 않는다.
// ■ 다른 패널 위에 떠야 한다 — 목록에 자기 Canvas(override sorting)를 둔다.
//   ⚠️ 정렬 덮어쓰기는 캔버스가 켜져 있을 때만 먹는다 — 켤 때마다 건다('SuggestView'와 같다).
// ■ 묶음 머리 — 항목이 여러 종류면 종류마다 흐린 머리 줄('분류' · '산업')을 두고, 두 번째 묶음부터 위에 얇은 줄을 긋는다
//   (2026-11-21 사용자 "종류별로 보기 힘들다"). 칸을 따로 나누는 것보다 목록 하나를 유지해 높이·스크롤이 늘지 않는다.
//   머리 줄은 누를 수 없다 — 줄 템플릿을 복제하되 체크·바탕·Toggle을 뗀다.
public class CheckDropdownView : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("머리 버튼 — 누르면 목록을 열고 닫는다. OnClick은 코드가 연결한다")]
    private Button headButton = null!;

    [SerializeField, Tooltip("머리 글자 — 무엇을 적을지는 부르는 쪽이 정한다('SetHeadText')")]
    private TMP_Text headText = null!;

    [SerializeField, Tooltip("펼쳐지는 목록 — 자기 Canvas가 있어야 한다. 씬에는 꺼 둔다")]
    private RectTransform list = null!;

    [SerializeField, Tooltip("줄 템플릿 — 체크 그림을 graphic으로 가진 Toggle. 꺼 둔다. 복제본이 템플릿 옆에 쌓인다")]
    private Toggle rowTemplate = null!;

    [CenterHeader("크기")]
    [SerializeField, Min(1f), Tooltip("줄 하나의 높이 — 목록 높이 = 줄 수 × 이 값 + 여백")]
    private float rowHeight = 24f;

    [SerializeField, Min(1f), Tooltip("묶음 머리 줄의 높이")]
    private float headerHeight = 18f;

    [SerializeField, Min(1f), Tooltip("목록이 이보다 길어지면 스크롤한다")]
    private float maxListHeight = 260f;

    // 다른 패널 위에 뜨는 정렬 순서 — 오버레이 캔버스(100) 바로 위. 막은 그 바로 밑.
    private const int ListSortingOrder    = 101;
    private const int BlockerSortingOrder = 100;

    // 목록 위아래 여백 합 — 줄 높이 합과 딱 같으면 소수점 차이로 스크롤바가 뜬다, 1을 더 둔다.
    private const float ListPadding = 5f;

    // 줄의 체크가 바뀌었다 — (항목 번호, 켜짐) (부르는 Presenter가 구독).
    public event Action<int, bool>? Toggled;

    private readonly List<Toggle>   _rows = new List<Toggle>();
    private readonly List<TMP_Text> _rowTexts = new List<TMP_Text>();
    private readonly List<Header>   _headers  = new List<Header>();

    // 묶음 머리 줄 하나 — 줄 템플릿 복제본(누를 수 없음) + 위 구분선
    private sealed class Header
    {
        public GameObject Root  = null!;
        public TMP_Text   Label = null!;
        public GameObject Line  = null!;
    }

    private Canvas      _listCanvas = null!;
    private float       _labelExtra; // 목록 너비 − 줄 글자 칸 너비 (체크 자리 · 틀 안쪽) — 씬에 놓인 그대로 한 번 잰다
    private int         _shownRows;
    private int         _shownHeaders;
    private GameObject? _blocker;

    public bool IsOpen => list.gameObject.activeSelf;

    // 펼쳐지는 목록 — 'UiSkin'이 틀 안쪽으로 줄을 들인다
    public RectTransform ListRect => list;

    // 자기 참조만 확인하고 배선한다 — 서비스를 조회하지 않으므로 Awake로 충분하다 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(headButton,  nameof(headButton));
        this.RequireRef(headText,    nameof(headText));
        this.RequireRef(list,        nameof(list));
        this.RequireRef(rowTemplate, nameof(rowTemplate));

        _listCanvas = list.GetComponent<Canvas>();
        this.RequireRef(_listCanvas, nameof(_listCanvas));

        TMP_Text? templateLabel = rowTemplate.GetComponentInChildren<TMP_Text>(true);
        _labelExtra = templateLabel != null ? list.rect.width - templateLabel.rectTransform.rect.width : 0f;

        rowTemplate.gameObject.SetActive(false);
        list.gameObject.SetActive(false);
        headButton.onClick.AddListener(OnHeadClicked);
    }

    // 꺼질 때(탭 전환·창 닫기) 열린 목록과 막을 함께 걷는다 (Unity 메시지)
    private void OnDisable()
    {
        Hide();
    }

    private void OnDestroy()
    {
        if (_blocker != null)
        {
            Destroy(_blocker);
        }
    }

    // 항목 이름과 체크 상태를 채운다 — 'labels'와 'isChecked'는 같은 길이다.
    //   sections : 묶음 (시작 항목 번호, 머리 글자) — 시작 번호 오름차순. null이면 머리 없이 늘어놓는다.
    public void SetOptions(IReadOnlyList<string> labels, IReadOnlyList<bool> isChecked,
                           IReadOnlyList<(int Start, string Title)>? sections = null)
    {
        while (_rows.Count < labels.Count)
        {
            Toggle row   = Instantiate(rowTemplate, rowTemplate.transform.parent);
            int    index = _rows.Count; // 반복 변수를 그대로 잡으면 모든 콜백이 마지막 값을 본다

            row.onValueChanged.AddListener(on => Toggled?.Invoke(index, on));
            _rows.Add(row);
            _rowTexts.Add(row.GetComponentInChildren<TMP_Text>(true));
        }

        for (int i = 0; i < _rows.Count; i++)
        {
            bool on = i < labels.Count;

            _rows[i].gameObject.SetActive(on);

            if (!on)
            {
                continue;
            }

            _rowTexts[i].text = labels[i];
            _rows[i].SetIsOnWithoutNotify(isChecked[i]);
        }

        _shownRows = labels.Count;
        ArrangeSections(sections);
        FitList();
    }

    // 묶음 머리 줄을 세우고, 머리 · 줄을 보이는 순서대로 다시 쌓는다 (SetOptions에서 호출).
    private void ArrangeSections(IReadOnlyList<(int Start, string Title)>? sections)
    {
        int count = sections?.Count ?? 0;

        while (_headers.Count < count)
        {
            _headers.Add(CreateHeader());
        }

        for (int h = 0; h < _headers.Count; h++)
        {
            bool on = h < count;

            _headers[h].Root.SetActive(on);

            if (on)
            {
                _headers[h].Label.text = UIRichText.Paint(sections![h].Title, UIThemeRole.TextDisabled);
                _headers[h].Line.SetActive(h > 0); // 첫 묶음 위에는 긋지 않는다 — 목록 틀이 이미 있다
            }
        }

        _shownHeaders = count;

        // 쌓는 순서 — 템플릿(꺼짐) 뒤에 [머리 0] 줄… [머리 1] 줄… 차례로
        int sibling = rowTemplate.transform.GetSiblingIndex() + 1;
        int next    = 0;

        for (int i = 0; i < _shownRows; i++)
        {
            while (next < count && sections![next].Start == i)
            {
                _headers[next++].Root.transform.SetSiblingIndex(sibling++);
            }

            _rows[i].transform.SetSiblingIndex(sibling++);
        }
    }

    // 머리 줄 하나를 만든다 — 줄 템플릿을 복제해 체크 · 바탕 · Toggle을 떼고 높이를 줄인다 (ArrangeSections에서 호출).
    private Header CreateHeader()
    {
        Toggle copy = Instantiate(rowTemplate, rowTemplate.transform.parent);

        copy.name = "Section Header (코드 생성)";

        if (copy.graphic != null)
        {
            copy.graphic.gameObject.SetActive(false); // 체크 그림
        }

        if (copy.targetGraphic != null && copy.targetGraphic.gameObject != copy.gameObject)
        {
            copy.targetGraphic.gameObject.SetActive(false); // 올렸을 때 바탕
        }

        GameObject root = copy.gameObject;

        Destroy(copy); // 누를 수 없게 — Toggle만 뗀다

        if (root.TryGetComponent(out LayoutElement layout))
        {
            layout.minHeight       = headerHeight;
            layout.preferredHeight = headerHeight;
        }

        TMP_Text label = root.GetComponentInChildren<TMP_Text>(true);

        // 머리는 줄 글자보다 왼쪽에서 — 체크 자리만큼 당긴다
        label.rectTransform.offsetMin = new Vector2(4f, label.rectTransform.offsetMin.y);

        var lineGo = new GameObject("Line", typeof(RectTransform), typeof(Image));
        var line   = (RectTransform)lineGo.transform;

        line.SetParent(root.transform, false);
        line.anchorMin        = new Vector2(0f, 1f);
        line.anchorMax        = new Vector2(1f, 1f);
        line.pivot            = new Vector2(0.5f, 1f);
        line.offsetMin        = new Vector2(4f, -1f);
        line.offsetMax        = new Vector2(-4f, 0f);
        lineGo.GetComponent<Image>().color         = new Color(0f, 0f, 0f, 0.25f);
        lineGo.GetComponent<Image>().raycastTarget = false;

        root.SetActive(true);

        return new Header { Root = root, Label = label, Line = lineGo };
    }

    // 목록 크기 — 너비는 머리 이상 · 가장 긴 줄까지('DropdownListFit'), 높이는 줄 수만큼(+ 틀 안쪽 여백 · 'UiSkin.InsetList'). 열 때마다 다시 잰다 — 안 구운 씬은 틀이 씬이 열린 뒤에 입혀진다
    private void FitList()
    {
        list.anchorMin = new Vector2(0f, list.anchorMin.y);
        list.anchorMax = new Vector2(1f, list.anchorMax.y);

        var   viewport = (RectTransform)rowTemplate.transform.parent.parent;
        float frame    = list.rect.height - viewport.rect.height;
        float height   = Mathf.Min(_shownRows * rowHeight + _shownHeaders * headerHeight + ListPadding + frame, maxListHeight);

        list.sizeDelta = new Vector2(0f, height);

        var shown = new List<TMP_Text>();

        for (int i = 0; i < _shownRows && i < _rowTexts.Count; i++)
        {
            shown.Add(_rowTexts[i]);
        }

        DropdownListFit.SetWidth(list, ((RectTransform)transform).rect.width, DropdownListFit.Widest(shown) + _labelExtra);
    }

    public void SetHeadText(string text)
    {
        headText.text = text;
    }

    public void Hide()
    {
        if (list != null)
        {
            list.gameObject.SetActive(false);
        }

        if (_blocker != null)
        {
            _blocker.SetActive(false);
        }
    }

    // 머리를 눌렀다 — 열려 있으면 닫고, 닫혀 있으면 연다 (headButton OnClick)
    private void OnHeadClicked()
    {
        if (IsOpen)
        {
            Hide();

            return;
        }

        Show();
    }

    private void Show()
    {
        GameObject blocker = GetOrCreateBlocker();

        blocker.SetActive(true);
        blocker.GetComponent<Canvas>().overrideSorting = true;
        blocker.GetComponent<Canvas>().sortingOrder    = BlockerSortingOrder;

        FitList();
        list.gameObject.SetActive(true);
        _listCanvas.overrideSorting = true;
        _listCanvas.sortingOrder    = ListSortingOrder;
    }

    // 화면 전체를 덮는 투명 막 — 바깥 클릭을 받아 닫는다. 처음 열 때 루트 캔버스 밑에 만든다 (Show에서 호출).
    // ※ 루트 캔버스에 두는 이유 — 이 드롭다운의 부모 패널 크기가 아니라 **화면 전체**를 덮어야 한다.
    private GameObject GetOrCreateBlocker()
    {
        if (_blocker != null)
        {
            return _blocker;
        }

        Canvas root = GetComponentInParent<Canvas>().rootCanvas;

        _blocker = new GameObject($"{name} Blocker", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(Image), typeof(Button));

        var rect = (RectTransform)_blocker.transform;
        rect.SetParent(root.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        _blocker.GetComponent<Image>().color = Color.clear;
        _blocker.GetComponent<Button>().onClick.AddListener(Hide);

        return _blocker;
    }
}

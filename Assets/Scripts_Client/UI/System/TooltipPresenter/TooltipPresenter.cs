using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 커서 밑의 'TooltipTrigger'를 찾아 그 옆에 툴팁을 띄운다. 어느 캔버스의 무엇이든 여기 하나가 그린다.
//
// ■ 호버를 EventSystem에 묻지 않는다
// 이 앱은 대개 **포커스 없이** 바탕화면 위에 떠 있다. 그때는 Unity 입력이 멈춰 'IPointerEnter'가 오지 않는다.
// 그래서 클릭스루 판정과 같은 길 — Win32 커서 좌표로 쏜 레이캐스트('WindowManager.UIHitsUnderCursor') — 을 읽는다.
// 레이캐스트는 그쪽이 프레임당 한 번만 쏘고, 여기는 그 결과의 맨 위 하나만 본다.
//
// ■ 맨 위 하나만 보는 이유
// 팝업 차단막이 떠 있으면 맨 위가 차단막이라 뒤 버튼의 툴팁이 뜨지 않는다 — 가려진 것에 뜨면 안 된다.
//
// ■ 툴팁 자신은 레이캐스트를 받지 않는다
// 받으면 커서 밑에 깔린 툴팁이 맨 위가 되어 대상을 잃고 꺼졌다 켜졌다 한다. 게다가 클릭스루 판정이
// 툴팁을 "콘텐츠"로 보고 창이 클릭을 먹는다. 'CanvasGroup.blocksRaycasts'를 끄고, 모든 그림의 'raycastTarget'도 끈다.
//
// ■ 자리 — 대상의 오른쪽 옆, 넘치면 왼쪽
// 커서를 따라다니지 않는다(글을 읽는 동안 흔들린다). 윗변을 대상 윗변에 맞추고, 창 아래로 넘치면 위로 밀어 넣는다.
//
// 상주 오버레이라 'CanvasGroup'으로 여닫는다('System 규칙.md' ②).
public class TooltipPresenter : MonoBehaviour
{
    // 올려 두고 이만큼 지나야 뜬다 — 지나가는 커서마다 뜨면 화면이 번쩍거린다.
    private const float ShowDelay = 0.3f;

    // 툴팁이 꺼진 뒤 이 안에 다른 대상에 닿으면 지연 없이 바로 띄운다.
    // 버튼 사이 간격(5px)을 지나는 한두 프레임 동안 대상이 비는데, 그때마다 0.3초를 다시 기다리면 끊겨 보인다.
    private const float SwitchGrace = 0.15f;

    // 대상과 툴팁 사이 간격
    private const float Gap = 6f;

    [CenterHeader("참조")]
    [SerializeField, Tooltip("여닫기용. 켜면 alpha 1, 끄면 0. blocksRaycasts·interactable은 늘 꺼 둔다")]
    private CanvasGroup canvasGroup = null!;

    [SerializeField, Tooltip("툴팁 창 (Panel). pivot 좌상단(0,1) — 코드가 이 점을 대상 옆에 놓는다")]
    private RectTransform panel = null!;

    [SerializeField, Tooltip("제목 한 줄")]
    private TMP_Text titleText = null!;

    [SerializeField, Tooltip("줄이 쌓이는 부모. 줄이 없는 툴팁(간단형)이면 통째로 꺼진다")]
    private RectTransform rowParent = null!;

    [SerializeField, Tooltip("줄 한 개 프리팹 (TooltipRowView)")]
    private TooltipRowView rowPrefab = null!;

    private WindowManager _window = null!;

    private readonly List<TooltipRowView> _rows = new List<TooltipRowView>();

    private TooltipTrigger? _hovered;      // 지금 커서 밑의 대상
    private TooltipTrigger? _shown;        // 지금 툴팁을 띄운 대상
    private GameObject?     _lastTopHit;   // 직전 프레임 맨 위 결과 — 같으면 부모 탐색을 건너뛴다
    private float           _hoverTime;    // 지금 대상에 머문 시간
    private float           _hiddenAt = float.NegativeInfinity; // 마지막으로 툴팁을 끈 시각

    // 참조 확보 → 초기화 (Unity 메시지)
    private void Start()
    {
        this.RequireRef(canvasGroup, nameof(canvasGroup));
        this.RequireRef(panel,       nameof(panel));
        this.RequireRef(titleText,   nameof(titleText));
        this.RequireRef(rowParent,   nameof(rowParent));
        this.RequireRef(rowPrefab,   nameof(rowPrefab));

        _window = Services.Get<WindowManager>();

        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable   = false;
        Hide();
    }

    // 커서 밑 대상을 확인하고 띄우거나 끈다 (Unity 메시지)
    private void Update()
    {
        var target = FindTrigger();

        if (target != _hovered)
        {
            _hovered   = target;
            _hoverTime = 0f;
        }
        else
        {
            _hoverTime += Time.unscaledDeltaTime;
        }

        if (_hovered == null)
        {
            if (_shown != null)
            {
                Hide();
            }

            return;
        }

        if (_hovered == _shown)
        {
            return;
        }

        // 다른 툴팁을 보던 중이었거나 막 끈 참이면 기다리지 않는다.
        bool isSwitching = _shown != null || Time.unscaledTime - _hiddenAt <= SwitchGrace;

        if (isSwitching || _hoverTime >= ShowDelay)
        {
            Show(_hovered);
        }
    }

    // 커서 밑 맨 위 결과에서 부모 방향으로 'TooltipTrigger'를 찾는다 (Update에서 호출).
    private TooltipTrigger? FindTrigger()
    {
        var hits = _window.UIHitsUnderCursor;

        if (hits.Count == 0)
        {
            _lastTopHit = null;

            return null;
        }

        var top = hits[0].gameObject;

        if (top == _lastTopHit)
        {
            return _hovered;
        }

        _lastTopHit = top;

        var trigger = top.GetComponentInParent<TooltipTrigger>();

        return trigger != null && trigger.enabled ? trigger : null;
    }

    // 대상의 내용을 그리고 옆에 놓는다. 내용이 없으면 끈다.
    private void Show(TooltipTrigger target)
    {
        var content = target.Build();

        if (content == null)
        {
            Hide();

            return;
        }

        _shown = target;
        Render(content);
        Place((RectTransform)target.transform);
        canvasGroup.alpha = 1f;
    }

    private void Hide()
    {
        if (_shown != null)
        {
            _hiddenAt = Time.unscaledTime;
        }

        _shown            = null;
        canvasGroup.alpha = 0f;
    }

    // 제목과 줄을 채운다. 줄은 만들어 둔 것을 재사용하고 모자랄 때만 만든다.
    private void Render(TooltipContent content)
    {
        titleText.text = content.Title;

        var lines = content.Lines;

        // ★ 줄을 만들고 채우기 **전에** 줄 영역을 켠다.
        //   간단형(줄 0개)이 한 번 뜨면 영역이 꺼진 채 남는데, 꺼진 부모 아래에 만든 줄은 'Awake'가
        //   부모가 켜질 때까지 미뤄진다 — 그 사이 'Bind'·폭 재기가 돌아 NRE가 났다(2026-09-26).
        //   꺼진 TMP는 글자 폭도 제대로 못 잰다.
        rowParent.gameObject.SetActive(lines.Count > 0);

        for (int i = 0; i < lines.Count; i++)
        {
            if (i >= _rows.Count)
            {
                _rows.Add(Instantiate(rowPrefab, rowParent));
            }

            _rows[i].gameObject.SetActive(true);
            _rows[i].Bind(lines[i]);
        }

        for (int i = lines.Count; i < _rows.Count; i++)
        {
            _rows[i].gameObject.SetActive(false);
        }

        FitColumns(lines.Count);

        // 크기를 지금 확정해야 아래 'Place'가 넘침을 잴 수 있다('UI 규칙.md' "만든 자리에서 바로 태운다").
        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
    }

    // 값 · 보조 값 열 폭을 이 툴팁에서 가장 긴 글자에 맞춘다 (Render에서 호출).
    //
    // 줄마다 제 폭을 쓰면 열이 들쭉날쭉해지고, 고정폭이면 긴 값이 '…'로 잘린다('TooltipRowView' 머리 주석).
    // 그래서 열마다 최댓값을 구해 모든 줄에 같은 폭을 준다. 라벨은 남는 폭을 차지하므로 따로 재지 않는다 —
    // 패널이 'ContentSizeFitter'로 가장 넓은 줄에 맞춰 늘어난다.
    private void FitColumns(int count)
    {
        float value = 0f;
        float sub   = 0f;

        for (int i = 0; i < count; i++)
        {
            var (rowValue, rowSub) = _rows[i].MeasureColumns();

            value = Mathf.Max(value, rowValue);
            sub   = Mathf.Max(sub,   rowSub);
        }

        for (int i = 0; i < count; i++)
        {
            _rows[i].SetColumnWidths(value, sub);
        }
    }

    // 툴팁을 대상 오른쪽 옆에 놓는다. 창 밖으로 넘치면 왼쪽으로 뒤집고, 세로는 창 안으로 밀어 넣는다.
    //
    // 대상과 툴팁은 캔버스가 달라도 같은 루트 캔버스 아래라, 월드 좌표를 이 오브젝트의 로컬로 옮기면 비교할 수 있다.
    // 이 오브젝트는 화면 전체로 늘어나 있으므로 그 사각형이 곧 창이다.
    private void Place(RectTransform target)
    {
        var area    = (RectTransform)transform;
        var bounds  = area.rect;
        var corners = new Vector3[4];

        target.GetWorldCorners(corners); // 좌하 · 좌상 · 우상 · 우하

        Vector2 targetMin = area.InverseTransformPoint(corners[0]);
        Vector2 targetMax = area.InverseTransformPoint(corners[2]);

        var size = panel.rect.size;
        var x    = targetMax.x + Gap;

        if (x + size.x > bounds.xMax)
        {
            x = targetMin.x - Gap - size.x;
        }

        x = Mathf.Clamp(x, bounds.xMin, Mathf.Max(bounds.xMin, bounds.xMax - size.x));

        var y = Mathf.Clamp(targetMax.y, Mathf.Min(bounds.yMax, bounds.yMin + size.y), bounds.yMax);

        panel.localPosition = new Vector3(x, y, 0f);
    }
}

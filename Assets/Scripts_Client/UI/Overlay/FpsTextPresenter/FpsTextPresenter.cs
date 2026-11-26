using TMPro;
using UnityEngine;

// FPS 텍스트 — 창 구석에 회색 작은 글씨로 현재 프레임을 띄운다.
// 설정은 보이기 · 숨기기뿐이다('DisplayManager.FpsTextShownChanged' 구독). 자리는 아래 규칙으로 저절로 정한다.
//
// ■ 위젯과 겹치지 않는다 (2026-11-27 사용자)
//   위 · 아래는 **위젯을 따라간다** — 위젯 · 알림 줄 · FPS가 한 가장자리에 모인다.
//   왼쪽 · 오른쪽은 위젯과 다른 열의 구석이다(위젯이 오른쪽 열이면 왼쪽, 그 밖은 오른쪽). 알림 줄은 위젯 열 안(위젯 위)에 붙으므로
//   다른 열의 구석에 있는 FPS와 만나지 않는다. 위젯 위치는 이벤트가 없어 Update에서 본다(int 비교).
//
// ⚠️ 오브젝트를 끄지 않고 텍스트만 끈다 — 자기를 끄면 다시 켤 이벤트를 받지 못한다.
// ⚠️ 텍스트의 'raycastTarget'은 꺼 둔다 — 켜면 동적 클릭스루가 그 구석을 콘텐츠로 보고 클릭을 안 통과시킨다.
public class FpsTextPresenter : MonoBehaviour
{
    // 숫자를 바꾸는 주기. 매 프레임 바꾸면 읽을 수 없고 문자열도 매번 생긴다.
    private const float RefreshInterval = 0.5f;

    [CenterHeader("참조")]
    [SerializeField, Tooltip("FPS를 쓰는 텍스트. raycastTarget은 꺼 둔다(클릭스루)")]
    private TMP_Text fpsText = null!;

    [SerializeField, Tooltip("위젯 위치 — 위 · 아래를 따라가고, 같은 쪽 열이면 반대 구석으로 비킨다")]
    private WidgetPositionLayout widgetLayout = null!;

    [CenterHeader("배치")]
    [SerializeField, Tooltip("창 구석에서 떨어뜨릴 거리 (캔버스 좌표)")]
    private Vector2 margin = new Vector2(8f, 6f);

    private DisplayManager _display = null!;

    private float _elapsed;
    private int   _frames;
    private int   _shownFps = -1; // 마지막으로 쓴 값 — 같으면 문자열을 다시 만들지 않는다

    private WidgetPosition? _placedFor; // 마지막으로 자리를 잡은 위젯 위치

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 초기화 순서로 진행한다 (클라 공통 규약)
    private void Start()
    {
        this.RequireRef(fpsText,      nameof(fpsText));
        this.RequireRef(widgetLayout, nameof(widgetLayout));

        fpsText.raycastTarget = false;

        _display = Services.Get<DisplayManager>();
        Subscribe();

        OnShownChanged(_display.FpsTextShown);

        _isReady = true;
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    private void OnEnable()
    {
        if (_isReady)
        {
            Subscribe();
        }
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
    }

    // 실제 시간으로 프레임을 세어 주기마다 숫자를 바꾼다 (Unity 메시지)
    private void Update()
    {
        if (_placedFor != widgetLayout.Position)
        {
            Place();
        }

        if (!fpsText.enabled)
        {
            return;
        }

        _elapsed += Time.unscaledDeltaTime;
        _frames++;

        if (_elapsed < RefreshInterval)
        {
            return;
        }

        int fps = Mathf.RoundToInt(_frames / _elapsed);

        _elapsed = 0f;
        _frames  = 0;

        if (fps == _shownFps)
        {
            return;
        }

        _shownFps    = fps;
        fpsText.text = $"FPS {fps}";
    }

    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed                 = true;
        _display.FpsTextShownChanged += OnShownChanged;
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed                 = false;
        _display.FpsTextShownChanged -= OnShownChanged;
    }

    // 보이기가 바뀌었다 (DisplayManager.FpsTextShownChanged 구독 · Start 초기화)
    private void OnShownChanged(bool shown)
    {
        fpsText.enabled = shown;

        if (!shown)
        {
            return;
        }

        // 다시 켜질 때 옛 숫자가 잠깐 남지 않도록 새로 센다.
        _elapsed  = 0f;
        _frames   = 0;
        _shownFps = -1;

        Place();
    }

    // 구석을 고른다 — 클래스 주석 "위젯과 겹치지 않는다" (OnShownChanged · 위젯 위치가 바뀐 Update)
    // 텍스트의 앵커·피벗·정렬을 그 구석으로 옮긴다.
    private void Place()
    {
        int widget = (int)widgetLayout.Position;

        _placedFor = widgetLayout.Position;

        bool isUpper = widget / 3 == 0;
        bool isLeft  = widget % 3 == 2; // 위젯이 오른쪽 열이면 왼쪽 구석

        var corner = new Vector2(isLeft ? 0f : 1f, isUpper ? 1f : 0f);
        var rect   = fpsText.rectTransform;

        rect.anchorMin        = corner;
        rect.anchorMax        = corner;
        rect.pivot            = corner;
        rect.anchoredPosition = new Vector2(isLeft ? margin.x : -margin.x, isUpper ? -margin.y : margin.y);

        fpsText.alignment = isLeft ? TextAlignmentOptions.Left : TextAlignmentOptions.Right;
    }
}

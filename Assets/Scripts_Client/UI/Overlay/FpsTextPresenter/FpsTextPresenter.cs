using TMPro;
using UnityEngine;

// FPS 텍스트 — 창 네 구석 중 한 곳에 회색 작은 글씨로 현재 프레임을 띄운다.
// 위치는 'DisplayManager.FpsTextPositionChanged'를 구독해 따라간다('Hidden'이면 안 보인다).
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

    [CenterHeader("배치")]
    [SerializeField, Tooltip("창 구석에서 떨어뜨릴 거리 (캔버스 좌표)")]
    private Vector2 margin = new Vector2(8f, 6f);

    private DisplayManager _display = null!;

    private float _elapsed;
    private int   _frames;
    private int   _shownFps = -1; // 마지막으로 쓴 값 — 같으면 문자열을 다시 만들지 않는다

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 초기화 순서로 진행한다 (클라 공통 규약)
    private void Start()
    {
        this.RequireRef(fpsText, nameof(fpsText));

        fpsText.raycastTarget = false;

        _display = Services.Get<DisplayManager>();
        Subscribe();

        OnPositionChanged(_display.FpsTextPosition);

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

        _isSubscribed                    = true;
        _display.FpsTextPositionChanged += OnPositionChanged;
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed                    = false;
        _display.FpsTextPositionChanged -= OnPositionChanged;
    }

    // 위치가 바뀌었다 (DisplayManager.FpsTextPositionChanged 구독 · Start 초기화)
    // 텍스트의 앵커·피벗·정렬을 그 구석으로 옮긴다.
    private void OnPositionChanged(FpsTextPosition position)
    {
        bool shown = position != FpsTextPosition.Hidden;

        fpsText.enabled = shown;

        if (!shown)
        {
            return;
        }

        // 다시 켜질 때 옛 숫자가 잠깐 남지 않도록 새로 센다.
        _elapsed  = 0f;
        _frames   = 0;
        _shownFps = -1;

        bool isLeft  = position == FpsTextPosition.UpperLeft  || position == FpsTextPosition.LowerLeft;
        bool isUpper = position == FpsTextPosition.UpperLeft  || position == FpsTextPosition.UpperRight;

        var corner = new Vector2(isLeft ? 0f : 1f, isUpper ? 1f : 0f);
        var rect   = fpsText.rectTransform;

        rect.anchorMin        = corner;
        rect.anchorMax        = corner;
        rect.pivot            = corner;
        rect.anchoredPosition = new Vector2(isLeft ? margin.x : -margin.x, isUpper ? -margin.y : margin.y);

        fpsText.alignment = isLeft ? TextAlignmentOptions.Left : TextAlignmentOptions.Right;
    }
}

using System.Collections;
using UnityEngine;

/// <summary>
/// 로딩 표시 — 서버 요청이 진행 중일 때만 뜬다. 'ServerWaitManager.BusyChanged'만 구독해
/// 몸통을 켜고 끈다. 직접 판단하지 않는다(무엇을 기다리는지 모른다).
///
/// ⚠️ 수동 닫기는 없다 — 정상 응답·타임아웃 어느 쪽이든 'ServerWaitManager'가 자동으로 내린다.
/// 실패·오류면 로딩은 사라지고 대신 'NoticePresenter'가 뜬다.
///
/// ■ 빠른 응답은 아예 안 띄운다 (깜빡임 제거)
/// 켜라는 신호가 와도 곧바로 띄우지 않고 'ShowDelaySeconds'만큼 미룬다. 그 안에 응답이 와서
/// 끄라는 신호가 오면 코루틴을 취소해 <b>한 번도 뜨지 않는다</b>. 느린 왕복만 실제로 표시된다.
/// </summary>
/// <remarks>
/// ⚠️ 오브젝트를 끄지 않고 'CanvasGroup'으로 표시/숨김한다 — 자기 자신을 끄면 다시 켤 이벤트를
/// 받지 못한다(꺼진 오브젝트는 콜백이 오지 않는다). alpha로 보이고, blocksRaycasts로 대기 중 뒤 UI를 막는다.
/// </remarks>
public class LoadingPresenter : MonoBehaviour
{
    // 요청이 이 시간 안에 끝나면 로딩을 아예 띄우지 않는다 — 빠른 왕복의 깜빡임을 없앤다.
    private const float ShowDelaySeconds = 0.15f;

    [CenterHeader("참조")]
    [SerializeField, Tooltip("로딩 표시 몸통의 CanvasGroup. alpha·blocksRaycasts로 표시/숨김한다(오브젝트는 끄지 않는다)")]
    private CanvasGroup group = null!;

    private ServerWaitManager _wait = null!;

    // grace를 기다리는 표시 코루틴. 대기가 그 전에 끝나면 취소한다.
    private Coroutine? _showDelay;

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 초기화 순서로 진행한다 (클라 공통 규약)
    private void Start()
    {
        this.RequireRef(group, nameof(group));

        _wait = Services.Get<ServerWaitManager>();
        Subscribe();

        SetVisible(false); // 시작은 숨김 — 요청이 뜨면 켜진다
        _isReady = true;
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    private void OnEnable()
    {
        if (_isReady)
            Subscribe();
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (_isSubscribed)
            return;

        _isSubscribed     = true;
        _wait.BusyChanged += OnBusyChanged;
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed)
            return;

        _isSubscribed     = false;
        _wait.BusyChanged -= OnBusyChanged;
    }

    // 대기 유무가 바뀌었다 (ServerWaitManager.BusyChanged 구독)
    // 켤 때는 grace만큼 미루고, 끌 때는 즉시 내리며 대기 중이던 표시도 취소한다.
    private void OnBusyChanged(bool busy)
    {
        if (busy)
        {
            if (_showDelay == null)
                _showDelay = StartCoroutine(ShowAfterDelay());
        }
        else
        {
            if (_showDelay != null)
            {
                StopCoroutine(_showDelay);
                _showDelay = null;
            }

            SetVisible(false);
        }
    }

    // grace가 지나도록 대기가 이어지면 그제야 표시한다 (OnBusyChanged가 시작).
    // 타임스케일이 0이어도(일시정지 연출 등) 실제 시간으로 흐른다.
    private IEnumerator ShowAfterDelay()
    {
        yield return new WaitForSecondsRealtime(ShowDelaySeconds);

        _showDelay = null;
        SetVisible(true);
    }

    // 몸통을 켜고 끈다 — 오브젝트는 항상 활성이라 이벤트를 계속 받는다(자기를 끄지 않는다).
    private void SetVisible(bool on)
    {
        group.alpha          = on ? 1f : 0f;
        group.blocksRaycasts = on; // 대기 중 뒤 UI 클릭 차단(모달)
        group.interactable   = on;
    }
}

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 꾹 누르면 반복되는 버튼 — 누른 순간 한 번, 잠깐 뒤부터 계속 'Repeated'를 낸다. 반복은 점점 빨라지다 상한 속도에서 유지된다.
//
// ■ 쓰는 곳
// 수량 ▲▼ (소모품 사용 창 — 2026-11-23 사용자 "꾹 눌러도 계속 올라가게, 점점 빨라지고 상한 속도에서 유지").
//
// ■ Button.onClick을 쓰지 않는다
// 첫 한 번을 누른 순간(PointerDown)에 낸다 — onClick까지 걸면 손을 뗄 때 한 번 더 올라간다.
// 같은 오브젝트의 'Button'은 모양(눌림 색 · 잠금)만 맡는다. 'interactable'이 꺼져 있으면 반복하지 않는다.
[RequireComponent(typeof(Button))]
public class HoldRepeatButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField, Tooltip("첫 반복까지 기다리는 시간(초) — 한 번만 누르려던 클릭이 반복으로 번지지 않게")]
    private float firstDelay = 0.35f;

    [SerializeField, Tooltip("반복 첫 간격(초)")]
    private float startInterval = 0.15f;

    [SerializeField, Tooltip("상한 속도의 간격(초) — 여기까지 빨라지고 그다음은 유지한다")]
    private float minInterval = 0.02f;

    [SerializeField, Tooltip("한 번 반복할 때마다 간격에 곱하는 값 — 작을수록 빨리 빨라진다")]
    private float acceleration = 0.85f;

    // 누른 순간 한 번 + 반복마다 한 번
    public event Action? Repeated;

    private Button                   _button = null!;
    private CancellationTokenSource? _holdCts;

    // 참조 확보 (Unity 메시지)
    private void Awake()
    {
        _button = GetComponent<Button>();
    }

    // 꺼지면 반복을 끊는다 — 창이 닫혀도 혼자 돌지 않게 (Unity 메시지)
    private void OnDisable()
    {
        StopHold();
    }

    // 누름 — 한 번 내고 반복을 시작한다 (EventSystem)
    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !_button.IsInteractable())
        {
            return;
        }

        StopHold();
        Repeated?.Invoke();

        _holdCts = new CancellationTokenSource();
        HoldAsync(_holdCts.Token).Forget();
    }

    // 뗌 · 벗어남 — 반복을 멈춘다 (EventSystem)
    public void OnPointerUp(PointerEventData eventData) => StopHold();

    public void OnPointerExit(PointerEventData eventData) => StopHold();

    // 반복 — 첫 지연 뒤 간격을 줄여 가며 낸다. 상한(minInterval)에 닿으면 그 간격을 지킨다
    private async UniTaskVoid HoldAsync(CancellationToken ct)
    {
        if (await UniTask.Delay(TimeSpan.FromSeconds(firstDelay), ignoreTimeScale: true, cancellationToken: ct).SuppressCancellationThrow())
        {
            return;
        }

        float interval = startInterval;

        while (!ct.IsCancellationRequested && _button.IsInteractable())
        {
            Repeated?.Invoke();

            if (await UniTask.Delay(TimeSpan.FromSeconds(interval), ignoreTimeScale: true, cancellationToken: ct).SuppressCancellationThrow())
            {
                return;
            }

            interval = Mathf.Max(minInterval, interval * acceleration);
        }
    }

    private void StopHold()
    {
        _holdCts?.Cancel();
        _holdCts?.Dispose();
        _holdCts = null;
    }
}

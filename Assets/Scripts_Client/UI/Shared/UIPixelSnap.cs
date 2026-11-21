using UnityEngine;

// 글자를 화면 픽셀 경계에 맞춘다 — 픽셀 폰트(갈무리)의 1px 획이 두 픽셀에 반씩 걸려 회색으로 번지지 않게.
//
// ■ 왜(2026-11-20 이슈 #132 — "수량 숫자가 흰색이 아니라 회색") — 원인을 화면 캡처로 확정했다:
//   ① 크기: 갈무리 EN은 글꼴 1점 = fontSize/12 px다. 자동 크기(13.7)면 획이 1.14px라 가장자리가 섞인다 → 12·24처럼 12의 배수만 또렷하다.
//   ② 자리: 칸 격자가 소수 좌표(721.37, 688.93)에 놓여, 크기를 12로 맞춰도 획이 두 픽셀에 걸친다.
//   둘 다 맞춰야 또렷했다(색을 흰색으로 바꾸거나 밑그림을 줄이는 것만으로는 회색이 남았다).
// ■ 어떻게 — 그리기 직전(Canvas.willRenderCanvases) 월드 좌표의 소수 부분만큼 anchoredPosition을 민다.
//   처음 값(_base)을 기억해 두고 매번 거기서 다시 계산하므로 오차가 쌓이지 않는다. 바뀔 게 없으면 손대지 않는다.
// ※ 캔버스 배율 1 · 회전 없음을 가정한다(이 프로젝트의 오버레이 캔버스). 배율이 있으면 그 배율로 나눠 민다.
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class UIPixelSnap : MonoBehaviour
{
    private RectTransform _rect = null!;
    private Vector2       _base;
    private bool          _hasBase;

    private void Awake()
    {
        _rect = (RectTransform)transform;
    }

    private void OnEnable()
    {
        Canvas.willRenderCanvases += Snap;
    }

    private void OnDisable()
    {
        Canvas.willRenderCanvases -= Snap;

        if (_hasBase && _rect != null)
        {
            _rect.anchoredPosition = _base;
        }
    }

    private void Snap()
    {
        if (!_hasBase)
        {
            _base    = _rect.anchoredPosition;
            _hasBase = true;
        }

        Vector3 scale = _rect.parent != null ? _rect.parent.lossyScale : Vector3.one;

        if (scale.x <= 0f || scale.y <= 0f)
        {
            return;
        }

        // 지금 자리에서 처음 자리로 되돌렸을 때의 월드 좌표 → 가장 가까운 픽셀로
        Vector2 now       = _rect.anchoredPosition;
        Vector3 world     = _rect.position;
        float   baseX     = world.x + (_base.x - now.x) * scale.x;
        float   baseY     = world.y + (_base.y - now.y) * scale.y;
        Vector2 target    = new Vector2(_base.x + (Mathf.Round(baseX) - baseX) / scale.x,
                                        _base.y + (Mathf.Round(baseY) - baseY) / scale.y);

        if ((target - now).sqrMagnitude > 0.0001f)
        {
            _rect.anchoredPosition = target;
        }
    }
}

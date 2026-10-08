using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 응축 진행 바의 한 칸 — ★ 한 단계(몫 8 · 14 · 22 · 32). 'CharacterCondensePresenter'가 4칸을 Bind한다.
//
// ■ 왜 한 줄이 아니라 ★마다 한 칸인가 (T-130 목업)
// 누적 0~76을 한 줄로 그리면 ★1(8)이 너무 짧아 첫 응축이 거의 안 차 보인다.
// ★마다 칸을 나눠 칸마다 그 단계 몫만큼 채우면, 한 번에 ★ 두 개를 넘는 것도 그대로 보인다.
//
// ※ 채움은 앵커로 한다 — 'Image.fillAmount'는 스프라이트가 있어야 하고, 두 겹(지금 · 미리보기)을 이어 붙이기 어렵다.
public class CondenseStageView : MonoBehaviour
{
    [SerializeField, Tooltip("지금 누적의 채움 — 왼쪽부터. 앵커 X로 폭을 정한다")]
    private RectTransform currentFill = null!;

    [SerializeField, Tooltip("이번에 넣을 몫의 채움 — 지금 채움 바로 뒤에 이어 붙는다")]
    private RectTransform previewFill = null!;

    [SerializeField, Tooltip("칸 글씨 — '★2 · 9/14'")]
    private TMP_Text labelText = null!;

    // 필수 참조 검증 — 서비스를 조회하지 않으므로 Awake로 충분하다 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(currentFill, nameof(currentFill));
        this.RequireRef(previewFill, nameof(previewFill));
        this.RequireRef(labelText,   nameof(labelText));
    }

    // 이 단계를 그린다 ('CharacterCondensePresenter'가 호출).
    //   current  : 이 단계 안에서 이미 찬 수 (0 ~ required)
    //   after    : 이번 재료까지 넣으면 이 단계 안에서 찰 수 (current ~ required)
    //   required : 이 단계의 몫
    //   star     : 이 칸이 여는 ★
    public void Bind(int current, int after, int required, int star)
    {
        float now  = required > 0 ? Mathf.Clamp01((float)current / required) : 0f;
        float then = required > 0 ? Mathf.Clamp01((float)after / required) : 0f;

        SetSpan(currentFill, 0f, now);
        SetSpan(previewFill, now, then);

        labelText.text = $"{star}성 · {Mathf.Min(after, required)}/{required}";
    }

    // 채움 하나를 [from, to] 비율 구간에 놓는다 — 폭 0이면 끈다 (Bind에서 호출).
    private static void SetSpan(RectTransform fill, float from, float to)
    {
        fill.gameObject.SetActive(to > from);
        fill.anchorMin = new Vector2(from, 0f);
        fill.anchorMax = new Vector2(to, 1f);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
    }
}

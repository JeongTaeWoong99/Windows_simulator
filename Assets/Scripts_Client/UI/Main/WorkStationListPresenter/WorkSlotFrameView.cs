using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 작업슬롯 칸 프레임 — 빈 칸 · 잠긴 칸의 그림과 문구 (2026-11-10). 배치된 칸은 위를 슬롯 뷰가 덮는다.
// 무엇을 적을지(조건 문구)는 'WorkStationListPresenter'가 정한다 (종속 View).
//
// ■ 그림 — 빈 칸은 돌바닥 벽돌 + 구리 꺾쇠('ui_slot_empty') + 영웅 실루엣, 잠긴 칸은 리벳 쇠판 빗금('ui_slot_locked') + 육각 자물쇠. 둘 다 Tiled.
//   그림이 없으면(그림 저장소 없음) 씬에 둔 그림 그대로다.
// ■ 버튼 색 반응은 그림('art')에 건다 — 프레임 바탕(테마 Button 색)은 그림 밑에 가려진다.
// ■ 빨간 점 — 지금 할 수 있는 일이 있는 칸 (T-145 · 2026-11-26). 무엇이 할 일인지는 Presenter가 정한다.
//   배치된 칸은 슬롯 뷰가 위를 덮어 가려진다 — Presenter도 끈다.
public class WorkSlotFrameView : MonoBehaviour
{
    [SerializeField, Tooltip("칸 그림 — 프레임을 꽉 채운다. 빈 칸·잠긴 칸에 따라 코드가 바꾼다")]
    private Image art = null!;

    [SerializeField, Tooltip("가운데 위 표시 — 빈 칸 십자 · 잠긴 칸 자물쇠")]
    private Image stateIcon = null!;

    [SerializeField, Tooltip("문구 — '비어있음.' · 해금 조건. 문구는 Presenter가 쓴다")]
    private TMP_Text label = null!;

    [SerializeField, Tooltip("오른쪽 위 빨간 점 — 지금 열 수 있는 잠긴 칸. 비우면 점 없이 지나간다")]
    private GameObject? noticeDot;

    [SerializeField, Tooltip("오른쪽 위 경고 점 — 쉬는 캐릭터를 넣을 수 있는 빈 칸(놔두면 손해). 올리면 툴팁. 비우면 점 없이 지나간다")]
    private WarningDotView? warningDot;

    public TMP_Text Label => label;

    private void Awake()
    {
        this.RequireRef(art,       nameof(art));
        this.RequireRef(stateIcon, nameof(stateIcon));
        this.RequireRef(label,     nameof(label));

        StretchLabelToBottom();
    }

    // 문구 칸의 아래 끝을 칸 바닥(여백 LabelBottomMargin)까지 늘린다 — 위 끝은 그대로(가운데 아래) · 글자는 위 정렬이라 자리는 같다.
    // ※ 높이 70 고정이라 조건이 세 줄인 잠긴 칸은 글자가 16px까지 줄었다 — 칸 아래 절반이 비어 있었다(2026-11-25).
    private void StretchLabelToBottom()
    {
        var rect = label.rectTransform;
        float top = rect.anchoredPosition.y; // 가운데 기준 위 끝(피벗이 위)

        rect.anchorMin = new Vector2(rect.anchorMin.x, 0f);
        rect.offsetMin = new Vector2(rect.offsetMin.x, LabelBottomMargin);
        rect.offsetMax = new Vector2(rect.offsetMax.x, top);
        rect.anchorMax = new Vector2(rect.anchorMax.x, 0.5f);
    }

    private const float LabelBottomMargin = 14f;

    // 빨간 점을 켜고 끈다 (WorkStationListPresenter.RefreshFrameNotices)
    public void SetNotice(bool on)
    {
        if (noticeDot != null && noticeDot.activeSelf != on)
        {
            noticeDot.SetActive(on);
        }
    }

    // 경고 점을 켜고 끈다 — 켜면 빨간 점보다 이긴다 (WorkStationListPresenter.RefreshFrameNotices)
    public void SetWarning(bool on, string message)
    {
        if (warningDot != null)
        {
            warningDot.Show(on, () => WarningDotView.Content(message));
        }
    }

    // 열린 빈 칸이면 'unlocked' — 문구와 그림을 함께 바꾼다
    public void SetState(bool unlocked, string text)
    {
        label.text = text;

        Sprite? sprite = unlocked ? FxSprites.UiSlotEmpty : FxSprites.UiSlotLocked;

        if (sprite != null)
        {
            art.sprite = sprite;
            art.type   = Image.Type.Tiled; // 벽돌·빗금은 되풀이해야 고르다 — 무늬 주기가 가운데 칸의 약수다
        }

        Sprite? icon = unlocked ? FxSprites.UiIconPlus : FxSprites.UiIconLock;

        stateIcon.sprite  = icon!;
        stateIcon.enabled = icon != null;
    }
}

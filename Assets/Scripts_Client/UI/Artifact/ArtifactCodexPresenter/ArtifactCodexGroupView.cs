using TMPro;
using UnityEngine;

// 도감의 등급 묶음 하나 — 머리 글자("일반  획득 3 / 12")와 그 등급의 칸들이 놓일 격자.
// 'ArtifactCodexPresenter'가 등급 수만큼 원본을 복제해 쓴다. 칸은 Presenter가 만들어 'SlotParent' 아래에 둔다.
public class ArtifactCodexGroupView : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("묶음 머리 — 등급 · 획득 수. 문구는 코드가 쓴다")]
    private TMP_Text headText = null!;

    [SerializeField, Tooltip("칸이 놓이는 격자(GridLayoutGroup)")]
    private Transform slotParent = null!;

    public Transform SlotParent => slotParent;

    // 머리 글자를 쓴다 ('ArtifactCodexPresenter'가 호출).
    public void SetHead(string text)
    {
        headText.text = text;
    }

    // 참조 검증 (Unity 메시지) — 복제된 묶음마다 돈다
    private void Awake()
    {
        this.RequireRef(headText,   nameof(headText));
        this.RequireRef(slotParent, nameof(slotParent));
    }
}

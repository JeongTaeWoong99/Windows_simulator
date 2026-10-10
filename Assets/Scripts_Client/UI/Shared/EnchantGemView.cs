using System.Collections.Generic;
using GameData;
using UnityEngine;
using UnityEngine.UI;

// 장비 칸 오른쪽 아래의 인챈트 보석 하나 — 색 = 인챈트 등급, 인챈트 전이면 빈 보석.
//
// ■ 왜 네모 1~3칸이 아니라 보석 하나인가 (2026-10-10 사용자 결정 · A안)
// 처음엔 칸마다 다른 등급이 박힐 수 있어(태스크바 히어로식) 칸 수만큼 네모를 그렸다.
// 지금 기획은 인챈트 등급이 **장비 하나에 하나**라 네모가 전부 같은 색 — 네모 셋이 정보 하나만 말했다.
// 칸 수는 장비 등급이 정하므로(일반·고급 1 · 희귀·영웅 2 · 전설·신화 3) 칸에서는 빼고 툴팁("능력치 칸 n/N")에 둔다.
//
// ■ 바탕과 같은 색이어도 읽히게
// 칸 바탕은 장비 등급색이라 영웅 장비에 영웅 인챈트면 같은 보라가 겹친다. 어두운 테두리(Outline)와
// 왼쪽 위 흰 반짝 점(shine)으로 보석의 모양이 색과 상관없이 선다.
//
// 인벤토리 칸('SlotView')과 목록 줄 아이콘('ItemIconView')이 같은 보석을 쓴다 — 같은 장비가 같게 보인다.
public class EnchantGemView : MonoBehaviour
{
    [SerializeField, Tooltip("보석 몸통 — 45° 돌린 네모. 색이 인챈트 등급이 된다")]
    private Image gemImage = null!;

    [SerializeField, Tooltip("왼쪽 위 흰 반짝 점 — 인챈트 전(빈 보석)에는 꺼진다")]
    private Image shineImage = null!;

    // 빈 보석의 몸통 색 — 바탕 위에 어둡게 비친다.
    private static readonly Color EmptyColor = new Color(0f, 0f, 0f, 0.35f);

    // 딤·흑백 전의 원래 색 ('SetTint'가 매번 여기서 다시 칠한다).
    private Color _baseColor = EmptyColor;

    // 필수 참조 검증 — 부르는 칸의 Bind보다 먼저 끝나 있어야 한다 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(gemImage,   nameof(gemImage));
        this.RequireRef(shineImage, nameof(shineImage));
    }

    // 장비의 능력치 칸 등급으로 보석을 그린다. null이나 빈 목록이면(장비가 아니다) 보석을 끈다.
    //   grades : 칸마다의 등급 — 'EquipLabel.ToSocketGrades'. 칸이 모두 같은 등급이라 박힌 첫 칸을 읽는다
    public void Bind(IReadOnlyList<GlobalRarity>? grades)
    {
        bool on = grades != null && grades.Count > 0;

        gameObject.SetActive(on);

        if (!on)
        {
            return;
        }

        GlobalRarity grade = GlobalRarity.None;

        foreach (GlobalRarity g in grades!)
        {
            if (g != GlobalRarity.None)
            {
                grade = g;

                break;
            }
        }

        bool isEnchanted = grade != GlobalRarity.None;

        _baseColor = isEnchanted ? RarityPalette.Get(grade) : EmptyColor;
        shineImage.enabled = isEnchanted;

        SetTint(false, false);
    }

    // 칸의 딤(나가 있음)·흑백(찾기 제외)을 보석에도 입힌다 ('SlotView.ApplyTint'가 호출).
    // ※ 색 규칙은 'SlotView.TintColor' 하나다 — 칸과 보석이 다르게 어두워지면 보석만 떠 보인다.
    public void SetTint(bool dimmed, bool filteredOut)
    {
        gemImage.color   = SlotView.TintColor(_baseColor, dimmed, filteredOut);
        shineImage.color = SlotView.TintColor(new Color(1f, 1f, 1f, 0.55f), dimmed, filteredOut);
    }
}

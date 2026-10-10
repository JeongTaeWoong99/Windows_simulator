using GameData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 목록 줄 왼쪽에 서는 작은 아이콘 칸 — 등급 바탕 · 아이콘(지금은 글자) · 모서리 수량 · 인챈트 보석.
//
// ■ 인벤토리 칸('SlotView')과 무엇이 다른가
// 'SlotView'는 격자 한 칸 전체(이름·적성·레벨·판매 표시…)라 무겁다. 여기는 **줄 안에 끼는 그림 하나**다 —
// 경매장 매물 줄 · 판매 목록 줄 · 장비 고르기 줄 · 우편 줄이 같은 칸을 쓴다.
// 이름은 줄이 적으므로 여기엔 없다. 등급색·인챈트 보석은 'SlotView'와 같게 둔다(같은 장비가 같게 보이도록).
//
// 값은 부르는 쪽이 'ItemIconContent'로 완성해 넘긴다 — 이 칸은 무엇을 그리는지 모른다.
public class ItemIconView : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("등급 바탕. 칸 전체를 덮는다 — 색만 등급에 따라 바뀐다")]
    private Image rarityImage = null!;

    [SerializeField, Tooltip("아이콘 자리 — 그림이 있으면 그 그림, 없으면 어두운 네모(프리팹 색) 위에 글자")]
    private Image iconImage = null!;

    [SerializeField, Tooltip("아이콘 대신 적는 글자 — 이름의 첫 글자")]
    private TMP_Text glyphText = null!;

    [SerializeField, Tooltip("오른쪽 아래 수량 — 'x12'. 비면 꺼진다")]
    private TMP_Text countText = null!;

    [CenterHeader("인챈트 보석 (장비)")]
    [SerializeField, Tooltip("오른쪽 아래 인챈트 보석 — 장비일 때만 켜진다('EnchantGemView')")]
    private EnchantGemView enchantGem = null!;

    private Color _iconPlaceholderColor; // 그림이 없을 때의 네모 색 — 프리팹 값

    // 필수 참조 검증 — 서비스를 조회하지 않으므로 Awake로 충분하다 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(rarityImage,     nameof(rarityImage));
        this.RequireRef(iconImage,       nameof(iconImage));
        this.RequireRef(glyphText,       nameof(glyphText));
        this.RequireRef(countText,       nameof(countText));
        this.RequireRef(enchantGem,      nameof(enchantGem));

        _iconPlaceholderColor = iconImage.color;
    }

    // 완성값을 그린다 (줄 View가 자기 Bind에서 호출).
    //
    // 그림이 있으면 그림을 놓고 글자를 끈다. 없으면 어두운 네모 위에 글자.
    public void Bind(in ItemIconContent content)
    {
        bool hasIcon = content.Icon != null;

        rarityImage.color        = RarityPalette.Get(content.Rarity);
        iconImage.sprite         = content.Icon;
        iconImage.preserveAspect = hasIcon;
        iconImage.color          = hasIcon ? Color.white : _iconPlaceholderColor;
        glyphText.text           = hasIcon ? "" : content.Glyph;

        bool hasCount = !string.IsNullOrEmpty(content.Count);

        countText.gameObject.SetActive(hasCount);
        countText.text = content.Count;

        enchantGem.Bind(content.Sockets);
    }

    // 칸을 비운다 — 줄이 풀로 돌아갈 때 부른다.
    public void Clear()
    {
        Bind(new ItemIconContent(GlobalRarity.None, "", "", null));
    }
}

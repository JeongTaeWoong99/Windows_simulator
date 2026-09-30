using System.Collections.Generic;
using GameData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 목록 줄 왼쪽에 서는 작은 아이콘 칸 — 등급 바탕 · 아이콘(지금은 글자) · 모서리 수량 · 능력치 칸.
//
// ■ 인벤토리 칸('SlotView')과 무엇이 다른가
// 'SlotView'는 격자 한 칸 전체(이름·적성·레벨·판매 표시…)라 무겁다. 여기는 **줄 안에 끼는 그림 하나**다 —
// 경매장 매물 줄 · 판매 목록 줄 · 장비 고르기 줄 · 우편 줄이 같은 칸을 쓴다.
// 이름은 줄이 적으므로 여기엔 없다. 등급색·능력치 칸 색 규칙은 'SlotView'와 같게 둔다(같은 장비가 같게 보이도록).
//
// 값은 부르는 쪽이 'ItemIconContent'로 완성해 넘긴다 — 이 칸은 무엇을 그리는지 모른다.
public class ItemIconView : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("등급 바탕. 칸 전체를 덮는다 — 색만 등급에 따라 바뀐다")]
    private Image rarityImage = null!;

    [SerializeField, Tooltip("아이콘 자리. 🎨 에셋이 없어 지금은 어두운 네모다")]
    private Image iconImage = null!;

    [SerializeField, Tooltip("아이콘 대신 적는 글자 — 이름의 첫 글자")]
    private TMP_Text glyphText = null!;

    [SerializeField, Tooltip("오른쪽 아래 수량 — 'x12'. 비면 꺼진다")]
    private TMP_Text countText = null!;

    [CenterHeader("능력치 칸 (장비)")]
    [SerializeField, Tooltip("능력치 칸 네모를 담은 아래 줄. 장비일 때만 켜진다")]
    private GameObject statSocketStrip = null!;

    // ⚠️ **배열 순서 = 화면의 왼쪽 → 오른쪽**이다. 칸 수가 적으면 앞(왼쪽)부터 꺼진다('SlotView'와 같다).
    [SerializeField, NonReorderable, Tooltip("능력치 칸 네모 6개. 왼쪽 → 오른쪽 순서")]
    private Image[] statSocketImages = new Image[0];

    // 빈 능력치 칸의 색 — 'SlotView'와 같은 값이다(같은 장비가 두 화면에서 같게 읽혀야 한다).
    private static readonly Color EmptySocketColor = new Color32(0x2A, 0x2A, 0x2A, 0xFF);

    // 필수 참조 검증 — 서비스를 조회하지 않으므로 Awake로 충분하다 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(rarityImage,     nameof(rarityImage));
        this.RequireRef(iconImage,       nameof(iconImage));
        this.RequireRef(glyphText,       nameof(glyphText));
        this.RequireRef(countText,       nameof(countText));
        this.RequireRef(statSocketStrip, nameof(statSocketStrip));

        if (statSocketImages.Length != EquipLabel.MaxStatSlotCount)
        {
            ClientLogger.Warn(ClientLogger.UI,
                $"능력치 칸 네모가 {statSocketImages.Length}개다 — 상한은 {EquipLabel.MaxStatSlotCount}칸이다.", this);
        }
    }

    // 완성값을 그린다 (줄 View가 자기 Bind에서 호출).
    //
    // TODO: 아이콘 에셋이 생기면 'iconImage.sprite'를 채우고 글자를 끈다.
    public void Bind(in ItemIconContent content)
    {
        rarityImage.color = RarityPalette.Get(content.Rarity);
        glyphText.text    = content.Glyph;

        bool hasCount = !string.IsNullOrEmpty(content.Count);

        countText.gameObject.SetActive(hasCount);
        countText.text = content.Count;

        SetStatSockets(content.Sockets);
    }

    // 칸을 비운다 — 줄이 풀로 돌아갈 때 부른다.
    public void Clear()
    {
        Bind(new ItemIconContent(GlobalRarity.None, "", "", null));
    }

    // 능력치 칸 — 오른쪽 끝부터 앉힌다. null이나 빈 목록이면 줄을 끈다 ('SlotView.SetStatSockets'와 같은 규칙).
    private void SetStatSockets(IReadOnlyList<GlobalRarity>? grades)
    {
        bool on = grades != null && grades.Count > 0;

        statSocketStrip.SetActive(on);

        if (!on)
        {
            return;
        }

        int offset = statSocketImages.Length - grades!.Count;

        for (int i = 0; i < statSocketImages.Length; i++)
        {
            Image? socket = statSocketImages[i];

            if (socket == null)
            {
                continue; // 배선 누락은 Awake가 이미 경고했다
            }

            int  line   = i - offset;
            bool isUsed = line >= 0;

            socket.gameObject.SetActive(isUsed);

            if (isUsed)
            {
                socket.color = grades[line] == GlobalRarity.None ? EmptySocketColor : RarityPalette.Get(grades[line]);
            }
        }
    }
}

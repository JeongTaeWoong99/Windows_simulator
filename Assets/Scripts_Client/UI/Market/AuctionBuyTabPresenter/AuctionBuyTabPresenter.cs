using System;
using UnityEngine;
using UnityEngine.UI;

// 경매장 [구매] 탭 안의 축. 값의 순서는 **화면의 버튼 순서와 같게 유지한다**.
//
// ⚠️ 씬에는 이 enum이 int로 저장돼 있다 — 중간에 끼우거나 재정렬하면 씬 배선을 함께 고친다('AuctionTab'과 같은 규칙).
public enum AuctionBuyKind
{
    // 자원 — 종류별로 묶어 수량으로 산다(거래소, 'MarketItemPresenter'). 기본 축이다.
    Item,

    // 장비 — 매물을 하나씩 검색해 통째로 산다('AuctionSearchPresenter').
    Equip,

    // 캐릭터 — 장비와 같은 검색 화면을 '캐릭터' 종류로 하나 더 둔다('AuctionSearchPresenter.searchKind' · #47).
    Character,
}

// 경매장 [구매] 탭의 축 줄 — 자원 · 장비 · 캐릭터 (씬 왼쪽부터).
//
// 모양은 'AuctionTabPresenter'와 같다 — 축마다 화면이 따로 있고 지금 축의 화면만 켠다.
// 자원과 개체는 사는 법이 달라 화면이 다르다: 자원은 같은 TID면 똑같아 "N개를 최대 P에"로 사고,
// 장비·캐릭터는 개체마다 인챈트·레벨·적성이 달라 매물을 하나씩 고른다.
//
// ⚠️ 화면은 꺼졌다 켜진다 — 각 Presenter는 'OnEnable'/'OnDisable'로 구독을 잇고 끊는다.
public class AuctionBuyTabPresenter : MonoBehaviour
{
    // [구매] 탭을 처음 열었을 때의 축.
    private const AuctionBuyKind DefaultKind = AuctionBuyKind.Item;

    // 축 하나 — 버튼과 그 버튼이 여는 화면. 인스펙터에서 짝지어 넣는다.
    [Serializable]
    private struct KindEntry
    {
        [Tooltip("이 줄이 어느 축인가")]
        public AuctionBuyKind kind;

        [Tooltip("그 축의 버튼 (씬 왼쪽부터 자원·장비·캐릭터)")]
        public Button button;

        [Tooltip("그 축을 누르면 켜지는 화면. 나머지 축의 화면은 꺼진다")]
        public GameObject page;
    }

    // ※ NonReorderable — 목록 순서를 화면·enum과 나란히 두고, [CenterHeader]가 건너뛰어지지 않게 한다('UI 규칙.md').
    [CenterHeader("참조")]
    [SerializeField, NonReorderable, Tooltip("축 버튼과 화면. AuctionBuyKind 값마다 정확히 한 줄씩, 화면과 같은 순서로 넣는다")]
    private KindEntry[] kinds = new KindEntry[0];


    [CenterHeader("선택 표시")]
    [SerializeField, Tooltip("지금 열려 있는 축의 버튼 색")]
    private UIThemeRole selectedRole = UIThemeRole.ButtonSelected;

    [SerializeField, Tooltip("열려 있지 않은 축의 버튼 색")]
    private UIThemeRole normalRole = UIThemeRole.Button;

    // 지금 열려 있는 축. [구매] 탭을 닫아도 유지된다.
    public AuctionBuyKind CurrentKind { get; private set; } = DefaultKind;

    // 참조 확보 → 배선 → 초기화 순서로 진행한다 (클라 공통 규약)
    private void Start()
    {
        ValidateKinds();
        BindButtons();
        ShowKind(DefaultKind);
    }

    #region 초기화

    // 인스펙터 배선이 'AuctionBuyKind'와 맞는지 본다 (Start에서 한 번).
    // 빠진 축은 눌러도 아무 일이 없어 고장처럼 보인다 — 원인이 인스펙터라는 걸 먼저 알린다.
    private void ValidateKinds()
    {
        foreach (AuctionBuyKind kind in Enum.GetValues(typeof(AuctionBuyKind)))
        {
            int count = 0;

            foreach (KindEntry entry in kinds)
            {
                if (entry.kind == kind && entry.button != null && entry.page != null)
                {
                    count++;
                }
            }

            if (count != 1)
            {
                ClientLogger.Error(ClientLogger.UI,
                    $"경매장 구매 축 '{kind}'의 버튼·화면 짝이 {count}개다 (정확히 1개여야 한다). " +
                    "Auction Buy Tab Presenter의 Kinds를 확인할 것.", this);
            }
        }
    }

    // 축 버튼을 배선한다 (Start에서 호출).
    private void BindButtons()
    {
        foreach (KindEntry entry in kinds)
        {
            if (entry.button == null)
            {
                continue;
            }

            // 반복 변수를 그대로 넘기면 모든 콜백이 마지막 값을 본다. 복사본을 캡처한다.
            AuctionBuyKind kind = entry.kind;
            entry.button.onClick.AddListener(() => ShowKind(kind));
        }
    }

    #endregion

    #region 축 전환

    // 그 축의 화면만 켜고 버튼 선택 표시를 맞춘다 (Start · 축 버튼).
    // ⚠️ Image.color를 직접 건드리지 않는다 — 색의 주인은 ColorBlock이다('AuctionTabPresenter'와 같다).
    private void ShowKind(AuctionBuyKind kind)
    {
        CurrentKind = kind;

        foreach (KindEntry entry in kinds)
        {
            bool on = entry.kind == kind;

            if (entry.page != null)
            {
                entry.page.SetActive(on);
            }

            if (entry.button == null)
            {
                continue;
            }

            Color target = UIThemePalette.Of(on ? selectedRole : normalRole);

            ColorBlock colors = entry.button.colors;
            colors.normalColor   = target;
            colors.selectedColor = target;
            entry.button.colors  = colors;
        }
    }

    #endregion
}

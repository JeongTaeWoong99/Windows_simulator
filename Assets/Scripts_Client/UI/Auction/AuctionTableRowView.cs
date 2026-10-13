using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 경매장 표 한 줄에 그릴 완성값. Presenter가 만들어 'AuctionTableRowView.Bind'에 넘긴다.
public struct AuctionTableRowContent
{
    public long            Key;        // 버튼을 누르면 되돌려 줄 값 — 매물 ID · 기록 ID
    public ItemIconContent Icon;       // 아이템 칸의 아이콘 — 등급 바탕 · 글자 · 수량 · 능력치 칸
    public string          Title;      // 이름 (등급 색) — '내 매물' 같은 꼬리표를 붙여도 된다
    public Color           TitleColor;
    public string          Sub;        // 이름 아래 한 줄 — 분류 · 능력치 · 레벨 등

    // 열 칸의 글자 — 프리팹의 'cellTexts' 순서와 같다(검색: 수량·단가·총액·남은 시간·판매자).
    public IReadOnlyList<string> Cells;

    public string? PrimaryLabel;   // 오른쪽 버튼 — '구매' · '취소'. null이면 숨긴다
    public bool    CanPrimary;
    public string? SecondaryLabel; // 그 왼쪽 버튼 — '시세'. null이면 숨긴다
    public bool    CanSecondary;

    public bool Highlight; // 줄 바탕을 강조색으로 옅게 — 내 매물 · 새 기록

    // 줄에 마우스를 올리면 띄울 툴팁 — **올리는 순간에 만든다**('TooltipTrigger'). null이면 뜨지 않는다.
    public Func<TooltipContent?>? Tooltip;
}

// 경매장 표(검색 결과 · 판매 중 · 거래 기록)의 한 줄 — 아이템 칸(아이콘 · 이름 · 한 줄 설명) + 열 칸들 + 버튼 둘.
//
// 세 표의 열 구성이 달라 프리팹은 표마다 하나씩이다(열 폭은 목업 그대로 'LayoutElement'로 고정).
// 클래스는 같다 — 무엇을 적을지는 각 Presenter가 'AuctionTableRowContent'로 완성해 넘기고,
// 버튼이 무엇을 하는지도 모른다. 눌리면 'Key'만 위로 던진다 (종속 View 규약은 'UI 규칙.md').
public class AuctionTableRowView : MonoBehaviour
{
    [CenterHeader("아이템 칸")]
    [SerializeField, Tooltip("아이콘 (ItemIconView 프리팹)")]
    private ItemIconView iconView = null!;

    [SerializeField, Tooltip("이름 (등급 색)")]
    private TMP_Text titleText = null!;

    [SerializeField, Tooltip("이름 아래 한 줄")]
    private TMP_Text subText = null!;


    [CenterHeader("열")]
    [SerializeField, NonReorderable, Tooltip("열 칸 글자 — 화면 왼쪽부터. 내용은 'AuctionTableRowContent.Cells' 순서")]
    private TMP_Text[] cellTexts = new TMP_Text[0];


    [CenterHeader("버튼")]
    [SerializeField, Tooltip("오른쪽 버튼(구매 · 취소). 없는 표면 비워 둔다. OnClick은 코드가 연결한다")]
    private Button? primaryButton;

    [SerializeField, Tooltip("오른쪽 버튼 글자")]
    private TMP_Text? primaryText;

    [SerializeField, Tooltip("그 왼쪽 버튼(시세). 없는 표면 비워 둔다. OnClick은 코드가 연결한다")]
    private Button? secondaryButton;

    [SerializeField, Tooltip("왼쪽 버튼 글자")]
    private TMP_Text? secondaryText;


    [CenterHeader("바탕")]
    [SerializeField, Tooltip("줄 바탕 — 강조 줄이면 강조색을 옅게 깐다. 레이캐스트를 받아야 툴팁이 뜬다")]
    private Image background = null!;

    [SerializeField, Tooltip("줄 루트의 툴팁 트리거. 내용은 코드가 넘긴다")]
    private TooltipTrigger tooltipTrigger = null!;

    // 강조 줄 바탕 — 패널 색에 강조색을 이만큼 섞는다(목업의 '내 매물' 줄 노랑 5%보다 조금 진하게).
    // ※ 투명도로 깔지 않는다 — 데스크톱 투명 창이라 알파가 낮은 칸은 바탕화면이 비쳐 진한 갈색으로 보였다.
    private const float HighlightMix = 0.12f;

    // 버튼이 눌렸다 — 이 줄의 'Key'를 싣는다 (목록을 가진 Presenter가 구독).
    public event Action<long>? PrimaryClicked;
    public event Action<long>? SecondaryClicked;

    private Color _normalBackground;
    private long  _key;

    // 자기 버튼만 배선한다 — 서비스를 조회하지 않으므로 Awake로 충분하다 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(iconView,       nameof(iconView));
        this.RequireRef(titleText,      nameof(titleText));
        this.RequireRef(subText,        nameof(subText));
        this.RequireRef(background,     nameof(background));
        this.RequireRef(tooltipTrigger, nameof(tooltipTrigger));

        _normalBackground = background.color;

        if (primaryButton != null)
        {
            primaryButton.onClick.AddListener(() => PrimaryClicked?.Invoke(_key));
        }

        if (secondaryButton != null)
        {
            secondaryButton.onClick.AddListener(() => SecondaryClicked?.Invoke(_key));
        }
    }

    // 이 줄이 그릴 값을 정한다 ('AuctionTableRowList'가 호출).
    public void Bind(AuctionTableRowContent content)
    {
        _key = content.Key;

        iconView.Bind(content.Icon);
        tooltipTrigger.SetProvider(content.Tooltip);

        titleText.text  = content.Title;
        titleText.color = content.TitleColor;
        subText.text    = content.Sub;

        for (int i = 0; i < cellTexts.Length; i++)
        {
            cellTexts[i].text = content.Cells != null && i < content.Cells.Count ? content.Cells[i] : "";
        }

        BindButton(primaryButton,   primaryText,   content.PrimaryLabel,   content.CanPrimary);
        BindButton(secondaryButton, secondaryText, content.SecondaryLabel, content.CanSecondary);

        background.color = content.Highlight
            ? Color.Lerp(UIThemePalette.Of(UIThemeRole.PanelSub), UIThemePalette.Of(UIThemeRole.Highlight), HighlightMix)
            : _normalBackground;
    }

    // 버튼 하나를 그린다 — 문구가 없으면 숨긴다 (Bind에서 호출).
    private static void BindButton(Button? button, TMP_Text? text, string? label, bool canAct)
    {
        if (button == null)
        {
            return;
        }

        button.gameObject.SetActive(label != null);
        button.interactable = canAct;

        if (text != null)
        {
            text.text = label ?? "";
        }
    }
}

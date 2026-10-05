using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 경매장 탭의 목록 한 줄에 그릴 완성값. Presenter가 만들어 'AuctionRowView.Bind'에 넘긴다.
public struct AuctionRowContent
{
    public long   Key;         // 버튼을 누르면 되돌려 줄 값 — 매물 ID · 자원 TID · 장비 개체 번호
    public ItemIconContent Icon; // 왼쪽 아이콘 칸 — 등급 바탕 · 글자 · 수량 · 능력치 칸
    public string Title;       // 이름
    public Color  TitleColor;  // 이름 색 — 등급 색
    public string Info;        // 둘째 줄 — 수량·남은 시간 등
    public string Detail;      // 셋째 줄 — 가격
    public string Seller;      // 판매자 줄. 비우면 줄을 숨긴다(거래소 자원 줄·등록 후보는 판매자가 없다)
    public string ActionLabel; // 버튼 문구 — '구매' · '취소' · '선택'
    public bool   CanAct;      // 버튼을 누를 수 있나
    public bool   Dimmed;      // 줄 전체를 흐리게 — 목록에는 보이지만 고를 수 없는 것(등록 불가 후보). 툴팁은 그대로 뜬다
    public bool   Selectable;  // 고르는 목록의 줄인가(등록 후보 · 거래소) — 참이면 버튼을 탭처럼 칠한다. 거짓이면 프리팹 색(구매·취소)
    public bool   Selected;    // 지금 고른 줄 — 'Selectable'일 때만 뜻이 있다. 문구('선택됨')만으로는 잘 안 보였다

    // 줄에 마우스를 올리면 띄울 툴팁 — **올리는 순간에 만든다**('TooltipTrigger'). null이면 뜨지 않는다.
    public Func<TooltipContent?>? Tooltip;
}

// 경매장 탭(구매 — 자원·장비 · 등록 · 내 매물)이 함께 쓰는 목록 한 줄. 아이콘 · 이름·정보·가격·판매자와 버튼 하나를 갖는다.
// 줄에 마우스를 올리면 상세 툴팁(등급·종류·가격 기준·능력치 칸)이 뜬다 — 줄에는 다 못 적는 것을 거기 둔다.
//
// 네 목록이 모양이 같아서 줄 하나를 같이 쓴다 — 무엇을 적을지는 각 Presenter가 'AuctionRowContent'로 완성해 넘긴다.
// 버튼이 무엇을 하는지도 모른다. 눌리면 'Key'만 위로 던진다 (종속 View 규약은 'UI 규칙.md').
public class AuctionRowView : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("왼쪽 아이콘 칸 (ItemIconView 프리팹)")]
    private ItemIconView iconView = null!;

    [SerializeField, Tooltip("줄 루트의 툴팁 트리거. 내용은 코드가 넘긴다 — 줄 바탕 Image가 레이캐스트를 받아야 뜬다")]
    private TooltipTrigger tooltipTrigger = null!;

    [SerializeField, Tooltip("이름 (등급 색)")]
    private TMP_Text titleText = null!;

    [SerializeField, Tooltip("둘째 줄 — 수량·남은 시간 등")]
    private TMP_Text infoText = null!;

    [SerializeField, Tooltip("셋째 줄 — 가격")]
    private TMP_Text detailText = null!;

    [SerializeField, Tooltip("판매자 이름 — 비면 숨긴다(거래소 자원 줄·등록 후보)")]
    private TMP_Text sellerText = null!;

    [SerializeField, Tooltip("줄의 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button actionButton = null!;

    [SerializeField, Tooltip("버튼 문구")]
    private TMP_Text actionText = null!;

    // 버튼이 눌렸다 — 이 줄의 'Key'를 싣는다 (목록을 가진 Presenter가 구독).
    public event Action<long>? ActionClicked;

    // 흐림 표시('Dimmed') — 프리팹에 없으면 Awake에서 붙인다. 레이캐스트는 막지 않는다(툴팁이 떠야 한다).
    private CanvasGroup _group = null!;

    // 흐린 줄의 투명도 — 인벤토리의 배치 중 딤과 비슷한 정도.
    private const float DimmedAlpha = 0.45f;

    // 프리팹에 구워 둔 버튼 색 — 고르지 않은 줄은 이 색으로 되돌린다('구매'·'취소' 줄도 같은 프리팹이라 색을 지어내지 않는다).
    private ColorBlock _normalColors;

    private long _key;

    // 자기 버튼만 배선한다 — 서비스를 조회하지 않으므로 Awake로 충분하다 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(iconView,       nameof(iconView));
        this.RequireRef(tooltipTrigger, nameof(tooltipTrigger));
        this.RequireRef(titleText,    nameof(titleText));
        this.RequireRef(infoText,     nameof(infoText));
        this.RequireRef(detailText,   nameof(detailText));
        this.RequireRef(sellerText,   nameof(sellerText));
        this.RequireRef(actionButton, nameof(actionButton));
        this.RequireRef(actionText,   nameof(actionText));

        _group = GetComponent<CanvasGroup>();

        if (_group == null)
        {
            _group = gameObject.AddComponent<CanvasGroup>();
        }

        _normalColors = actionButton.colors;

        actionButton.onClick.AddListener(OnActionClicked);
    }

    // 이 줄이 그릴 값을 정한다 ('AuctionRowList'가 호출).
    public void Bind(AuctionRowContent content)
    {
        _key = content.Key;

        iconView.Bind(content.Icon);
        tooltipTrigger.SetProvider(content.Tooltip);

        titleText.text            = content.Title;
        titleText.color           = content.TitleColor;
        infoText.text             = content.Info;
        detailText.text           = content.Detail;
        sellerText.text           = content.Seller ?? "";
        sellerText.gameObject.SetActive(!string.IsNullOrEmpty(content.Seller));
        actionText.text           = content.ActionLabel;
        actionButton.interactable = content.CanAct;
        _group.alpha              = content.Dimmed ? DimmedAlpha : 1f;

        PaintSelection(content.Selectable, content.Selected);
    }

    // 고르는 목록이면 탭과 같은 두 색(고름 = ButtonSelected · 안 고름 = Button)으로, 아니면 프리팹 색으로 칠한다 (Bind에서 호출).
    //
    // ⚠️ 프리팹 색은 'ButtonPrimary'(주황)다 — 'ButtonSelected'와 거의 같은 주황이라, 고른 줄만 칠하면 차이가 안 보였다(2026-10-05).
    //   그래서 안 고른 줄을 파랑('Button')으로 내린다. 탭 줄과 같은 규칙이라 "주황 = 고른 것"으로 읽힌다.
    // ⚠️ Image.color를 직접 건드리지 않는다 — 색의 주인은 ColorBlock이다(탭 버튼 'AuctionTabPresenter'와 같다).
    private void PaintSelection(bool selectable, bool selected)
    {
        if (!selectable)
        {
            actionButton.colors = _normalColors;

            return;
        }

        Color      target = UIThemePalette.Of(selected ? UIThemeRole.ButtonSelected : UIThemeRole.Button);
        ColorBlock colors = _normalColors;

        colors.normalColor      = target;
        colors.selectedColor    = target;
        colors.highlightedColor = Color.Lerp(target, Color.white, 0.15f);
        actionButton.colors     = colors;
    }

    // 버튼 눌림 — 이 줄의 키를 위로 던진다 (actionButton.onClick)
    private void OnActionClicked()
    {
        ActionClicked?.Invoke(_key);
    }
}

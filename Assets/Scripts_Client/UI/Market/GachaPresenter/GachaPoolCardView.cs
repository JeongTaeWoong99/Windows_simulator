using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 뽑기 풀 카드 한 장 — 제목(괄호) · 도움말 아이콘(?) · 대표 아이콘 · 종수 · [1회] · [10회] ('GachaPresenter'가 Bind한다).
//
// ■ 카드 하나 = 풀 하나 (T-143)
// 버튼 8개를 나열하던 화면을 풀마다 카드로 묶었다. 카드는 같은 프리팹(`GachaPoolCardView.prefab`)을 풀 수만큼 둔다.
// 무엇을 뽑을지·비용·잠금은 Presenter가 정하고, 카드는 받은 값을 그리고 누름을 위로 던지기만 한다.
public class GachaPoolCardView : MonoBehaviour
{
    [CenterHeader("머리줄")]
    [SerializeField, Tooltip("제목 옆 도움말 아이콘(HelpIcon 프리팹) — 올리면 등급 확률 툴팁. 내용은 Presenter가 SetProvider로 넘긴다")]
    private TooltipTrigger rateTrigger = null!;

    [SerializeField, Tooltip("풀 이름 + 괄호(그 풀이 올려 주는 것)")]
    private TMP_Text titleText = null!;

    [CenterHeader("왼쪽")]
    [SerializeField, Tooltip("풀 대표 아이콘 — 가장 높은 등급의 첫 항목")]
    private ItemIconView icon = null!;

    [SerializeField, Tooltip("나오는 종수 — '무기 31종'")]
    private TMP_Text kindText = null!;

    [CenterHeader("1회")]
    [SerializeField, Tooltip("1회 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button singleButton = null!;

    [SerializeField, Tooltip("1회 버튼 머리줄 — '1회'")]
    private TMP_Text singleCountText = null!;

    [SerializeField, Tooltip("1회 비용")]
    private TMP_Text singleCostText = null!;

    [CenterHeader("10회")]
    [SerializeField, Tooltip("10회 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button multiButton = null!;

    [SerializeField, Tooltip("10회 버튼 머리줄 — '10회'")]
    private TMP_Text multiCountText = null!;

    [SerializeField, Tooltip("10회 비용")]
    private TMP_Text multiCostText = null!;

    // 버튼을 눌렀다 — true면 10회 쪽이다.
    public event Action<bool>? DrawClicked;

    private long _singleCost;
    private long _multiCost;

    // 지금 비용 글자가 '모자람' 색인가 — 바뀐 때만 글자를 다시 쓴다.
    private bool? _singleShort;
    private bool? _multiShort;

    // 필수 참조 검증 · 버튼 배선 — Presenter가 Bind를 부르기 전에 끝나 있어야 한다 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(rateTrigger,     nameof(rateTrigger));
        this.RequireRef(titleText,       nameof(titleText));
        this.RequireRef(icon,            nameof(icon));
        this.RequireRef(kindText,        nameof(kindText));
        this.RequireRef(singleButton,    nameof(singleButton));
        this.RequireRef(singleCountText, nameof(singleCountText));
        this.RequireRef(singleCostText,  nameof(singleCostText));
        this.RequireRef(multiButton,     nameof(multiButton));
        this.RequireRef(multiCountText,  nameof(multiCountText));
        this.RequireRef(multiCostText,   nameof(multiCostText));

        singleButton.onClick.AddListener(() => DrawClicked?.Invoke(false));
        multiButton.onClick.AddListener(() => DrawClicked?.Invoke(true));
    }

    // 풀 하나를 그린다 (GachaPresenter.Start에서 한 번).
    //   title : '캐릭터 뽑기 (일꾼)'처럼 완성된 제목 · kind : '무기 31종'
    //   rates : 도움말 아이콘에 올렸을 때 띄울 확률 툴팁을 만드는 함수
    public void Bind(string title, string kind, in ItemIconContent iconContent,
                     int singleCount, long singleCost, int multiCount, long multiCost,
                     Func<TooltipContent?> rates)
    {
        titleText.text       = title;
        kindText.text        = kind;
        singleCountText.text = $"{singleCount}회";
        multiCountText.text  = $"{multiCount}회";

        icon.Bind(iconContent);
        rateTrigger.SetProvider(rates);

        _singleCost  = singleCost;
        _multiCost   = multiCost;
        _singleShort = null;
        _multiShort  = null;
    }

    // 버튼 잠금과 비용 색을 맞춘다 (GachaPresenter.Refresh에서 호출).
    //   isWaiting : 앞 요청의 응답을 기다리는 중 — 둘 다 잠근다. 비용 색은 골드만 본다
    public void SetState(long gold, bool isWaiting)
    {
        bool singleShort = gold < _singleCost;
        bool multiShort  = gold < _multiCost;

        singleButton.interactable = !isWaiting && !singleShort;
        multiButton.interactable  = !isWaiting && !multiShort;

        if (_singleShort != singleShort)
        {
            _singleShort        = singleShort;
            singleCostText.text = CostText(_singleCost, singleShort);
        }

        if (_multiShort != multiShort)
        {
            _multiShort        = multiShort;
            multiCostText.text = CostText(_multiCost, multiShort);
        }
    }

    // 비용 글자 — 모자라면 경고색, 아니면 재화색.
    private static string CostText(long cost, bool isShort)
        => UIRichText.Paint($"{cost:N0} G", isShort ? UIThemeRole.Negative : UIThemeRole.Highlight);
}

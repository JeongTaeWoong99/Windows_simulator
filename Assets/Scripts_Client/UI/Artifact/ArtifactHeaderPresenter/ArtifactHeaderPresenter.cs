using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 아티팩트 화면 머리 줄 — 제목 · 도움말(?) · 보유 골드 · [X]. [X]가 화면을 닫는다.
// 경매장 머리 줄('AuctionHeaderPresenter')과 같은 모양이다 — 화면이 하단 메뉴를 덮어 연 버튼으로 다시 닫을 수 없다.
// ※ 키(Esc)로는 닫지 않는다 — 인게임 키는 마우스와 함께 쓰는 것만 둔다('KeyInputManager' 머리말).
public class ArtifactHeaderPresenter : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("보유 골드 글자")]
    private TMP_Text goldText = null!;

    [SerializeField, Tooltip("닫기 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button closeButton = null!;

    [SerializeField, Tooltip("제목 옆 도움말 아이콘(HelpIcon 프리팹)의 툴팁 트리거 — 얻는 법 · 장착 · 켜고 끄기 · 파는 법")]
    private TooltipTrigger helpTooltip = null!;

    private UIManager       _ui   = null!;
    private PlayerDataModel _data = null!;

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 배선 → 초기화 순서로 진행한다 (클라 공통 규약)
    private void Start()
    {
        this.RequireRef(goldText,    nameof(goldText));
        this.RequireRef(closeButton, nameof(closeButton));
        this.RequireRef(helpTooltip, nameof(helpTooltip));

        _ui   = Services.Get<UIManager>();
        _data = Services.Get<PlayerDataModel>();

        Subscribe();

        closeButton.onClick.AddListener(() => _ui.ShowArtifact(false));
        helpTooltip.SetProvider(BuildGuideTooltip);

        RefreshGold();

        _isReady = true;
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    private void OnEnable()
    {
        if (_isReady)
        {
            Subscribe();
            RefreshGold();
        }
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
    }

    // 재화 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed = true;

        _data.CurrencyChanged += RefreshGold;
        Lang.Changed          += RefreshGold;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed = false;

        _data.CurrencyChanged -= RefreshGold;
        Lang.Changed          -= RefreshGold;
    }

    // 보유 골드를 그린다 (Start · OnEnable · PlayerDataModel.CurrencyChanged 구독) — 팔면 굴러 오른다
    private void RefreshGold()
    {
        string label = UIRichText.Label(Lang.Get("UI.Auction.GoldOwned"));

        NumberRoll.Set(goldText, _data.Gold, v => $"{label}  {UIRichText.Gold((long)Math.Round(v))}", punch: false);
    }

    // 도움말 — 얻는 법 · 장착 · 켜고 끄기 · 파는 법.
    private static TooltipContent BuildGuideTooltip()
        => new TooltipContent(Lang.Get("UI.Artifact.HelpTitle"))
            .Row(Lang.Get("UI.Artifact.HelpGet"), "")
            .Row(Lang.Get("UI.Artifact.HelpEquip"), "")
            .Row(Lang.Get("UI.Artifact.HelpToggle"), "")
            .Row(Lang.Get("UI.Artifact.HelpSell"), "");
}

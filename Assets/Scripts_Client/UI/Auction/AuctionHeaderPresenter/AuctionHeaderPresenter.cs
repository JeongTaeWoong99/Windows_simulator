using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 경매장 머리 줄 — 제목 · 도움말(?) · 보유 골드 · [X]. [X]가 경매장을 닫는다 (T-147).
//
// ■ 왜 닫는 길이 따로 있나
//   경매장이 가운데 띠를 다 덮어 하단 메뉴의 [경매장] 버튼이 가려진다 — 연 버튼으로 다시 닫을 수 없다.
// ■ 키(Esc)로는 닫지 않는다 — 인게임 키는 마우스와 함께 쓰는 것만 둔다('KeyInputManager' 머리말 · 2026-10-15 사용자 지시).
public class AuctionHeaderPresenter : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("보유 골드 글자")]
    private TMP_Text goldText = null!;

    [SerializeField, Tooltip("닫기 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button closeButton = null!;

    [SerializeField, Tooltip("제목 옆 도움말 아이콘(HelpIcon 프리팹)의 툴팁 트리거 — 사는 법 · 우편")]
    private TooltipTrigger helpTooltip = null!;

    private UIManager       _ui   = null!;
    private PlayerDataModel _data = null!;

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 배선 → 초기화 순서로 진행한다 (클라 공통 규약)
    private void Start()
    {
        this.RequireRef(goldText,       nameof(goldText));
        this.RequireRef(closeButton,    nameof(closeButton));
        this.RequireRef(helpTooltip,    nameof(helpTooltip));

        _ui   = Services.Get<UIManager>();
        _data = Services.Get<PlayerDataModel>();

        Subscribe();

        closeButton.onClick.AddListener(Close);
        helpTooltip.SetProvider(AuctionText.BuildGuideTooltip);

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

    // 보유 골드를 그린다 (Start · OnEnable · PlayerDataModel.CurrencyChanged 구독)
    private void RefreshGold()
    {
        string label = UIRichText.Label(Lang.Get("UI.Auction.GoldOwned"));

        // 사고팔아 바뀌면 굴러간다 — 글자를 튀기지 않는다(머리 줄이라 '보유 골드' 글자까지 함께 커진다)
        NumberRoll.Set(goldText, _data.Gold, v => $"{label}  {UIRichText.Gold((long)Math.Round(v))}", punch: false);
    }

    // 경매장을 닫는다 (closeButton OnClick)
    private void Close()
    {
        _ui.ShowAuction(false);
    }
}

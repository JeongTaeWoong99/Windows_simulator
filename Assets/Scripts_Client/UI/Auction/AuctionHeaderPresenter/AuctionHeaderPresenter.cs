using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 경매장 머리 줄 — 제목 · 보유 골드 · [X]. [X]와 Esc가 경매장을 닫는다 (T-147).
//
// ■ 왜 닫는 길이 따로 있나
//   경매장이 가운데 띠를 다 덮어 하단 메뉴의 [경매장] 버튼이 가려진다 — 연 버튼으로 다시 닫을 수 없다.
//
// ■ Esc는 경매장 위에 아무것도 막고 있지 않을 때만 닫는다 ('AuctionCanvasView.IsCovered')
//   확인·수량 팝업이 떠 있을 때 Esc가 뒤의 경매장을 닫으면 팝업만 홀로 남는다.
//   로딩 중에 닫으면 안의 Presenter가 구독을 끊어 응답을 놓친다 — 클릭은 로딩 차단막이 막지만 키는 못 막는다.
// ■ 판매 등록 창이 떠 있으면 Esc는 그 창부터 닫는다 — 창은 경매장 안에 있어 'IsCovered'에 걸리지 않는다.
public class AuctionHeaderPresenter : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("보유 골드 글자")]
    private TMP_Text goldText = null!;

    [SerializeField, Tooltip("닫기 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button closeButton = null!;

    [SerializeField, Tooltip("경매장 캔버스 — 위에 막는 오버레이가 떠 있는지 묻는다")]
    private AuctionCanvasView canvasView = null!;

    [SerializeField, Tooltip("판매 등록 창 — 떠 있으면 Esc가 이것부터 닫는다")]
    private AuctionRegisterPresenter registerWindow = null!;

    private UIManager       _ui   = null!;
    private KeyInputManager _keys = null!;
    private PlayerDataModel _data = null!;

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 배선 → 초기화 순서로 진행한다 (클라 공통 규약)
    private void Start()
    {
        this.RequireRef(goldText,       nameof(goldText));
        this.RequireRef(closeButton,    nameof(closeButton));
        this.RequireRef(canvasView,     nameof(canvasView));
        this.RequireRef(registerWindow, nameof(registerWindow));

        _ui   = Services.Get<UIManager>();
        _keys = Services.Get<KeyInputManager>();
        _data = Services.Get<PlayerDataModel>();

        Subscribe();

        closeButton.onClick.AddListener(Close);

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
    }

    // Esc를 본다 (Unity 메시지). 경매장이 열려 있을 때만 돈다 — 닫히면 이 오브젝트도 꺼진다.
    private void Update()
    {
        if (!_keys.WasClosePressed || canvasView.IsCovered)
        {
            return;
        }

        if (registerWindow.IsOpen)
        {
            registerWindow.Close();

            return;
        }

        Close();
    }

    // 보유 골드를 그린다 (Start · OnEnable · PlayerDataModel.CurrencyChanged 구독)
    private void RefreshGold()
    {
        goldText.text = $"{UIRichText.Label("보유 골드")}  {UIRichText.Gold(_data.Gold)}";
    }

    // 경매장을 닫는다 (closeButton OnClick · Esc)
    private void Close()
    {
        _ui.ShowAuction(false);
    }
}

using System.Collections.Generic;
using GameData;
using MikaNetwork;
using MikaProtocol;
using UnityEngine;

// 상점 패널 — 거점 열의 상점 화면. 품목마다 카드 한 장(1장 · 10장 구매)을 둔다 (T-184 · #78).
//
// ■ 품목은 'ShopTable'(Shop.xlsx)에서 읽는다
// 카드는 품목 수만큼 프리팹에서 한 번 만든다 — 엑셀에 품목이 늘면 화면도 저절로 는다.
// 카드는 뽑기 화면과 같은 'GachaPoolCardView'다. 뽑기권은 여는 풀(OpenGachaId)이 있어 확률 툴팁도 그대로 쓴다.
//
// ■ 가격은 서버가 정한다
// 클라는 품목과 개수만 보낸다. 10장 비용은 'PriceGold × 10'으로 미리 보여 줄 뿐이다 — 서버도 같은 곱으로 받는다.
//
// ■ 산 뽑기권은 인벤토리에서 연다
// 상자와 같은 'C_ItemUseRequest'로 열리고 결과도 같은 팝업에 뜬다 — 여기서 열지 않는다.
// 카드의 '보유 n장'이 인벤토리에 들어온 것을 보여 준다.
public class ShopPresenter : MonoBehaviour
{
    // 버튼 두 개의 구매 개수. 서버가 받는 범위는 1~50장이다('InvalidShopRequest').
    private const int SingleCount = 1;
    private const int MultiCount  = 10;

    // 카드 하나의 품목 — 버튼 잠금 판정이 매 재화 변경마다 돌므로 가격을 미리 읽어 둔다.
    private readonly struct ShopEntry
    {
        public readonly ShopTableRow      Row;
        public readonly GachaPoolCardView Card;

        public ShopEntry(ShopTableRow row, GachaPoolCardView card)
        {
            Row  = row;
            Card = card;
        }
    }

    [CenterHeader("참조")]
    [SerializeField, Tooltip("품목 카드 프리팹 (GachaPoolCardView — 뽑기 화면과 같은 카드)")]
    private GachaPoolCardView cardPrefab = null!;

    [SerializeField, Tooltip("카드를 놓을 곳 (스크롤 Content)")]
    private Transform cardParent = null!;

    private readonly List<ShopEntry> _entries = new List<ShopEntry>();

    private PlayerDataModel   _data    = null!;
    private NetworkManager    _network = null!;
    private ServerWaitManager _wait    = null!;

    // 진행 중인 구매 대기의 손잡이. 응답이 오면 결과를 보고하고, 무응답이면 스스로 타임아웃돼 잠금을 푼다.
    private ServerWaitHandle? _waitHandle;

    // 응답을 기다리는 중인가 — 연타로 두 번 나가면 골드가 꼬이므로 버튼을 잠근다.
    private bool _isWaiting;

    // 마지막 구매의 개수 — 응답에 개수가 없어 완료 토스트에 쓰려고 기억해 둔다.
    private int _lastCount;

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 배선 → 초기화 순서로 진행한다 (클라 공통 규약)
    private void Start()
    {
        this.RequireRef(cardPrefab, nameof(cardPrefab));
        this.RequireRef(cardParent, nameof(cardParent));

        _data    = Services.Get<PlayerDataModel>();
        _network = NetworkManager.Instance;
        _wait    = Services.Get<ServerWaitManager>();

        BuildCards();

        // 카드 글씨는 한 번 그리고 두므로 언어가 바뀔 때만 다시 쓴다 — 꺼져 있어도 받는다
        Lang.Changed += RedrawCardTexts;

        Subscribe();
        Refresh(); // 로그인 재화·인벤토리가 이미 와 있을 수 있다

        _isReady = true;
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    // ★ 닫혀 있는 동안 골드·보유 수가 바뀌었을 수 있다 — 다시 그린다.
    private void OnEnable()
    {
        if (_isReady)
        {
            Subscribe();
            Refresh();
        }
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Lang.Changed -= RedrawCardTexts;
    }

    #region 구독

    // 구매 성공·실패 도착과 재화·인벤토리 변경 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed           = true;
        _data.ShopBuyCompleted += OnShopBuyCompleted;
        _data.ShopBuyFailed    += OnShopBuyFailed;
        _data.CurrencyChanged  += Refresh;
        _data.InventoryChanged += Refresh;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed           = false;
        _data.ShopBuyCompleted -= OnShopBuyCompleted;
        _data.ShopBuyFailed    -= OnShopBuyFailed;
        _data.CurrencyChanged  -= Refresh;
        _data.InventoryChanged -= Refresh;
    }

    #endregion

    #region 초기화

    // 품목마다 카드를 만들고 버튼을 배선한다 (Start에서 한 번).
    private void BuildCards()
    {
        IReadOnlyList<ShopTableRow> rows = GameDataLoader.GetShopItems();

        if (rows.Count == 0)
        {
            ClientLogger.Error(ClientLogger.UI, "ShopTable에 품목이 하나도 없다 — 상점이 빈다.", this);
        }

        foreach (ShopTableRow row in rows)
        {
            GachaPoolCardView card = Instantiate(cardPrefab, cardParent);
            card.name = $"Shop Card {row.ShopTID}";

            // 반복 변수를 그대로 넘기면 모든 콜백이 마지막 값을 본다. 복사본을 캡처한다.
            int shopTid = row.ShopTID;
            card.DrawClicked += isMulti => Buy(shopTid, isMulti ? MultiCount : SingleCount);

            _entries.Add(new ShopEntry(row, card));
            DrawCard(row, card);
        }
    }

    // 카드 글씨를 지금 언어로 다시 쓴다 (Lang.Changed 구독).
    private void RedrawCardTexts()
    {
        foreach (ShopEntry entry in _entries)
        {
            DrawCard(entry.Row, entry.Card);
        }

        Refresh();
    }

    // 카드 한 장의 제목·아이콘·확률 툴팁·버튼 글씨를 그린다 (BuildCards · 언어 변경).
    // 여는 풀이 있으면(뽑기권) 제목 괄호와 확률 툴팁을 그 풀에서 센다 — 뽑기 카드와 같은 계산이다.
    private static void DrawCard(ShopTableRow row, GachaPoolCardView card)
    {
        string itemName = GameDataLoader.GetItemName(row.ItemTID);
        int    gachaId  = GameDataLoader.GetOpenGachaId(row.ItemTID);

        GachaPoolSummary? summary = gachaId != 0 ? GachaPoolSummary.Build(gachaId, itemName) : null;

        string subtitle = summary?.Subtitle ?? "";
        string title    = subtitle.Length > 0
            ? $"{itemName} {UIRichText.Small(UIRichText.Label($"({subtitle})"))}"
            : itemName;

        card.Bind(title, "", ItemIconContent.ForItem(row.ItemTID, 1L),
                  Lang.Format("UI.Shop.Count", SingleCount), row.PriceGold * SingleCount,
                  Lang.Format("UI.Shop.Count", MultiCount),  row.PriceGold * MultiCount,
                  () => summary?.BuildRateTooltip());
    }

    #endregion

    #region 구매 요청

    // 'shopTid' 품목을 'count'개 산다 (카드의 DrawClicked에 코드로 연결).
    //
    // 로그인 전에 보내면 서버가 User를 못 찾아 조용히 버린다 — 보내기 전에 여기서 끊고 이유를 남긴다.
    private void Buy(int shopTid, int count)
    {
        if (_isWaiting)
        {
            return; // 앞 요청의 응답을 기다리는 중 — 연타 방지
        }

        if (!_data.IsLoggedIn)
        {
            ClientLogger.Warn(ClientLogger.Send, "상점 구매 요청을 보내지 않았다 — 로그인이 먼저다(서버가 응답 없이 버린다)");

            return;
        }

        _lastCount = count;

        _network.Send(new C_ShopBuyRequest
        {
            ShopTID = shopTid,
            Count   = count
        });

        ClientLogger.Info(ClientLogger.Send, $"상점 구매 요청 — ShopTID={shopTid}, {count}개");

        // 대기 시작 — 로딩 표시·무응답 감시·알림은 ServerWaitManager가 공통으로 처리한다.
        _isWaiting = true;
        Refresh();
        _waitHandle = _wait.Begin(Lang.Format("UI.Shop.Wait", count), onClosed: OnWaitClosed);
    }

    // 대기가 끝났다(성공·실패·타임아웃 공통) — 버튼을 다시 연다 (ServerWaitManager.Begin의 onClosed)
    private void OnWaitClosed()
    {
        _isWaiting  = false;
        _waitHandle = null;
        Refresh();
    }

    // 구매 성공 도착 — 대기를 닫고 무엇을 샀는지 한 줄 알린다 (PlayerDataModel.ShopBuyCompleted 구독)
    // ※ 산 것은 인벤토리로 바로 들어온다 — 어디서 여는지를 함께 말한다.
    private void OnShopBuyCompleted(int shopTid)
    {
        _waitHandle?.Succeed();

        string itemName = GameDataLoader.TryGetShopItem(shopTid, out ShopTableRow row)
            ? GameDataLoader.GetItemName(row.ItemTID)
            : shopTid.ToString();

        _wait.RaiseToast(Lang.Format("UI.Shop.Bought", itemName, _lastCount));
    }

    // 구매 실패 도착 — 사유를 사람이 읽을 문구로 옮겨 알림에 띄운다 (PlayerDataModel.ShopBuyFailed 구독)
    private void OnShopBuyFailed(EResultCode code)
    {
        _waitHandle?.Fail(ResultMessages.ToText(code));
    }

    #endregion

    #region 표시 갱신

    // 버튼 잠금과 보유 수를 지금 상태에 맞춘다 (Start · OnEnable · 재화·인벤토리 변경 · 대기 시작/종료).
    //
    // 두 축을 함께 본다 — 대기 중이면 전부 잠기고, 대기가 풀려도 골드가 모자란 버튼은 잠긴 채로 남는다.
    // ※ 클라 판단은 표시용일 뿐이다. 실제 거절은 서버가 한다('NotEnoughCurrency' · 'StorageFull').
    private void Refresh()
    {
        foreach (ShopEntry entry in _entries)
        {
            entry.Card.SetState(_data.Gold, _isWaiting);
            entry.Card.SetKind(Lang.Format("UI.Shop.Owned", _data.GetItemCount(entry.Row.ItemTID)));
        }
    }

    #endregion
}

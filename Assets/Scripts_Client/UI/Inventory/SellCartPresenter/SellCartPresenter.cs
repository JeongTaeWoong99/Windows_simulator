using System.Collections.Generic;
using GameData;
using MikaNetwork;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// 인벤토리 오른쪽의 판매 목록 — 담은 것들을 보여 주고 한 번에 판다.
//
// ■ 무엇을 보여 주나
// 자원·캐릭터·장비 칸을 우클릭해 담은 것들('SellCartModel')의 줄 목록·합계 골드·판매 버튼이다.
// 여기서 파는 값은 **즉시 판매가**(정해진 값)다. 더 비싸게 팔려면 경매장에 올린다(즉시 판매가 이상 — 'Market 규칙.md').
// 담고 → 판매 버튼, 이 두 단계가 곧 확인 절차라 별도 확인 모달을 두지 않는다
// (화면 중앙 팝업을 최소화한다 — 기획 P1).
//
// ■ 담긴 내용을 여기서 들고 있지 않다
// 격자('InventoryGridPresenter')도 같은 목록을 봐야 담김 표시를 켤 수 있다. 둘 중 하나가
// 들고 있으면 패널끼리 서로를 참조하게 되므로, 상태는 'SellCartModel'에 두고 양쪽이 구독한다
// ('Inventory 규칙.md').
//
// ※ 이 자리는 원래 "고른 항목의 상세"였다. 2026-09-04에 판매 목록이 들어오면서 역할이 바뀌었고,
//   이름도 2026-09-12에 'StorageInformationPresenter' → 'SellCartPresenter'로 맞췄다
//   ('SellCartModel'과 짝이 된다). 칸의 상세 정보는 이 패널로 돌아오지 않는다 —
//   커서 옆 호버 UI로 간다(일감 'T-050').
//
// ■ 자원과 개체는 패킷이 다르다 (T-075 · 2026-10-03)
//   자원은 'C_ItemSellRequest', 캐릭터·장비는 'C_EntitySellRequest'. 섞어 담았으면 [판매] 한 번에 둘 다 보내고
//   응답 둘을 다 받은 뒤에 대기를 닫는다. 각각 전부 되거나 전혀 안 되므로, 한쪽만 실패하면
//   성공한 쪽만 목록에서 빠지고 실패한 쪽은 사유와 함께 남는다.
public class SellCartPresenter : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("판매 줄 프리팹 (SellCartRowView 포함). 담긴 수만큼 만들어 재사용한다")]
    private SellCartRowView rowPrefab = null!;

    [SerializeField, Tooltip("줄이 쌓이는 부모 — Sell Scroll View Panel > Viewport > Content")]
    private RectTransform rowParent = null!;

    [SerializeField, Tooltip("담긴 것이 없을 때의 안내 문구. 빈 칸은 고장과 구분되지 않는다")]
    private TMP_Text emptyText = null!;

    [SerializeField, Tooltip("합계 골드")]
    private TMP_Text totalText = null!;

    [SerializeField, Tooltip("상위 등급이 담겼을 때만 켜지는 경고. 오판매를 막는 자리다")]
    private GameObject highRarityWarning = null!;

    [SerializeField, Tooltip("판매 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button sellButton = null!;

    [SerializeField, Tooltip("비우기 버튼 — 담은 것을 모두 빼고 패널을 닫는다. OnClick은 코드가 연결한다")]
    private Button clearButton = null!;

    [SerializeField, Tooltip("인벤토리 탭 줄. 특성 탭에서는 이 패널이 물러난다")]
    [FormerlySerializedAs("storageTabs")]
    private InventoryTabPresenter inventoryTabs = null!;

    [SerializeField, Tooltip("큐브 창 — 이 자리를 빌려 쓴다. 열리면 판매 목록을 비우고 물러난다")]
    private EquipEnchantPresenter equipEnchant = null!;

    [SerializeField, Tooltip("응축 창 — 이 자리를 빌려 쓴다. 열리면 판매 목록을 비우고 물러난다")]
    private CharacterCondensePresenter characterCondense = null!;

    // 만들어 둔 줄. 담고 빼기를 반복하므로 파괴하지 않고 꺼 두었다가 다시 쓴다.
    private UIRowList<SellCartRowView> _rows = null!;

    private PlayerDataModel   _data    = null!;
    private SellCartModel     _cart    = null!;
    private NetworkManager    _network = null!;
    private ServerWaitManager _wait    = null!;

    // 진행 중인 판매 대기의 손잡이. 응답이 오면 결과를 보고하고, 무응답이면 스스로 타임아웃돼 잠금을 푼다.
    private ServerWaitHandle? _waitHandle;

    // 응답을 기다리는 중인가 — 연타로 두 번 나가면 두 번 팔린다.
    private bool _isWaiting;

    // 아직 안 온 응답 수(자원·개체 각 1) · 그 사이에 받은 실패 — 둘 다 오면 대기를 한 번에 닫는다.
    private int          _pendingResponses;
    private EResultCode? _failedCode;

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 배선 → 초기화 순서로 진행한다 (클라 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다.
    private void Start()
    {
        this.RequireRef(rowPrefab,         nameof(rowPrefab));
        this.RequireRef(rowParent,         nameof(rowParent));
        this.RequireRef(emptyText,         nameof(emptyText));
        this.RequireRef(totalText,         nameof(totalText));
        this.RequireRef(highRarityWarning, nameof(highRarityWarning));
        this.RequireRef(sellButton,        nameof(sellButton));
        this.RequireRef(clearButton,       nameof(clearButton));
        this.RequireRef(inventoryTabs,     nameof(inventoryTabs));
        this.RequireRef(equipEnchant,      nameof(equipEnchant));
        this.RequireRef(characterCondense, nameof(characterCondense));

        _data    = Services.Get<PlayerDataModel>();
        _cart    = Services.Get<SellCartModel>();
        _network = NetworkManager.Instance;
        _wait    = Services.Get<ServerWaitManager>();

        // ⚠️ 탭 구독만 Start/OnDestroy에 건다 — 이 패널은 자기 오브젝트를 끄기 때문이다.
        //    'OnDisable'에서 풀면 다시 켤 신호를 받을 길이 사라져 자원 탭에 영영 못 돌아온다
        //    (도구 줄이 같은 이유로 그렇게 한다 → 'Inventory 규칙.md').
        inventoryTabs.TabChanged  += ApplyTab;
        equipEnchant.OpenChanged  += OnEnchantOpenChanged;
        characterCondense.OpenChanged += OnEnchantOpenChanged;
        _cart.Changed             += OnCartChanged; // 비어 있으면 꺼지므로 켜는 신호도 여기서 받는다

        // 줄은 재사용하므로 만들 때 한 번만 구독한다 — 다시 걸면 중복으로 쌓인다.
        _rows = new UIRowList<SellCartRowView>(rowPrefab, rowParent, row => row.RemoveClicked += OnRowRemoveClicked, row => row.Clear());

        Subscribe();
        sellButton.onClick.AddListener(OnSellClicked);
        clearButton.onClick.AddListener(OnClearClicked);
        Refresh(); // 인벤토리를 닫아 둔 사이에 담긴 것이 있을 수 있다

        _isReady = true;

        ApplyTab(inventoryTabs.CurrentTab); // 탭 줄이 먼저 돌아 통지가 지나갔을 수 있다
    }

    // 탭 구독 해제 (Unity 메시지). 자기 오브젝트를 끄므로 여기서만 푼다.
    private void OnDestroy()
    {
        inventoryTabs.TabChanged -= ApplyTab;
        equipEnchant.OpenChanged -= OnEnchantOpenChanged;
        characterCondense.OpenChanged -= OnEnchantOpenChanged;
        _cart.Changed             -= OnCartChanged;
    }

    // 판매 목록을 보일지 정한다 (Start · TabChanged · 카트 변경 · 대기 종료).
    //
    // **담긴 것이 있을 때만 보인다(2026-10-09 사용자 결정)** — 비어 있는 목록이 자리를 차지하면
    // 격자가 반으로 줄어든다. 꺼지면 격자(flexH 1)가 그 자리까지 넓힌다.
    // 칸을 우클릭해 담는 순간 나타나고, 마지막 줄을 빼거나 [비우기]·판매를 마치면 사라진다.
    // ※ 판매 응답을 기다리는 동안은 비어도 남는다 — 응답 전에 카트가 먼저 비워진다.
    // ※ 특성 탭에서는 사라진다 — 격자 자체가 물러나고 트리가 그 자리를 쓴다.
    //   담아 둔 목록은 'SellCartModel'에 남아 있으므로 돌아오면 그대로 보인다.
    // ※ 큐브 창·응축 창이 열려 있어도 물러난다 — 같은 자리를 빌려 쓴다(T-095 · T-130).
    private void ApplyTab(InventoryTab tab)
    {
        bool hasContent = _cart.Count > 0 || _isWaiting;

        gameObject.SetActive(hasContent && tab != InventoryTab.Trait && !equipEnchant.IsOpen && !characterCondense.IsOpen);
    }

    // 카트가 바뀌었다 — 비었으면 물러나고, 처음 담겼으면 나타난다 (SellCartModel.Changed 구독 · Start/OnDestroy).
    private void OnCartChanged()
    {
        ApplyTab(inventoryTabs.CurrentTab);
    }

    // 담은 것을 모두 뺀다 — 비면 'OnCartChanged'가 패널을 닫는다 (clearButton OnClick에 코드로 연결).
    private void OnClearClicked()
    {
        if (!_isWaiting)
        {
            _cart.Clear();
        }
    }

    // 큐브 창·응축 창이 열리고 닫혔다 — 열리면 목록을 비우고 물러난다 (둘의 OpenChanged 구독).
    // ※ 응축 창도 비운다 — 재료로 고른 캐릭터가 카트에도 남아 있으면 판매와 응축이 같은 개체를 두고 다툰다.
    //
    // ★ 비우는 이유(2026-10-03 사용자 결정): 보이지 않는 카트에 담긴 것이 남으면, 돌아왔을 때
    //   "언제 담았지"가 되고 큐브를 쓰려던 장비가 카트에 섞여 함께 팔릴 수 있다.
    // ※ 판매 응답을 기다리는 중이면 비우지 않는다 — 응답이 성공한 쪽을 스스로 비운다.
    private void OnEnchantOpenChanged(bool open)
    {
        if (open && !_isWaiting)
        {
            _cart.Clear();
        }

        ApplyTab(inventoryTabs.CurrentTab);
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    //
    // ★ 재구독만으로는 부족하다 — 닫혀 있는 동안 채취로 수량이 줄어 카트가 깎였을 수 있다.
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

    #region 구독

    // 판매 목록 변경·판매 응답 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed              = true;
        _cart.Changed             += Refresh;
        _data.ItemSellCompleted   += OnSellCompleted;
        _data.ItemSellFailed      += OnSellFailed;
        _data.EntitySellCompleted += OnEntitySellCompleted;
        _data.EntitySellFailed    += OnSellFailed;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed              = false;
        _cart.Changed             -= Refresh;
        _data.ItemSellCompleted   -= OnSellCompleted;
        _data.ItemSellFailed      -= OnSellFailed;
        _data.EntitySellCompleted -= OnEntitySellCompleted;
        _data.EntitySellFailed    -= OnSellFailed;
    }

    #endregion

    #region 목록 그리기

    // 줄 툴팁 — 담은 수량 · 보유 · 개당 즉시 판매가 · 합계, 그리고 경매로 올리면의 단가 범위.
    // ※ 담긴 수량은 올리는 순간의 카트에서 읽는다 — 줄을 그린 뒤 담기·빼기가 있었을 수 있다.
    private TooltipContent? BuildRowTooltip(int itemId)
    {
        long count = 0L;

        foreach (ItemInfo entry in _cart.Entries)
        {
            if (entry.ItemId == itemId)
            {
                count = entry.Count;
            }
        }

        if (count <= 0L)
        {
            return null;
        }

        GlobalRarity rarity    = GameDataLoader.GetItemRarity(itemId);
        int          basePrice = GameDataLoader.GetItemPrice(itemId);

        return new TooltipContent(GameDataLoader.GetItemName(itemId))
            .Row("등급", RarityLabel.Get(rarity), "", RarityPalette.Get(rarity))
            .Row("담은 수량", $"{count:N0} 개", $"보유 {_data.GetItemCount(itemId):N0}", null)
            .Row("즉시 판매가", $"{AuctionModel.InstantSellPrice(basePrice):N0} 골드", "개당", null)
            .Row("받을 골드", $"{AuctionModel.InstantSellTotal(basePrice, count):N0} 골드")
            .Header("경매로 올리면")
            .Row("경매 등록가", AuctionModel.FormatBand(basePrice), "개당 단가", null)
            .Row("경매장 [등록] 탭에서 직접 정합니다", "");
    }

    // 개체 줄 툴팁 — 등급 · 즉시 판매가, 그리고 경매로 올리면의 단가 범위.
    private static TooltipContent BuildEntityTooltip(string name, GlobalRarity rarity, int basePrice)
    {
        return new TooltipContent(name)
            .Row("등급", RarityLabel.Get(rarity), "", RarityPalette.Get(rarity))
            .Row("즉시 판매가", $"{AuctionModel.InstantSellTotal(basePrice, 1):N0} 골드")
            .Header("경매로 올리면")
            .Row("경매 등록가", AuctionModel.FormatBand(basePrice), "", null)
            .Row("경매장 [등록] 탭에서 직접 정합니다", "");
    }

    // 담긴 목록·합계·경고·버튼 상태를 화면에 반영한다 (Start · OnEnable · SellCartModel.Changed 구독).
    // 줄 순서 = 자원 → 캐릭터 → 장비.
    private void Refresh()
    {
        int index = 0;

        foreach (ItemInfo entry in _cart.Entries)
        {
            SellCartRowView row       = _rows.Get(index++);
            int             itemId    = entry.ItemId;
            int             basePrice = GameDataLoader.GetItemPrice(itemId);

            row.Bind(SellCartKind.Item,
                     itemId,
                     GameDataLoader.GetItemName(itemId),
                     entry.Count,
                     AuctionModel.InstantSellTotal(basePrice, entry.Count),
                     ItemIconContent.ForItem(itemId, 0L),
                     () => BuildRowTooltip(itemId));
        }

        foreach (long characterId in _cart.CharacterIds)
        {
            SellCartRowView row       = _rows.Get(index++);
            int             tid       = _data.GetCharacterTid(characterId);
            string          name      = GameDataLoader.GetCharacterName(tid);
            int             basePrice = GameDataLoader.GetCharacterPrice(tid);
            GlobalRarity    rarity    = GameDataLoader.GetCharacterRarity(tid);

            row.Bind(SellCartKind.Character,
                     characterId,
                     name,
                     1,
                     AuctionModel.InstantSellTotal(basePrice, 1),
                     ItemIconContent.ForCharacter(tid),
                     () => BuildEntityTooltip(name, rarity, basePrice));
        }

        foreach (EquipInfo equip in _data.Equips)
        {
            if (!_cart.ContainsEquip(equip.EquipId) || !GameDataLoader.TryGetEquip(equip.EquipTid, out EquipTableRow equipRow))
            {
                continue;
            }

            SellCartRowView row       = _rows.Get(index++);
            string          name      = equipRow.Name;
            int             basePrice = equipRow.BasePrice;
            GlobalRarity    rarity    = equipRow.GlobalRarity;

            row.Bind(SellCartKind.Equip,
                     equip.EquipId,
                     name,
                     1,
                     AuctionModel.InstantSellTotal(basePrice, 1),
                     ItemIconContent.ForEquip(equip.EquipTid, equip.EnchantOptions),
                     () => BuildEntityTooltip(name, rarity, basePrice));
        }

        int count = index;

        _rows.HideFrom(count);

        emptyText.gameObject.SetActive(count == 0);
        // '즉시 판매'라고 못 박는다 — 경매장(직접 가격, 즉시 판매가 이상)과 다른 파는 법이라는 걸 합계 줄에서 말한다.
        totalText.text = $"즉시 판매 {_cart.TotalPrice:N0} G";

        // 상위 등급은 다시 모으기 어렵다. 목록에서 빼지 않고 눈에 띄게만 알린다 —
        // 빼 버리면 "왜 안 담기지"가 되고, 팔 자유는 남겨 둔다.
        highRarityWarning.SetActive(_cart.HasHighRarity);

        ApplySellButton();

        // 방금 만든 줄은 아직 프리팹에 저장된 크기 그대로다 — uGUI의 레이아웃 계산은
        // 이 프레임 **맨 끝**(Canvas.willRenderCanvases)에 돌기 때문이다.
        // 그 사이 'WidgetPositionLayout.VerifyNoOverflow'가 'LateUpdate'에서 훑고 지나가
        // "자식이 부모보다 넓다" → 다음 검사에서 "해소됐다"가 왕복으로 찍힌다.
        // 여기서 미리 태워 두면 아무도 반쯤 놓인 줄을 보지 않는다.
        // ※ 프리팹 크기를 부모보다 작게 저장해 피하는 방법은 자식 하나만 커도 다시 터진다.
        LayoutRebuilder.ForceRebuildLayoutImmediate(rowParent);
    }

    // 판매 버튼을 열고 닫는다 — 담긴 것이 없거나 응답을 기다리는 중이면 잠근다.
    private void ApplySellButton()
    {
        sellButton.interactable  = !_isWaiting && _cart.Count > 0;
        clearButton.interactable = !_isWaiting;
    }

    #endregion

    #region 판매 요청

    // 줄의 빼기를 눌렀다 (SellCartRowView.RemoveClicked 구독)
    private void OnRowRemoveClicked(SellCartRowView row)
    {
        if (_isWaiting)
        {
            return; // 이미 나간 요청의 목록을 바꾸면 화면과 요청이 어긋난다
        }

        switch (row.Kind)
        {
            case SellCartKind.Character: _cart.RemoveCharacter(row.Key); break;
            case SellCartKind.Equip:     _cart.RemoveEquip(row.Key);     break;
            default:                     _cart.Remove((int)row.Key);     break;
        }
    }

    // 담긴 것을 한 번에 판다 (sellButton OnClick에 코드로 연결).
    //
    // 로그인 전에 보내면 서버가 User를 못 찾아 조용히 버린다 — 클라 입장에선 응답도 오류도
    // 없어서 "눌렀는데 아무 일도 안 일어난다"로만 보인다. 보내기 전에 여기서 끊고 이유를 남긴다.
    private void OnSellClicked()
    {
        if (_isWaiting || _cart.Count == 0)
        {
            return;
        }

        if (!_data.IsLoggedIn)
        {
            ClientLogger.Warn(ClientLogger.Send, "판매 요청을 보내지 않았다 — 로그인이 먼저다(서버가 응답 없이 버린다)");

            return;
        }

        _pendingResponses = 0;
        _failedCode       = null;

        if (_cart.HasItems)
        {
            _network.Send(new C_ItemSellRequest
            {
                Items = _cart.ToRequestItems()
            });
            _pendingResponses++;
        }

        if (_cart.HasEntities)
        {
            // 응답에 개체 ID가 없다 — 무엇을 지울지 모델에 먼저 적어 둔다.
            var characterIds = new List<long>(_cart.CharacterIds);
            var equipIds     = new List<long>(_cart.EquipIds);

            _data.BeginEntitySell(characterIds, equipIds);
            _network.Send(new C_EntitySellRequest
            {
                CharacterIds = characterIds,
                EquipIds     = equipIds,
            });
            _pendingResponses++;
        }

        ClientLogger.Info(ClientLogger.Send, $"판매 요청 — {_cart.Count}줄(패킷 {_pendingResponses}개), 예상 {_cart.TotalPrice:N0} G");

        // 대기 시작 — 로딩 표시·무응답 감시·알림은 ServerWaitManager가 공통으로 처리한다.
        _isWaiting  = true;
        ApplySellButton();
        _waitHandle = _wait.Begin("판매", onClosed: OnWaitClosed);
    }

    // 대기가 끝났다(성공·실패·타임아웃 공통) — 버튼 잠금을 푼다 (ServerWaitManager.Begin의 onClosed)
    private void OnWaitClosed()
    {
        _isWaiting        = false;
        _waitHandle       = null;
        _pendingResponses = 0;
        ApplySellButton();
        ApplyTab(inventoryTabs.CurrentTab); // 다 팔렸으면 이제 물러난다
    }

    // 자원 판매 성공 — 자원을 비운다 (PlayerDataModel.ItemSellCompleted 구독)
    //
    // ※ 골드 표시는 상태바가 'CurrencyChanged'로 따로 갱신한다. 여기서 잔액을 만지지 않는다.
    private void OnSellCompleted(long gainedGold)
    {
        ClientLogger.Info(ClientLogger.Recv, $"자원 판매 완료 — {gainedGold:N0} G 획득");

        _cart.ClearItems(); // Changed가 발행되어 목록·합계가 다시 그려진다
        OnResponsePart();
    }

    // 개체 판매 성공 — 개체를 비운다 (PlayerDataModel.EntitySellCompleted 구독)
    // ※ 판 개체는 'PlayerDataModel'이 이미 지웠다 — 카트도 그 변경을 따라 빼지만, 남은 것이 없게 한 번 더 비운다.
    private void OnEntitySellCompleted(long gainedGold)
    {
        ClientLogger.Info(ClientLogger.Recv, $"개체 판매 완료 — {gainedGold:N0} G 획득");

        _cart.ClearEntities();
        OnResponsePart();
    }

    // 판매 실패 — 사유를 기억해 둔다 (PlayerDataModel.ItemSellFailed · EntitySellFailed 구독)
    //
    // ★ 실패한 쪽 목록을 비우지 않는다. 전부 되거나 전혀 안 되므로 아무것도 팔리지 않았고,
    //   담아 둔 것을 다시 고르게 하면 조작을 처음부터 반복시키는 셈이다.
    private void OnSellFailed(EResultCode code)
    {
        _failedCode ??= code;
        OnResponsePart();
    }

    // 응답 하나가 왔다 — 보낸 것이 다 오면 대기를 닫는다(실패가 하나라도 있으면 그 사유로).
    private void OnResponsePart()
    {
        if (_waitHandle == null || --_pendingResponses > 0)
        {
            return;
        }

        if (_failedCode is EResultCode code)
        {
            _waitHandle.Fail(ResultMessages.ToText(code));
        }
        else
        {
            _waitHandle.Succeed();
        }
    }

    #endregion
}

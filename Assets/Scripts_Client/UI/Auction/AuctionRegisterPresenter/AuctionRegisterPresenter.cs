using System.Collections.Generic;
using GameData;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CharacterInfo = MikaProtocol.CharacterInfo;

// 판매 등록 창 — 경매장 안에 뜨는 모달(딤 바탕 + 가운데 창). 왼쪽에서 올릴 것을 고르고 오른쪽 카드에서 수량·단가를 정해 올린다 (T-147).
//
// ■ 여는 길 ('Open' · 'OpenEmpty')
//   판매 탭 [+ 판매 등록]        : 빈 카드로 연다.
//   인벤토리 [경매 등록]         : 그 개체를 골라 둔다('AuctionCanvasView.OpenRegister').
//   시세 탭 [팔러 가기]          : 그 TID의 첫 물건(올릴 수 있는 것)을 골라 둔다('OpenRegisterFor').
//   처음 열리는 순간이면 아직 Start 전이다 — 적어 두었다가 Start 끝에서 적용한다.
//
// ■ 무엇을 올릴 수 있나
//   자원: 보유한 것 전부 — 수량을 정한다. 매물은 통째로만 팔린다.
//   장비: 캐릭터가 끼고 있지 않은 개체 — 수량은 1. 인챈트까지 그대로 넘어간다.
//   캐릭터: 일하지 않는(작업슬롯 배치·장비 착용이 없는) 개체 — 수량은 1. 레벨·적성까지 넘어간다(T-096 · #47).
//   아티팩트는 이 창에서 올리지 않는다.
//
// ■ 올릴 수 없는 것도 목록에 보인다 (2026-10-03)
//   끼고 있는 장비·일하는 캐릭터·마지막 캐릭터는 줄을 흐리게 두고 [선택]을 잠근다 — 사유는 툴팁에 적는다.
//   빼 버리면 "내 캐릭터가 왜 없지?"가 된다. 사유 문구는 즉시 판매와 같은 'EntityBlockText'다.
//   ※ 서버도 같은 사유로 거절한다(AuctionEquipWorn · AuctionCharacterBusy · AuctionLastCharacter).
//
// ■ '이름으로 찾기'는 종류를 가리지 않는다 — 치면 자원·장비·캐릭터 전부에서 후보를 띄우고, 고르면 그 종류로 바꿔 골라 둔다
//   (2026-10-14 사용자 요청 — 종류를 먼저 고르고 그 안에서만 찾는 길이 답답했다).
//
// ■ 고르면 그 아이템의 시세 요약을 받는다(대기 없이 — 'SummaryCompleted'로 카드만 다시 그린다).
//   [최저가에 맞춤]은 그 최저가로 단가를 채운다(하한 아래면 하한). [시세 보기]는 시세 탭으로 가며 창을 닫는다.
//
// ■ 단가는 즉시 판매가 이상이면 얼마든(상한 1조 · 자율 경제, 2026-10-05 · #56).
//   범위 밖 값은 입력칸이 고친다 — 위로 넘치면 바로, 아래로 모자라면 입력이 끝날 때('AuctionInput').
//   등록비(총액의 1%, 최소 1)는 등록 순간 빠진다. 취소하면 돌려받지 못하고, 만료되면 돌려받는다.
//   팔리면 판매 수수료(5%)를 떼고 우편으로 받는다 — 카드 요약에 '팔리면 받는 금액'으로 적는다.
//
// ■ 올린 물건이 인벤토리에서 빠지는 일은 'PlayerDataModel'이 한다. 여기는 목록을 다시 그릴 뿐이다.
public class AuctionRegisterPresenter : MonoBehaviour
{
    // 종류 드롭다운 — 항목 순서 = 값 순서.
    private enum RegisterKind
    {
        Item,      // 자원
        Equip,     // 장비
        Character, // 캐릭터
    }

    [CenterHeader("창")]
    [SerializeField, Tooltip("[닫기] (창 오른쪽 위 ✕ · 아래 [닫기] 둘 다 이 버튼 하나로). OnClick은 코드가 연결한다")]
    private Button closeButton = null!;

    [SerializeField, Tooltip("딤 바탕 — 누르면 닫는다. OnClick은 코드가 연결한다")]
    private Button dimButton = null!;

    [SerializeField, Tooltip("경매장 캔버스 — [시세 보기]")]
    private AuctionCanvasView canvasView = null!;


    [CenterHeader("왼쪽 — 올릴 것 고르기")]
    [SerializeField, Tooltip("종류 드롭다운 — 자원·장비·캐릭터. 항목은 코드가 채운다")]
    private TMP_Dropdown kindDropdown = null!;

    [SerializeField, Tooltip("이름 찾기 입력칸 — 치는 대로 목록을 거르고, 아래에 모든 종류의 후보를 띄운다")]
    private TMP_InputField findInput = null!;

    [SerializeField, Tooltip("찾기 입력칸 아래 후보 목록")]
    private SuggestView findSuggestView = null!;

    [SerializeField, Tooltip("후보 한 줄 프리팹")]
    private AuctionRowView rowPrefab = null!;

    [SerializeField, Tooltip("줄이 쌓이는 Content (VLG + ContentSizeFitter)")]
    private Transform rowParent = null!;

    [SerializeField, Tooltip("올릴 수 있는 것이 없을 때만 보인다")]
    private TMP_Text emptyText = null!;


    [CenterHeader("오른쪽 — 카드")]
    [SerializeField, Tooltip("아직 아무것도 안 골랐을 때만 보인다")]
    private GameObject emptyCard = null!;

    [SerializeField, Tooltip("고른 뒤에만 보이는 카드 전부")]
    private GameObject card = null!;

    [SerializeField, Tooltip("큰 아이콘 (ItemIconView)")]
    private ItemIconView iconView = null!;

    [SerializeField, Tooltip("이름 (등급 색)")]
    private TMP_Text nameText = null!;

    [SerializeField, Tooltip("한 줄 설명 — 보유 · 능력치 · 레벨")]
    private TMP_Text subText = null!;

    [SerializeField, Tooltip("시세 요약 한 줄")]
    private TMP_Text marketText = null!;

    [SerializeField, Tooltip("[시세 보기]. OnClick은 코드가 연결한다")]
    private Button priceButton = null!;


    [CenterHeader("수량 (자원만)")]
    [SerializeField, Tooltip("수량 줄 전체 — 장비·캐릭터는 끈다")]
    private GameObject countRow = null!;

    [SerializeField, Tooltip("[−]. OnClick은 코드가 연결한다")]
    private Button minusButton = null!;

    [SerializeField, Tooltip("올릴 수량. Content Type은 Integer Number")]
    private TMP_InputField countInput = null!;

    [SerializeField, Tooltip("[+]. OnClick은 코드가 연결한다")]
    private Button plusButton = null!;

    [SerializeField, Tooltip("[최대]. OnClick은 코드가 연결한다")]
    private Button maxButton = null!;

    [SerializeField, Tooltip("수량 아래 안내 — 보유 · 한 번에 올릴 수 있는 수")]
    private TMP_Text countHelpText = null!;


    [CenterHeader("단가")]
    [SerializeField, Tooltip("개당 단가. Content Type은 Integer Number")]
    private TMP_InputField priceInput = null!;

    [SerializeField, Tooltip("[최저가에 맞춤]. OnClick은 코드가 연결한다")]
    private Button lowestButton = null!;

    [SerializeField, Tooltip("단가 아래 안내 — 단가 범위")]
    private TMP_Text priceHelpText = null!;


    [CenterHeader("요약 · 등록")]
    [SerializeField, Tooltip("경매 총액 · 등록비 · 팔리면 받는 금액 · 즉시 판매하면")]
    private TMP_Text summaryText = null!;

    [SerializeField, Tooltip("'판매 중 n / 20'")]
    private TMP_Text slotsText = null!;

    [SerializeField, Tooltip("[등록]. OnClick은 코드가 연결한다")]
    private Button registerButton = null!;

    private readonly List<AuctionRowContent> _contents    = new List<AuctionRowContent>();
    private readonly List<Suggestion> _suggestions = new List<Suggestion>();

    // 자원 줄·제안이 훑는 TID — 인벤토리·창고에 같은 TID가 따로 있어도 한 줄로 선다('PlayerDataModel.CollectOwnedItemIds').
    private readonly List<int> _ownedItemIds = new List<int>();

    private AuctionModel      _auction = null!;
    private PlayerDataModel   _data    = null!;
    private ServerWaitManager _wait    = null!;
    private UIManager         _ui      = null!;
    private AuctionRowList    _rows    = null!;

    // 고른 것 — 자원이면 TID, 장비·캐릭터면 개체 번호. 0이면 아직 안 골랐다.
    private long _selectedKey;

    // Start 전에 들어온 'Open' — 드롭다운 항목을 채운 뒤에 적용한다.
    private bool         _hasPendingOpen;
    private RegisterKind _pendingKind;
    private long         _pendingKey;
    private bool         _pendingByTid;

    // 진행 중인 대기의 손잡이 — 응답이 오면 결과를 보고한다.
    private ServerWaitHandle? _waitHandle;

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    private RegisterKind CurrentKind => (RegisterKind)kindDropdown.value;

    // 참조 확보 → 구독 → 배선 → 초기화 순서로 진행한다 (클라 공통 규약)
    private void Start()
    {
        this.RequireRef(closeButton,    nameof(closeButton));
        this.RequireRef(dimButton,      nameof(dimButton));
        this.RequireRef(canvasView,     nameof(canvasView));
        this.RequireRef(kindDropdown,   nameof(kindDropdown));
        this.RequireRef(findInput,      nameof(findInput));
        this.RequireRef(findSuggestView, nameof(findSuggestView));
        this.RequireRef(rowPrefab,      nameof(rowPrefab));
        this.RequireRef(rowParent,      nameof(rowParent));
        this.RequireRef(emptyText,      nameof(emptyText));
        this.RequireRef(emptyCard,      nameof(emptyCard));
        this.RequireRef(card,           nameof(card));
        this.RequireRef(iconView,       nameof(iconView));
        this.RequireRef(nameText,       nameof(nameText));
        this.RequireRef(subText,        nameof(subText));
        this.RequireRef(marketText,     nameof(marketText));
        this.RequireRef(priceButton,    nameof(priceButton));
        this.RequireRef(countRow,       nameof(countRow));
        this.RequireRef(minusButton,    nameof(minusButton));
        this.RequireRef(countInput,     nameof(countInput));
        this.RequireRef(plusButton,     nameof(plusButton));
        this.RequireRef(maxButton,      nameof(maxButton));
        this.RequireRef(countHelpText,  nameof(countHelpText));
        this.RequireRef(priceInput,     nameof(priceInput));
        this.RequireRef(lowestButton,   nameof(lowestButton));
        this.RequireRef(priceHelpText,  nameof(priceHelpText));
        this.RequireRef(summaryText,    nameof(summaryText));
        this.RequireRef(slotsText,      nameof(slotsText));
        this.RequireRef(registerButton, nameof(registerButton));

        _auction = Services.Get<AuctionModel>();
        _data    = Services.Get<PlayerDataModel>();
        _wait    = Services.Get<ServerWaitManager>();
        _ui      = Services.Get<UIManager>();
        _rows    = new AuctionRowList(rowPrefab, rowParent, OnRowSelected);

        FillKindOptions();

        Subscribe();

        closeButton.onClick.AddListener(Close);
        dimButton.onClick.AddListener(Close);
        priceButton.onClick.AddListener(OnPriceClicked);
        kindDropdown.onValueChanged.AddListener(_ => OnKindChanged());
        findInput.onValueChanged.AddListener(OnFindChanged);
        findSuggestView.Picked += OnSuggestionPicked;
        minusButton.onClick.AddListener(() => StepCount(-1L));
        plusButton.onClick.AddListener(() => StepCount(+1L));
        maxButton.onClick.AddListener(() => SetCount(GetMaxCount()));
        countInput.onValueChanged.AddListener(_ => OnCountChanged());
        countInput.onEndEdit.AddListener(_ => OnCountEndEdit());
        priceInput.onValueChanged.AddListener(_ => OnPriceChanged());
        priceInput.onEndEdit.AddListener(_ => OnPriceEndEdit());
        lowestButton.onClick.AddListener(OnLowestClicked);
        registerButton.onClick.AddListener(OnRegisterClicked);

        Refresh();

        _isReady = true;

        if (_hasPendingOpen)
        {
            _hasPendingOpen = false;
            ApplyOpen(_pendingKind, _pendingKey, _pendingByTid);
        }
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    // ★ 닫혀 있는 동안 채취·판매로 인벤토리가 바뀌었을 수 있다 — 재구독과 함께 다시 그린다.
    private void OnEnable()
    {
        if (_isReady)
        {
            Subscribe();
            OnLanguageChanged(); // 꺼져 있는 동안 언어가 바뀌었을 수 있다 — 종류 드롭다운까지 다시 쓴다(Refresh 포함)
        }
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
    }

    #region 열기 · 닫기

    // 빈 카드로 연다 — 판매 탭 [+ 판매 등록].
    public void OpenEmpty()
    {
        gameObject.SetActive(true);

        if (_isReady)
        {
            _selectedKey = 0L;
            Refresh();
        }
    }

    // 이 물건을 골라 둔 채로 연다 (AuctionCanvasView.OpenRegister · OpenRegisterFor).
    //   key   : byTid면 TID(그 TID의 첫 물건을 고른다), 아니면 자원 TID · 장비/캐릭터 개체 번호
    public void Open(EAuctionKind kind, long key, bool byTid)
    {
        RegisterKind registerKind = kind switch
        {
            EAuctionKind.Equip     => RegisterKind.Equip,
            EAuctionKind.Character => RegisterKind.Character,
            _                      => RegisterKind.Item,
        };

        gameObject.SetActive(true);

        if (!_isReady)
        {
            _hasPendingOpen = true;
            _pendingKind    = registerKind;
            _pendingKey     = key;
            _pendingByTid   = byTid;

            return;
        }

        ApplyOpen(registerKind, key, byTid);
    }

    // 창을 닫는다 — 고른 것과 찾기 글자는 남겨 둔다(다시 열면 이어서) (closeButton · dimButton).
    private void Close()
    {
        gameObject.SetActive(false);
    }

    // 종류를 바꾸고 그 줄을 고른다 — 흐린 줄(올릴 수 없음)이면 고르지 않고 사유를 알린다.
    private void ApplyOpen(RegisterKind kind, long key, bool byTid)
    {
        kindDropdown.SetValueWithoutNotify((int)kind);
        findInput.SetTextWithoutNotify("");
        _selectedKey = 0L;
        Refresh();

        long target = byTid && kind != RegisterKind.Item ? FindFirstByTid(kind, (int)key) : key;
        int  index  = _contents.FindIndex(content => content.Key == target);

        if (index < 0)
        {
            _wait.RaiseNotice(Lang.Get(byTid ? "UI.Auction.NothingToList" : "UI.Auction.NotFound"));

            return;
        }

        if (_contents[index].Dimmed)
        {
            string? reason = kind switch
            {
                RegisterKind.Character => EntityBlockText.ForCharacter(_data, target, _data.Characters.Count - 1),
                RegisterKind.Equip     => EntityBlockText.ForEquip(_data, target),
                _                      => EntityBlockText.ForItem(_data, (int)target),
            };
            _wait.RaiseNotice(Lang.Format("UI.Inventory.CantList", reason ?? ""));

            return;
        }

        OnRowSelected(target);
    }

    // 이 TID의 물건 중 처음 올릴 수 있는 것의 개체 번호 — 없으면 흐린 것이라도 첫 것, 그것도 없으면 0.
    private long FindFirstByTid(RegisterKind kind, int tid)
    {
        long fallback = 0L;

        if (kind == RegisterKind.Equip)
        {
            foreach (EquipInfo equip in _data.Equips)
            {
                if (equip.EquipTid != tid) continue;
                if (EntityBlockText.ForEquip(_data, equip.EquipId) == null) return equip.EquipId;
                if (fallback == 0L) fallback = equip.EquipId;
            }

            return fallback;
        }

        int remainingAfter = _data.Characters.Count - 1;

        foreach (CharacterInfo character in _data.Characters)
        {
            if (character.CharacterTid != tid) continue;
            if (EntityBlockText.ForCharacter(_data, character.CharacterId, remainingAfter) == null) return character.CharacterId;
            if (fallback == 0L) fallback = character.CharacterId;
        }

        return fallback;
    }

    #endregion

    #region 구독

    // 등록·시세 응답과 인벤토리·장비·재화 변경 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed = true;

        _auction.RegisterCompleted    += OnRegisterCompleted;
        _auction.SummaryCompleted     += OnSummaryCompleted;
        _auction.MyListingsCompleted  += OnMyListingsCompleted;
        _data.InventoryChanged        += Refresh;
        _data.EquipsChanged           += Refresh;
        _data.CharactersChanged       += Refresh;
        _data.WorkStationSlotsChanged += Refresh; // 배치가 풀리면 흐린 캐릭터가 살아난다
        _data.CurrencyChanged         += RefreshCard;
        Lang.Changed                  += OnLanguageChanged;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed = false;

        _auction.RegisterCompleted    -= OnRegisterCompleted;
        _auction.SummaryCompleted     -= OnSummaryCompleted;
        _auction.MyListingsCompleted  -= OnMyListingsCompleted;
        _data.InventoryChanged        -= Refresh;
        _data.EquipsChanged           -= Refresh;
        _data.CharactersChanged       -= Refresh;
        _data.WorkStationSlotsChanged -= Refresh;
        _data.CurrencyChanged         -= RefreshCard;
        Lang.Changed                  -= OnLanguageChanged;
    }

    // 언어가 바뀌었다 — 종류 드롭다운 · 목록 · 카드를 다시 쓴다 (Lang.Changed 구독)
    private void OnLanguageChanged()
    {
        FillKindOptions();
        Refresh();
    }

    // 종류 드롭다운을 지금 언어로 채운다 — 'RegisterKind' 순서. 고른 값은 그대로 둔다 (Start · 언어 변경)
    private void FillKindOptions()
    {
        int value = kindDropdown.value;

        kindDropdown.ClearOptions();
        kindDropdown.AddOptions(new List<string> { Lang.Get("UI.Auction.KindItem"), Lang.Get("UI.Equip.Gear"), Lang.Get("UI.Common.Character") });
        kindDropdown.SetValueWithoutNotify(value);
    }

    #endregion

    #region 입력

    // 찾기 입력 — 지금 종류의 목록을 거르고, 모든 종류에서 후보를 띄운다 (findInput.onValueChanged)
    private void OnFindChanged(string text)
    {
        Refresh();
        CollectSuggestions(text);
        findSuggestView.Show(_suggestions);
    }

    // 후보를 골랐다 — 그 종류로 바꿔 골라 두고, 목록은 그 이름으로 거른다 (SuggestView.Picked 구독)
    // ※ 흐린 것(올릴 수 없음)을 고르면 'ApplyOpen'이 사유를 알리고 고르지 않는다.
    private void OnSuggestionPicked(Suggestion suggestion)
    {
        RegisterKind kind = (EAuctionKind)suggestion.Kind switch
        {
            EAuctionKind.Equip     => RegisterKind.Equip,
            EAuctionKind.Character => RegisterKind.Character,
            _                      => RegisterKind.Item,
        };

        ApplyOpen(kind, suggestion.Key, byTid: false);

        findInput.SetTextWithoutNotify(suggestion.Name);
        Refresh();
    }

    // 종류가 바뀌었다 — 고른 것을 비운다 (kindDropdown.onValueChanged)
    private void OnKindChanged()
    {
        _selectedKey = 0L;
        Refresh();
    }

    // 줄의 [선택] — 그것을 고르고 기본값을 채운 뒤 시세 요약을 받는다 (AuctionRowView.ActionClicked)
    //
    // 기본 단가는 하한(= 즉시 판매가)이다 — 가장 흔한 출발점이다. 자원은 보유량 전부(한 번 상한까지)를 기본 수량으로 둔다.
    private void OnRowSelected(long key)
    {
        _selectedKey = key;

        countInput.SetTextWithoutNotify(GetMaxCount().ToString());
        priceInput.SetTextWithoutNotify(AuctionModel.MinUnitPrice(GetBasePrice()).ToString());

        _auction.RequestSummary(ToAuctionKind(CurrentKind), GetSelectedTid());

        Refresh();
    }

    // [−] · [+] — 1씩 (minusButton · plusButton)
    private void StepCount(long delta)
    {
        long current = long.TryParse(countInput.text, out long value) ? value : 1L;

        SetCount(current + delta);
    }

    // 수량을 넣는다 — 1 ~ 최대로 자른다 (StepCount · maxButton).
    private void SetCount(long count)
    {
        if (_selectedKey == 0L)
        {
            return;
        }

        count = System.Math.Clamp(count, 1L, System.Math.Max(GetMaxCount(), 1L));

        countInput.SetTextWithoutNotify(count.ToString());
        RefreshCard();
    }

    // 수량 입력 — 최대를 넘으면 최대로 고친다 (countInput.onValueChanged)
    private void OnCountChanged()
    {
        if (_selectedKey != 0L)
        {
            AuctionInput.ClampMax(countInput, GetMaxCount());
        }

        RefreshCard();
    }

    // 수량 입력 끝 — 비었거나 0이면 1로 둔다 (countInput.onEndEdit)
    private void OnCountEndEdit()
    {
        if (_selectedKey != 0L && AuctionInput.ClampMin(countInput, 1L))
        {
            RefreshCard();
        }
    }

    // 단가 입력 — 상한을 넘으면 상한으로 고친다 (priceInput.onValueChanged)
    private void OnPriceChanged()
    {
        if (_selectedKey != 0L)
        {
            AuctionInput.ClampMax(priceInput, AuctionModel.MaxUnitPrice);
        }

        RefreshCard();
    }

    // 단가 입력 끝 — 하한보다 낮으면 하한으로 올린다 (priceInput.onEndEdit)
    // ※ 치는 도중에는 고치지 않는다 — 하한 150을 치려고 '1'을 누른 순간 올리면 이어 칠 수 없다.
    private void OnPriceEndEdit()
    {
        if (_selectedKey != 0L && AuctionInput.ClampMin(priceInput, AuctionModel.MinUnitPrice(GetBasePrice())))
        {
            RefreshCard();
        }
    }

    // [최저가에 맞춤] — 지금 최저가로 단가를 채운다. 하한 아래면 하한 (lowestButton OnClick)
    private void OnLowestClicked()
    {
        MarketItemInfo? summary = GetSelectedSummary();

        if (summary == null || summary.AvailableCount <= 0L)
        {
            return;
        }

        long price = System.Math.Max(summary.LowestUnitPrice, AuctionModel.MinUnitPrice(GetBasePrice()));

        priceInput.SetTextWithoutNotify(price.ToString());
        RefreshCard();
    }

    // [시세 보기] — 시세 탭으로 가며 창을 닫는다 (priceButton OnClick)
    private void OnPriceClicked()
    {
        if (_selectedKey == 0L)
        {
            return;
        }

        canvasView.OpenPrice(ToAuctionKind(CurrentKind), GetSelectedTid());
        Close();
    }

    // [등록] — 확인을 받고 보낸다 (registerButton OnClick)
    private void OnRegisterClicked()
    {
        if (_waitHandle != null || !TryReadForm(out int count, out long unitPrice))
        {
            return;
        }

        RegisterKind kind    = CurrentKind;
        long         key     = _selectedKey;
        long         total   = count * unitPrice;
        long         fee     = AuctionModel.ListingFee(total);
        long         instant = AuctionModel.InstantSellTotal(GetBasePrice(), count);

        string what = kind switch
        {
            RegisterKind.Item  => Lang.Format("UI.Auction.RegisterWhatItem", GameDataLoader.GetItemName((int)key), count.ToString("N0"), unitPrice.ToString("N0"), total.ToString("N0")),
            RegisterKind.Equip => Lang.Format("UI.Auction.RegisterWhatOne", GameDataLoader.GetEquipName(_data.GetEquipTid(key)), unitPrice.ToString("N0")),
            _                  => Lang.Format("UI.Auction.RegisterWhatOne", GameDataLoader.GetCharacterName(_data.GetCharacterTid(key)), unitPrice.ToString("N0")),
        };

        // 즉시 판매했을 때의 값을 함께 적는다 — 경매가 즉시 판매와 무엇이 다른지 마지막으로 비교하는 자리다.
        _ui.AskConfirm(
            Lang.Format("UI.Auction.ConfirmRegister", what, instant.ToString("N0"), fee.ToString("N0")),
            () =>
            {
                switch (kind)
                {
                    case RegisterKind.Item:  _auction.RegisterItem((int)key, count, unitPrice); break;
                    case RegisterKind.Equip: _auction.RegisterEquip(key, unitPrice);            break;
                    default:                 _auction.RegisterCharacter(key, unitPrice);        break;
                }

                _waitHandle = _wait.Begin(Lang.Get("UI.Auction.WaitRegister"), onClosed: OnWaitClosed);
                RefreshCard();
            });
    }

    #endregion

    #region 응답 (AuctionModel 구독)

    // 등록 결과 — 성공이면 알리고 고른 것을 비운 뒤 창을 닫는다 (AuctionModel.RegisterCompleted 구독)
    private void OnRegisterCompleted(EResultCode code, long fee)
    {
        if (code != EResultCode.Ok)
        {
            _waitHandle?.Fail(ResultMessages.ToText(code));

            return;
        }

        _waitHandle?.Succeed();
        _wait.RaiseNotice(Lang.Format("UI.Auction.Registered", fee.ToString("N0")));

        _selectedKey = 0L;
        Close();
    }

    // 시세 요약 도착 — 카드의 시세 줄만 다시 그린다 (AuctionModel.SummaryCompleted 구독)
    private void OnSummaryCompleted(EResultCode code) => RefreshCard();

    // 내 매물 도착 — '판매 중 n / 20' (AuctionModel.MyListingsCompleted 구독)
    private void OnMyListingsCompleted(EResultCode code) => RefreshCard();

    // 대기가 끝났다(성공·실패·타임아웃 공통) — 버튼을 다시 연다 (ServerWaitManager.Begin의 onClosed)
    private void OnWaitClosed()
    {
        _waitHandle = null;

        if (_isReady && isActiveAndEnabled)
        {
            RefreshCard();
        }
    }

    #endregion

    #region 표시 갱신

    // 후보 목록과 카드를 지금 상태로 그린다 (인벤토리·장비 변경 · 종류 변경 · 찾기 · 선택).
    //
    // 고른 것이 인벤토리에서 사라졌으면(다 팔았다·다른 경로로 빠졌다) 선택을 푼다.
    private void Refresh()
    {
        _contents.Clear();

        if (CurrentKind == RegisterKind.Item)
        {
            AddItemRows();
        }
        else if (CurrentKind == RegisterKind.Equip)
        {
            AddEquipRows();
        }
        else
        {
            AddCharacterRows();
        }

        // 고른 것이 사라졌거나 흐려졌으면(장비를 꼈다·배치했다) 선택을 푼다 — 찾기로 가려진 것은 그대로 둔다.
        if (_selectedKey != 0L && !IsSelectable(_selectedKey))
        {
            _selectedKey = 0L;
        }

        string query = findInput.text;

        _contents.RemoveAll(content => !string.IsNullOrWhiteSpace(query) && !content.Title.Contains(query.Trim(), System.StringComparison.OrdinalIgnoreCase));

        // 선택 표시는 목록을 다 모은 뒤에 붙인다 — 위에서 선택이 풀렸을 수 있다.
        for (int i = 0; i < _contents.Count; i++)
        {
            AuctionRowContent content = _contents[i];
            content.Selectable  = true;
            content.Selected    = !content.Dimmed && content.Key == _selectedKey;
            content.ActionLabel = Lang.Get(content.Dimmed ? "UI.Auction.ActUnavailable" : content.Selected ? "UI.Auction.ActSelected" : "UI.Enchant.Select");
            _contents[i]        = content;
        }

        _rows.Show(_contents);

        emptyText.gameObject.SetActive(_contents.Count == 0);
        emptyText.text = Lang.Get(string.IsNullOrWhiteSpace(query) ? "UI.Auction.RegisterEmpty" : "UI.Auction.RegisterNoMatch");

        RefreshCard();
    }

    // 내 물건 전부(자원 → 장비 → 캐릭터)에서 이름이 맞는 것을 후보로 모은다 — 장비·캐릭터는 개체마다 한 줄.
    private void CollectSuggestions(string query)
    {
        _suggestions.Clear();

        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        int max = findSuggestView.Capacity;

        _data.CollectOwnedItemIds(_ownedItemIds);

        foreach (int itemId in _ownedItemIds)
        {
            if (_suggestions.Count >= max) return;
            if (!AuctionText.MatchesName(EAuctionKind.Item, itemId, query)) continue;

            string blocked = EntityBlockText.ForItem(_data, itemId) != null ? $"{UIRichText.Dot}{Lang.Get("UI.Auction.CantListShort")}" : "";

            _suggestions.Add(AuctionText.ToSuggestion(EAuctionKind.Item, itemId, itemId,
                $"{Lang.Get("UI.Auction.KindItem")}{UIRichText.Dot}{UIRichText.Label(Lang.Get("UI.Inventory.Owned"))} {Lang.Format("UI.Inventory.CountUnitShort", _data.GetItemCount(itemId).ToString("N0"))}{blocked}"));
        }

        foreach (EquipInfo equip in _data.Equips)
        {
            if (_suggestions.Count >= max) return;
            if (!AuctionText.MatchesName(EAuctionKind.Equip, equip.EquipTid, query)) continue;

            string blocked = EntityBlockText.ForEquip(_data, equip.EquipId) != null ? $"{UIRichText.Dot}{Lang.Get("UI.Auction.CantListShort")}" : "";

            _suggestions.Add(AuctionText.ToSuggestion(EAuctionKind.Equip, equip.EquipTid, equip.EquipId,
                $"{Lang.Get("UI.Equip.Gear")}{UIRichText.Dot}{AuctionText.FormatStatSummary(equip.EquipTid, equip.EnchantOptions)}{blocked}"));
        }

        int remainingAfter = _data.Characters.Count - 1;

        foreach (CharacterInfo character in _data.Characters)
        {
            if (_suggestions.Count >= max) return;
            if (!AuctionText.MatchesName(EAuctionKind.Character, character.CharacterTid, query)) continue;

            string blocked = EntityBlockText.ForCharacter(_data, character.CharacterId, remainingAfter) != null ? $"{UIRichText.Dot}{Lang.Get("UI.Auction.CantListShort")}" : "";

            _suggestions.Add(AuctionText.ToSuggestion(EAuctionKind.Character, character.CharacterTid, character.CharacterId,
                $"{Lang.Get("UI.Common.Character")}{UIRichText.Dot}{AuctionText.FormatCharacterSummary(character)}{blocked}"));
        }
    }

    // 고른 것이 아직 목록에 있고 흐리지 않은가 — 찾기로 거르기 전에 본다.
    private bool IsSelectable(long key)
    {
        foreach (AuctionRowContent content in _contents)
        {
            if (content.Key == key)
            {
                return !content.Dimmed;
            }
        }

        return false;
    }

    // 보유한 자원 줄을 모은다 (Refresh에서 호출). 0개로 남은 캐시 항목은 뺀다.
    // ※ 창고에 맡긴 것도 합쳐 한 줄이다 — 서버가 인벤토리 것부터 빼고 모자란 만큼 창고에서 뺀다(T-163 · 이슈 #74).
    private void AddItemRows()
    {
        _data.CollectOwnedItemIds(_ownedItemIds);

        foreach (int itemId in _ownedItemIds)
        {
            int     basePrice = GameDataLoader.GetItemPrice(itemId);
            long    owned     = _data.GetItemCount(itemId);
            string? blocked   = EntityBlockText.ForItem(_data, itemId); // 잠긴 종류는 흐리게 — 사유는 툴팁에(T-159)

            _contents.Add(new AuctionRowContent
            {
                Key        = itemId,
                Icon       = ItemIconContent.ForItem(itemId, owned),
                Title      = GameDataLoader.GetItemName(itemId),
                TitleColor = RarityPalette.Get(GameDataLoader.GetItemRarity(itemId)),
                Info       = $"{UIRichText.Label(Lang.Get("UI.Inventory.Owned"))} {Lang.Format("UI.Inventory.CountUnitShort", owned.ToString("N0"))}{UIRichText.Dot}{UIRichText.Label(Lang.Get("UI.Equip.InstantSellPrice"))} {AuctionModel.InstantSellPrice(basePrice):N0} G",
                Detail     = $"{UIRichText.Label(Lang.Get("UI.Auction.AuctionUnitPrice"))} {UIRichText.Paint(AuctionModel.FormatBand(basePrice), UIThemeRole.Highlight)}",
                CanAct     = blocked == null,
                Dimmed     = blocked != null,
                Tooltip    = () => AuctionText.BuildRegisterItemTooltip(itemId, _data.GetItemCount(itemId), blocked),
            });
        }
    }

    // 장비 줄을 모은다 (Refresh에서 호출). 끼고 있는 장비는 흐리게 — 사유는 툴팁에.
    private void AddEquipRows()
    {
        foreach (EquipInfo equip in _data.Equips)
        {
            if (!GameDataLoader.TryGetEquip(equip.EquipTid, out EquipTableRow row))
            {
                continue;
            }

            EquipInfo target  = equip; // 툴팁은 올리는 순간에 만든다 — 반복 변수 대신 복사본을 잡는다
            string?   blocked = EntityBlockText.ForEquip(_data, equip.EquipId);

            _contents.Add(new AuctionRowContent
            {
                Key        = equip.EquipId,
                Icon       = ItemIconContent.ForEquip(equip.EquipTid, equip.EnchantOptions).WithWarehouse(equip.Container == EContainer.Warehouse),
                Title      = Loc.Get(row.NameLocKey),
                TitleColor = RarityPalette.Get(row.GlobalRarity),
                Info       = $"{EquipLabel.GetKindName(row.EquipKind)}{UIRichText.Dot}{AuctionText.FormatStatSummary(equip.EquipTid, equip.EnchantOptions)}{UIRichText.Dot}{UIRichText.Label(Lang.Get("UI.Equip.InstantSellPrice"))} {AuctionModel.InstantSellPrice(row.BasePrice):N0} G",
                Detail     = $"{UIRichText.Label(Lang.Get("UI.Auction.AuctionUnitPrice"))} {UIRichText.Paint(AuctionModel.FormatBand(row.BasePrice), UIThemeRole.Highlight)}",
                CanAct     = blocked == null,
                Dimmed     = blocked != null,
                Tooltip    = () => AuctionText.BuildRegisterEquipTooltip(target, blocked),
            });
        }
    }

    // 캐릭터 줄을 모은다 (Refresh에서 호출). 일하는 캐릭터·마지막 캐릭터는 흐리게 — 사유는 툴팁에.
    private void AddCharacterRows()
    {
        int remainingAfter = _data.Characters.Count - 1; // 하나를 올리면 남는 수 — 서버 'AuctionLastCharacter' 판정

        foreach (CharacterInfo character in _data.Characters)
        {
            CharacterInfo target    = character;
            int           basePrice = GameDataLoader.GetCharacterPrice(character.CharacterTid);
            string?       blocked   = EntityBlockText.ForCharacter(_data, character.CharacterId, remainingAfter);

            _contents.Add(new AuctionRowContent
            {
                Key        = character.CharacterId,
                Icon       = ItemIconContent.ForCharacter(character.CharacterTid).WithWarehouse(character.Container == EContainer.Warehouse),
                Title      = GameDataLoader.GetCharacterName(character.CharacterTid),
                TitleColor = RarityPalette.Get(GameDataLoader.GetCharacterRarity(character.CharacterTid)),
                Info       = $"{AuctionText.FormatCharacterSummary(character)}{UIRichText.Dot}{UIRichText.Label(Lang.Get("UI.Equip.InstantSellPrice"))} {AuctionModel.InstantSellPrice(basePrice):N0} G",
                Detail     = $"{UIRichText.Label(Lang.Get("UI.Auction.AuctionUnitPrice"))} {UIRichText.Paint(AuctionModel.FormatBand(basePrice), UIThemeRole.Highlight)}",
                CanAct     = blocked == null,
                Dimmed     = blocked != null,
                Tooltip    = () => AuctionText.BuildRegisterCharacterTooltip(target, blocked),
            });
        }
    }

    // 카드의 문구·잠금을 맞춘다 (입력 변경 · 재화 변경 · 선택 · 시세 도착 · 대기 시작/종료).
    private void RefreshCard()
    {
        long active = _auction.MyListings.Count;
        bool isFull = active >= Constants.AuctionMaxActiveListings;

        slotsText.text = $"{UIRichText.Label(Lang.Get("UI.Auction.OnSale"))} {UIRichText.Paint(active.ToString("N0"), isFull ? UIThemeRole.Negative : UIThemeRole.Highlight)} / {Constants.AuctionMaxActiveListings:N0}";

        bool has = _selectedKey != 0L;

        emptyCard.SetActive(!has);
        card.SetActive(has);

        if (!has)
        {
            registerButton.interactable = false;

            return;
        }

        EAuctionKind    kind      = ToAuctionKind(CurrentKind);
        int             tid       = GetSelectedTid();
        int             basePrice = GetBasePrice();
        MarketItemInfo? summary   = GetSelectedSummary();
        bool            isItem    = CurrentKind == RegisterKind.Item;

        iconView.Bind(BuildIcon());
        nameText.text  = AuctionText.GetName(kind, tid);
        nameText.color = RarityPalette.Get(AuctionText.GetRarity(kind, tid));
        subText.text   = BuildSubText();

        marketText.text          = AuctionPriceText.FormatSummaryLine(summary);
        lowestButton.interactable = summary != null && summary.AvailableCount > 0L;

        countRow.SetActive(isItem);

        if (isItem)
        {
            countHelpText.text = UIRichText.Label(Lang.Format("UI.Auction.CountHelp", _data.GetItemCount(tid).ToString("N0"), AuctionModel.MaxListingCount.ToString("N0")));
        }

        priceHelpText.text = UIRichText.Label(Lang.Format("UI.Auction.PriceHelp", AuctionModel.MinUnitPrice(basePrice).ToString("N0"), AuctionModel.FormatMaxUnitPrice()));

        if (!TryReadForm(out int count, out long unitPrice))
        {
            summaryText.text            = UIRichText.Label(Lang.Get("UI.Auction.FormHint"));
            registerButton.interactable = false;

            return;
        }

        long total   = count * unitPrice;
        long fee     = AuctionModel.ListingFee(total);
        long net     = AuctionModel.SaleNet(total);
        long instant = AuctionModel.InstantSellTotal(basePrice, count);
        bool canPay  = _data.Gold >= fee;

        summaryText.text =
            $"{UIRichText.Label(Lang.Get("UI.Auction.AuctionTotal"))}<pos=55%>{UIRichText.Gold(total)}\n" +
            $"{UIRichText.Label(Lang.Format("UI.Auction.FeeNow", AuctionModel.FormatPermille(AuctionModel.ListingFeePermille)))}<pos=55%>{UIRichText.Paint($"−{fee:N0} G", canPay ? UIThemeRole.TextMain : UIThemeRole.Negative)}\n" +
            $"{UIRichText.Label(Lang.Format("UI.Auction.NetIfSold", AuctionModel.FormatPermille(Constants.AuctionSaleFeePermille)))}<pos=55%>{UIRichText.Paint($"{net:N0} G", UIThemeRole.Positive)}\n" +
            UIRichText.Small($"{UIRichText.Label(Lang.Get("UI.Auction.IfInstant"))}<pos=55%>{UIRichText.Label(Lang.Format("UI.Auction.InstantVia", instant.ToString("N0")))}");

        registerButton.interactable = _waitHandle == null && canPay && !isFull;
    }

    // 카드 아이콘 — 자원은 보유 수량, 장비는 인챈트 칸까지.
    private ItemIconContent BuildIcon()
    {
        if (CurrentKind == RegisterKind.Item)
        {
            int itemId = (int)_selectedKey;

            return ItemIconContent.ForItem(itemId, _data.GetItemCount(itemId));
        }

        if (CurrentKind == RegisterKind.Character)
        {
            return ItemIconContent.ForCharacter(_data.GetCharacterTid(_selectedKey)).WithWarehouse(_data.IsCharacterInWarehouse(_selectedKey));
        }

        EquipInfo? equip = FindEquip(_selectedKey);

        return ItemIconContent.ForEquip(_data.GetEquipTid(_selectedKey), equip?.EnchantOptions).WithWarehouse(equip?.Container == EContainer.Warehouse);
    }

    // 카드 이름 아래 한 줄 — 자원: 분류 · 보유 / 장비: 종류 · 능력치 / 캐릭터: 레벨 · 응축 · 적성.
    private string BuildSubText()
    {
        if (CurrentKind == RegisterKind.Item)
        {
            int itemId = (int)_selectedKey;

            return $"{AuctionText.FormatSub(EAuctionKind.Item, itemId)}{UIRichText.Dot}{UIRichText.Label(Lang.Get("UI.Inventory.Owned"))} {Lang.Format("UI.Inventory.CountUnitShort", _data.GetItemCount(itemId).ToString("N0"))}";
        }

        if (CurrentKind == RegisterKind.Character)
        {
            return AuctionText.FormatCharacterSummary(FindCharacter(_selectedKey));
        }

        EquipInfo? equip    = FindEquip(_selectedKey);
        int        equipTid = _data.GetEquipTid(_selectedKey);

        return $"{AuctionText.FormatSub(EAuctionKind.Equip, equipTid)}{UIRichText.Dot}{AuctionText.FormatStatSummary(equipTid, equip?.EnchantOptions)}";
    }

    #endregion

    #region 보조

    private static EAuctionKind ToAuctionKind(RegisterKind kind) => kind switch
    {
        RegisterKind.Equip     => EAuctionKind.Equip,
        RegisterKind.Character => EAuctionKind.Character,
        _                      => EAuctionKind.Item,
    };

    // 고른 것의 TID — 자원은 키 그대로, 장비·캐릭터는 개체에서.
    private int GetSelectedTid() => CurrentKind switch
    {
        RegisterKind.Equip     => _data.GetEquipTid(_selectedKey),
        RegisterKind.Character => _data.GetCharacterTid(_selectedKey),
        _                      => (int)_selectedKey,
    };

    private MarketItemInfo? GetSelectedSummary() => _selectedKey == 0L ? null : _auction.GetSummary(ToAuctionKind(CurrentKind), GetSelectedTid());

    // 올릴 수 있는 최대 수량 — 자원은 보유량(한 번에 'AuctionModel.MaxListingCount'까지), 장비·캐릭터는 1.
    // ※ 넘기면 서버가 'AuctionInvalidRequest'로 거절한다 — 입력 단계에서 먼저 자른다(#56).
    private long GetMaxCount()
    {
        return CurrentKind == RegisterKind.Item
            ? System.Math.Min(_data.GetItemCount((int)_selectedKey), AuctionModel.MaxListingCount)
            : 1L;
    }

    // 고른 것의 기준가(BasePrice) — 즉시 판매가·경매 등록가 범위의 재료다. 못 찾으면 0.
    private int GetBasePrice()
    {
        if (CurrentKind == RegisterKind.Item)
        {
            return GameDataLoader.GetItemPrice((int)_selectedKey);
        }

        if (CurrentKind == RegisterKind.Character)
        {
            return GameDataLoader.GetCharacterPrice(_data.GetCharacterTid(_selectedKey));
        }

        return GameDataLoader.TryGetEquip(_data.GetEquipTid(_selectedKey), out EquipTableRow row) ? row.BasePrice : 0;
    }

    // 등록 칸 입력을 읽는다. 수량이 올릴 수 있는 최대 수량 밖이거나 단가가 경매 등록가 범위 밖이면 false.
    private bool TryReadForm(out int count, out long unitPrice)
    {
        unitPrice = 0L;
        count     = 0;

        if (_selectedKey == 0L)
        {
            return false;
        }

        int basePrice = GetBasePrice();

        // 장비·캐릭터는 수량 줄을 숨긴다 — 입력칸 글자와 상관없이 1이다.
        if (CurrentKind != RegisterKind.Item)
        {
            count = 1;
        }
        else if (!int.TryParse(countInput.text, out count) || count < 1 || count > GetMaxCount())
        {
            return false;
        }

        return long.TryParse(priceInput.text, out unitPrice)
            && unitPrice >= AuctionModel.MinUnitPrice(basePrice)
            && unitPrice <= AuctionModel.MaxUnitPrice;
    }

    // 보유 캐릭터에서 개체 번호로 찾는다. 없으면 null.
    private CharacterInfo? FindCharacter(long characterId)
    {
        foreach (CharacterInfo character in _data.Characters)
        {
            if (character.CharacterId == characterId)
            {
                return character;
            }
        }

        return null;
    }

    // 보유 장비에서 개체 번호로 찾는다. 없으면 null.
    private EquipInfo? FindEquip(long equipId)
    {
        foreach (EquipInfo equip in _data.Equips)
        {
            if (equip.EquipId == equipId)
            {
                return equip;
            }
        }

        return null;
    }

    #endregion
}

using System.Collections.Generic;
using GameData;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CharacterInfo = MikaProtocol.CharacterInfo;

// 경매 등록 화면 — 경매장 탭의 '등록' 하위 탭. 인벤토리의 자원·장비·캐릭터를 골라 단가를 정해 올린다.
//
// ■ 무엇을 올릴 수 있나
//   자원: 보유한 것 전부 — 수량을 정한다. 거래소에서 일부씩 팔린다.
//   장비: 캐릭터가 끼고 있지 않은 개체 — 수량은 1. 인챈트까지 그대로 넘어간다.
//   캐릭터: 일하지 않는(작업슬롯 배치·장비 착용이 없는) 개체 — 수량은 1. 레벨·적성까지 넘어간다(T-096 · #47).
//
// ■ 올릴 수 없는 것도 목록에 보인다 (2026-10-03)
//   끼고 있는 장비·일하는 캐릭터·마지막 캐릭터는 줄을 흐리게 두고 [선택]을 잠근다 — 사유는 툴팁에 적는다.
//   빼 버리면 "내 캐릭터가 왜 없지?"가 된다. 사유 문구는 즉시 판매와 같은 'EntityBlockText'다.
//   ※ 서버도 같은 사유로 거절한다(AuctionEquipWorn · AuctionCharacterBusy · AuctionLastCharacter).
//
// ■ 인벤토리에서 바로 오기 ('Preselect')
//   인벤토리 [경매 등록]이 시장 창을 열며 종류와 개체를 넘긴다 — 그 줄을 미리 골라 둔다.
//
// ■ 두 가지 파는 법을 화면이 구분해 말한다 (2026-09-30)
//   즉시 판매(인벤토리 [판매]) : 즉시 판매가에 바로 판다 — 값이 정해져 있다.
//   경매 등록(여기)            : 단가를 직접 정한다 — 즉시 판매가 이상이면 얼마든(상한 10조 · 자율 경제, 2026-10-05).
//   그래서 줄·힌트·확인 창마다 '즉시 판매가'와 '경매 등록가 범위'를 나란히 적는다.
//   범위 밖 값은 입력칸이 고친다 — 위로 넘치면 바로, 아래로 모자라면 입력이 끝날 때('AuctionInput').
//   등록비(총액의 1%, 최소 1)는 등록 순간 빠진다. 취소하면 돌려받지 못하고, 만료되면 돌려받는다.
//
// ■ 올린 물건이 인벤토리에서 빠지는 일은 'PlayerDataModel'이 한다. 여기는 목록을 다시 그릴 뿐이다.
public class AuctionRegisterPresenter : MonoBehaviour
{
    // 등록 화면의 종류 드롭다운 — 항목 순서 = 값 순서.
    private enum RegisterKind
    {
        Item,      // 자원
        Equip,     // 장비
        Character, // 캐릭터
    }

    [CenterHeader("안내")]
    [SerializeField, Tooltip("화면 위 고정 안내 — 경매 등록과 즉시 판매의 차이. 문구는 코드가 채운다(배수는 Constants)")]
    private TMP_Text guideText = null!;


    [CenterHeader("목록")]
    [SerializeField, Tooltip("종류 드롭다운 — 자원·장비·캐릭터. 항목은 코드가 채운다")]
    private TMP_Dropdown kindDropdown = null!;

    [SerializeField, Tooltip("후보 한 줄 프리팹")]
    private AuctionRowView rowPrefab = null!;

    [SerializeField, Tooltip("줄이 쌓이는 Content (VLG + ContentSizeFitter)")]
    private Transform rowParent = null!;

    [SerializeField, Tooltip("올릴 수 있는 것이 없을 때만 보인다")]
    private GameObject emptyText = null!;


    [CenterHeader("등록")]
    [SerializeField, Tooltip("고른 물건")]
    private TMP_Text selectedText = null!;

    [SerializeField, Tooltip("올릴 수량 (자원만). Content Type은 Integer Number")]
    private TMP_InputField countInput = null!;

    [SerializeField, Tooltip("개당 단가. Content Type은 Integer Number")]
    private TMP_InputField priceInput = null!;

    [SerializeField, Tooltip("경매 등록가 범위 · 총액 · 등록비 · 즉시 판매 비교")]
    private TMP_Text hintText = null!;

    [SerializeField, Tooltip("등록 버튼. OnClick은 코드가 연결한다")]
    private Button registerButton = null!;

    private readonly List<AuctionRowContent> _contents = new List<AuctionRowContent>();

    private AuctionModel      _auction = null!;
    private PlayerDataModel   _data    = null!;
    private ServerWaitManager _wait    = null!;
    private UIManager         _ui      = null!;
    private AuctionRowList    _rows    = null!;

    // 고른 것 — 자원이면 TID, 장비·캐릭터면 개체 번호. 0이면 아직 안 골랐다.
    private long _selectedKey;

    // Start 전에 들어온 미리 고르기('Preselect') — 드롭다운 항목을 채운 뒤에 적용한다.
    private RegisterKind? _pendingKind;
    private long          _pendingKey;

    // 진행 중인 대기의 손잡이 — 응답이 오면 결과를 보고한다.
    private ServerWaitHandle? _waitHandle;

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    private RegisterKind CurrentKind => (RegisterKind)kindDropdown.value;

    // 참조 확보 → 구독 → 배선 → 초기화 순서로 진행한다 (클라 공통 규약)
    private void Start()
    {
        this.RequireRef(guideText,      nameof(guideText));
        this.RequireRef(kindDropdown,   nameof(kindDropdown));
        this.RequireRef(rowPrefab,      nameof(rowPrefab));
        this.RequireRef(rowParent,      nameof(rowParent));
        this.RequireRef(emptyText,      nameof(emptyText));
        this.RequireRef(selectedText,   nameof(selectedText));
        this.RequireRef(countInput,     nameof(countInput));
        this.RequireRef(priceInput,     nameof(priceInput));
        this.RequireRef(hintText,       nameof(hintText));
        this.RequireRef(registerButton, nameof(registerButton));

        _auction = Services.Get<AuctionModel>();
        _data    = Services.Get<PlayerDataModel>();
        _wait    = Services.Get<ServerWaitManager>();
        _ui      = Services.Get<UIManager>();
        _rows    = new AuctionRowList(rowPrefab, rowParent, OnRowSelected);

        kindDropdown.ClearOptions();
        kindDropdown.AddOptions(new List<string> { "자원", "장비", "캐릭터" });

        guideText.text = BuildGuideText();

        Subscribe();

        kindDropdown.onValueChanged.AddListener(_ => OnKindChanged());
        countInput.onValueChanged.AddListener(_ => OnCountChanged());
        countInput.onEndEdit.AddListener(_ => OnCountEndEdit());
        priceInput.onValueChanged.AddListener(_ => OnPriceChanged());
        priceInput.onEndEdit.AddListener(_ => OnPriceEndEdit());
        registerButton.onClick.AddListener(OnRegisterClicked);

        Refresh();

        _isReady = true;

        if (_pendingKind is RegisterKind pending)
        {
            _pendingKind = null;
            ApplyPreselect(pending, _pendingKey);
        }
    }

    // 이 종류·개체를 미리 골라 둔다 — 인벤토리 [경매 등록]이 시장 창을 열며 부른다('MarketCanvasView.OpenAuctionRegister').
    // ※ 처음 열리는 순간이면 아직 Start 전이다 — 적어 두었다가 Start 끝에서 적용한다.
    public void Preselect(EAuctionKind kind, long key)
    {
        RegisterKind registerKind = kind switch
        {
            EAuctionKind.Equip     => RegisterKind.Equip,
            EAuctionKind.Character => RegisterKind.Character,
            _                      => RegisterKind.Item,
        };

        if (!_isReady)
        {
            _pendingKind = registerKind;
            _pendingKey  = key;

            return;
        }

        ApplyPreselect(registerKind, key);
    }

    // 종류를 바꾸고 그 줄을 고른다 — 흐린 줄(올릴 수 없음)이면 고르지 않고 사유를 알린다.
    private void ApplyPreselect(RegisterKind kind, long key)
    {
        kindDropdown.SetValueWithoutNotify((int)kind);
        _selectedKey = 0L;
        Refresh();

        int index = _contents.FindIndex(content => content.Key == key);

        if (index < 0)
        {
            return;
        }

        if (_contents[index].Dimmed)
        {
            string? reason = kind == RegisterKind.Character
                ? EntityBlockText.ForCharacter(_data, key, _data.Characters.Count - 1)
                : EntityBlockText.ForEquip(_data, key);
            _wait.RaiseNotice($"올릴 수 없습니다 — {reason}");

            return;
        }

        OnRowSelected(key);
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    // ★ 닫혀 있는 동안 채취·판매로 인벤토리가 바뀌었을 수 있다 — 재구독과 함께 다시 그린다.
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

    // 등록 응답과 인벤토리·장비·재화 변경 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed = true;

        _auction.RegisterCompleted    += OnRegisterCompleted;
        _data.InventoryChanged        += Refresh;
        _data.EquipsChanged           += Refresh;
        _data.CharactersChanged       += Refresh;
        _data.WorkStationSlotsChanged += Refresh; // 배치가 풀리면 흐린 캐릭터가 살아난다
        _data.CurrencyChanged         += RefreshForm;
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
        _data.InventoryChanged        -= Refresh;
        _data.EquipsChanged           -= Refresh;
        _data.CharactersChanged       -= Refresh;
        _data.WorkStationSlotsChanged -= Refresh;
        _data.CurrencyChanged         -= RefreshForm;
    }

    #endregion

    #region 입력

    // 종류가 바뀌었다 — 고른 것을 비운다 (kindDropdown.onValueChanged)
    private void OnKindChanged()
    {
        _selectedKey = 0L;
        Refresh();
    }

    // 줄의 [선택] — 그것을 고르고 기본값을 채운다 (AuctionRowView.ActionClicked)
    //
    // 기본 단가는 하한(= 즉시 판매가)이다 — 가장 흔한 출발점이다. 자원은 보유량 전부를 기본 수량으로 둔다.
    private void OnRowSelected(long key)
    {
        _selectedKey = key;

        countInput.text = CurrentKind == RegisterKind.Item ? _data.GetItemCount((int)key).ToString() : "1";
        priceInput.text = AuctionModel.MinUnitPrice(GetBasePrice()).ToString();

        Refresh();
    }

    // 수량 입력 — 보유량을 넘으면 보유량으로 고친다 (countInput.onValueChanged)
    private void OnCountChanged()
    {
        if (_selectedKey != 0L)
        {
            AuctionInput.ClampMax(countInput, GetMaxCount());
        }

        RefreshForm();
    }

    // 수량 입력 끝 — 비었거나 0이면 1로 둔다 (countInput.onEndEdit)
    private void OnCountEndEdit()
    {
        if (_selectedKey != 0L && AuctionInput.ClampMin(countInput, 1L))
        {
            RefreshForm();
        }
    }

    // 단가 입력 — 상한을 넘으면 상한으로 고친다 (priceInput.onValueChanged)
    private void OnPriceChanged()
    {
        if (_selectedKey != 0L)
        {
            AuctionInput.ClampMax(priceInput, AuctionModel.MaxUnitPrice);
        }

        RefreshForm();
    }

    // 단가 입력 끝 — 하한보다 낮으면 하한으로 올린다 (priceInput.onEndEdit)
    // ※ 치는 도중에는 고치지 않는다 — 하한 150을 치려고 '1'을 누른 순간 올리면 이어 칠 수 없다.
    private void OnPriceEndEdit()
    {
        if (_selectedKey != 0L && AuctionInput.ClampMin(priceInput, AuctionModel.MinUnitPrice(GetBasePrice())))
        {
            RefreshForm();
        }
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
            RegisterKind.Item  => $"{GameDataLoader.GetItemName((int)key)} {count:N0}개를 개당 {unitPrice:N0} G(총 {total:N0} G)에",
            RegisterKind.Equip => $"{GameDataLoader.GetEquipName(_data.GetEquipTid(key))}을(를) {unitPrice:N0} G에",
            _                  => $"{GameDataLoader.GetCharacterName(_data.GetCharacterTid(key))}을(를) {unitPrice:N0} G에",
        };

        // 즉시 판매했을 때의 값을 함께 적는다 — 경매가 즉시 판매와 무엇이 다른지 마지막으로 비교하는 자리다.
        _ui.AskConfirm(
            $"{what} 경매에 올립니다. (즉시 판매하면 {instant:N0} G)\n" +
            $"등록비 {fee:N0} G가 바로 빠집니다 — 취소하면 돌려받지 못하고, 기간이 끝나면 돌려받습니다.",
            () =>
            {
                switch (kind)
                {
                    case RegisterKind.Item:  _auction.RegisterItem((int)key, count, unitPrice); break;
                    case RegisterKind.Equip: _auction.RegisterEquip(key, unitPrice);            break;
                    default:                 _auction.RegisterCharacter(key, unitPrice);        break;
                }

                _waitHandle = _wait.Begin("경매 등록", onClosed: OnWaitClosed);
                RefreshForm();
            });
    }

    #endregion

    #region 응답 (AuctionModel 구독)

    // 등록 결과 — 성공이면 알리고 고른 것을 비운다 (AuctionModel.RegisterCompleted 구독)
    private void OnRegisterCompleted(EResultCode code, long fee)
    {
        if (code != EResultCode.Ok)
        {
            _waitHandle?.Fail(ResultMessages.ToText(code));

            return;
        }

        _waitHandle?.Succeed();
        _wait.RaiseNotice($"등록했습니다. (등록비 {fee:N0} G)\n팔리면 대금이 우편으로 옵니다.");

        _selectedKey = 0L;
        Refresh();
    }

    // 대기가 끝났다(성공·실패·타임아웃 공통) — 버튼을 다시 연다 (ServerWaitManager.Begin의 onClosed)
    private void OnWaitClosed()
    {
        _waitHandle = null;
        RefreshForm();
    }

    #endregion

    #region 표시 갱신

    // 후보 목록과 등록 칸을 지금 상태로 그린다 (인벤토리·장비 변경 · 종류 변경 · 선택).
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

        // 고른 것이 사라졌거나 흐려졌으면(장비를 꼈다·배치했다) 선택을 푼다.
        if (_selectedKey != 0L && !_contents.Exists(content => content.Key == _selectedKey && !content.Dimmed))
        {
            _selectedKey = 0L;
        }

        // 선택 표시는 목록을 다 모은 뒤에 붙인다 — 위에서 선택이 풀렸을 수 있다.
        for (int i = 0; i < _contents.Count; i++)
        {
            AuctionRowContent content = _contents[i];
            content.ActionLabel = content.Dimmed ? "불가" : content.Key == _selectedKey ? "선택됨" : "선택";
            _contents[i]        = content;
        }

        _rows.Show(_contents);
        emptyText.SetActive(_contents.Count == 0);

        RefreshForm();
    }

    // 보유한 자원 줄을 모은다 (Refresh에서 호출). 0개로 남은 캐시 항목은 뺀다.
    private void AddItemRows()
    {
        foreach (ItemInfo item in _data.Inventory)
        {
            if (item.Count <= 0)
            {
                continue;
            }

            int basePrice = GameDataLoader.GetItemPrice(item.ItemId);

            int  itemId = item.ItemId;
            long owned  = item.Count;

            _contents.Add(new AuctionRowContent
            {
                Key        = itemId,
                Icon       = ItemIconContent.ForItem(itemId, owned),
                Title      = GameDataLoader.GetItemName(itemId),
                TitleColor = RarityPalette.Get(GameDataLoader.GetItemRarity(itemId)),
                Info       = $"{UIRichText.Label("보유")} {owned:N0}개{UIRichText.Dot}{UIRichText.Label("즉시 판매가")} {AuctionModel.InstantSellPrice(basePrice):N0} G",
                Detail     = $"{UIRichText.Label("경매 단가")} {UIRichText.Paint(AuctionModel.FormatBand(basePrice), UIThemeRole.Highlight)}",
                CanAct     = true,
                Tooltip    = () => AuctionText.BuildRegisterItemTooltip(itemId, _data.GetItemCount(itemId)),
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
                Icon       = ItemIconContent.ForEquip(equip.EquipTid, equip.EnchantOptions),
                Title      = row.Name,
                TitleColor = RarityPalette.Get(row.GlobalRarity),
                Info       = $"{EquipLabel.GetKindName(row.EquipKind)}{UIRichText.Dot}{AuctionText.FormatStatSummary(equip.EquipTid, equip.EnchantOptions)}{UIRichText.Dot}{UIRichText.Label("즉시 판매가")} {AuctionModel.InstantSellPrice(row.BasePrice):N0} G",
                Detail     = $"{UIRichText.Label("경매 단가")} {UIRichText.Paint(AuctionModel.FormatBand(row.BasePrice), UIThemeRole.Highlight)}",
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
                Icon       = ItemIconContent.ForCharacter(character.CharacterTid),
                Title      = GameDataLoader.GetCharacterName(character.CharacterTid),
                TitleColor = RarityPalette.Get(GameDataLoader.GetCharacterRarity(character.CharacterTid)),
                Info       = $"{AuctionText.FormatCharacterSummary(character)}{UIRichText.Dot}{UIRichText.Label("즉시 판매가")} {AuctionModel.InstantSellPrice(basePrice):N0} G",
                Detail     = $"{UIRichText.Label("경매 단가")} {UIRichText.Paint(AuctionModel.FormatBand(basePrice), UIThemeRole.Highlight)}",
                CanAct     = blocked == null,
                Dimmed     = blocked != null,
                Tooltip    = () => AuctionText.BuildRegisterCharacterTooltip(target, blocked),
            });
        }
    }

    // 등록 칸의 문구·잠금을 맞춘다 (입력 변경 · 재화 변경 · 선택 · 대기 시작/종료).
    private void RefreshForm()
    {
        countInput.interactable = _selectedKey != 0L && CurrentKind == RegisterKind.Item;
        priceInput.interactable = _selectedKey != 0L;

        if (_selectedKey == 0L)
        {
            selectedText.text           = UIRichText.Label("아래 목록에서 올릴 것을 [선택]하세요.");
            hintText.text               = "";
            registerButton.interactable = false;

            return;
        }

        selectedText.text = BuildSelectedText();

        int    basePrice = GetBasePrice();
        string band      = $"{UIRichText.Label("단가 범위")} {UIRichText.Paint(AuctionModel.FormatBand(basePrice), UIThemeRole.Highlight)}{BuildMarketHint()}";

        if (!TryReadForm(out int count, out long unitPrice))
        {
            hintText.text               = $"{band}\n{UIRichText.Label("수량과 단가를 범위 안에서 넣으세요.")}";
            registerButton.interactable = false;

            return;
        }

        long total   = count * unitPrice;
        long fee     = AuctionModel.ListingFee(total);
        long instant = AuctionModel.InstantSellTotal(basePrice, count);
        long gain    = total - instant;
        bool canPay  = _data.Gold >= fee;

        // 둘째 줄 = 경매, 셋째 줄 = 즉시 판매 — 두 방식을 줄로 갈라 나란히 비교한다.
        string feeText  = UIRichText.Paint($"{fee:N0} G", canPay ? UIThemeRole.TextMain : UIThemeRole.Negative);
        string gainText = gain > 0L ? UIRichText.Paint($" (경매가 {gain:N0} G 더 받음)", UIThemeRole.Positive) : "";

        hintText.text =
            $"{band}\n" +
            $"{UIRichText.Label("경매")}  {UIRichText.Label("총")} {UIRichText.Gold(total)}{UIRichText.Dot}{UIRichText.Label("등록비")} {feeText}{gainText}\n" +
            UIRichText.Small($"{UIRichText.Label("즉시 판매하면")} {instant:N0} G {UIRichText.Label("— 인벤토리 [판매]")}");
        registerButton.interactable = _waitHandle == null && canPay;
    }

    // 고른 것의 이름 줄 (RefreshForm에서 호출).
    private string BuildSelectedText()
    {
        if (CurrentKind == RegisterKind.Item)
        {
            int itemId = (int)_selectedKey;

            return $"<b>{GameDataLoader.GetItemName(itemId)}</b>  {UIRichText.Label("보유")} {_data.GetItemCount(itemId):N0}개";
        }

        if (CurrentKind == RegisterKind.Character)
        {
            CharacterInfo? character = FindCharacter(_selectedKey);

            return $"<b>{GameDataLoader.GetCharacterName(_data.GetCharacterTid(_selectedKey))}</b>  {AuctionText.FormatCharacterSummary(character)}";
        }

        int equipTid = _data.GetEquipTid(_selectedKey);

        return $"<b>{GameDataLoader.GetEquipName(equipTid)}</b>  {EquipLabel.GetEffectText(equipTid)}";
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

    #endregion

    #region 보조

    // 화면 위 안내문 — 경매 등록과 즉시 판매를 두 줄로 갈라 적는다 (Start에서 한 번).
    // ※ 굵은 머리말로 두 방식을 가른다 — 한 문장에 섞으면 "어느 쪽이 정해진 값인가"가 안 읽힌다.
    private static string BuildGuideText()
    {
        string feePercent = $"{AuctionModel.ListingFeePermille / 10f:0.#}%";

        return
            $"<b>경매 등록</b>  {UIRichText.Label("단가를 직접 정해 올립니다 · 팔리면 대금이 우편으로 옵니다")}\n" +
            UIRichText.Small($"{UIRichText.Label("단가 범위")} 즉시 판매가 이상 (상한 {AuctionModel.FormatMaxUnitPrice()} G){UIRichText.Dot}{UIRichText.Label("등록비")} {feePercent} (취소하면 돌려받지 못함)") + "\n" +
            $"<b>즉시 판매</b>  {UIRichText.Label("인벤토리 [판매] — 정해진 값(즉시 판매가)에 바로 팝니다")}";
    }

    // 거래소 최저가 참고 — 자원이고 거래소 목록에 매물이 있을 때만 " · 거래소 최저 N G". 없으면 빈 문자열.
    // ※ 새로 조회하지 않는다(빈도 제한) — [구매] 탭에서 받아 둔 목록을 볼 뿐이라 낡았을 수 있다.
    private string BuildMarketHint()
    {
        if (CurrentKind != RegisterKind.Item)
        {
            return "";
        }

        foreach (MarketItemInfo item in _auction.MarketItems)
        {
            if (item.Tid == (int)_selectedKey && item.AvailableCount > 0)
            {
                return $"{UIRichText.Dot}{UIRichText.Label("거래소 최저")} {item.LowestUnitPrice:N0} G";
            }
        }

        return "";
    }

    // 올릴 수 있는 최대 수량 — 자원은 보유량, 장비·캐릭터는 1.
    private long GetMaxCount()
    {
        return CurrentKind == RegisterKind.Item ? _data.GetItemCount((int)_selectedKey) : 1L;
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

    // 등록 칸 입력을 읽는다. 수량이 보유량 밖이거나 단가가 경매 등록가 범위 밖이면 false.
    private bool TryReadForm(out int count, out long unitPrice)
    {
        unitPrice = 0L;
        count     = 0;

        if (_selectedKey == 0L)
        {
            return false;
        }

        int basePrice = GetBasePrice();

        bool isCountValid = int.TryParse(countInput.text, out count) && count >= 1 && count <= GetMaxCount();

        return isCountValid
            && long.TryParse(priceInput.text, out unitPrice)
            && unitPrice >= AuctionModel.MinUnitPrice(basePrice)
            && unitPrice <= AuctionModel.MaxUnitPrice;
    }

    #endregion
}

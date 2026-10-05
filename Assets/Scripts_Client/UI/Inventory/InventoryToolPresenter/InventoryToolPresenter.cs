using System.Collections.Generic;
using GameData;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CharacterInfo = MikaProtocol.CharacterInfo;

// 인벤토리 탭 줄 아래의 도구 줄 — 두 줄이다 (2026-09-29 · T-073 · T-069).
//   1줄: 정렬 방향 화살표 · 정렬 기준 · 일괄 담기(범위 + [상자 제외] + 버튼)
//   2줄: 찾기 — 이름 검색 · 분류(산업 · 상자 · 기타) · 등급 · [초기화]
//
// 탭 줄보다 낮은 별도 줄이다. 탭은 "무엇을 보는가", 이 줄은 "그것을 어떻게 다루는가"라
// 같은 줄에 두면 네 탭 사이에 성격이 다른 버튼이 끼어든다.
//
// ■ 특성 탭에서는 줄째 숨는다
//   특성은 정렬할 목록도, 팔 것도 없다. 잠긴 버튼만 남겨 두면 "지금 못 하는 것"이 계속 눈에 남는다
//   (탭 버튼을 잠그는 것과는 판단이 다르다 — 거기서는 **탭 자체가 선택지**라 사라지면 무엇이 있는지 알 수 없다).
//
// ■ 일괄 담기는 '담기'까지다
//   버튼을 눌러도 팔리지 않는다. 판매 목록에 **보유 수량 전부**를 담을 뿐이고, 파는 것은 아래 [판매]다.
//   우클릭 담기와 같은 자리에 쌓이므로 담긴 뒤 하나씩 빼는 것도 그대로 된다.
//
// ■ 일괄 담기는 '더하기'가 아니라 '다시 잡기'다
//   누를 때마다 **지금 탭의 것만** 비우고 고른 범위만 담는다. 그래야 화면의 `○○ 이하`와 팔릴 것이 늘 같다.
//   다른 탭에서 담아 둔 것은 범위와 상관없으니 남긴다.
//
// ■ 캐릭터·장비도 일괄로 담는다 (2026-10-05)
//   팔 수 없는 개체(배치 중 · 장비 착용 · 마지막 캐릭터 · 끼고 있는 장비)는 **건너뛰고** 몇 개를 뺐는지 알린다 —
//   서버는 하나라도 걸리면 개체 판매 전체를 거절하므로, 담아 두면 [판매]가 통째로 막힌다.
//   장비는 인챈트 등급도 범위로 본다 — 일반 장비에 신화 인챈트가 붙었으면 '일반 이하'에 담기지 않는다.
//
// ■ 자원은 상자를 빼고 담는다 — [상자 제외] 토글, 기본 켬 (2026-10-05)
//   상자는 열어야 값이 나온다(내용물 기대값이 판매가의 몇 배). 일괄 판매에 섞이면 모르고 판다.
//
// ■ 분류 드롭다운 — 자원 탭은 산업 뒤에 '상자' · '기타'가 붙는다 (2026-10-05)
//   상자만 모아 보고 열거나, 큐브·구슬 같은 산업 밖 아이템만 볼 수 있게. 캐릭터 탭은 산업 축이 없어 감춘다.
//
// ■ 찾기는 탭을 바꾸거나 인벤토리를 닫으면 비운다
//   거른 채로 남으면 다음에 열었을 때 "아이템이 사라졌다"가 된다. 이 줄은 특성 탭에서도, 인벤토리를 닫을 때도
//   꺼지므로 'OnDisable' 한 곳에서 비운다.
public class InventoryToolPresenter : MonoBehaviour
{
    // 정렬 기준 드롭다운의 문구. 'InventorySortKey' 순서와 같다.
    private static readonly string[] SortKeyLabels = { "등급순", "이름순", "수량순" };

    // 드롭다운에 세울 등급. 'None'과 'Max'를 뺀 실제 등급만, 낮은 것부터.
    private static readonly GlobalRarity[] SellRarities =
    {
        GlobalRarity.Common,
        GlobalRarity.Uncommon,
        GlobalRarity.Rare,
        GlobalRarity.Epic,
        GlobalRarity.Legendary,
        GlobalRarity.Mythic,
    };

    [CenterHeader("참조")]
    [SerializeField, Tooltip("정렬 방향 버튼 (정사각형). OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button sortButton = null!;

    [SerializeField, Tooltip("정렬 방향 화살표 — ▼(등급 높은 순) · ▲(낮은 순). 🎨 스프라이트가 오면 Image로 바꾼다")]
    private TMP_Text sortArrowText = null!;

    [SerializeField, Tooltip("일괄 담기 범위 — '○○ 이하'. 목록은 코드가 채운다")]
    private TMP_Dropdown bulkRarityDropdown = null!;

    [SerializeField, Tooltip("일괄 담기 버튼 (정사각형). OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button bulkSellButton = null!;

    [SerializeField, Tooltip("일괄 담기에서 상자를 뺄지 — 자원 탭에서만 보인다. 시작은 켬(코드가 정한다)")]
    private Toggle excludeBoxToggle = null!;

    [SerializeField, Tooltip("탭 줄 — 지금 어느 탭인지 여기서 듣는다")]
    private InventoryTabPresenter tabs = null!;

    [SerializeField, Tooltip("칸 격자 — 정렬을 지시할 상대")]
    private InventoryGridPresenter grid = null!;

    [CenterHeader("정렬 기준")]
    [SerializeField, Tooltip("정렬 기준 — 등급순 · 이름순 · 수량순(자원 탭만). 목록은 코드가 채운다")]
    private TMP_Dropdown sortKeyDropdown = null!;

    [CenterHeader("찾기")]
    [SerializeField, Tooltip("이름 검색창. 입력하는 대로 걸러진다")]
    private TMP_InputField searchInput = null!;

    [SerializeField, Tooltip("분류 필터 — '전체' + 5산업 (+ 자원 탭은 상자 · 기타). 캐릭터 탭에서는 감춘다. 목록은 코드가 채운다")]
    private TMP_Dropdown industryDropdown = null!;

    [SerializeField, Tooltip("등급 필터 — '등급 전체' + 등급별. 목록은 코드가 채운다")]
    private TMP_Dropdown rarityDropdown = null!;

    [SerializeField, Tooltip("찾기 조건을 모두 비우는 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button resetButton = null!;

    // 등급 높은 순으로 시작한다 — 값나가는 것이 위에 오는 쪽이 기본이다.
    private InventorySortOrder _order = InventorySortOrder.Descending;

    // 지금 고른 정렬 기준. 탭이 그 기준을 못 쓰면 등급으로 돌아간다.
    private InventorySortKey _sortKey = InventorySortKey.Rarity;

    // 기준 드롭다운의 항목 순서 — 탭마다 다르다(수량은 자원만). 드롭다운 값 → 기준.
    private readonly List<InventorySortKey> _sortKeyOptions = new List<InventorySortKey>();

    // 분류 드롭다운의 항목 순서 — 탭마다 다르다(상자·기타는 자원만). 드롭다운 값 → (산업, 묶음). 0번은 늘 '전체'.
    private readonly List<(byte Industry, InventoryItemGroup Group)> _categoryOptions = new List<(byte, InventoryItemGroup)>();

    private PlayerDataModel   _data = null!;
    private SellCartModel     _cart = null!;
    private ServerWaitManager _wait = null!;

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnDisable 가드

    // 참조 확보 → 구독 → 배선 순서로 진행한다 (클라 공통 규약)
    private void Start()
    {
        this.RequireRef(sortButton,         nameof(sortButton));
        this.RequireRef(sortArrowText,      nameof(sortArrowText));
        this.RequireRef(bulkRarityDropdown, nameof(bulkRarityDropdown));
        this.RequireRef(bulkSellButton,     nameof(bulkSellButton));
        this.RequireRef(excludeBoxToggle,   nameof(excludeBoxToggle));
        this.RequireRef(tabs,               nameof(tabs));
        this.RequireRef(grid,               nameof(grid));
        this.RequireRef(sortKeyDropdown,    nameof(sortKeyDropdown));
        this.RequireRef(searchInput,        nameof(searchInput));
        this.RequireRef(industryDropdown,   nameof(industryDropdown));
        this.RequireRef(rarityDropdown,     nameof(rarityDropdown));
        this.RequireRef(resetButton,        nameof(resetButton));

        _data = Services.Get<PlayerDataModel>();
        _cart = Services.Get<SellCartModel>();
        _wait = Services.Get<ServerWaitManager>();

        Subscribe();

        BuildRarityOptions();
        BuildFilterOptions();

        excludeBoxToggle.SetIsOnWithoutNotify(true); // 기본은 상자를 뺀다 — 모르고 파는 쪽이 더 아깝다

        sortButton.onClick.AddListener(OnSortClicked);
        bulkSellButton.onClick.AddListener(OnBulkSellClicked);
        sortKeyDropdown.onValueChanged.AddListener(OnSortKeyChanged);

        searchInput.onValueChanged.AddListener(_ => ApplyFilter());
        industryDropdown.onValueChanged.AddListener(_ => ApplyFilter());
        rarityDropdown.onValueChanged.AddListener(_ => ApplyFilter());
        resetButton.onClick.AddListener(ResetFilter);

        RefreshArrow();

        _isReady = true;

        // ★ 탭 줄의 'Start'가 먼저 돌았으면 이벤트를 이미 놓쳤다 — 지금 값을 물어 맞춘다.
        //   유니티는 두 'Start'의 순서를 보장하지 않는다.
        ApplyTab(tabs.CurrentTab);
    }

    // 찾기 조건을 비운다 (Unity 메시지) — 특성 탭으로 가거나 인벤토리를 닫으면 이 줄이 꺼진다.
    private void OnDisable()
    {
        if (!_isReady)
        {
            return; // Start 전 — 서비스 조회가 끝나지 않아 격자에 말을 걸 수 없다
        }

        ResetFilter();
    }

    // 구독 해제 (Unity 메시지)
    private void OnDestroy()
    {
        Unsubscribe();
    }

    #region 구독

    // 탭 전환 구독 (Start에서 한 번).
    //
    // ⚠️ 'OnEnable/OnDisable'이 아니라 'Start/OnDestroy'다 — 이 줄은 **특성 탭에서 스스로 꺼진다.**
    //   꺼질 때 구독을 놓으면 다시 켤 신호('TabChanged')를 받을 길이 사라져 영영 안 돌아온다
    //   ('InventoryGridPresenter'가 로그인 구독을 'OnDestroy'에서 푸는 것과 같은 이유다).
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed   = true;
        tabs.TabChanged += ApplyTab;
    }

    // 구독 해제 (OnDestroy에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed   = false;
        tabs.TabChanged -= ApplyTab;
    }

    #endregion

    #region 초기화

    // 드롭다운에 '○○ 이하' 목록을 채운다 (Start에서 한 번).
    //
    // 씬에 적어 두지 않는 이유 — 등급이 늘면 목록과 'SellRarities'가 따로 놀고,
    // 어느 쪽이 진짜인지 눌러 보기 전에는 알 수 없다.
    private void BuildRarityOptions()
    {
        bulkRarityDropdown.ClearOptions();

        var options = new List<string>(SellRarities.Length);

        foreach (GlobalRarity rarity in SellRarities)
        {
            options.Add($"{RarityLabel.Get(rarity)} 이하");
        }

        bulkRarityDropdown.AddOptions(options);
        bulkRarityDropdown.SetValueWithoutNotify(0); // 일반 이하 — 가장 안전한 쪽에서 시작한다
        bulkRarityDropdown.RefreshShownValue();
    }

    // 등급 찾기 드롭다운의 목록을 채운다 (Start에서 한 번). 0번은 늘 '전체'다.
    // ※ 분류 드롭다운은 탭마다 항목이 달라 탭을 바꿀 때 채운다('RebuildCategoryOptions').
    private void BuildFilterOptions()
    {
        var rarities = new List<string> { "등급 전체" };

        foreach (GlobalRarity rarity in SellRarities)
        {
            rarities.Add(RarityLabel.Get(rarity));
        }

        rarityDropdown.ClearOptions();
        rarityDropdown.AddOptions(rarities);
        rarityDropdown.SetValueWithoutNotify(0);
        rarityDropdown.RefreshShownValue();
    }

    #endregion

    #region 정렬

    // 정렬 방향을 뒤집고 지금 탭을 다시 줄 세운다 (Sort Button OnClick에 코드로 연결)
    private void OnSortClicked()
    {
        _order = _order == InventorySortOrder.Descending
            ? InventorySortOrder.Ascending
            : InventorySortOrder.Descending;

        RefreshArrow();
        grid.SortCurrent(_sortKey, _order);
    }

    // 정렬 기준을 골랐다 — 지금 방향 그대로 다시 줄 세운다 (Sort Key Dropdown onValueChanged에 코드로 연결)
    private void OnSortKeyChanged(int index)
    {
        if (index < 0 || index >= _sortKeyOptions.Count)
        {
            return;
        }

        _sortKey = _sortKeyOptions[index];
        grid.SortCurrent(_sortKey, _order);
    }

    // 이 탭이 거를 수 있는 분류만 드롭다운에 세운다 (ApplyTab에서 호출). 0번은 늘 '전체'다.
    //
    // ※ 산업 순서는 격자의 적성 스트립('InventoryGridPresenter.StripIndustries')과 같다 — 화면마다 순서가 다르면 헷갈린다.
    // ※ 묶음(상자 · 기타)이 붙는 탭은 '산업 전체'가 아니라 '분류 전체'라고 적는다 — 상자는 산업이 아니다.
    private void RebuildCategoryOptions()
    {
        bool hasGroups = grid.CurrentSupportsGroupFilter;

        _categoryOptions.Clear();
        _categoryOptions.Add((0, InventoryItemGroup.None));

        var labels = new List<string> { hasGroups ? "분류 전체" : "산업 전체" };

        foreach (EIndustryType industry in InventoryGridPresenter.StripIndustries)
        {
            _categoryOptions.Add(((byte)industry, InventoryItemGroup.None));
            labels.Add(IndustryLabel.Get(industry));
        }

        if (hasGroups)
        {
            _categoryOptions.Add((0, InventoryItemGroup.Box));
            labels.Add("상자");
            _categoryOptions.Add((0, InventoryItemGroup.Other));
            labels.Add("기타");
        }

        industryDropdown.ClearOptions();
        industryDropdown.AddOptions(labels);
        industryDropdown.SetValueWithoutNotify(0);
        industryDropdown.RefreshShownValue();
    }

    // 이 탭이 쓸 수 있는 기준만 드롭다운에 세운다 (ApplyTab에서 호출).
    //
    // 쓸 수 없는 기준은 **감춘다** — 캐릭터·장비에서 '수량순'을 고르면 아무 일도 안 일어나 고장처럼 보인다.
    // 지금 기준을 이 탭이 못 쓰면 등급으로 돌린다. 줄 세우기는 하지 않는다 — [정렬]과 기준 선택만이 자리를 바꾼다.
    private void RebuildSortKeyOptions()
    {
        _sortKeyOptions.Clear();

        var labels = new List<string>();

        foreach (InventorySortKey key in (InventorySortKey[])System.Enum.GetValues(typeof(InventorySortKey)))
        {
            // 등급·이름은 어느 탭이나 된다. 격자에 물어보는 것은 탭마다 다른 것뿐이다 —
            // 격자가 아직 탭을 받기 전이어도(Start 순서) 두 기준은 늘 세워 둔다.
            if (key == InventorySortKey.Count && !grid.CurrentSupportsSortKey(key))
            {
                continue;
            }

            _sortKeyOptions.Add(key);
            labels.Add(SortKeyLabels[(int)key]);
        }

        if (!_sortKeyOptions.Contains(_sortKey))
        {
            _sortKey = InventorySortKey.Rarity;
        }

        sortKeyDropdown.ClearOptions();
        sortKeyDropdown.AddOptions(labels);
        sortKeyDropdown.SetValueWithoutNotify(_sortKeyOptions.IndexOf(_sortKey));
        sortKeyDropdown.RefreshShownValue();
    }

    // 화살표를 지금 방향으로 맞춘다 (Start · 방향을 바꿀 때).
    //
    // ▼가 "위에서 아래로 낮아진다"(등급 높은 순)다 — 방향이 곧 목록의 모양이다.
    private void RefreshArrow()
    {
        sortArrowText.text = _order == InventorySortOrder.Descending ? "▼" : "▲";
    }

    #endregion

    #region 일괄 담기

    // 고른 등급 이하를 판매 목록에 담는다 — 지금 탭의 것만 (Bulk Sell Button OnClick에 코드로 연결)
    //
    // 팔지 않고 담기까지만 한다 — 확인 절차는 아래 [판매] 버튼이다('Inventory 규칙.md').
    private void OnBulkSellClicked()
    {
        switch (tabs.CurrentTab)
        {
            case InventoryTab.Resource:  BulkAddItems();      break;
            case InventoryTab.Character: BulkAddCharacters(); break;
            case InventoryTab.Equipment: BulkAddEquips();     break;
        }
    }

    // 자원 — 고른 등급 이하를 보유 수량 전부. [상자 제외]가 켜져 있으면 상자는 뺀다 (OnBulkSellClicked에서 호출).
    private void BulkAddItems()
    {
        // ★ 먼저 비운다 — 이 버튼은 "더 담기"가 아니라 **범위를 다시 잡는 것**이다.
        //   비우지 않으면 영웅 이하로 담았다가 일반 이하로 다시 누를 때 영웅·희귀가 그대로 남아,
        //   화면의 범위(`일반 이하`)와 실제로 팔릴 것이 어긋난다.
        //   ⚠️ 우클릭으로 하나씩 담아 둔 자원도 함께 빠진다. 범위를 다시 잡는다는 뜻이 그것이다.
        _cart.ClearItems(); // 우클릭으로 담아 둔 캐릭터·장비는 범위와 상관없으니 남긴다

        GlobalRarity limit      = SelectedRarity;
        bool         excludeBox = excludeBoxToggle.isOn;
        int          added      = 0;
        int          skipped    = 0;

        foreach (ItemInfo item in _data.Inventory)
        {
            if (item.Count <= 0)
            {
                continue; // 서버가 0개가 된 아이템도 실어 보낸다
            }

            if (GameDataLoader.GetItemRarity(item.ItemId) > limit)
            {
                continue;
            }

            if (excludeBox && GameDataLoader.IsBox(item.ItemId))
            {
                skipped++;

                continue;
            }

            _cart.Add(item.ItemId, item.Count);
            added++;
        }

        string skippedText = skipped > 0 ? $" (상자 {skipped}종 제외 — [상자 제외]를 끄면 함께 담깁니다)" : "";

        if (added == 0)
        {
            _wait.RaiseNotice($"{LimitLabel} 이하로 담을 자원이 없습니다.{skippedText}");

            return;
        }

        // 뺀 것이 있을 때만 알린다 — 판매 목록이 채워지는 것은 눈에 보이지만, 빠진 상자는 보이지 않는다.
        if (skipped > 0)
        {
            _wait.RaiseNotice($"자원 {added}종을 담았습니다.{skippedText}");
        }

        ClientLogger.Info(ClientLogger.UI, $"일괄 담기 — {limit} 이하 자원 {added}종 · 상자 {skipped}종 제외");
    }

    // 캐릭터 — 고른 등급 이하 중 팔 수 있는 것만 (OnBulkSellClicked에서 호출).
    //
    // ※ 판정은 우클릭 담기와 같은 'EntityBlockText'다. 마지막 한 명이 남도록 고를 때마다 남는 수를 줄여 가며 묻는다.
    // ※ 다 고른 뒤 한 번에 담는다('SellCartModel.AddEntities') — 하나씩 담으면 격자가 개체 수만큼 다시 그린다.
    private void BulkAddCharacters()
    {
        _cart.ClearCharacters(); // 범위를 다시 잡는다 — 담아 둔 자원·장비는 남긴다

        GlobalRarity limit   = SelectedRarity;
        var          picked  = new List<long>();
        int          blocked = 0;

        foreach (CharacterInfo character in _data.Characters)
        {
            if (GameDataLoader.GetCharacterRarity(character.CharacterTid) > limit)
            {
                continue;
            }

            // 방금 비웠으니 담길 캐릭터는 'picked'뿐이다.
            int remainingAfter = _data.Characters.Count - picked.Count - 1;

            if (EntityBlockText.ForCharacter(_data, character.CharacterId, remainingAfter) != null)
            {
                blocked++;

                continue;
            }

            picked.Add(character.CharacterId);
        }

        _cart.AddEntities(picked, System.Array.Empty<long>());
        NotifyEntityBulk("캐릭터", "명", picked.Count, blocked, "배치 중 · 장비 착용 · 마지막 캐릭터");
    }

    // 장비 — 고른 등급 이하 중 끼고 있지 않은 것만. 인챈트 등급도 범위 안이어야 한다 (OnBulkSellClicked에서 호출).
    private void BulkAddEquips()
    {
        _cart.ClearEquips(); // 범위를 다시 잡는다 — 담아 둔 자원·캐릭터는 남긴다

        GlobalRarity limit   = SelectedRarity;
        var          picked  = new List<long>();
        int          blocked = 0;

        foreach (EquipInfo equip in _data.Equips)
        {
            // 'EnchantGrade'는 0(없음) 또는 'GlobalRarity' 값이다 — 장비 등급과 같은 축으로 비교한다.
            if (GameDataLoader.GetEquipRarity(equip.EquipTid) > limit || (GlobalRarity)equip.EnchantGrade > limit)
            {
                continue;
            }

            if (EntityBlockText.ForEquip(_data, equip.EquipId) != null)
            {
                blocked++;

                continue;
            }

            picked.Add(equip.EquipId);
        }

        _cart.AddEntities(System.Array.Empty<long>(), picked);
        NotifyEntityBulk("장비", "개", picked.Count, blocked, "캐릭터가 끼고 있는 장비");
    }

    // 개체 일괄 담기 결과를 알린다 — 뺀 것이 있을 때와 하나도 못 담았을 때만 (BulkAddCharacters · BulkAddEquips).
    private void NotifyEntityBulk(string kind, string unit, int added, int blocked, string blockedReason)
    {
        string blockedText = blocked > 0 ? $" ({blockedReason} {blocked}{unit} 제외)" : "";

        if (added == 0)
        {
            _wait.RaiseNotice($"{LimitLabel} 이하로 담을 {kind}이(가) 없습니다.{blockedText}");

            return;
        }

        if (blocked > 0)
        {
            _wait.RaiseNotice($"{kind} {added}{unit}을(를) 담았습니다.{blockedText}");
        }

        ClientLogger.Info(ClientLogger.UI, $"일괄 담기 — {SelectedRarity} 이하 {kind} {added}{unit} · 제외 {blocked}{unit}");
    }

    // 드롭다운이 가리키는 범위의 이름 — "일반".
    private string LimitLabel => RarityLabel.Get(SelectedRarity);

    // 드롭다운이 가리키는 등급. 목록과 값 배열이 어긋나면 가장 낮은 등급으로 떨어진다.
    private GlobalRarity SelectedRarity
    {
        get
        {
            int index = bulkRarityDropdown.value;

            return index >= 0 && index < SellRarities.Length ? SellRarities[index] : GlobalRarity.Common;
        }
    }

    #endregion

    #region 찾기

    // 지금 위젯 값으로 찾기 조건을 만들어 격자에 건다 (검색창 · 드롭다운 onValueChanged).
    private void ApplyFilter()
    {
        int rarityIndex   = rarityDropdown.value - 1; // 0번은 '등급 전체'
        int categoryIndex = industryDropdown.value;   // 0번은 '전체'

        GlobalRarity rarity = rarityIndex >= 0 && rarityIndex < SellRarities.Length
            ? SellRarities[rarityIndex]
            : GlobalRarity.None;

        (byte industry, InventoryItemGroup group) = industryDropdown.gameObject.activeSelf
                                                     && categoryIndex >= 0 && categoryIndex < _categoryOptions.Count
            ? _categoryOptions[categoryIndex]
            : ((byte)0, InventoryItemGroup.None);

        grid.FilterCurrent(new InventoryFilter(searchInput.text, rarity, industry, group));
    }

    // 찾기 조건을 모두 비운다 ([초기화] · 탭 전환 · OnDisable).
    //
    // 위젯은 알림 없이 되돌린다 — 알림을 켜 두면 위젯 셋이 각자 'ApplyFilter'를 불러 격자가 세 번 다시 그린다.
    private void ResetFilter()
    {
        searchInput.SetTextWithoutNotify("");
        industryDropdown.SetValueWithoutNotify(0);
        rarityDropdown.SetValueWithoutNotify(0);

        grid.FilterCurrent(default);
    }

    #endregion

    #region 표시

    // 이 탭에서 도구 줄을 보일지 정하고, 탭에 맞게 위젯을 다시 세운다 (Start · TabChanged 구독).
    //
    // 특성 탭에서는 **줄째** 감춘다 — 정렬할 목록도 팔 것도 없다.
    // 자식만 끄면 배경과 줄 높이가 빈 띠로 남는다. 자기를 끄면 부모 세로 배치에서 아예 빠진다.
    //
    // 스스로 꺼져도 구독은 살아 있다 — 구독을 'Start/OnDestroy'에 건 이유가 이것이다('Subscribe' 주석).
    // ※ 찾기 조건은 탭마다 비운다 — 떠난 탭의 조건은 격자가 이미 풀었고('ShowTab'), 위젯만 남아 있으면 거짓말이 된다.
    private void ApplyTab(InventoryTab tab)
    {
        gameObject.SetActive(tab != InventoryTab.Trait);

        if (tab == InventoryTab.Trait)
        {
            return; // OnDisable이 비운다
        }

        RebuildSortKeyOptions();
        RebuildCategoryOptions();

        // 산업 축이 없는 탭(캐릭터)은 드롭다운을 감춘다 — 골라도 아무 일이 없으면 고장처럼 보인다.
        industryDropdown.gameObject.SetActive(grid.CurrentSupportsIndustryFilter);

        // 상자 제외는 자원 탭에서만 뜻이 있다.
        excludeBoxToggle.gameObject.SetActive(tab == InventoryTab.Resource);

        ResetFilter();
    }

    #endregion
}

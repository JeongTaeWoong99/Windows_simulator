using System;
using System.Collections.Generic;
using System.Text;
using GameData;
using MikaNetwork;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 큐브 창 — 장비 하나에 인챈트 큐브를 써서 능력치 칸을 다시 뽑는다 (T-095 · 이슈 #46).
//
// ■ 판매 목록 자리를 빌려 쓴다 (2026-10-03 사용자 결정)
// 장비 탭에서 장비를 **좌클릭**하면 판매 목록이 비워지며 물러나고 이 창이 그 자리에 열린다.
// 닫기 · 다른 탭 · 우클릭(판매 담기)이면 물러나고 판매 목록이 돌아온다.
// 판매 목록은 'OpenChanged'를 듣고 스스로 물러난다 — 이 창이 판매 목록을 쥐지 않는다.
//
// ■ 규칙 (서버 'EnchantCatalog' · 이슈 #46 서버 코멘트)
//   - 칸 수 = **장비 등급**(일반·고급 1 · 희귀·영웅 2 · 전설·신화 3). 인챈트 등급은 장비 하나에 하나다.
//   - 첫 사용은 판정 없이 **일반**으로 칸을 채운다. 그다음부터는 확률로 **한 단계** 오르고, 내려가지 않는다.
//   - 어느 쪽이든 **칸 전부를 다시 뽑는다**(칸 선택 없음). 같은 옵션이 겹쳐 나올 수 있다.
//   - 착용 중이면 서버가 거절한다(EnchantEquipped) → [해제하고 사용]이 해제부터 보낸다(사용자 결정 D4-B).
// 이 규칙은 문서를 안 읽으면 화면만 보고 알 수 없었다 — 한 줄 요약(규칙 줄)을 늘 보이고,
// 전체 규칙과 확률 표는 규칙 줄·큐브 버튼에 올리면 툴팁으로 펼친다('EquipLabel.BuildCubeRuleTooltip').
//
// ■ 결과는 응답이 그린다
// 이전 칸은 **보낼 때** 복사해 둔다(응답에 없다). 이후 칸·등급은 'S_EquipEnchantResponse'의 값이다.
// 캐시는 'S_EquipSyncResponse'가 따로 갱신한다 — 칸 네모·툴팁은 그쪽을 따라간다.
public class EquipEnchantPresenter : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("인벤토리 탭 줄. 장비 탭을 떠나면 이 창이 닫힌다")]
    private InventoryTabPresenter inventoryTabs = null!;

    [SerializeField, Tooltip("장비 아이콘 칸 (ItemIconView 프리팹) — 칸 네모까지 그린다")]
    private ItemIconView itemIcon = null!;

    [SerializeField, Tooltip("장비 이름 — 등급색")]
    private TMP_Text nameText = null!;

    [SerializeField, Tooltip("이름 아래 한 줄 — '희귀 무기 · 기본 농사 +30% · 능력치 칸 2개'")]
    private TMP_Text infoText = null!;

    [SerializeField, Tooltip("판매 목록으로 돌아가는 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button closeButton = null!;

    [CenterHeader("비교 상자")]
    [SerializeField, Tooltip("왼쪽 상자 제목 — 쓰기 전 '현재 · 인챈트 희귀' / 쓴 뒤 '이전 · 희귀'")]
    private TMP_Text beforeTitleText = null!;

    [SerializeField, Tooltip("왼쪽 상자 칸 목록 (칸마다 한 줄)")]
    private TMP_Text beforeOptionsText = null!;

    [SerializeField, Tooltip("두 상자 사이 '▶' — 쓰기 전에는 꺼진다")]
    private GameObject arrow = null!;

    [SerializeField, Tooltip("오른쪽 상자 — 쓰기 전에는 꺼진다")]
    private GameObject afterBox = null!;

    [SerializeField, Tooltip("오른쪽 상자 제목 — '등급 상승! 희귀 ▶ 영웅' · '다시 뽑음 · 희귀'")]
    private TMP_Text afterTitleText = null!;

    [SerializeField, Tooltip("오른쪽 상자 칸 목록")]
    private TMP_Text afterOptionsText = null!;

    [CenterHeader("규칙 · 큐브")]
    [SerializeField, Tooltip("한 줄 요약 — 이번에 쓰면 무엇이 되나. 올리면 전체 규칙·확률 표")]
    private TMP_Text ruleText = null!;

    [SerializeField, Tooltip("규칙 줄의 툴팁 트리거")]
    private TooltipTrigger ruleTooltip = null!;

    [SerializeField, NonReorderable, Tooltip("큐브 버튼 — 'EnchantItemTable' 순서로 채운다. 남는 버튼은 꺼진다")]
    private EnchantCubeView[] cubeViews = new EnchantCubeView[0];

    [SerializeField, Tooltip("[큐브 사용] 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button confirmButton = null!;

    [SerializeField, Tooltip("버튼 글씨 — '큐브 사용 (인챈트 큐브 1개)'. 코드가 채운다")]
    private TMP_Text confirmLabel = null!;

    [CenterHeader("색")]
    [SerializeField, Tooltip("[큐브 사용] — 지금 쓸 수 있을 때")]
    private UIThemeRole confirmReadyRole = UIThemeRole.ButtonPrimary;

    [SerializeField, Tooltip("[큐브 사용] — 큐브가 없거나 응답을 기다릴 때. 눌리기는 한다(누르면 이유 알림)")]
    private UIThemeRole confirmBlockedRole = UIThemeRole.ButtonDisabled;

    // 방금 쓴 결과 — 이전 ▶ 이후. 장비를 바꾸거나 창을 닫으면 버린다.
    private sealed class EnchantResult
    {
        public GlobalRarity BeforeGrade;
        public GlobalRarity AfterGrade;
        public bool         RankedUp;
        public readonly List<int> BeforeOptions = new List<int>();
        public readonly List<int> AfterOptions  = new List<int>();
    }

    // 창이 열렸는가. 판매 목록이 이걸 보고 물러난다.
    public bool IsOpen => _equipId != 0L;

    // 열림이 바뀌었다 ('SellCartPresenter'가 구독 — 열리면 카트를 비우고 물러난다).
    public event Action<bool>? OpenChanged;

    private readonly List<EnchantOptionTableRow?> _options = new List<EnchantOptionTableRow?>();
    private readonly List<int>                    _sending = new List<int>();
    private readonly StringBuilder                _text    = new StringBuilder();

    private PlayerDataModel   _data    = null!;
    private UIManager         _ui      = null!;
    private NetworkManager    _network = null!;
    private ServerWaitManager _wait    = null!;

    private long           _equipId;      // 창에 띄운 장비 개체. 0이면 닫혀 있다
    private int            _cubeIndex;    // 고른 큐브 — 'GameDataLoader.EnchantCubes'의 순서
    private EnchantResult? _result;       // 지금 장비에 방금 쓴 결과. 없으면 '현재' 상자만 그린다

    // 진행 중인 요청 — 해제(D4-B의 첫 단계) 또는 큐브 사용. null이면 기다리는 것이 없다.
    private ServerWaitHandle? _waitHandle;
    private long _unequipThenEnchantId;   // 해제 응답을 기다리는 장비. 0이면 해제를 보내지 않았다
    private long _enchantingEquipId;      // 큐브 응답을 기다리는 장비. 0이면 보내지 않았다
    private int  _enchantingCubeTid;

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 배선 → 초기화 순서로 진행한다 (클라 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다.
    private void Start()
    {
        this.RequireRef(inventoryTabs,     nameof(inventoryTabs));
        this.RequireRef(itemIcon,          nameof(itemIcon));
        this.RequireRef(nameText,          nameof(nameText));
        this.RequireRef(infoText,          nameof(infoText));
        this.RequireRef(closeButton,       nameof(closeButton));
        this.RequireRef(beforeTitleText,   nameof(beforeTitleText));
        this.RequireRef(beforeOptionsText, nameof(beforeOptionsText));
        this.RequireRef(arrow,             nameof(arrow));
        this.RequireRef(afterBox,          nameof(afterBox));
        this.RequireRef(afterTitleText,    nameof(afterTitleText));
        this.RequireRef(afterOptionsText,  nameof(afterOptionsText));
        this.RequireRef(ruleText,          nameof(ruleText));
        this.RequireRef(ruleTooltip,       nameof(ruleTooltip));
        this.RequireRef(confirmButton,     nameof(confirmButton));
        this.RequireRef(confirmLabel,      nameof(confirmLabel));

        if (cubeViews.Length == 0)
        {
            throw new InvalidOperationException($"{name}: 'cubeViews'가 비어 있다 — 큐브 버튼을 인스펙터에 넣을 것.");
        }

        _data    = Services.Get<PlayerDataModel>();
        _ui      = Services.Get<UIManager>();
        _network = NetworkManager.Instance;
        _wait    = Services.Get<ServerWaitManager>();

        closeButton.onClick.AddListener(Close);
        confirmButton.onClick.AddListener(OnConfirmClicked);
        ruleTooltip.SetProvider(EquipLabel.BuildCubeRuleTooltip);

        for (int i = 0; i < cubeViews.Length; i++)
        {
            cubeViews[i].Clicked += OnCubeClicked;
        }

        // ⚠️ 탭·응답 구독은 Start/OnDestroy에 건다 — 이 창은 자기 오브젝트를 끈다.
        //    응답이 늦게 오는 사이 창을 닫아도 대기 손잡이가 닫혀야 로딩이 남지 않는다(격자의 상자 개봉과 같은 이유).
        inventoryTabs.TabChanged    += OnTabChanged;
        _data.EquipEnchantCompleted += OnEnchantCompleted;
        _data.EquipCompleted        += OnUnequipCompleted;

        _isReady = true;

        Subscribe();

        // 닫힌 채 깨어났으면(씬에 켜진 채 저장됨) 바로 물러난다 — 판매 목록 자리를 비워 두면 안 된다.
        if (!IsOpen)
        {
            gameObject.SetActive(false);

            return;
        }

        Redraw();
    }

    // 탭·응답 구독 해제 (Unity 메시지). 자기 오브젝트를 끄므로 여기서만 푼다.
    private void OnDestroy()
    {
        if (!_isReady)
        {
            return;
        }

        inventoryTabs.TabChanged    -= OnTabChanged;
        _data.EquipEnchantCompleted -= OnEnchantCompleted;
        _data.EquipCompleted        -= OnUnequipCompleted;
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    // ★ 닫혀 있는 동안 큐브 수·장비가 바뀌었을 수 있다 — 다시 그린다.
    private void OnEnable()
    {
        if (!_isReady)
        {
            return;
        }

        Subscribe();
        Redraw();
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
    }

    #region 열고 닫기

    // 이 장비로 창을 연다 (인벤토리 격자가 장비 칸 좌클릭에서 호출).
    //
    // ※ 아직 'Start'가 안 돌았을 수 있다(꺼진 채 저장된 창) — 장비만 기억하고 켜면 'Start'가 그린다.
    public void Open(long equipId)
    {
        if (equipId == 0L || equipId == _equipId)
        {
            return;
        }

        bool wasOpen = IsOpen;

        _equipId = equipId;
        _result  = null; // 다른 장비의 결과를 이 장비에 그리지 않는다

        gameObject.SetActive(true);

        if (_isReady)
        {
            Redraw();
        }

        if (!wasOpen)
        {
            OpenChanged?.Invoke(true);
        }
    }

    // 창을 닫고 판매 목록에 자리를 돌려준다 (닫기 버튼 · 탭 전환 · 격자의 우클릭 판매 담기 · 장비가 사라졌을 때).
    // ※ 응답을 기다리는 중이어도 닫는다 — 대기 손잡이는 응답 구독(Start/OnDestroy)이 닫는다.
    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        _equipId = 0L;
        _result  = null;

        gameObject.SetActive(false);
        OpenChanged?.Invoke(false);
    }

    // 장비 탭을 떠나면 닫는다 (InventoryTabPresenter.TabChanged 구독).
    private void OnTabChanged(InventoryTab tab)
    {
        if (tab != InventoryTab.Equipment)
        {
            Close();
        }
    }

    #endregion

    #region 구독

    // 장비·인벤토리 변경 구독 (Start · OnEnable에서 호출) — 칸이 바뀌고 큐브 수가 준다.
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed           = true;
        _data.EquipsChanged    += Redraw;
        _data.InventoryChanged += Redraw;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed           = false;
        _data.EquipsChanged    -= Redraw;
        _data.InventoryChanged -= Redraw;
    }

    #endregion

    #region 그리기

    // 창 전체를 다시 그린다 (Open · OnEnable · 장비·인벤토리 변경 · 큐브 고르기 · 응답).
    private void Redraw()
    {
        if (!IsOpen)
        {
            return;
        }

        EquipInfo? equip = _data.FindEquip(_equipId);

        // 팔았거나 경매에 올려 사라졌다 — 비어 있는 창을 남기지 않는다.
        if (equip == null || !GameDataLoader.TryGetEquip(equip.EquipTid, out EquipTableRow row))
        {
            Close();

            return;
        }

        var grade     = (GlobalRarity)equip.EnchantGrade;
        int slotCount = EquipLabel.GetStatSlotCount(row.GlobalRarity);

        DrawHeader(equip, row, slotCount);
        DrawCompare(equip, grade);
        DrawRule(grade, slotCount);
        DrawCubes(grade);
        DrawConfirm(equip);
    }

    // 머리 — 아이콘 · 이름(등급색) · 등급·종류·기본 능력치·칸 수 한 줄.
    private void DrawHeader(EquipInfo equip, EquipTableRow row, int slotCount)
    {
        itemIcon.Bind(ItemIconContent.ForEquip(equip.EquipTid, equip.EnchantOptions));

        nameText.text  = row.Name;
        nameText.color = RarityPalette.Get(row.GlobalRarity);

        string worn = equip.EquippedCharacterId == 0L
            ? ""
            : $"{UIRichText.Dot}{_data.GetCharacterName(equip.EquippedCharacterId)} 착용 중";

        infoText.text = $"{RarityLabel.Get(row.GlobalRarity)} {EquipLabel.GetKindName(row.EquipKind)}{UIRichText.Dot}"
                      + $"기본 {EquipLabel.GetEffectText(equip.EquipTid)}{UIRichText.Dot}능력치 칸 {slotCount}개{worn}";
    }

    // 비교 상자 — 쓰기 전에는 '현재' 하나, 쓴 뒤에는 '이전 ▶ 결과'.
    private void DrawCompare(EquipInfo equip, GlobalRarity grade)
    {
        if (_result == null)
        {
            beforeTitleText.text   = grade == GlobalRarity.None ? "현재 · 인챈트 없음" : $"현재 · 인챈트 {GradeText(grade)}";
            beforeOptionsText.text = FormatOptions(equip.EquipTid, equip.EnchantOptions);

            arrow.SetActive(false);
            afterBox.SetActive(false);

            return;
        }

        beforeTitleText.text   = _result.BeforeGrade == GlobalRarity.None ? "이전 · 인챈트 없음" : $"이전 · {GradeText(_result.BeforeGrade)}";
        beforeOptionsText.text = FormatOptions(equip.EquipTid, _result.BeforeOptions);

        afterTitleText.text = _result.RankedUp
            ? $"{Colorize("등급 상승!", UIThemePalette.Of(UIThemeRole.Highlight))} {GradeText(_result.BeforeGrade)} ▶ {GradeText(_result.AfterGrade)}"
            : _result.BeforeGrade == GlobalRarity.None
                ? $"인챈트가 붙었다 · {GradeText(_result.AfterGrade)}"
                : $"다시 뽑음 · {GradeText(_result.AfterGrade)}";
        afterOptionsText.text = FormatOptions(equip.EquipTid, _result.AfterOptions);

        arrow.SetActive(true);
        afterBox.SetActive(true);
    }

    // 규칙 줄 — 이번에 쓰면 무엇이 되나 한 줄. 전체 규칙은 올리면 툴팁으로 펼친다.
    private void DrawRule(GlobalRarity grade, int slotCount)
    {
        const string More = "  (올리면 규칙·확률)";

        // 큐브 차이는 상승 확률뿐이라 첫 사용·신화에서는 어느 큐브든 결과가 같다 — 비싼 큐브를 고르면 알린다.
        // 근본 해결(상급 큐브에 다른 장점을 주거나 큐브를 하나로 합치기)은 서버·기획 몫이다(T-095 후속 이슈).
        if (IsWastedCube(grade, out string cheaper))
        {
            ruleText.text = Colorize(grade == GlobalRarity.None
                                         ? $"첫 사용은 어느 큐브든 {GradeText(GlobalRarity.Common)} 확정 — {cheaper}와 결과가 같습니다."
                                         : $"최고 등급이라 오를 등급이 없어 {cheaper}와 결과가 같습니다.",
                                     UIThemePalette.Of(UIThemeRole.Highlight)) + More;

            return;
        }

        if (grade == GlobalRarity.None)
        {
            ruleText.text = $"처음 쓰면 {GradeText(GlobalRarity.Common)} 등급으로 칸 {slotCount}개를 채웁니다.{More}";
        }
        else if (grade >= GlobalRarity.Mythic)
        {
            ruleText.text = $"최고 등급입니다. 쓰면 칸 {slotCount}개를 같은 등급으로 다시 뽑습니다.{More}";
        }
        else
        {
            ruleText.text = $"쓰면 칸 {slotCount}개를 전부 다시 뽑고, 확률로 {GradeText(grade + 1)}(으)로 오릅니다. 내려가지 않습니다.{More}";
        }
    }

    // 고른 큐브가 이 등급에서 더 싼 큐브와 결과가 같은가 — 상승이 없는 첫 사용·신화에서 확률이 가장 낮은 큐브가 아니면 그렇다.
    //   cheaper : 같은 결과를 내는 큐브 이름 (상승 배율이 가장 낮은 것)
    private bool IsWastedCube(GlobalRarity grade, out string cheaper)
    {
        cheaper = "";

        IReadOnlyList<EnchantItemTableRow> cubes = GameDataLoader.EnchantCubes;

        if ((grade != GlobalRarity.None && grade < GlobalRarity.Mythic) || _cubeIndex >= cubes.Count)
        {
            return false;
        }

        EnchantItemTableRow lowest = cubes[0];

        foreach (EnchantItemTableRow cube in cubes)
        {
            if (cube.UpRatePermille < lowest.UpRatePermille)
            {
                lowest = cube;
            }
        }

        if (cubes[_cubeIndex].UpRatePermille <= lowest.UpRatePermille)
        {
            return false;
        }

        cheaper = GameDataLoader.GetItemName(lowest.ItemTID);

        return true;
    }

    // 큐브 버튼 — 'EnchantItemTable' 순서로 채우고 남는 버튼은 끈다.
    private void DrawCubes(GlobalRarity grade)
    {
        IReadOnlyList<EnchantItemTableRow> cubes = GameDataLoader.EnchantCubes;

        if (_cubeIndex >= cubes.Count)
        {
            _cubeIndex = 0;
        }

        for (int i = 0; i < cubeViews.Length; i++)
        {
            if (i >= cubes.Count)
            {
                cubeViews[i].Hide();

                continue;
            }

            int    tid   = cubes[i].ItemTID;
            int    owned = _data.GetItemCount(tid);
            string what  = grade == GlobalRarity.None ? "첫 사용 · 일반 확정"
                         : grade >= GlobalRarity.Mythic ? "옵션만 다시 뽑기"
                         : $"{RarityLabel.Get(grade + 1)} 상승 {EquipLabel.FormatPermyriad(GameDataLoader.GetEnchantUpPermyriad(grade, tid))}";

            cubeViews[i].Bind(GameDataLoader.GetItemName(tid),
                              $"보유 {owned:N0}개{UIRichText.Dot}{what}",
                              owned > 0,
                              i == _cubeIndex,
                              EquipLabel.BuildCubeRuleTooltip);
        }
    }

    // [큐브 사용] — 글씨와 색. 착용 중이면 '해제하고 사용', 방금 썼으면 '한 번 더'.
    private void DrawConfirm(EquipInfo equip)
    {
        IReadOnlyList<EnchantItemTableRow> cubes = GameDataLoader.EnchantCubes;

        if (cubes.Count == 0)
        {
            confirmLabel.text = "큐브 정보가 없습니다";
            PaintConfirm(false);

            return;
        }

        int    tid   = cubes[_cubeIndex].ItemTID;
        string cube  = GameDataLoader.GetItemName(tid);
        bool   owned = _data.GetItemCount(tid) > 0;

        confirmLabel.text = IsWaiting                          ? "응답을 기다리는 중"
                          : !owned                             ? $"{cube}가 없습니다"
                          : equip.EquippedCharacterId != 0L    ? $"해제하고 사용 ({cube} 1개)"
                          : _result != null                    ? $"한 번 더 ({cube} 1개)"
                          : $"큐브 사용 ({cube} 1개)";

        PaintConfirm(owned && !IsWaiting);
    }

    // 칸 목록 문구 — 칸마다 한 줄, 등급색. 빈 칸은 흐린 '비어 있음'.
    private string FormatOptions(int equipTid, IReadOnlyList<int> optionTids)
    {
        EquipLabel.ReadStatOptions(equipTid, optionTids, _options);

        _text.Clear();

        for (int i = 0; i < _options.Count; i++)
        {
            if (i > 0)
            {
                _text.Append('\n');
            }

            EnchantOptionTableRow? option = _options[i];

            _text.Append(option == null
                ? Colorize("비어 있음", UIThemePalette.Of(UIThemeRole.TextDisabled))
                : Colorize(EquipLabel.GetOptionText(option), RarityPalette.Get(option.Grade)));
        }

        return _text.ToString();
    }

    // [큐브 사용] 색 — 쓸 수 있으면 강조색, 아니면 회색 (DrawConfirm에서 호출).
    //
    // ※ 'interactable'은 끄지 않는다 — 눌러서 이유를 들을 수 있어야 한다('TraitPresenter.PaintConfirm'과 같은 공식).
    private void PaintConfirm(bool ready)
    {
        Color      color  = UIThemePalette.Of(ready ? confirmReadyRole : confirmBlockedRole);
        ColorBlock colors = confirmButton.colors;

        colors.normalColor      = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.15f);
        colors.pressedColor     = Color.Lerp(color, Color.black, 0.2f);
        colors.selectedColor    = color;

        confirmButton.colors = colors;
    }

    // 등급 이름을 그 등급색으로.
    private static string GradeText(GlobalRarity grade) => Colorize(RarityLabel.Get(grade), RarityPalette.Get(grade));

    private static string Colorize(string text, Color color) => $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";

    #endregion

    #region 큐브 사용

    private bool IsWaiting => _waitHandle != null;

    // 큐브 버튼을 눌렀다 — 고르기만 한다 (EnchantCubeView.Clicked 구독).
    private void OnCubeClicked(EnchantCubeView view)
    {
        int index = Array.IndexOf(cubeViews, view);

        if (index < 0 || index == _cubeIndex)
        {
            return;
        }

        _cubeIndex = index;
        Redraw();
    }

    // [큐브 사용]을 눌렀다 (confirmButton OnClick에 코드로 연결).
    //
    // 판정 순서 — 대기 중이면 무시 → 큐브가 없으면 알림 → 착용 중이면 해제를 묻는다 → 보낸다.
    // ※ 덮어쓰기 경고는 두지 않는다(사용자 결정 D5-A) — 등급은 내려가지 않고, 지금 칸이 상자에 보인다.
    private void OnConfirmClicked()
    {
        if (IsWaiting || !IsOpen)
        {
            return;
        }

        if (!_data.IsLoggedIn)
        {
            ClientLogger.Warn(ClientLogger.Send, "큐브 사용 요청을 보내지 않았다 — 로그인이 먼저다(서버가 응답 없이 버린다)");

            return;
        }

        EquipInfo?                         equip = _data.FindEquip(_equipId);
        IReadOnlyList<EnchantItemTableRow> cubes = GameDataLoader.EnchantCubes;

        if (equip == null || cubes.Count == 0)
        {
            return;
        }

        int cubeTid = cubes[_cubeIndex].ItemTID;

        if (_data.GetItemCount(cubeTid) <= 0)
        {
            _wait.RaiseNotice($"{GameDataLoader.GetItemName(cubeTid)}가 없습니다.");

            return;
        }

        if (equip.EquippedCharacterId != 0L)
        {
            long   equipId   = equip.EquipId;
            string character = _data.GetCharacterName(equip.EquippedCharacterId);

            _ui.AskConfirm($"{character}이(가) 끼고 있는 장비입니다.\n해제한 뒤 큐브를 사용합니다. (사용 후 다시 끼지 않습니다)",
                           () => RequestUnequip(equipId, cubeTid));

            return;
        }

        RequestEnchant(equip, cubeTid);
    }

    // 착용 중인 장비를 먼저 벗긴다 — 응답이 오면 큐브를 보낸다 (D4-B · AskConfirm 확인에서 호출).
    //
    // ※ 확인 팝업이 떠 있는 사이 상황이 바뀌었을 수 있다 — 다시 찾아 본다.
    private void RequestUnequip(long equipId, int cubeTid)
    {
        EquipInfo? equip = _data.FindEquip(equipId);

        if (IsWaiting || equip == null || equip.EquippedCharacterId == 0L)
        {
            return;
        }

        _network.Send(new C_UnequipRequest { CharacterId = equip.EquippedCharacterId, Slot = equip.EquippedSlot });

        ClientLogger.Info(ClientLogger.Send, $"큐브 사용 전 해제 — 장비 #{equipId} @캐릭터 {equip.EquippedCharacterId} {equip.EquippedSlot}");

        _unequipThenEnchantId = equipId;
        _enchantingCubeTid    = cubeTid;
        _waitHandle           = _wait.Begin("장비 해제", onClosed: OnWaitClosed);
        Redraw();
    }

    // 해제 결과 — 내가 보낸 해제면 이어서 큐브를 보낸다 (PlayerDataModel.EquipCompleted 구독).
    // ※ 장착·해제 응답은 작업슬롯 화면도 듣는다 — 내가 보낸 것이 없으면 무시한다.
    private void OnUnequipCompleted(bool success, EResultCode code)
    {
        if (_unequipThenEnchantId == 0L || _waitHandle == null)
        {
            return;
        }

        long equipId = _unequipThenEnchantId;
        int  cubeTid = _enchantingCubeTid;

        _unequipThenEnchantId = 0L;

        if (!success)
        {
            _waitHandle.Fail(ResultMessages.ToText(code));

            return;
        }

        _waitHandle.Succeed();

        // ★ 응답이 개체 동기화보다 먼저 올 수 있다 — 캐시가 아직 '착용 중'이어도 서버는 이미 벗겼다.
        EquipInfo? equip = _data.FindEquip(equipId);

        if (equip != null)
        {
            RequestEnchant(equip, cubeTid);
        }
    }

    // 큐브 사용을 보낸다 (OnConfirmClicked · 해제 성공 뒤).
    private void RequestEnchant(EquipInfo equip, int cubeTid)
    {
        // 이전 칸은 응답에 없다 — 보내는 순간을 복사해 둔다.
        _sending.Clear();
        _sending.AddRange(equip.EnchantOptions);

        _network.Send(new C_EquipEnchantRequest { EquipId = equip.EquipId, ItemTid = cubeTid });

        ClientLogger.Info(ClientLogger.Send, $"큐브 사용 — 장비 #{equip.EquipId}, 큐브 {cubeTid}, 지금 등급 {equip.EnchantGrade}");

        _enchantingEquipId = equip.EquipId;
        _waitHandle        = _wait.Begin("큐브 사용", onClosed: OnWaitClosed);
        Redraw();
    }

    // 큐브 사용 결과 (PlayerDataModel.EquipEnchantCompleted 구독).
    // ※ 창을 닫았거나 다른 장비로 바꿨어도 대기는 닫는다. 결과는 그 장비가 지금 창에 있을 때만 그린다.
    private void OnEnchantCompleted(S_EquipEnchantResponse res)
    {
        if (_enchantingEquipId == 0L || res.EquipId != _enchantingEquipId)
        {
            return;
        }

        _enchantingEquipId = 0L;

        if (res.Result != EResultCode.Ok)
        {
            _waitHandle?.Fail(ResultMessages.ToText(res.Result));

            return;
        }

        if (res.EquipId == _equipId)
        {
            var result = new EnchantResult
            {
                BeforeGrade = (GlobalRarity)res.BeforeGrade,
                AfterGrade  = (GlobalRarity)res.AfterGrade,
                RankedUp    = res.Success,
            };

            result.BeforeOptions.AddRange(_sending);
            result.AfterOptions.AddRange(res.Options);

            _result = result;
        }

        _waitHandle?.Succeed();
    }

    // 대기가 끝났다(성공·실패·타임아웃 공통) — 버튼을 푼다 (ServerWaitManager.Begin의 onClosed)
    // ※ 해제가 끝나고 큐브 대기가 이어 열렸으면 손잡이를 지우지 않는다 — 앞 손잡이의 닫힘이 뒤에 온다.
    private void OnWaitClosed()
    {
        if (_waitHandle != null && !_waitHandle.IsClosed)
        {
            return;
        }

        _waitHandle           = null;
        _unequipThenEnchantId = 0L;
        _enchantingEquipId    = 0L;
        Redraw();
    }

    #endregion
}

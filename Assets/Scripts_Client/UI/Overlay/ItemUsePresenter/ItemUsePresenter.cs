using System;
using System.Collections.Generic;
using GameData;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CharacterInfo = MikaProtocol.CharacterInfo;

// 소모품 사용 창 — 무엇에 몇 개를 쓸지 고르고 확인을 누르면 그 선택을 돌려준다 (T-194 · 2026-11-14 사용자 결정).
//
// ■ 지금 누가 쓰나
// 인벤토리 자원 탭의 소모품 칸 좌클릭이다(상자 칸 좌클릭이 상자를 여는 것과 같은 동선).
// 대상은 'ItemUseTable.UseEffect'가 정한다 — 영약·지도·나침반은 슬롯(최대 'SlotCount'개), 비약은 캐릭터 하나, 묘약은 대상 없음.
//
// ■ 요청은 보내지 않는다
// 'AmountInputPresenter'처럼 **고른 것만 돌려준다.** 요청·응답 대기·거절 문구는 부른 쪽(인벤토리 격자)이 맡는다 —
// 상자 개봉과 같은 'C_ItemUseRequest'라 대기 손잡이도 한 벌이다. 되돌릴 수 없는 묘약의 확인도 부른 쪽이 묻는다.
//
// ■ 고르는 규칙은 화면 편의일 뿐이다
// 만렙 캐릭터·쉬는 슬롯(지도)을 흐리게 막지만 판정은 서버가 한다('InvalidUseTarget' · 'UseTargetIdle' · 'CharacterMaxLevel').
//
// ■ 여닫는 법은 'AmountInputPresenter'와 같다 — 상주 오브젝트를 'CanvasGroup'으로 여닫는다('Overlay 규칙.md').
public class ItemUsePresenter : MonoBehaviour
{
    // 고른 결과 — 'C_ItemUseRequest'에 그대로 옮겨 담는다
    public readonly struct Choice
    {
        public readonly int        ItemTid;
        public readonly int        Count;
        public readonly long       TargetCharacterId; // 비약만 — 그 밖은 0
        public readonly List<int>? TargetSlots;       // 영약·지도·나침반만 — 그 밖은 null

        public Choice(int itemTid, int count, long targetCharacterId, List<int>? targetSlots)
        {
            ItemTid           = itemTid;
            Count             = count;
            TargetCharacterId = targetCharacterId;
            TargetSlots       = targetSlots;
        }
    }

    // 대상 한 줄
    private sealed class Row
    {
        public Button     Button  = null!;
        public TMP_Text   Label   = null!;
        public ItemIconView? Icon;  // 줄 왼쪽 작은 아이콘 — 견본에 없으면 null
        public ColorBlock Normal;
        public long       Key;      // 슬롯 번호 또는 캐릭터 개체 번호
        public bool       Usable;
        public bool       Selected;
    }

    // 대상 줄 견본에서 줄 글씨 오브젝트 이름 — 아이콘 칸에도 글씨(수량)가 있어 이름으로 찾는다
    private const string RowLabelName = "Text (TMP)";

    private enum TargetKind
    {
        None,
        Slot,
        Character,
    }

    [CenterHeader("참조")]
    [SerializeField, Tooltip("자기 CanvasGroup. 이 오브젝트를 끄지 않고 alpha·blocksRaycasts로 여닫는다")]
    private CanvasGroup group = null!;

    [SerializeField, Tooltip("가운데 판 — 열 때 살짝 커지며 나타난다")]
    private RectTransform? panel;

    [SerializeField, Tooltip("제목 — '신속의 영약 사용'")]
    private TMP_Text titleText = null!;

    [SerializeField, Tooltip("머리줄 아이콘 — 쓰는 소모품 그림. 비우면 아이콘 없이 연다")]
    private Image? itemIcon;

    [SerializeField, Tooltip("효과 한 줄 — '작업속도 +30% · 판정 20회'")]
    private TMP_Text effectText = null!;

    [SerializeField, Tooltip("고르는 말 — '슬롯을 고르세요 (최대 2개)'. 대상이 없는 소모품이면 끈다")]
    private TMP_Text pickText = null!;

    [SerializeField, Tooltip("대상 목록 스크롤 — 대상이 없는 소모품이면 끈다")]
    private GameObject targetScroll = null!;

    [SerializeField, Tooltip("대상 줄이 쌓이는 곳 (스크롤 Content)")]
    private RectTransform targetContent = null!;

    [SerializeField, Tooltip("대상 줄 견본 — targetContent 아래에 꺼 둔다. 자식 'Text (TMP)'가 줄 글씨, 자식 ItemIconView가 왼쪽 아이콘이다")]
    private Button rowTemplate = null!;

    [SerializeField, Tooltip("쓸 수 있는 대상이 하나도 없을 때만 보인다")]
    private TMP_Text emptyText = null!;

    [SerializeField, Tooltip("개수 줄 — 1개만 쓸 수 있으면 끈다")]
    private GameObject amountRow = null!;

    [SerializeField, Tooltip("개수 줄의 말 — '개수 (최대 50)'")]
    private TMP_Text amountLabel = null!;

    [SerializeField, Tooltip("개수 입력칸. Content Type은 Integer Number로 둔다")]
    private TMP_InputField amountInput = null!;

    [SerializeField, Tooltip("개수 ▲ — 1 올린다. 꾹 누르면 점점 빨리 오른다. 비우면 버튼 없이 연다")]
    private HoldRepeatButton? amountUpButton;

    [SerializeField, Tooltip("개수 ▼ — 1 내린다. 꾹 누르면 점점 빨리 내린다. 비우면 버튼 없이 연다")]
    private HoldRepeatButton? amountDownButton;

    [SerializeField, Tooltip("미리보기 한 줄 — '죽음의 사신 Lv34 (12%) → Lv36 (45%)'. 비약에서 캐릭터를 골랐을 때만 글씨가 찬다")]
    private TMP_Text? previewText;

    [SerializeField, Tooltip("사용 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button useButton = null!;

    [SerializeField, Tooltip("취소 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button cancelButton = null!;

    private PlayerDataModel _data = null!;

    private readonly List<Row> _rows = new List<Row>();

    private int                     _itemTid;
    private int                     _expPerItem; // 비약 1개의 경험치 — 미리보기용 (비약이 아니면 0)
    private int                     _maxCount;
    private int                     _maxTargets;
    private TargetKind              _kind;
    private Action<Choice>?         _onConfirm;

    // 참조 확보 → 배선 → 초기화 순서로 진행한다 (클라 공통 규약. 구독할 이벤트가 없다)
    private void Start()
    {
        this.RequireRef(group,         nameof(group));
        this.RequireRef(titleText,     nameof(titleText));
        this.RequireRef(effectText,    nameof(effectText));
        this.RequireRef(pickText,      nameof(pickText));
        this.RequireRef(targetScroll,  nameof(targetScroll));
        this.RequireRef(targetContent, nameof(targetContent));
        this.RequireRef(rowTemplate,   nameof(rowTemplate));
        this.RequireRef(emptyText,     nameof(emptyText));
        this.RequireRef(amountRow,     nameof(amountRow));
        this.RequireRef(amountLabel,   nameof(amountLabel));
        this.RequireRef(amountInput,   nameof(amountInput));
        this.RequireRef(useButton,     nameof(useButton));
        this.RequireRef(cancelButton,  nameof(cancelButton));

        _data = Services.Get<PlayerDataModel>();

        useButton.onClick.AddListener(OnUseClicked);
        cancelButton.onClick.AddListener(Close);
        BackdropClose.Bind(gameObject, Close); // 차단막을 누르면 취소
        amountInput.onValueChanged.AddListener(_ => RefreshUseButton());
        if (amountUpButton != null)
        {
            amountUpButton.Repeated += () => StepAmount(+1);
        }

        if (amountDownButton != null)
        {
            amountDownButton.Repeated += () => StepAmount(-1);
        }
        rowTemplate.gameObject.SetActive(false);

        SetVisible(false, instant: true); // 시작은 닫힘 — 씬에 열린 채 저장됐어도 여기서 정리된다
    }

    #region 여닫기

    // 소모품 사용 창을 연다 ('UIManager.AskItemUse'가 호출).
    //   owned     : 보유 개수 — 개수 상한이 된다
    //   onConfirm : [사용]을 누르면 받을 곳. 취소면 불리지 않는다
    public void Open(int itemTid, int owned, Action<Choice> onConfirm)
    {
        if (!ItemUseLabel.TryGet(itemTid, out ItemUseTableRow use))
        {
            ClientLogger.Warn(ClientLogger.UI, $"소모품이 아닌 아이템으로 사용 창을 열려 했다 — TID={itemTid}", this);

            return;
        }

        _itemTid    = itemTid;
        _onConfirm  = onConfirm;
        _expPerItem = use.UseEffect == ItemUseEffect.CharacterExp ? use.EffectValue : 0;
        _kind      = ItemUseLabel.TargetsSlots(use.UseEffect)     ? TargetKind.Slot
                   : ItemUseLabel.TargetsCharacter(use.UseEffect) ? TargetKind.Character
                   : TargetKind.None;

        // 묘약은 한 번에 1개뿐이다(서버 규칙). 그 밖은 상자와 같은 상한 — 넘기면 서버가 'InvalidUseCount'로 거절한다
        _maxCount   = use.UseEffect == ItemUseEffect.TraitReset ? 1 : Mathf.Max(1, Mathf.Min(owned, (int)Constants.BoxOpenMax));
        _maxTargets = _kind == TargetKind.Slot ? Mathf.Max(1, use.SlotCount) : 1;

        string name = GameDataLoader.GetItemName(itemTid);

        titleText.text  = Lang.Format("UI.ItemUse.Title", name);

        if (itemIcon != null)
        {
            Sprite? sprite = VisualCatalog.ItemIconOf(itemTid);

            itemIcon.sprite  = sprite!;
            itemIcon.enabled = sprite != null; // 그림이 없는 소모품은 흰 네모 대신 비운다
        }

        effectText.text = ItemUseLabel.Effect(use);

        BuildRows(use);

        bool hasTargets = _kind != TargetKind.None;

        pickText.gameObject.SetActive(hasTargets);
        targetScroll.SetActive(hasTargets);
        pickText.text = _kind == TargetKind.Slot ? Lang.Format("UI.ItemUse.PickSlots", _maxTargets) : Lang.Get("UI.ItemUse.PickCharacter");

        amountRow.SetActive(_maxCount > 1);
        amountLabel.text = Lang.Format("UI.ItemUse.Amount", $"{_maxCount:N0}");
        amountInput.SetTextWithoutNotify("1"); // 소모품은 상자와 달리 전량이 기본 동선이 아니다 — 1개부터

        RefreshUseButton();
        SetVisible(true);
    }

    // 창을 닫고 대기 중인 콜백을 버린다 (취소 · 확인 · 'UIManager.CloseAllExceptWidget').
    // ★ public인 이유 — 상주라 'OnDisable'이 오지 않는다('AmountInputPresenter.Close'와 같다).
    public void Close()
    {
        _onConfirm = null;

        SetVisible(false);
    }

    // 보이기·차단을 함께 켜고 끈다 (Open · Close에서 호출). 오브젝트는 끄지 않는다
    private void SetVisible(bool on, bool instant = false)
    {
        PanelTransition.SetVisible(group, on, panel, instant);
    }

    #endregion

    #region 대상 줄

    // 대상 줄을 다시 쌓는다 (Open에서 호출). 줄은 지우지 않고 남는 것을 끈다
    private void BuildRows(ItemUseTableRow use)
    {
        int shown = 0;

        if (_kind == TargetKind.Slot)
        {
            foreach (WorkStationSlotInfo slot in _data.WorkStationSlots)
            {
                bool running = WorkStationProgress.IsAssigned(slot) && slot.Industry != EIndustryType.None;

                // 지도(즉시 판정)는 도는 슬롯에만 — 버프는 쉬는 슬롯에도 걸린다(서버 'requireActive')
                bool usable = running || use.UseEffect != ItemUseEffect.InstantJudge;

                // 아이콘은 슬롯에 앉은 캐릭터 — 비어 있으면 아이콘 없이
                CharacterInfo? worker = running ? _data.FindCharacter(slot.CharacterId) : null;

                BindRow(shown++, slot.SlotIndex, SlotLabel(slot, running), usable, worker != null ? ItemIconContent.ForCharacter(worker.CharacterTid) : null);
            }
        }
        else if (_kind == TargetKind.Character)
        {
            var order = new List<CharacterInfo>(_data.Characters);

            // 높은 레벨부터 — 비약은 주로 키우는 캐릭터에게 쓴다. 만렙은 맨 아래로
            order.Sort((a, b) =>
            {
                bool aMax = IsMaxLevel(a.Level);
                bool bMax = IsMaxLevel(b.Level);

                return aMax != bMax ? aMax.CompareTo(bMax) : b.Level.CompareTo(a.Level);
            });

            foreach (CharacterInfo character in order)
            {
                bool   max   = IsMaxLevel(character.Level);
                string label = $"{_data.GetCharacterName(character.CharacterId)} {UIRichText.Small(RaceLabel.Append($"Lv{character.Level}", character.CharacterTid))}";

                if (max)
                {
                    label += UIRichText.Dot + Lang.Get("UI.ItemUse.MaxLevel");
                }

                BindRow(shown++, character.CharacterId, label, !max, ItemIconContent.ForCharacter(character.CharacterTid));
            }
        }

        for (int i = shown; i < _rows.Count; i++)
        {
            _rows[i].Button.gameObject.SetActive(false);
        }

        bool anyUsable = false;

        for (int i = 0; i < shown; i++)
        {
            anyUsable |= _rows[i].Usable;
        }

        emptyText.text = Lang.Get("UI.ItemUse.NoTarget");
        emptyText.gameObject.SetActive(_kind != TargetKind.None && !anyUsable);
    }

    // 'index'번째 줄을 그린다 — 모자라면 견본을 복제한다 (BuildRows에서 호출)
    //   icon : 줄 왼쪽 아이콘 — null이면 아이콘 칸을 끈다(쉬는 슬롯)
    private void BindRow(int index, long key, string label, bool usable, ItemIconContent? icon)
    {
        while (_rows.Count <= index)
        {
            Button button = Instantiate(rowTemplate, targetContent);
            var    row    = new Row
            {
                Button = button,
                Label  = button.transform.Find(RowLabelName).GetComponent<TMP_Text>(),
                Icon   = button.GetComponentInChildren<ItemIconView>(true),
                Normal = button.colors,
            };

            button.name = $"Target Row {_rows.Count}";
            button.onClick.AddListener(() => OnRowClicked(row));
            _rows.Add(row);
        }

        Row found = _rows[index];

        found.Key                 = key;
        found.Usable              = usable;
        found.Selected            = false;
        found.Label.text          = label;
        found.Button.interactable = usable;
        found.Button.gameObject.SetActive(true);

        if (found.Icon != null)
        {
            found.Icon.gameObject.SetActive(icon.HasValue);

            if (icon.HasValue)
            {
                found.Icon.Bind(icon.Value);
            }
        }

        PaintRow(found);
    }

    // 슬롯 줄 글씨 — '#2 채굴 Lv3 · 별빛드래곤 · 남은 12회' / '#4 · 쉬는 중'
    private string SlotLabel(WorkStationSlotInfo slot, bool running)
    {
        string text = $"#{WorkStationSlotView.DisplayNumber(slot.SlotIndex)}";

        if (running)
        {
            text += $" {IndustryLabel.Get(slot.Industry)} Lv{slot.IndustryLevel}{UIRichText.Dot}{_data.GetCharacterName(slot.CharacterId)}";
        }
        else
        {
            text += UIRichText.Dot + Lang.Get("UI.ItemUse.Idle");
        }

        // 같은 소모품이 이미 걸려 있으면 남은 횟수를 붙인다 — 합산된다는 것을 미리 보인다
        if (slot.Buffs != null)
        {
            foreach (SlotBuffInfo buff in slot.Buffs)
            {
                if (buff.ItemTID == _itemTid && buff.Remaining > 0)
                {
                    text += UIRichText.Dot + Lang.Format("UI.ItemUse.BuffLeft", buff.Remaining);
                }
            }
        }

        return text;
    }

    // 다음 레벨의 필요 경험치가 없으면 만렙이다 — 인벤토리 'LV.MAX' 배지와 같은 판정
    private static bool IsMaxLevel(int level) => !GameDataLoader.TryGetRequiredExp(level + 1, out _);

    // 줄 눌림 — 고르기를 뒤집는다. 하나만 고르는 대상이면 다른 줄을 푼다 (줄 Button.onClick에 코드로 연결)
    private void OnRowClicked(Row row)
    {
        if (!row.Usable)
        {
            return;
        }

        if (row.Selected)
        {
            row.Selected = false;
        }
        else
        {
            if (_maxTargets == 1)
            {
                foreach (Row other in _rows)
                {
                    if (other.Selected)
                    {
                        other.Selected = false;
                        PaintRow(other);
                    }
                }
            }
            else if (SelectedCount() >= _maxTargets)
            {
                return; // 꽉 찼다 — 먼저 하나를 풀어야 한다
            }

            row.Selected = true;
        }

        PaintRow(row);
        RefreshUseButton();
    }

    // 고른 줄은 탭과 같은 두 색(고름 = ButtonSelected · 안 고름 = Button)으로 칠한다 — 'AuctionRowView.PaintSelection'과 같은 규칙.
    // ⚠️ Image.color를 직접 건드리지 않는다 — 색의 주인은 ColorBlock이다
    private static void PaintRow(Row row)
    {
        Color      target = UIThemePalette.Of(row.Selected ? UIThemeRole.ButtonSelected : UIThemeRole.Button);
        ColorBlock colors = row.Normal;

        colors.normalColor      = target;
        colors.selectedColor    = target;
        colors.highlightedColor = UIThemePalette.HoverOf(target);
        row.Button.colors       = colors;
    }

    private int SelectedCount()
    {
        int count = 0;

        foreach (Row row in _rows)
        {
            if (row.Selected && row.Button.gameObject.activeSelf)
            {
                count++;
            }
        }

        return count;
    }

    #endregion

    #region 확인

    // 개수 ▲▼ — 1씩 올리고 내린다. 1 ~ 상한에서 멈춘다 (amountUpButton · amountDownButton OnClick에 코드로 연결)
    // ※ 입력칸 값을 바꾸면 'onValueChanged'가 [사용] 잠금을 다시 본다.
    private void StepAmount(int delta)
    {
        int current = int.TryParse(amountInput.text, out int parsed) ? parsed : 1;

        amountInput.text = Mathf.Clamp(current + delta, 1, _maxCount).ToString();
    }

    // [사용]을 열지 정한다 — 대상이 필요하면 하나 이상 골라야 하고, 개수가 1 이상이어야 한다
    private void RefreshUseButton()
    {
        bool amountOk = !amountRow.activeSelf || (int.TryParse(amountInput.text, out int amount) && amount >= 1);
        bool targetOk = _kind == TargetKind.None || SelectedCount() > 0;

        useButton.interactable = amountOk && targetOk;

        RefreshPreview();
    }

    // 미리보기 — 고른 캐릭터가 이 개수를 먹으면 몇 레벨 몇 %가 되나 (RefreshUseButton에서 함께 호출).
    // 비약이 아니거나 캐릭터를 안 골랐으면 비운다. 만렙을 넘는 개수는 서버가 필요한 만큼만 빼므로 '최대'로 적는다.
    private void RefreshPreview()
    {
        if (previewText == null)
        {
            return;
        }

        previewText.text = "";

        if (_expPerItem <= 0 || !TryGetSelectedCharacter(out CharacterInfo character))
        {
            return;
        }

        int count = amountRow.activeSelf && int.TryParse(amountInput.text, out int parsed) ? Mathf.Clamp(parsed, 1, _maxCount) : 1;

        (int level, int exp) = ItemUseLabel.ProjectExp(character.Level, character.Exp, (long)_expPerItem * count);

        previewText.text = Lang.Format("UI.ItemUse.ExpPreview",
                                       _data.GetCharacterName(character.CharacterId),
                                       ItemUseLabel.LevelWithPercent(character.Level, character.Exp),
                                       ItemUseLabel.LevelWithPercent(level, exp));
    }

    // 고른 캐릭터 한 명 — 비약은 하나만 고른다
    private bool TryGetSelectedCharacter(out CharacterInfo character)
    {
        character = null!;

        if (_kind != TargetKind.Character)
        {
            return false;
        }

        foreach (Row row in _rows)
        {
            if (row.Selected && row.Button.gameObject.activeSelf && _data.FindCharacter(row.Key) is { } found)
            {
                character = found;

                return true;
            }
        }

        return false;
    }

    // 사용 — 고른 것을 돌려준다 (useButton.onClick에 코드로 연결).
    // ※ 콜백을 부르기 전에 닫는다 — 받은 쪽이 곧바로 확인 팝업을 열 수 있다('AmountInputPresenter'와 같은 이유)
    private void OnUseClicked()
    {
        int count = 1;

        if (amountRow.activeSelf)
        {
            if (!int.TryParse(amountInput.text, out count))
            {
                return;
            }

            count = Mathf.Clamp(count, 1, _maxCount);
        }

        long       characterId = 0L;
        List<int>? slots       = null;

        foreach (Row row in _rows)
        {
            if (!row.Selected || !row.Button.gameObject.activeSelf)
            {
                continue;
            }

            if (_kind == TargetKind.Character)
            {
                characterId = row.Key;
            }
            else if (_kind == TargetKind.Slot)
            {
                (slots ??= new List<int>()).Add((int)row.Key);
            }
        }

        Action<Choice>? callback = _onConfirm;
        var             choice   = new Choice(_itemTid, count, characterId, slots);

        Close();

        callback?.Invoke(choice);
    }

    #endregion
}

using System;
using GameData;
using TMPro;
using UnityEngine;

// 큐브 자동 설정 칸 — 목표 등급 · 칸 1~3 · 상한 · 편의 줄의 칩 (이슈 #53).
// 비교 상자(BEFORE · AFTER)와 **같은 자리 · 같은 높이**에 갈아 끼운다 — 창 크기가 출렁이지 않게.
//
// ■ 칸 줄은 장비 칸 수만큼 켠다 (2026-11-22 사용자 — 시안 A '칸마다 고르기')
//   줄마다 '전체' + 옵션 6개. 칸이 적어 줄이 빠지면 줄들이 아래로 붙는다(세로 배치 아래 정렬) — 상한 줄과 안내 줄 사이가 벌어지지 않게.
//
// 값을 갖지 않는다 — 큐브 창이 'EnchantAutoTarget'을 들고 'Bind'로 그리게 하고, 누른 칩은 이벤트로 돌려준다.
// (종속 View 규약은 'UI 규칙.md'의 "종속 View 쪽 규약")
public class EnchantAutoPanelView : MonoBehaviour
{
    // 칸 줄 하나 — 줄 · 이름 · 칩('EnchantAutoTarget.PickSteps' 순서 7개).
    [Serializable]
    private sealed class SlotRow
    {
        [Tooltip("줄 오브젝트 — 장비 칸 수보다 뒤면 끈다")]
        public GameObject row = null!;

        [Tooltip("줄 이름 글씨 — '칸 1'")]
        public TMP_Text label = null!;

        [NonReorderable, Tooltip("칩 — 전체 · 농사 · 채굴 · 벌목 · 사냥 · 발굴 · 경험치 순서로 7개")]
        public EnchantChipView[] chips = new EnchantChipView[0];
    }

    // 상한 칩 값 — 마지막 0은 '보유 전부'.
    public static readonly int[] CapSteps = { 100, 300, 1000, 3000, 0 };

    [CenterHeader("칩")]
    [SerializeField, NonReorderable, Tooltip("목표 등급 칩 — 고급 · 희귀 · 영웅 · 전설 · 신화 순서로 5개")]
    private EnchantChipView[] gradeChips = new EnchantChipView[0];

    [SerializeField, NonReorderable, Tooltip("칸 줄 3개 — 위부터 칸 1 · 2 · 3")]
    private SlotRow[] slotRows = new SlotRow[0];

    [SerializeField, NonReorderable, Tooltip("상한 칩 — 'CapSteps' 순서(100 · 300 · 1,000 · 3,000 · 전부)로 5개")]
    private EnchantChipView[] capChips = new EnchantChipView[0];

    [SerializeField, NonReorderable, Tooltip("편의 칩 — 신화에 도달하면 멈춤 · 목표 등급보다 높으면 멈춤 순서로 2개")]
    private EnchantChipView[] optionChips = new EnchantChipView[0];

    // 편의 칩 자리 ('optionChips' 순서)
    public const int OptionStopMythic = 0;
    public const int OptionStopAbove  = 1;

    public event Action<GlobalRarity>?          GradeClicked; // 목표 등급
    public event Action<int, EnchantAutoFocus>? PickClicked;  // 칸 줄(0부터) · 누른 칩
    public event Action<int>?                   CapClicked;   // 상한 — 0이면 보유 전부
    public event Action<int>?                   OptionClicked; // 편의 칩 자리 — 'Option*'

    // 칩을 배선한다 — 큐브 창이 Bind를 부르기 전에 연결돼 있어야 해서 Awake (Unity 메시지)
    private void Awake()
    {
        RequireCount(gradeChips, 5, nameof(gradeChips));
        RequireCount(capChips, CapSteps.Length, nameof(capChips));
        RequireCount(optionChips, 2, nameof(optionChips));

        if (slotRows.Length != EnchantAutoTarget.MaxSlots)
        {
            throw new InvalidOperationException($"{name}: 'slotRows'에 줄 {EnchantAutoTarget.MaxSlots}개를 넣을 것 (지금 {slotRows.Length}개).");
        }

        for (int i = 0; i < gradeChips.Length; i++)
        {
            var grade = (GlobalRarity)((int)GlobalRarity.Uncommon + i);
            gradeChips[i].Clicked += _ => GradeClicked?.Invoke(grade);
        }

        for (int r = 0; r < slotRows.Length; r++)
        {
            RequireCount(slotRows[r].chips, EnchantAutoTarget.PickSteps.Length, $"{nameof(slotRows)}[{r}].chips");

            for (int i = 0; i < slotRows[r].chips.Length; i++)
            {
                int row   = r;
                var focus = EnchantAutoTarget.PickSteps[i];
                slotRows[r].chips[i].Clicked += _ => PickClicked?.Invoke(row, focus);
            }
        }

        for (int i = 0; i < capChips.Length; i++)
        {
            int cap = CapSteps[i];
            capChips[i].Clicked += _ => CapClicked?.Invoke(cap);
        }

        for (int i = 0; i < optionChips.Length; i++)
        {
            int option = i;
            optionChips[i].Clicked += _ => OptionClicked?.Invoke(option);
        }
    }

    // 줄을 그린다 (큐브 창이 자동 설정을 열 때 · 칩을 누를 때 · 장비·인벤토리가 바뀔 때).
    //   current   : 장비의 지금 인챈트 등급 — 목표는 그보다 높은 등급만 누른다(신화면 신화만, 칸 조건 필수)
    //   slotCount : 장비 등급이 정한 칸 수 — 켜는 칸 줄 수
    public void Bind(EnchantAutoTarget target, GlobalRarity current, int slotCount)
    {
        bool atTop = current >= GlobalRarity.Mythic;

        for (int i = 0; i < gradeChips.Length; i++)
        {
            var  grade    = (GlobalRarity)((int)GlobalRarity.Uncommon + i);
            bool canPress = atTop ? grade == GlobalRarity.Mythic : grade > current;

            gradeChips[i].Bind(Lang.Get($"UI.Enchant.Chip.{grade}"), target.Grade == grade, canPress); // 칩용 짧은 이름 — 영어 'Legendary'가 칩에서 절반 크기로 줄었다
        }

        for (int r = 0; r < slotRows.Length; r++)
        {
            SlotRow row = slotRows[r];
            bool    on  = r < slotCount;

            row.row.SetActive(on);

            if (!on)
            {
                continue;
            }

            row.label.text = Lang.Format("UI.Enchant.SlotRow", r + 1);

            for (int i = 0; i < row.chips.Length; i++)
            {
                var focus = EnchantAutoTarget.PickSteps[i];

                row.chips[i].Bind(EnchantAutoTarget.GetChipName(focus), target.IsPicked(r, focus), true);
            }
        }

        for (int i = 0; i < capChips.Length; i++)
        {
            int cap = CapSteps[i];

            capChips[i].Bind(cap == 0 ? Lang.Get("UI.Enchant.CapAll") : Lang.Format("UI.Tool.NEquip", $"{cap:N0}"), target.Cap == cap, true);
        }

        // 편의 — 토글 둘. 무엇을 하는지 글씨로 다 적는다(2026-11-22 사용자 — '신화면 멈춤'은 뜻이 흐렸다 · [칸 초기화]는 뺐다)
        optionChips[OptionStopMythic].Bind(Lang.Get("UI.Enchant.StopMythic"), target.StopOnMythic, true);
        optionChips[OptionStopAbove].Bind(Lang.Get("UI.Enchant.StopAbove"), target.StopAboveGoal, true);
    }

    private void RequireCount(EnchantChipView[] chips, int count, string field)
    {
        if (chips.Length != count)
        {
            throw new InvalidOperationException($"{name}: '{field}'에 칩 {count}개를 넣을 것 (지금 {chips.Length}개).");
        }
    }
}

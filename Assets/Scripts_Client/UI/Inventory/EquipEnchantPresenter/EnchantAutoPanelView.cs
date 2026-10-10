using System;
using GameData;
using UnityEngine;

// 큐브 자동 설정 칸 — 목표 등급 · 노릴 옵션 · 조합 · 상한 네 줄의 칩 (이슈 #53).
// 비교 상자(BEFORE · AFTER)와 **같은 자리 · 같은 높이**에 갈아 끼운다 — 창 크기가 출렁이지 않게.
//
// 값을 갖지 않는다 — 큐브 창이 'EnchantAutoTarget'을 들고 'Bind'로 그리게 하고, 누른 칩은 이벤트로 돌려준다.
// (종속 View 규약은 'UI 규칙.md'의 "종속 View 쪽 규약")
public class EnchantAutoPanelView : MonoBehaviour
{
    // 상한 칩 값 — 마지막 0은 '보유 전부'.
    public static readonly int[] CapSteps = { 100, 300, 1000, 3000, 0 };

    [CenterHeader("칩")]
    [SerializeField, NonReorderable, Tooltip("목표 등급 칩 — 고급 · 희귀 · 영웅 · 전설 · 신화 순서로 5개")]
    private EnchantChipView[] gradeChips = new EnchantChipView[0];

    [SerializeField, NonReorderable, Tooltip("노릴 옵션 칩 — 'EnchantAutoFocus' 순서(없음 · 농사 · 낚시 · 채굴 · 벌목 · 사냥 · 경험치)로 7개")]
    private EnchantChipView[] focusChips = new EnchantChipView[0];

    [SerializeField, NonReorderable, Tooltip("조합 칩 4개 — 왼쪽부터 '전체'가 많은 순. 칸 수 + 1개만 켜진다")]
    private EnchantChipView[] comboChips = new EnchantChipView[0];

    [SerializeField, NonReorderable, Tooltip("상한 칩 — 'CapSteps' 순서(100 · 300 · 1,000 · 3,000 · 전부)로 5개")]
    private EnchantChipView[] capChips = new EnchantChipView[0];

    public event Action<GlobalRarity>?     GradeClicked; // 목표 등급
    public event Action<EnchantAutoFocus>? FocusClicked; // 노릴 옵션
    public event Action<int>?              ComboClicked; // 조합 — '전체' 칸 수
    public event Action<int>?              CapClicked;   // 상한 — 0이면 보유 전부

    private int _slotCount;

    // 칩을 배선한다 — 큐브 창이 Bind를 부르기 전에 연결돼 있어야 해서 Awake (Unity 메시지)
    private void Awake()
    {
        RequireCount(gradeChips, 5, nameof(gradeChips));
        RequireCount(focusChips, 7, nameof(focusChips));
        RequireCount(comboChips, EnchantAutoTarget.MaxSlots + 1, nameof(comboChips));
        RequireCount(capChips, CapSteps.Length, nameof(capChips));

        for (int i = 0; i < gradeChips.Length; i++)
        {
            var grade = (GlobalRarity)((int)GlobalRarity.Uncommon + i);
            gradeChips[i].Clicked += _ => GradeClicked?.Invoke(grade);
        }

        for (int i = 0; i < focusChips.Length; i++)
        {
            var focus = (EnchantAutoFocus)i;
            focusChips[i].Clicked += _ => FocusClicked?.Invoke(focus);
        }

        for (int i = 0; i < comboChips.Length; i++)
        {
            int index = i;
            comboChips[i].Clicked += _ => ComboClicked?.Invoke(_slotCount - index);
        }

        for (int i = 0; i < capChips.Length; i++)
        {
            int cap = CapSteps[i];
            capChips[i].Clicked += _ => CapClicked?.Invoke(cap);
        }
    }

    // 네 줄을 그린다 (큐브 창이 자동 설정을 열 때 · 칩을 누를 때 · 장비·인벤토리가 바뀔 때).
    //   current   : 장비의 지금 인챈트 등급 — 목표는 그보다 높은 등급만 누른다(신화면 신화만, 칸 조건 필수)
    //   slotCount : 장비 등급이 정한 칸 수 — 조합 칩 수가 정해진다
    public void Bind(EnchantAutoTarget target, GlobalRarity current, int slotCount)
    {
        _slotCount = slotCount;

        bool atTop = current >= GlobalRarity.Mythic;

        for (int i = 0; i < gradeChips.Length; i++)
        {
            var  grade    = (GlobalRarity)((int)GlobalRarity.Uncommon + i);
            bool canPress = atTop ? grade == GlobalRarity.Mythic : grade > current;

            gradeChips[i].Bind(RarityLabel.Get(grade), target.Grade == grade, canPress);
        }

        for (int i = 0; i < focusChips.Length; i++)
        {
            var focus = (EnchantAutoFocus)i;

            // 이미 신화면 등급만으로는 목표가 이미 채워져 있다 — '없음'을 막는다
            focusChips[i].Bind(EnchantAutoTarget.GetFocusName(focus), target.Focus == focus, !(atTop && focus == EnchantAutoFocus.None));
        }

        for (int i = 0; i < comboChips.Length; i++)
        {
            if (i > slotCount)
            {
                comboChips[i].Hide();

                continue;
            }

            int allCount = slotCount - i;

            comboChips[i].Bind(EnchantAutoTarget.GetComboLabel(target.Focus, slotCount, allCount),
                               target.UsesSlots && target.IsComboOn(allCount),
                               target.UsesSlots);
        }

        for (int i = 0; i < capChips.Length; i++)
        {
            int cap = CapSteps[i];

            capChips[i].Bind(cap == 0 ? "보유 전부" : $"{cap:N0}개", target.Cap == cap, true);
        }
    }

    private void RequireCount(EnchantChipView[] chips, int count, string field)
    {
        if (chips.Length != count)
        {
            throw new InvalidOperationException($"{name}: '{field}'에 칩 {count}개를 넣을 것 (지금 {chips.Length}개).");
        }
    }
}

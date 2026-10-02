using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// 특성 표의 한 칸 — 종류 이름 · 'Lv 2/5' · 레벨 눈금. 눌리면 'Clicked'만 쏜다.
//
// 이 칸은 레벨 조건도, 포인트가 남았는지도 모른다 — 완성된 문구와 상태만 'Bind'로 받는다.
// (종속 View 규약은 'UI 규칙.md'의 "종속 View 쪽 규약")
//
// ■ 눈금은 최대 레벨만큼만 켠다
// 프리팹에 눈금을 넉넉히(10개) 깔아 두고 최대 레벨만큼 켜고, 지금 레벨만큼 채운다.
// 개척(최대 5)과 속도(최대 10)가 같은 칸 폭에서 "얼마나 찼나"로 읽힌다.
public class TraitNodeView : MonoBehaviour
{
    // 칸의 상태 셋. 색이 곧 이 값이다.
    public enum NodeState
    {
        // 최대 레벨까지 올렸다
        MaxLevel,

        // 다음 레벨의 계정 레벨 조건을 채웠다 (포인트 부족은 여기 포함 — 모든 칸에 똑같이 걸려 색으로 가르지 않는다)
        Available,

        // 다음 레벨의 계정 레벨이 모자란다
        Locked,
    }

    [CenterHeader("참조")]
    [SerializeField, Tooltip("종류 이름 (예: '개척' · 공통 줄은 '공통 산출량')")]
    private TMP_Text nameText = null!;

    [SerializeField, Tooltip("레벨 한 줄 (예: 'Lv 2/5')")]
    [FormerlySerializedAs("detailText")]
    private TMP_Text levelText = null!;

    [SerializeField, NonReorderable, Tooltip("레벨 눈금. 최대 레벨만큼 켜지고 지금 레벨만큼 채워진다 — 가장 큰 최대 레벨 이상 넣어 둔다")]
    private Image[] pips = new Image[0];

    [SerializeField, Tooltip("이 칸을 고르는 버튼(누르면 정보 영역에 펼쳐진다). OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button button = null!;

    [SerializeField, Tooltip("지금 고른 칸 표시(테두리). 평소 꺼져 있다")]
    private GameObject selectedMark = null!;

    [CenterHeader("색")]
    // ⚠️ 'Image.color'가 아니라 'ColorBlock'을 칠한다 — 'Selectable'이 실행 중 'Image.color'를
    //    덮어써서, 마우스가 스치기만 해도 색이 돌아간다(산업 버튼과 같은 함정).
    [SerializeField, Tooltip("최대 레벨인 칸")]
    [FormerlySerializedAs("learnedRole")]
    private UIThemeRole maxLevelRole = UIThemeRole.ButtonSelected;

    [SerializeField, Tooltip("다음 레벨을 올릴 수 있는 칸")]
    private UIThemeRole availableRole = UIThemeRole.Button;

    [SerializeField, Tooltip("다음 레벨의 계정 레벨이 모자란 칸")]
    private UIThemeRole lockedRole = UIThemeRole.ButtonDisabled;

    [SerializeField, Tooltip("채운 눈금 (지금 레벨까지)")]
    private UIThemeRole pipFilledRole = UIThemeRole.TextMain;

    [SerializeField, Tooltip("최대 레벨 칸(밝은 바탕)의 채운 눈금")]
    private UIThemeRole pipFilledOnMaxRole = UIThemeRole.TextDark;

    [SerializeField, Range(0f, 1f), Tooltip("빈 눈금의 어둡기 — 'Overlay'(검정)에 이 투명도를 준다")]
    private float pipEmptyAlpha = 0.35f;

    // 이 칸을 눌렀다 ('TraitPresenter'가 구독).
    public event Action<TraitNodeView>? Clicked;

    // 이 칸이 그리고 있는 특성. 미바인딩·빈 칸이면 0.
    public int UserTraitTid { get; private set; }

    // 자기 버튼만 배선한다 — 서비스를 조회하지 않으므로 Awake로 충분하고,
    // 그래야 패널의 Start가 Bind를 부르기 전에 이미 연결돼 있다 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(nameText,     nameof(nameText));
        this.RequireRef(levelText,    nameof(levelText));
        this.RequireRef(button,       nameof(button));
        this.RequireRef(selectedMark, nameof(selectedMark));

        button.onClick.AddListener(() => Clicked?.Invoke(this));
    }

    // 이 칸이 그릴 특성을 정한다 ('TraitPresenter'가 호출).
    //   userTraitTid : 레벨 올리기 요청에 그대로 실린다
    //   displayName  : 종류 이름
    //   level · max  : 'Lv 2/5'와 눈금
    //   state        : 색을 정한다
    //   isSelected   : 지금 정보 영역에 펼쳐진 칸인지
    public void Bind(int userTraitTid, string displayName, int level, int max, NodeState state, bool isSelected)
    {
        UserTraitTid   = userTraitTid;
        nameText.text  = displayName;
        levelText.text = $"Lv {level}/{max}";

        selectedMark.SetActive(isSelected);
        SetBackgroundVisible(true);

        button.interactable = true;

        ApplyState(state);
        ApplyPips(level, max, state == NodeState.MaxLevel);
    }

    // 줄 맞추기용 빈 칸으로 만든다 ('TraitPresenter'가 호출) — 자리만 차지하고 보이지도 눌리지도 않는다.
    //
    // ※ 칸을 빼지 않고 비우는 이유: 줄의 칸이 열(개척·속도·산출량) 자리를 지켜야 머리 줄과 맞는다.
    //   회색으로 두면 "잠긴 특성"으로 읽혀 바탕째 끈다.
    public void BindBlank()
    {
        Clear();
        SetBackgroundVisible(false);
        ApplyPips(0, 0, false);

        button.interactable = false;
    }

    // 칸을 비운다. 오브젝트는 살려 두고 재사용한다.
    public void Clear()
    {
        UserTraitTid   = 0;
        nameText.text  = "";
        levelText.text = "";

        selectedMark.SetActive(false);
    }

    // 칸 바탕(버튼 그래픽)을 켜고 끈다 (Bind · BindBlank에서 호출).
    private void SetBackgroundVisible(bool visible)
    {
        if (button.targetGraphic != null)
        {
            button.targetGraphic.enabled = visible;
        }
    }

    // 눈금을 최대 레벨만큼 켜고 지금 레벨만큼 채운다 (Bind · BindBlank에서 호출).
    //
    // ※ 눈금이 최대 레벨보다 적으면 앞쪽만 그린다 — 넘친 레벨이 안 보일 뿐 깨지지는 않는다.
    private void ApplyPips(int level, int max, bool onBrightBackground)
    {
        Color filled = UIThemePalette.Of(onBrightBackground ? pipFilledOnMaxRole : pipFilledRole);
        Color empty  = UIThemePalette.Of(UIThemeRole.Overlay);

        empty.a = pipEmptyAlpha;

        for (int i = 0; i < pips.Length; i++)
        {
            pips[i].gameObject.SetActive(i < max);
            pips[i].color = i < level ? filled : empty;
        }
    }

    // 상태에 맞춰 색을 칠한다 (Bind에서 호출).
    //
    // ⚠️ 네 상태를 같은 색으로 덮는다 — 기본값을 두면 **마우스를 올렸다는 이유로, 마지막에
    //    눌렀다는 이유로** 색이 바뀐다. 이 칸의 색은 "더 올릴 수 있는가" 하나만 말해야 한다.
    // ※ 잠긴 칸도 누를 수 있다 — 누르면 정보부터 펼쳐지므로, 무엇이 모자란지 읽을 수 있어야 한다.
    //   그래서 잠김 회색도 'disabledColor'가 아니라 네 상태 전부에 칠한다.
    private void ApplyState(NodeState state)
    {
        Color target = UIThemePalette.Of(state switch
        {
            NodeState.MaxLevel  => maxLevelRole,
            NodeState.Available => availableRole,
            _                   => lockedRole,
        });

        ColorBlock colors = button.colors;

        colors.normalColor      = target;
        colors.highlightedColor = target;
        colors.pressedColor     = target;
        colors.selectedColor    = target;
        colors.disabledColor    = target;

        button.colors = colors;
    }
}

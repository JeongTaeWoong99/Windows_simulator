using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// 특성 표의 한 칸. 특성 하나를 보여 주고 눌리면 'Clicked'만 쏜다.
//
// 이 칸은 레벨 조건도, 포인트가 남았는지도 모른다 — 완성된 문구와 상태만 'Bind'로 받는다.
// (종속 View 규약은 'UI 규칙.md'의 "종속 View 쪽 규약")
//
// ※ 예전엔 노드 사슬이라 위 노드와 잇는 선을 칸이 들고 있었다. 레벨형으로 바뀌며(2026-10-02 · T-116)
//   사슬이 없어져 선도 걷어냈다.
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
    [SerializeField, Tooltip("특성 이름 (예: '농사 속도')")]
    private TMP_Text nameText = null!;

    [SerializeField, Tooltip("레벨·효과 한 줄 (예: 'Lv 3/10 · +15%')")]
    private TMP_Text detailText = null!;

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

    // 이 칸을 눌렀다 ('TraitPresenter'가 구독).
    public event Action<TraitNodeView>? Clicked;

    // 이 칸이 그리고 있는 특성. 미바인딩·빈 칸이면 0.
    public int UserTraitTid { get; private set; }

    // 자기 버튼만 배선한다 — 서비스를 조회하지 않으므로 Awake로 충분하고,
    // 그래야 패널의 Start가 Bind를 부르기 전에 이미 연결돼 있다 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(nameText,     nameof(nameText));
        this.RequireRef(detailText,   nameof(detailText));
        this.RequireRef(button,       nameof(button));
        this.RequireRef(selectedMark, nameof(selectedMark));

        button.onClick.AddListener(() => Clicked?.Invoke(this));
    }

    // 이 칸이 그릴 특성을 정한다 ('TraitPresenter'가 호출).
    //   userTraitTid : 레벨 올리기 요청에 그대로 실린다
    //   displayName  : 이미 완성된 이름 문구
    //   detail       : 레벨·효과 한 줄
    //   state        : 색을 정한다
    //   isSelected   : 지금 정보 영역에 펼쳐진 칸인지
    public void Bind(int userTraitTid, string displayName, string detail, NodeState state, bool isSelected)
    {
        UserTraitTid    = userTraitTid;
        nameText.text   = displayName;
        detailText.text = detail;

        selectedMark.SetActive(isSelected);
        SetBackgroundVisible(true);

        button.interactable = true;

        ApplyState(state);
    }

    // 줄 맞추기용 빈 칸으로 만든다 ('TraitPresenter'가 호출) — 자리만 차지하고 보이지도 눌리지도 않는다.
    //
    // ※ 칸을 빼지 않고 비우는 이유: 격자가 형제 순서대로 채워져, 칸을 빼면 뒤 칸이 당겨져
    //   열(개척·속도·산출량)이 어긋난다. 회색으로 두면 "잠긴 특성"으로 읽혀 바탕째 끈다.
    public void BindBlank()
    {
        Clear();
        SetBackgroundVisible(false);

        button.interactable = false;
    }

    // 칸 바탕(버튼 그래픽)을 켜고 끈다 (Bind · BindBlank에서 호출).
    private void SetBackgroundVisible(bool visible)
    {
        if (button.targetGraphic != null)
        {
            button.targetGraphic.enabled = visible;
        }
    }

    // 칸을 비운다. 오브젝트는 살려 두고 재사용 풀로 되돌린다.
    public void Clear()
    {
        UserTraitTid    = 0;
        nameText.text   = "";
        detailText.text = "";

        selectedMark.SetActive(false);
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

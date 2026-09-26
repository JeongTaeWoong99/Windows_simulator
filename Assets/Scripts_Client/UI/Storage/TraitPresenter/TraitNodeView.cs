using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 특성 트리의 한 칸. 노드 하나를 보여 주고 눌리면 'Clicked'만 쏜다.
//
// 이 칸은 무엇이 열리는지도, 포인트가 남았는지도 모른다 — 완성된 문구와 상태만 'Bind'로 받는다.
// (종속 View 규약은 'UI 규칙.md'의 "종속 View 쪽 규약")
//
// ■ 위로 잇는 선은 이 칸이 들고 있다
// 트리의 선은 **같은 열의 바로 위 노드와만** 이어지므로, 칸마다 자기 위쪽 선 한 조각을
// 쥐고 있으면 그리는 코드가 따로 필요 없다. 각 열의 첫 줄만 선을 끄면 된다.
public class TraitNodeView : MonoBehaviour
{
    // 노드의 상태 셋. 색이 곧 이 값이다.
    public enum NodeState
    {
        // 이미 찍었다
        Learned,

        // 조건을 만족해 지금 찍을 수 있다
        Available,

        // 계정 레벨·선행이 모자라 못 찍는다
        Locked,
    }

    [CenterHeader("참조")]
    [SerializeField, Tooltip("노드 이름 (예: '농사 속도 10%')")]
    private TMP_Text nameText = null!;

    [SerializeField, Tooltip("조건·효과 한 줄 (예: 'Lv15 필요')")]
    private TMP_Text detailText = null!;

    [SerializeField, Tooltip("이 노드를 찍는 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button button = null!;

    [SerializeField, Tooltip("위 노드와 잇는 세로 선. 각 열의 첫 줄에서는 꺼진다")]
    private Image linkImage = null!;

    [CenterHeader("색")]
    // ⚠️ 'Image.color'가 아니라 'ColorBlock'을 칠한다 — 'Selectable'이 실행 중 'Image.color'를
    //    덮어써서, 마우스가 스치기만 해도 색이 돌아간다(산업 버튼과 같은 함정).
    [SerializeField, Tooltip("찍은 노드")]
    private UIThemeRole learnedRole = UIThemeRole.ButtonSelected;

    [SerializeField, Tooltip("지금 찍을 수 있는 노드")]
    private UIThemeRole availableRole = UIThemeRole.Button;

    [SerializeField, Tooltip("잠긴 노드. disabledColor 자리에 들어간다")]
    private UIThemeRole lockedRole = UIThemeRole.ButtonDisabled;

    // 이 노드를 눌렀다 ('TraitPresenter'가 구독).
    public event Action<TraitNodeView>? Clicked;

    // 이 칸이 그리고 있는 특성 노드. 미바인딩이면 0.
    public int UserTraitTid { get; private set; }

    // 자기 버튼만 배선한다 — 서비스를 조회하지 않으므로 Awake로 충분하고,
    // 그래야 패널의 Start가 Bind를 부르기 전에 이미 연결돼 있다 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(nameText,   nameof(nameText));
        this.RequireRef(detailText, nameof(detailText));
        this.RequireRef(button,     nameof(button));
        this.RequireRef(linkImage,  nameof(linkImage));

        button.onClick.AddListener(() => Clicked?.Invoke(this));
    }

    // 이 칸이 그릴 노드를 정한다 ('TraitPresenter'가 호출).
    //   userTraitTid : 찍기 요청에 그대로 실린다
    //   displayName  : 이미 완성된 이름 문구
    //   detail       : 조건·효과 한 줄
    //   state        : 색과 누를 수 있는지를 함께 정한다
    //   hasLink      : 위 노드와 선으로 이을지 (각 열의 첫 줄은 false)
    public void Bind(int userTraitTid, string displayName, string detail, NodeState state, bool hasLink)
    {
        UserTraitTid    = userTraitTid;
        nameText.text   = displayName;
        detailText.text = detail;

        linkImage.gameObject.SetActive(hasLink);

        ApplyState(state);
    }

    // 칸을 비운다. 오브젝트는 살려 두고 재사용 풀로 되돌린다 — 탭을 오갈 때마다 다시 쓴다.
    public void Clear()
    {
        UserTraitTid    = 0;
        nameText.text   = "";
        detailText.text = "";
    }

    // 상태에 맞춰 색과 누를 수 있는지를 칠한다 (Bind에서 호출).
    //
    // ⚠️ 네 상태를 같은 색으로 덮는다 — 기본값을 두면 **마우스를 올렸다는 이유로, 마지막에
    //    눌렀다는 이유로** 색이 바뀐다. 이 칸의 색은 "찍었는가" 하나만 말해야 한다.
    // ⚠️ 잠긴 색만은 'disabledColor'다 — 회색을 'normalColor'에 넣으면 잠근 순간 무시된다.
    private void ApplyState(NodeState state)
    {
        Color target = UIThemePalette.Of(state switch
        {
            NodeState.Learned => learnedRole,
            _                 => availableRole,
        });

        ColorBlock colors = button.colors;

        colors.normalColor      = target;
        colors.highlightedColor = target;
        colors.pressedColor     = target;
        colors.selectedColor    = target;
        colors.disabledColor    = UIThemePalette.Of(lockedRole);

        button.colors = colors;

        // 찍은 노드도 누를 수는 있게 둔다 — 서버가 'AlreadyUnlocked'로 돌려보내기 전에
        // 클라가 "이미 배웠습니다"로 막는다(누를 수 없는 칸과 색으로 구분되어야 하므로).
        button.interactable = state != NodeState.Locked;
    }
}

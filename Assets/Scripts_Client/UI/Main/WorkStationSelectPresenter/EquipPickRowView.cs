using System;
using GameData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 장비 고르기 목록의 한 줄 — 아이콘(능력치 칸 포함) · 이름 · 효과 한 줄 · 오른쪽 비교값. 바탕은 등급 색이다.
// 비교값은 '지금 낀 것보다 이 산업 속도가 얼마나 오르나'다(▲ +25% · ▼ −5%) — 같은 종류 여럿 중 고르는 근거를 한눈에(2026-11-13 사용자 A안).
// 마우스를 올리면 인벤토리 장비 칸과 같은 툴팁(기본 능력치 · 능력치 칸 전부)이 뜬다 — 같은 종류 여럿 중 무엇을 낄지 고르는 자리다.
//
// 눌리면 'PickClicked'만 쏜다 — 어느 캐릭터의 어느 칸에 끼우는지는 이 줄이 모른다.
// 무엇을 몇 줄 그릴지는 'WorkStationSelectPresenter'가 정한다.
// (종속 View 규약은 'UI 규칙.md'의 "종속 View 쪽 규약")
//
// ■ 읽기는 인벤토리 장비 탭과 같다
// 이름 · 효과 · 등급색을 인벤토리 칸('SlotView')과 같은 출처에서 받는다('EquipLabel' · 'RarityPalette') —
// 같은 장비가 두 화면에서 다르게 보이면 어느 쪽이 맞는지 알 수 없다.
//
// ■ 여기 오는 것은 **인벤토리에 있는 장비뿐이다**
// 누가 끼고 있는지 알릴 일이 없어 착용자 칸을 두지 않는다 — 끼운 것은 목록에서 아예 빠진다
// ('WorkStationSelectPresenter.RefreshEquipPicker').
//
// ■ 파괴하지 않고 풀로 되돌린다
// 칸을 오갈 때마다 목록이 통째로 바뀌므로, 남는 줄은 'Clear()' 후 꺼 둔다.
public class EquipPickRowView : MonoBehaviour
{
    [CenterHeader("참조")]
    // 줄 바탕을 장비의 등급 색으로 칠한다('SetRarity'). 인벤토리 칸과 같은 표('RarityPalette')를 쓴다.
    [SerializeField, Tooltip("줄 바탕 — 프리팹 루트의 Image. 등급 색으로 칠해진다")]
    private Image backgroundImage = null!;


    [SerializeField, Tooltip("왼쪽 아이콘 칸 (ItemIconView 프리팹) — 능력치 칸이 보인다")]
    private ItemIconView iconView = null!;

    [SerializeField, Tooltip("줄 루트의 툴팁 트리거. 내용은 코드가 넘긴다")]
    private TooltipTrigger tooltipTrigger = null!;

    [SerializeField, Tooltip("장비 이름")]
    private TMP_Text nameText = null!;

    [SerializeField, Tooltip("효과 한 줄 — '발굴 +30%' · '전산업 +5%'")]
    private TMP_Text effectText = null!;

    [SerializeField, Tooltip("(선택) 오른쪽 비교값 — 지금 낀 것 대비 이 산업 속도 차이 '▲ +25%'. 비우면 적지 않는다")]
    private TMP_Text? deltaText;

    [SerializeField, Tooltip("줄 전체를 덮는 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button pickButton = null!;

    // 이 줄을 눌렀다 ('WorkStationSelectPresenter'가 구독).
    public event Action<EquipPickRowView>? PickClicked;

    // 이 줄이 그리고 있는 장비 개체 번호. 미바인딩이면 0.
    public long EquipId { get; private set; }

    // 버튼 잠금의 두 사정 — 응답 대기('SetPickable') · 레벨 모자람('SetLevelLocked'). 둘 다 풀려야 눌린다.
    private bool _isPickable = true;
    private bool _isLevelLocked;

    private const float LockedAlpha = 0.45f;   // 레벨 모자람 줄의 흐림
    private CanvasGroup? _dimGroup;              // 처음 잠길 때 붙인다 — 프리팹을 고치지 않으려고

    // 자기 버튼만 배선한다 — 서비스를 조회하지 않으므로 Awake로 충분하고,
    // 그래야 패널이 Bind를 부르기 전에 이미 연결돼 있다 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(backgroundImage, nameof(backgroundImage));
        this.RequireRef(iconView,        nameof(iconView));
        this.RequireRef(tooltipTrigger,  nameof(tooltipTrigger));
        this.RequireRef(nameText,        nameof(nameText));
        this.RequireRef(effectText,      nameof(effectText));
        this.RequireRef(pickButton,      nameof(pickButton));

        RaritySkin.Dress(backgroundImage);

        pickButton.onClick.AddListener(() => PickClicked?.Invoke(this));
    }

    // 이 줄이 그릴 장비를 정한다 ('WorkStationSelectPresenter'가 호출).
    //   equipId     : 서버가 발급한 개체 번호. 장착 요청에 그대로 실린다
    //   displayName : 종류(TID)로 테이블에서 읽은 이름
    //   effect      : 'EquipLabel.GetEffectText'가 만든 한 줄
    public void Bind(long equipId, string displayName, string effect)
    {
        EquipId         = equipId;
        nameText.text   = displayName;
        effectText.text = effect;
    }

    // 지금 낀 것과 비교한 값을 적는다 ('WorkStationSelectPresenter'가 Bind 뒤에 호출).
    //   delta : 색까지 입힌 완성 문자열 — 무엇과 비교할지는 패널이 안다
    public void SetCompare(string delta)
    {
        if (deltaText != null)
        {
            deltaText.text = delta;
        }
    }

    // 줄 바탕을 이 장비의 등급 색으로 칠한다 ('WorkStationSelectPresenter'가 Bind 뒤에 호출).
    //   rarity : 종류(TID)로 읽은 등급. 개체 번호로 읽으면 'None'이 되어 회색으로 칠해진다
    public void SetRarity(GlobalRarity rarity)
    {
        RaritySkin.Set(backgroundImage, rarity);
    }

    // 아이콘과 툴팁을 정한다 ('WorkStationSelectPresenter'가 Bind 뒤에 호출).
    //   icon    : 'ItemIconContent.ForEquip'이 만든 완성값 — 능력치 칸까지
    //   tooltip : 줄에 마우스를 올리면 부를 툴팁 함수
    public void SetDetail(in ItemIconContent icon, Func<TooltipContent?> tooltip)
    {
        iconView.Bind(icon);
        tooltipTrigger.SetProvider(tooltip);
    }

    // 캐릭터 레벨이 모자라 낄 수 없는 줄인가 ('WorkStationSelectPresenter'가 Bind 뒤에 호출 · T-200 · 이슈 #146).
    // 참이면 대기와 상관없이 늘 잠기고 줄 전체가 흐려진다. 툴팁은 그대로 뜬다(알파만 낮추고 레이캐스트는 막지 않는다).
    // ※ 버튼의 꺼진 색만으로는 등급 바탕이 그대로라 잠긴 줄이 구별되지 않았다(2026-11-23 화면 확인) — 'CanvasGroup' 알파로 흐린다.
    public void SetLevelLocked(bool locked)
    {
        _isLevelLocked          = locked;
        pickButton.interactable = _isPickable && !locked;

        if (locked || _dimGroup != null)
        {
            // ※ '??'를 쓰지 않는다 — 에디터의 GetComponent는 없을 때 가짜 null을 돌려줘 '??'를 지나친다.
            if (_dimGroup == null && !TryGetComponent(out _dimGroup))
            {
                _dimGroup = gameObject.AddComponent<CanvasGroup>();
            }

            _dimGroup!.alpha = locked ? LockedAlpha : 1f;
        }
    }

    // 버튼을 잠그거나 푼다 — 응답을 기다리는 동안만 잠긴다. 레벨 잠금('SetLevelLocked')은 이 값과 따로 남는다.
    public void SetPickable(bool on)
    {
        _isPickable             = on;
        pickButton.interactable = on && !_isLevelLocked;
    }


    // 줄을 비운다. 오브젝트는 살려 두고 재사용 풀로 되돌린다.
    // 바탕색도 되돌린다 — 풀에서 다시 쓰일 때 이전 장비의 등급 색이 남지 않게.
    public void Clear()
    {
        EquipId               = 0;
        iconView.Clear();
        tooltipTrigger.SetProvider(null);
        nameText.text         = "";
        effectText.text       = "";
        SetCompare("");
        SetLevelLocked(false);
        RaritySkin.Set(backgroundImage, GlobalRarity.None);
    }
}

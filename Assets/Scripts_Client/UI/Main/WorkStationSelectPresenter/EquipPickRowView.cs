using System;
using GameData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 장비 고르기 목록의 한 줄 — 이름 · 효과 한 줄. 바탕은 등급 색이다.
//
// 눌리면 'PickClicked'만 쏜다 — 어느 캐릭터의 어느 칸에 끼우는지는 이 줄이 모른다.
// 무엇을 몇 줄 그릴지는 'WorkStationSelectPresenter'가 정한다.
// (종속 View 규약은 'UI 규칙.md'의 "종속 View 쪽 규약")
//
// ■ 읽기는 창고 장비 탭과 같다
// 이름 · 효과 · 등급색을 창고 칸('SlotView')과 같은 출처에서 받는다('EquipLabel' · 'RarityPalette') —
// 같은 장비가 두 화면에서 다르게 보이면 어느 쪽이 맞는지 알 수 없다.
//
// ■ 여기 오는 것은 **창고에 있는 장비뿐이다**
// 누가 끼고 있는지 알릴 일이 없어 착용자 칸을 두지 않는다 — 끼운 것은 목록에서 아예 빠진다
// ('WorkStationSelectPresenter.RefreshEquipPicker').
//
// ■ 파괴하지 않고 풀로 되돌린다
// 칸을 오갈 때마다 목록이 통째로 바뀌므로, 남는 줄은 'Clear()' 후 꺼 둔다.
public class EquipPickRowView : MonoBehaviour
{
    [CenterHeader("참조")]
    // 줄 바탕을 장비의 등급 색으로 칠한다('SetRarity'). 창고 칸과 같은 표('RarityPalette')를 쓴다.
    [SerializeField, Tooltip("줄 바탕 — 프리팹 루트의 Image. 등급 색으로 칠해진다")]
    private Image backgroundImage = null!;

    [SerializeField, Tooltip("장비 이름")]
    private TMP_Text nameText = null!;

    [SerializeField, Tooltip("효과 한 줄 — '낚시 +30%' · '전산업 +5%'")]
    private TMP_Text effectText = null!;

    [SerializeField, Tooltip("줄 전체를 덮는 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button pickButton = null!;

    // 이 줄을 눌렀다 ('WorkStationSelectPresenter'가 구독).
    public event Action<EquipPickRowView>? PickClicked;

    // 이 줄이 그리고 있는 장비 개체 번호. 미바인딩이면 0.
    public long EquipId { get; private set; }

    // 자기 버튼만 배선한다 — 서비스를 조회하지 않으므로 Awake로 충분하고,
    // 그래야 패널이 Bind를 부르기 전에 이미 연결돼 있다 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(backgroundImage, nameof(backgroundImage));
        this.RequireRef(nameText,        nameof(nameText));
        this.RequireRef(effectText,      nameof(effectText));
        this.RequireRef(pickButton,      nameof(pickButton));

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

    // 줄 바탕을 이 장비의 등급 색으로 칠한다 ('WorkStationSelectPresenter'가 Bind 뒤에 호출).
    //   rarity : 종류(TID)로 읽은 등급. 개체 번호로 읽으면 'None'이 되어 회색으로 칠해진다
    public void SetRarity(GlobalRarity rarity)
    {
        backgroundImage.color = RarityPalette.Get(rarity);
    }

    // 버튼을 잠그거나 푼다 — 응답을 기다리는 동안만 잠긴다.
    public void SetPickable(bool on)
    {
        pickButton.interactable = on;
    }

    // 줄을 비운다. 오브젝트는 살려 두고 재사용 풀로 되돌린다.
    // 바탕색도 되돌린다 — 풀에서 다시 쓰일 때 이전 장비의 등급 색이 남지 않게.
    public void Clear()
    {
        EquipId               = 0;
        nameText.text         = "";
        effectText.text       = "";
        backgroundImage.color = RarityPalette.Unknown;
    }
}

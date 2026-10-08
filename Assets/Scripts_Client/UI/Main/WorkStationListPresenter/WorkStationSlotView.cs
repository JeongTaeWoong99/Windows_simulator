using System.Collections.Generic;
using GameData;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 작업슬롯 한 칸의 표시. 프리팹에 붙는다.
//
// ■ 스스로 시간을 세지 않는다
//   Update·코루틴을 두지 않고 'WorkStationListPresenter'가 계산해 넘겨 준 값만 그린다.
//   상시 실행 앱이라 칸마다 루프를 돌리면 슬롯 수만큼 낭비가 곱해진다.
//
// ■ 표시는 값의 진실이 아니다
//   카운트다운은 연출이고 판정은 서버가 한다. 어긋나도 다음 스냅샷이 교정한다.
public class WorkStationSlotView : MonoBehaviour
{
    [CenterHeader("참조")]
    // 칸 바탕을 배치된 캐릭터의 등급 색으로 칠한다('SetRarity'). 인벤토리 칸('SlotView')과 같은 표('RarityPalette')를 쓴다.
    // 🎨 등급 이미지가 나오면 색 대신 여기에 스프라이트를 넣는다.
    [SerializeField, Tooltip("칸 바탕 — 프리팹 루트의 Image. 등급 색으로 칠해진다")]
    private Image backgroundImage = null!;

    [SerializeField, Tooltip("슬롯 번호·산업(+레벨)·캐릭터·속도")]
    private TMP_Text slotText = null!;

    [SerializeField, Tooltip("다음 수확까지 남은 시간")]
    private TMP_Text remainText = null!;

    [SerializeField, Tooltip("판정 진행도 (0~1). 표시 전용이라 interactable은 꺼 둔다")]
    private Slider progressSlider = null!;

    // ※ 남은 시간 줄('Remain Text')의 오른쪽에 붙는다 — 인벤토리 캐릭터 칸에서 LV 배지 줄 오른쪽에 붙는 것과 짝이다(T-104).
    //   여기는 칸이 넓어 'EquipIconsView'(등급 바탕 + 장비 아이콘)를 쓴다.
    [SerializeField, Tooltip("배치된 캐릭터가 낀 장비 4칸 — 무기·장신구1·장신구2·보석 (EquipIconsView 프리팹)")]
    private EquipPipsView equipPips = null!;

    [SerializeField, Tooltip("슬롯 무대 (Visible Panel). 비우면 연출 없이 바탕만 남는다")]
    private SlotStageView? stage;

    // 무대 안이 아니라 칸 루트에 붙는다 — 무대는 'RectMask2D'라 위로 오르는 아이콘이 잘린다.
    [SerializeField, Tooltip("아이템 획득 연출 (칸 루트의 ItemGainEffectView). 비우면 연출 없이 지나간다")]
    private ItemGainEffectView? gainEffect;

    private WorkStationSlotInfo? _slot;

    // 이 뷰가 그리고 있는 슬롯 번호. 미바인딩이면 -1.
    public int SlotIndex => _slot?.SlotIndex ?? -1;

    // 배치돼 있고 속도가 0이 아니어서 카운트다운을 돌릴 수 있는가.
    public bool IsRunning => _slot != null && _slot.CharacterId != 0 && _slot.CurrentWorkSpeed > 0;

    // 필수 참조 검증 — 서비스를 조회하지 않으므로 Awake로 충분하고,
    // 그래야 WorkStationListPresenter가 Bind를 부르기 전에 이미 검증돼 있다 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(backgroundImage, nameof(backgroundImage));
        this.RequireRef(slotText,       nameof(slotText));
        this.RequireRef(remainText,     nameof(remainText));
        this.RequireRef(progressSlider, nameof(progressSlider));
        this.RequireRef(equipPips,      nameof(equipPips));
    }

    // 슬롯 스냅샷을 반영한다 (WorkStationListPresenter가 호출).
    //   characterName : 배치된 캐릭터의 표시 이름. 뷰가 직접 조회하지 않는다 —
    //                   'slot.CharacterId'는 개체 번호라 테이블에서 이름이 안 나오고,
    //                   보유 목록을 거쳐야 한다. 그 변환은 세션을 아는 패널의 몫이다.
    //   characterTid  : 배치된 캐릭터의 종류(TID) — 무대가 그림을 고른다. 이름과 같은 이유로 패널이 넘긴다
    //   characterLevel : 배치된 캐릭터의 레벨 — 이름 바로 뒤에 붙인다. 0이면 적지 않는다(모르는 개체)
    public void Bind(WorkStationSlotInfo slot, string characterName, int characterTid, int characterLevel)
    {
        _slot = slot;

        if (stage != null)
        {
            stage.Show(slot.CharacterId != 0 ? characterTid : 0, slot.Industry, slot.IndustryLevel);
            stage.DrawIdle(); // 돌지 않는 칸은 이 장면에 멈춰 있다. 도는 칸은 다음 Tick이 덮는다
        }

        string industry  = IndustryLabel.Get(slot.Industry);
        string character = slot.CharacterId == 0 ? "-"
                         : characterLevel > 0     ? $"{characterName} Lv{characterLevel}"
                         : characterName;

        if (!IsRunning)
        {
            slotText.text          = $"슬롯 {slot.SlotIndex} · 대기";
            remainText.text        = "배치 없음";
            progressSlider.value   = 0f;

            return;
        }

        // 산업 레벨은 산업 바로 뒤에 붙인다 — '·'로 가르면 무엇의 레벨인지 모호해진다.
        // 레벨 이름('저수지')은 세팅 화면의 레벨 버튼에만 쓴다. 최대 6자('신성한 대지')라
        // 여기 적으면 한 줄이 길어지는데, 'Slot Text'는 오토사이징이라 그만큼 글자가 작아진다.
        // 레벨 0은 적지 않는다 — 서버 기본값이 1이라 올 일이 없고, 'Lv0'은 없는 값이다.
        string industryWithLevel = slot.IndustryLevel > 0 ? $"{industry} Lv{slot.IndustryLevel}" : industry;

        // 천분율 → 배율 ('Constants.WorkSpeedScale' = 1.0배). 최소 주기로 잘린 속도를 적고, 잘렸으면 '(최대)'를 붙인다 (T-102)
        float  speedMultiplier = WorkStationProgress.GetEffectiveSpeed(slot) / (float)Constants.WorkSpeedScale;
        string capMark         = WorkStationProgress.IsAtMinCycle(slot) ? "(최대)" : "";
        slotText.text = $"슬롯 {slot.SlotIndex} · {industryWithLevel} · {character} · {speedMultiplier:0.00}배{capMark}";
    }

    // 칸 바탕을 배치된 캐릭터의 등급 색으로 칠한다 (WorkStationListPresenter가 Bind 뒤에 호출).
    //   rarity : 종류(TID)로 읽은 등급. 모르는 개체면 'None' → 회색('Unknown')
    public void SetRarity(GlobalRarity rarity)
    {
        backgroundImage.color = RarityPalette.Get(rarity);
    }

    // 배치된 캐릭터의 장착 네모를 그린다 (WorkStationListPresenter가 Bind 뒤에 호출).
    //   grades : 'EquipLabel.WornSlots' 순서의 등급 — 'None'은 빈 칸
    //   icons  : 같은 순서의 장비 아이콘 — 빈 칸·그림 없음은 null
    //   tooltip : 장비 줄에 올리면 띄울 내용 — 칸마다 낀 장비 이름·등급
    public void SetEquipPips(IReadOnlyList<GlobalRarity> grades, IReadOnlyList<Sprite?> icons, System.Func<TooltipContent?> tooltip)
    {
        equipPips.Bind(grades, icons);
        equipPips.SetTooltip(tooltip);
    }

    // 이 칸에서 얻은 아이템을 띄운다 — 쓰러지는 대상 자리에서, 무대 그림이 없으면 무대 가운데에서
    // (WorkStationListPresenter의 GatherResultReceived 구독에서 호출).
    // 땅이 흐르면 대상과 함께 흘러간다 — 화면에 박혀 있으면 캐릭터를 따라오는 것처럼 보인다.
    public void PlayGain(IReadOnlyList<Sprite?> icons)
    {
        if (gainEffect == null)
        {
            return;
        }

        if (stage == null)
        {
            gainEffect.Play(transform.position, icons);

            return;
        }

        SlotStageView stageView = stage;
        Vector3       point     = stageView.TryGetHarvestPoint(out Vector3 harvest) ? harvest : stageView.transform.position;
        double        ground    = stageView.GroundDistance;

        gainEffect.Play(point, icons, () => stageView.FollowGround(point, ground));
    }

    // 진행도와 남은 시간을 갱신한다 (WorkStationListPresenter의 Update가 매 프레임 호출).
    //   cycleSeconds : 판정 1회의 초 — 무대가 진행도를 주기 안의 순간으로 바꾼다
    public void Tick(float progress, float remainSeconds, float cycleSeconds)
    {
        progressSlider.value = progress;
        remainText.text      = $"{remainSeconds:0.0}초 후 수확";

        if (stage != null)
        {
            stage.Tick(progress * cycleSeconds, cycleSeconds, Time.deltaTime);
        }
    }
}

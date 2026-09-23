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
    // 칸 바탕을 배치된 캐릭터의 등급 색으로 칠한다('SetRarity'). 창고 칸('SlotView')과 같은 표('RarityPalette')를 쓴다.
    // 🎨 등급 이미지가 나오면 색 대신 여기에 스프라이트를 넣는다.
    [SerializeField, Tooltip("칸 바탕 — 프리팹 루트의 Image. 등급 색으로 칠해진다")]
    private Image backgroundImage = null!;

    [SerializeField, Tooltip("슬롯 번호·산업(+레벨)·캐릭터·속도")]
    private TMP_Text slotText = null!;

    [SerializeField, Tooltip("다음 수확까지 남은 시간")]
    private TMP_Text remainText = null!;

    [SerializeField, Tooltip("판정 진행도 (0~1). 표시 전용이라 interactable은 꺼 둔다")]
    private Slider progressSlider = null!;

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
    }

    // 슬롯 스냅샷을 반영한다 (WorkStationListPresenter가 호출).
    //   characterName : 배치된 캐릭터의 표시 이름. 뷰가 직접 조회하지 않는다 —
    //                   'slot.CharacterId'는 개체 번호라 테이블에서 이름이 안 나오고,
    //                   보유 목록을 거쳐야 한다. 그 변환은 세션을 아는 패널의 몫이다.
    public void Bind(WorkStationSlotInfo slot, string characterName)
    {
        _slot = slot;

        string industry  = IndustryLabel.Get(slot.Industry);
        string character = slot.CharacterId != 0 ? characterName : "-";

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

        // 천분율 → 배율 (1000 = 1.0배)
        float speedMultiplier = slot.CurrentWorkSpeed / 1000f;
        slotText.text = $"슬롯 {slot.SlotIndex} · {industryWithLevel} · {character} · {speedMultiplier:0.00}배";
    }

    // 칸 바탕을 배치된 캐릭터의 등급 색으로 칠한다 (WorkStationListPresenter가 Bind 뒤에 호출).
    //   rarity : 종류(TID)로 읽은 등급. 모르는 개체면 'None' → 회색('Unknown')
    public void SetRarity(GlobalRarity rarity)
    {
        backgroundImage.color = RarityPalette.Get(rarity);
    }

    // 진행도와 남은 시간을 갱신한다 (WorkStationListPresenter의 Update가 매 프레임 호출).
    public void Tick(float progress, float remainSeconds)
    {
        progressSlider.value = progress;
        remainText.text      = $"{remainSeconds:0.0}초 후 수확";
    }
}

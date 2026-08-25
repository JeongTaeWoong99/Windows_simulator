using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 상주 위젯 스트립의 한 칸. 프리팹('WidgetMiniSlotView.prefab')에 붙는다.
//
// ■ 큰 창의 'WorkStationSlotView'와 다른 점
//   글자를 쓰지 않는다. 위젯 높이는 87px뿐이라 슬롯 번호·산업·남은 초를 넣을 자리가 없다.
//   여기서 도는 것은 게이지 하나이고, 나머지는 캐릭터 그림이 채운다.
//
// ■ 스스로 시간을 세지 않는다
//   Update·코루틴을 두지 않고 'WidgetPresenter'가 계산해 넘겨 준 값만 그린다.
//   상시 실행 앱이라 칸마다 루프를 돌리면 슬롯 수만큼 낭비가 곱해진다.
//
// ⏸ 아직 자리만 잡아 둔 것 — 캐릭터 스프라이트와 수확 표시는 에셋이 나온 뒤에 붙인다.
public class WidgetMiniSlotView : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("캐릭터 그림 자리. 스프라이트는 아직 비어 있고 나중에 교체한다")]
    private Image characterImage = null!;

    [SerializeField, Tooltip("수확 표시가 떠오를 자리. 지금은 빈 문자열로 둔다")]
    private TMP_Text harvestText = null!;

    [SerializeField, Tooltip("판정 진행도 (0~1). 표시 전용이라 interactable은 꺼 둔다")]
    private Slider progressSlider = null!;

    private WorkStationSlotInfo? _slot;

    // 배치돼 있고 속도가 0이 아니어서 카운트다운을 돌릴 수 있는가.
    public bool IsRunning => _slot != null && WorkStationProgress.IsRunning(_slot);

    // 필수 참조 검증 — 서비스를 조회하지 않으므로 Awake로 충분하고,
    // 그래야 WidgetPresenter가 Bind를 부르기 전에 이미 검증돼 있다 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(characterImage, nameof(characterImage));
        this.RequireRef(harvestText,    nameof(harvestText));
        this.RequireRef(progressSlider, nameof(progressSlider));

        harvestText.text = string.Empty;
    }

    // 슬롯 스냅샷을 반영한다 (WidgetPresenter가 호출).
    //
    // ⏸ 캐릭터 그림 교체 지점 — 'slot.CharacterId'로 캐릭터 테이블을 조회해
    //    'characterImage.sprite'를 갈아 끼우는 자리다. 에셋이 없어 지금은 자리만 잡는다.
    public void Bind(WorkStationSlotInfo slot)
    {
        _slot = slot;

        progressSlider.value = WorkStationProgress.CalculateProgress(slot);
    }

    // 진행도를 갱신한다 (WidgetPresenter의 Update가 매 프레임 호출).
    public void Tick(float progress)
    {
        progressSlider.value = progress;
    }

    // 이 칸에서 수확이 났다 (WidgetPresenter의 GatherResultReceived 구독에서 호출).
    //
    // ⏸ 수확 표시가 떠오르는 연출이 붙을 자리다. 지금은 게이지만 처음으로 되돌린다 —
    //    다음 슬롯 동기화가 오기 전까지 이전 사이클의 진행도를 그리고 있지 않도록.
    public void MarkHarvested()
    {
        progressSlider.value = 0f;
    }
}

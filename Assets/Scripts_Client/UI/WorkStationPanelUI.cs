using System;
using System.Collections.Generic;
using MikaProtocol;
using UnityEngine;

/// <summary>
/// 작업슬롯 목록 화면. 서버 스냅샷만큼 <see cref="WorkStationSlotView"/>를 만들고,
/// 카운트다운을 <b>여기 한 곳에서</b> 계산해 각 뷰에 넘긴다.
///
/// ■ 두 축을 섞지 않는다
///   - <b>데이터 갱신</b>은 이벤트(옵저버) — SessionManager.WorkStationSlotsChanged
///   - <b>시간 진행</b>은 이 클래스의 Update 하나
///   슬롯마다 Update를 두면 상시 실행 앱에서 비용이 슬롯 수만큼 곱해진다.
///
/// ■ 서버는 주기(초)를 보내지 않는다
///   슬롯마다 속도가 다르고 버프로 바뀌기 때문이다. 대신 진행도·속도·1회 비용을 주므로
///   클라가 직접 계산한다. 나중에 주기 규칙이 바뀌어도 이 계산은 그대로다.
/// </summary>
public class WorkStationPanelUI : MonoBehaviour
{
    // 작업량 단위는 "밀리초 × 천분율 속도"다. 1초 × 1.0배 = 1000ms × 1000 = 1,000,000 단위.
    // 남은 시간을 초로 되돌릴 때 이 값으로 나눈다.
    private const float UnitsPerSecondAtBaseSpeed = 1000f;

    [CenterHeader("< 참조 >")]
    [SerializeField, Tooltip("슬롯 한 칸 프리팹 (WorkStationSlotView 포함). 빈 프레임 안에 생성된다")]
    private WorkStationSlotView slotPrefab = null!;

    [SerializeField, Tooltip("칸 프레임(Slot)들이 들어 있는 부모 — Slot Scroll View > Viewport > Content")]
    private Transform slotParent = null!;

    // 슬롯 번호 → 뷰. 스냅샷이 다시 와도 같은 칸을 재사용해 깜빡임을 막는다.
    private readonly Dictionary<int, WorkStationSlotView> _views = new Dictionary<int, WorkStationSlotView>();

    private SessionManager _session = null!;
    private bool           _isSubscribed;

    // 서비스 확보 후 최초 구독 (Unity 메시지)
    // ※ OnEnable에서 Get 하지 않는다 — MonoService 주석의 초기화 순서 규칙 참조.
    private void Start()
    {
        // 필수 참조 검증 — 미연결이면 여기서 멈춘다(WindowPanelUI와 같은 규칙).
        this.RequireRef(slotPrefab, nameof(slotPrefab));
        this.RequireRef(slotParent, nameof(slotParent));

        _session = Services.Get<SessionManager>();
        Subscribe();
        Rebuild(); // 이미 스냅샷을 받은 뒤에 켜졌을 수 있다
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    private void OnEnable()
    {
        if (_session != null)
            Subscribe();
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
    }

    // 카운트다운 진행 — 슬롯 전체를 여기서 한 번에 계산한다 (Unity 메시지)
    private void Update()
    {
        if (_session == null)
            return;

        foreach (var slot in _session.WorkStationSlots)
        {
            if (!_views.TryGetValue(slot.SlotIndex, out var view) || !view.IsRunning)
                continue;

            view.Tick(CalculateProgress(slot), CalculateRemainSeconds(slot));
        }
    }

    #region 구독

    // 슬롯 스냅샷 변경 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
            return;

        _isSubscribed                    = true;
        _session.WorkStationSlotsChanged += Rebuild;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
            return;

        _isSubscribed                    = false;
        _session.WorkStationSlotsChanged -= Rebuild;
    }

    #endregion

    #region 목록 구성

    /// <summary>
    /// 스냅샷대로 슬롯 뷰를 만들고 갱신한다 (WorkStationSlotsChanged 구독).
    /// 슬롯 번호가 곧 프레임 순서다 — 슬롯 0은 <c>Content</c>의 첫 자식 프레임 안에 들어간다.
    /// 인벤토리와 달리 번호가 고정이라 "빈 프레임 찾기"가 아니라 자리를 직접 고른다.
    /// </summary>
    private void Rebuild()
    {
        foreach (var slot in _session.WorkStationSlots)
        {
            if (!_views.TryGetValue(slot.SlotIndex, out var view))
            {
                if (slot.SlotIndex < 0 || slot.SlotIndex >= slotParent.childCount)
                {
                    ClientLog.Warn(ClientLog.UI, $"슬롯 {slot.SlotIndex}에 해당하는 칸 프레임이 없다. 프레임을 늘려야 한다.", this);
                    continue;
                }

                Transform frame = slotParent.GetChild(slot.SlotIndex);

                view      = Instantiate(slotPrefab, frame);
                view.name = $"WorkStationSlot {slot.SlotIndex}";
                SnapToFrame(view.transform as RectTransform);

                _views.Add(slot.SlotIndex, view);
            }

            view.Bind(slot, _session.GetCharacterName(slot.CharacterId));
        }
    }

    /// <summary>
    /// 프리팹을 프레임 안에 안착시킨다 — 위치를 0으로 맞춰 프레임 정중앙에 놓는다.
    /// Instantiate 직후의 RectTransform은 프리팹에 저장된 좌표를 그대로 들고 온다.
    /// </summary>
    private static void SnapToFrame(RectTransform? rect)
    {
        if (rect == null)
            return;

        rect.anchoredPosition3D = Vector3.zero;
        rect.localScale         = Vector3.one;
        rect.localRotation      = Quaternion.identity;
    }

    #endregion

    #region 카운트다운 계산 (서버 식 그대로)

    /// <summary>
    /// 마지막 정산 이후 쌓인 작업량 중 <b>이번 판정에 해당하는 몫</b>을 구한다.
    /// 판정 1회 비용으로 나눈 나머지라, 여러 판정이 밀려 있어도 현재 사이클만 남는다.
    /// </summary>
    private static long GetPendingUnits(WorkStationSlotInfo slot)
    {
        double elapsedMs   = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - slot.LastTickAtUnix * 1000L);
        double accumulated = slot.ProgressUnits + elapsedMs * slot.CurrentWorkSpeed;

        return (long)(accumulated % slot.JudgeCostUnits);
    }

    // 판정 진행도 0~1 (Update에서 호출)
    private static float CalculateProgress(WorkStationSlotInfo slot)
    {
        return Mathf.Clamp01((float)GetPendingUnits(slot) / slot.JudgeCostUnits);
    }

    // 다음 수확까지 남은 초 (Update에서 호출)
    private static float CalculateRemainSeconds(WorkStationSlotInfo slot)
    {
        long remainUnits = slot.JudgeCostUnits - GetPendingUnits(slot);

        // IsRunning이 CurrentWorkSpeed > 0을 보장하지만, 계산식만 떼어 봐도 안전하도록 가드를 남긴다.
        if (slot.CurrentWorkSpeed <= 0)
            return 0f;

        return remainUnits / (float)slot.CurrentWorkSpeed / UnitsPerSecondAtBaseSpeed;
    }

    #endregion
}

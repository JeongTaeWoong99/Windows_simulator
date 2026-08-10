using System;
using System.Collections.Generic;
using MikaProtocol;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 작업슬롯 목록 패널. 서버 스냅샷만큼 <see cref="WorkStationSlotView"/>를 만들고,
/// 카운트다운을 <b>여기 한 곳에서</b> 계산해 각 뷰에 넘긴다.
///
/// <para>
/// ■ 두 축을 섞지 않는다<br/>
/// <b>데이터 갱신</b>은 이벤트(옵저버) — <c>PlayerDataModel.WorkStationSlotsChanged</c>.
/// <b>시간 진행</b>은 이 클래스의 <see cref="Update"/> 하나.
/// 슬롯마다 Update를 두면 상시 실행 앱에서 비용이 슬롯 수만큼 곱해진다.
/// </para>
///
/// <para>
/// ■ 서버는 주기(초)를 보내지 않는다<br/>
/// 슬롯마다 속도가 다르고 버프로 바뀌기 때문이다. 대신 진행도·속도·1회 비용을 주므로
/// 클라가 직접 계산한다. 나중에 주기 규칙이 바뀌어도 이 계산은 그대로다.
/// </para>
///
/// <para>
/// ■ 칸을 누르면 선택 화면에 자리를 넘긴다<br/>
/// <code>
/// selectPresenter.Open(slotIndex)                    번호를 먼저 넣는다
/// ui.ShowMainScreen(MainScreen.WorkStationSelect)    자리를 넘긴다 (이 패널은 여기서 꺼진다)
/// </code>
/// <b>여기가 선택 화면을 참조하는 유일한 방향이다.</b> 반대로 선택 화면이 이 목록을 구독하면
/// 안 된다 — 선택 화면은 평소 꺼져 있어서 클릭 신호를 못 받는다. <b>살아 있는 쪽이 넘긴다.</b>
/// </para>
///
/// <para>
/// ※ 예전엔 <c>SlotClicked</c> 이벤트만 쏘고 상위 <c>WorkStationPresenter</c>가 받아서 갈아 끼웠다.
/// 세 화면(목록·선택·설정)이 <c>#Main Canvas</c>의 형제로 눕혀지면서 그 층이 사라졌다 —
/// 전환은 <c>UIManager</c> 한 곳이 한다.
/// </para>
/// </summary>
public class WorkStationListPresenter : MonoBehaviour
{
    // 작업량 단위는 "밀리초 × 천분율 속도"다. 1초 × 1.0배 = 1000ms × 1000 = 1,000,000 단위.
    // 남은 시간을 초로 되돌릴 때 이 값으로 나눈다.
    private const float UnitsPerSecondAtBaseSpeed = 1000f;

    [CenterHeader("참조")]
    [SerializeField, Tooltip("슬롯 한 칸 프리팹 (WorkStationSlotView 포함). 빈 프레임 안에 생성된다")]
    private WorkStationSlotView slotPrefab = null!;

    [SerializeField, Tooltip("칸 프레임(Slot)들이 들어 있는 부모 — Viewport > Content")]
    private Transform slotParent = null!;

    [SerializeField, Tooltip("칸을 누르면 갈아 끼울 화면 — WorkStation Select Presenter")]
    private WorkStationSelectPresenter selectPresenter = null!;

    // 슬롯 번호 → 뷰. 스냅샷이 다시 와도 같은 칸을 재사용해 깜빡임을 막는다.
    private readonly Dictionary<int, WorkStationSlotView> _views = new Dictionary<int, WorkStationSlotView>();

    private PlayerDataModel _data = null!;
    private UIManager       _ui   = null!;
    private bool            _isSubscribed;
    private bool            _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 초기화 순서로 진행한다 (클라 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다(MonoService 주석).
    private void Start()
    {
        this.RequireRef(slotPrefab,      nameof(slotPrefab));
        this.RequireRef(slotParent,      nameof(slotParent));
        this.RequireRef(selectPresenter, nameof(selectPresenter));

        _data = Services.Get<PlayerDataModel>();
        _ui   = Services.Get<UIManager>();

        Subscribe();
        BindFrameButtons();
        Rebuild(); // 이미 스냅샷을 받은 뒤에 켜졌을 수 있다

        _isReady = true;
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    //
    // ★ 재구독만으로는 부족하다 — 닫혀 있는 동안 도착한 스냅샷을 놓쳤기 때문이다.
    //   캐시(PlayerDataModel)는 계속 살아 있으므로 다시 그리기만 하면 즉시 맞는다.
    private void OnEnable()
    {
        if (!_isReady)
            return;

        Subscribe();
        Rebuild();
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
    }

    // 카운트다운 진행 — 슬롯 전체를 여기서 한 번에 계산한다 (Unity 메시지)
    private void Update()
    {
        if (!_isReady)
            return;

        foreach (var slot in _data.WorkStationSlots)
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

        _isSubscribed                 = true;
        _data.WorkStationSlotsChanged += Rebuild;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
            return;

        _isSubscribed                 = false;
        _data.WorkStationSlotsChanged -= Rebuild;
    }

    #endregion

    #region 칸 클릭

    /// <summary>
    /// 칸 프레임의 버튼을 슬롯 번호와 묶는다 (Start에서 한 번).
    ///
    /// <para>
    /// <b>버튼은 프레임에 붙어 있어야 한다 — 안에 생기는 뷰가 아니라.</b>
    /// 비어 있는 슬롯에는 뷰가 만들어지지 않는데, 빈 칸이야말로 눌러서 배치할 대상이다.
    /// </para>
    /// </summary>
    private void BindFrameButtons()
    {
        for (int i = 0; i < slotParent.childCount; i++)
        {
            var button = slotParent.GetChild(i).GetComponent<Button>();
            if (button == null)
            {
                ClientLogger.Warn(ClientLogger.UI,
                    $"칸 프레임 {slotParent.GetChild(i).name}에 Button이 없어 클릭을 받을 수 없다.", this);
                continue;
            }

            // 반복 변수를 그대로 넘기면 모든 콜백이 마지막 값을 본다. 복사본을 캡처한다.
            int slotIndex = i;
            button.onClick.AddListener(() => OpenSelect(slotIndex));
        }
    }

    /// <summary>
    /// 그 칸의 배치/해제 화면으로 갈아 끼운다 (칸 프레임 OnClick에 코드로 연결).
    /// <b>배치 여부와 상관없이 열린다</b> — 빈 칸이면 배치, 찬 칸이면 해제가 뜬다.
    ///
    /// <para>
    /// ⚠️ <b>번호를 먼저 넣고 화면을 넘긴다.</b> 꺼져 있던 화면은 <c>Start()</c>가 아직 안 돌았을 수
    /// 있어, 켠 뒤에 번호를 넣으면 초기화가 덮어쓴다.
    /// </para>
    /// </summary>
    private void OpenSelect(int slotIndex)
    {
        selectPresenter.Open(slotIndex);
        _ui.ShowMainScreen(MainScreen.WorkStationSelect);
    }

    #endregion

    #region 목록 구성

    /// <summary>
    /// 스냅샷대로 슬롯 뷰를 만들고 갱신한다 (WorkStationSlotsChanged 구독).
    /// 슬롯 번호가 곧 프레임 순서다 — 슬롯 0은 <c>Content</c>의 첫 자식 프레임 안에 들어간다.
    /// 인벤토리와 달리 번호가 고정이라 "빈 프레임 찾기"가 아니라 자리를 직접 고른다.
    ///
    /// <para>
    /// ■ 배치된 칸에만 뷰를 둔다<br/>
    /// 배치가 풀리면 뷰를 <b>지운다.</b> 남겨 두고 "대기"라고 적으면 빈 칸과 구분이 안 되고,
    /// 무엇보다 뷰가 프레임 위를 덮어 <b>칸을 눌러 배치 화면으로 들어가는 길을 막는다.</b>
    /// </para>
    /// </summary>
    private void Rebuild()
    {
        foreach (var slot in _data.WorkStationSlots)
        {
            // 비어 있는 칸은 프레임만 남긴다 — 그래야 눌러서 배치할 수 있다
            if (!IsAssigned(slot))
            {
                RemoveView(slot.SlotIndex);
                continue;
            }

            if (!_views.TryGetValue(slot.SlotIndex, out var view))
            {
                if (slot.SlotIndex < 0 || slot.SlotIndex >= slotParent.childCount)
                {
                    ClientLogger.Warn(ClientLogger.UI, $"슬롯 {slot.SlotIndex}에 해당하는 칸 프레임이 없다. 프레임을 늘려야 한다.", this);
                    continue;
                }

                Transform frame = slotParent.GetChild(slot.SlotIndex);

                view      = Instantiate(slotPrefab, frame);
                view.name = $"WorkStationSlot {slot.SlotIndex}";
                SnapToFrame(view.transform as RectTransform);

                _views.Add(slot.SlotIndex, view);
            }

            view.Bind(slot, _data.GetCharacterName(slot.CharacterId));
        }
    }

    /// <summary>배치가 풀린 칸의 뷰를 지운다 (<see cref="Rebuild"/>에서 호출).</summary>
    private void RemoveView(int slotIndex)
    {
        if (!_views.TryGetValue(slotIndex, out var view))
            return;

        _views.Remove(slotIndex); // Update가 죽은 뷰를 만지지 않도록 먼저 뺀다

        if (view != null)
            Destroy(view.gameObject);
    }

    /// <summary>
    /// 칸이 배치 상태인가 — 산업과 캐릭터가 둘 다 차 있어야 배치다.
    /// <c>IsRunning</c>과 다르다. 그쪽은 속도까지 봐서 "카운트다운을 돌릴 수 있는가"를 뜻한다.
    /// </summary>
    private static bool IsAssigned(WorkStationSlotInfo slot)
        => slot.Industry != EIndustryType.None && slot.CharacterId != 0;

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
        double elapsedMs   = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - slot.LastTickAtUnixMs);
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

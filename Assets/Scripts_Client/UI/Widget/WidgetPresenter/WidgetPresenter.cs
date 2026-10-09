using System.Collections.Generic;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 상주 위젯의 내용 — 상단 한 줄(요약)과 아래 스트립(배치된 칸들), 그리고 3열을 여는 버튼.
//
// ★ 위젯 말고는 바탕화면에 아무것도 안 떠 있으므로, 이 버튼이 3열을 여는 유일한 입구다.
// 열려 있으면 위젯만 남기고 전부 접는다 → 'UIManager.ToggleAll'
//
// ■ 두 축을 섞지 않는다
// 데이터 갱신은 이벤트(옵저버) — 'CurrencyChanged' · 'WorkStationSlotsChanged' · 'GatherResultReceived' · 'GatherValueEarned'.
// 시간 진행은 이 클래스의 'Update' 하나. 칸마다 Update를 두면 상시 실행 앱에서
// 비용이 슬롯 수만큼 곱해진다 ('WorkStationListPresenter'와 같은 판단).
// 측정 시간·시간당 골드도 같은 Update에서 초가 바뀔 때만 다시 쓴다.
//
// ■ 상단 줄은 아이콘 + 숫자 (2026-10-09 사용자 요청)
// 보유 · 가동 · 누적 · 시간당 · 측정 시간. 87px 줄에 글자 라벨을 붙이면 숫자가 밀린다.
// 누적·시간당은 즉시 판매가 환산 추정치다 → 'WidgetEarningTracker'.
//
// ■ 스트립은 배치된 칸만, 왼쪽부터
// 큰 창의 목록은 빈 칸도 프레임으로 남긴다 — 눌러서 배치해야 하기 때문이다.
// 위젯은 누를 것이 없으므로 빈 칸을 두지 않는다. 해제되면 뷰를 파괴하고
// 레이아웃이 뒤를 당겨 채운다.
//
// ⚠️ 연출을 여기서 돌리지 않는다. 배경·캐릭터 모션은 큰 창 전용이다.
// 상시 실행 앱에서 리소스는 기능이 아니라 생존 조건이고, 급격한 애니메이션은
// P1(주의를 뺏지 않는다)을 정면으로 어긴다.
// → GameDesign/design/ui/README.md 2.1
// ※ 예외 하나 — 수확 때 머리 자리에서 아이템 아이콘이 작게 떠올라 사라진다(2026-10-08 사용자 요청).
//   수확 때만 1초 남짓 돌고 끝나는 트윈이라 상시 루프가 아니다('ItemGainEffectView').
// ※ 예외 둘 — 머리가 일하는 동안 2px 통통 튀고 수확 때 펄쩍 뛴다(2026-10-08 사용자 선택 · 'WidgetHeadMotion').
//   정수 px로 끊고 칸마다 위상을 달리해 스트립 전체가 출렁이지 않게 한다.
public class WidgetPresenter : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("열기/닫기 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button toggleButton = null!;

    [SerializeField, Tooltip("수익 집계 초기화 버튼 — 누적·측정 시간을 0으로. OnClick은 코드가 연결한다")]
    private Button resetButton = null!;

    [CenterHeader("상단 줄")]
    [SerializeField, Tooltip("골드 보유량")]
    private TMP_Text goldText = null!;

    [SerializeField, Tooltip("가동 슬롯 — 돌고 있는 칸 / 전체 칸")]
    private TMP_Text activeSlotText = null!;

    [SerializeField, Tooltip("누적 골드 — 마지막 초기화 이후 수확의 즉시 판매가 합")]
    private TMP_Text totalText = null!;

    [SerializeField, Tooltip("시간당 골드 — 누적 ÷ 측정 시간. 1분 전에는 '—'")]
    private TMP_Text perHourText = null!;

    [SerializeField, Tooltip("측정 시간 — 마지막 초기화 이후 경과")]
    private TMP_Text elapsedText = null!;

    [CenterHeader("스트립")]
    [SerializeField, Tooltip("미니 슬롯 프리팹 (WidgetMiniSlotView 포함)")]
    private WidgetMiniSlotView miniSlotPrefab = null!;

    [SerializeField, Tooltip("미니 슬롯이 들어갈 부모 — Strip Panel. 왼쪽 정렬 레이아웃이 붙어 있어야 한다")]
    private Transform stripParent = null!;

    // 슬롯 번호 → 칸. 스냅샷이 다시 와도 같은 칸을 재사용해 깜빡임을 막는다.
    private readonly Dictionary<int, WidgetMiniSlotView> _views = new Dictionary<int, WidgetMiniSlotView>();

    private PlayerDataModel _data = null!;
    private bool            _isSubscribed;
    private bool            _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    private readonly WidgetEarningTracker _earnings = new WidgetEarningTracker();
    private long                 _shownSecond = -1; // 마지막으로 그린 측정 시간(초) — 초가 바뀔 때만 다시 쓴다

    // 획득 연출에 넘길 아이템 — 수확마다 새로 만들지 않고 비워 다시 쓴다
    private readonly List<ItemGainEffectView.Gain> _gains = new List<ItemGainEffectView.Gain>();

    // 참조 확보 → 구독 → 초기화 순서로 진행한다 (클라 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다(MonoService 주석).
    private void Start()
    {
        this.RequireRef(toggleButton,   nameof(toggleButton));
        this.RequireRef(resetButton,    nameof(resetButton));
        this.RequireRef(goldText,       nameof(goldText));
        this.RequireRef(activeSlotText, nameof(activeSlotText));
        this.RequireRef(totalText,      nameof(totalText));
        this.RequireRef(perHourText,    nameof(perHourText));
        this.RequireRef(elapsedText,    nameof(elapsedText));
        this.RequireRef(miniSlotPrefab, nameof(miniSlotPrefab));
        this.RequireRef(stripParent,    nameof(stripParent));

        _data = Services.Get<PlayerDataModel>();
        toggleButton.onClick.AddListener(Services.Get<UIManager>().ToggleAll);
        resetButton.onClick.AddListener(ResetEarnings);

        Subscribe();
        RefreshGold();
        BeginEarningsIfLoggedIn(); // 이미 로그인한 뒤에 켜졌을 수 있다
        RefreshEarnings();
        Rebuild(); // 이미 스냅샷을 받은 뒤에 켜졌을 수 있다

        _isReady = true;
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    //
    // ★ 위젯은 상주라 평소 꺼지지 않지만, 규약을 예외로 두지 않는다 —
    //   나중에 "위젯 접기"가 생기면 조용히 어긋나는 자리가 된다.
    private void OnEnable()
    {
        if (!_isReady)
        {
            return;
        }

        Subscribe();
        RefreshGold();
        BeginEarningsIfLoggedIn();
        RefreshEarnings();
        Rebuild();
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
    }

    // 카운트다운 진행 — 스트립 전체를 여기서 한 번에 계산한다 (Unity 메시지)
    private void Update()
    {
        if (!_isReady)
        {
            return;
        }

        if (_earnings.IsStarted && (long)_earnings.ElapsedSeconds(Time.realtimeSinceStartupAsDouble) != _shownSecond)
        {
            RefreshEarnings();
        }

        foreach (var slot in _data.WorkStationSlots)
        {
            if (!_views.TryGetValue(slot.SlotIndex, out var view) || !view.gameObject.activeSelf || !view.IsRunning)
            {
                continue;
            }

            view.Tick(WorkStationProgress.CalculateProgress(slot));
        }
    }

    #region 구독

    // 재화·슬롯·채취 결과 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed                 = true;
        _data.CurrencyChanged         += RefreshGold;
        _data.WorkStationSlotsChanged += Rebuild;
        _data.GatherResultReceived    += OnGatherResultReceived;
        _data.GatherValueEarned       += OnGatherValueEarned;
        _data.LoginCompleted          += OnLoginCompleted;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed                 = false;
        _data.CurrencyChanged         -= RefreshGold;
        _data.WorkStationSlotsChanged -= Rebuild;
        _data.GatherResultReceived    -= OnGatherResultReceived;
        _data.GatherValueEarned       -= OnGatherValueEarned;
        _data.LoginCompleted          -= OnLoginCompleted;
    }

    #endregion

    #region 표시

    // 골드를 현재 값으로 갱신한다 (CurrencyChanged 구독 · Start · OnEnable)
    private void RefreshGold()
    {
        goldText.text = _data.Gold.ToString("N0"); // 천 단위 구분
    }

    // 누적 · 시간당 · 측정 시간을 다시 쓴다 (Update에서 초가 바뀔 때 · 수확 · 초기화)
    private void RefreshEarnings()
    {
        double now     = Time.realtimeSinceStartupAsDouble;
        long   elapsed = (long)_earnings.ElapsedSeconds(now);

        _shownSecond     = elapsed;
        totalText.text   = _earnings.TotalGold.ToString("N0");
        perHourText.text = _earnings.TryGetPerHour(now, out long perHour) ? $"{perHour:N0}/h" : "—/h";
        elapsedText.text = $"{elapsed / 3600}:{elapsed / 60 % 60:00}:{elapsed % 60:00}";
    }

    // 로그인에 성공하면 그때부터 센다 (LoginCompleted 구독). 로그인 전에는 캐릭터가 일하지 않는다.
    private void OnLoginCompleted(bool success, EResultCode result)
    {
        BeginEarningsIfLoggedIn();
        RefreshEarnings();
    }

    // 로그인 상태면 집계를 시작한다 (로그인 응답 · Start · OnEnable). 이미 세는 중이면 그대로 둔다.
    private void BeginEarningsIfLoggedIn()
    {
        if (_data.IsLoggedIn)
        {
            _earnings.Begin(Time.realtimeSinceStartupAsDouble);
        }
    }

    // 수확 하나의 환산 골드를 누적에 더한다 (GatherValueEarned 구독)
    private void OnGatherValueEarned(long gold)
    {
        _earnings.Add(gold);
        RefreshEarnings();
    }

    // 집계를 0으로 되돌린다 (초기화 버튼). 잃는 것이 통계뿐이라 확인 창을 두지 않는다.
    private void ResetEarnings()
    {
        _earnings.Reset(Time.realtimeSinceStartupAsDouble);
        RefreshEarnings();
    }

    // 스냅샷대로 스트립을 다시 짜고 상단의 가동 수를 갱신한다 (WorkStationSlotsChanged 구독).
    //
    // ★ 여기가 카운트다운의 기준점 교정이기도 하다 — 새 스냅샷을 'Bind'로 넣으면
    //   그다음 Update부터 서버가 보낸 시각을 기준으로 다시 센다.
    private void Rebuild()
    {
        int activeCount = 0;

        foreach (var slot in _data.WorkStationSlots)
        {
            if (!WorkStationProgress.IsAssigned(slot))
            {
                RemoveView(slot.SlotIndex);

                continue;
            }

            activeCount++;

            if (!_views.TryGetValue(slot.SlotIndex, out var view))
            {
                view      = Instantiate(miniSlotPrefab, stripParent);
                view.name = $"Widget Mini Slot {slot.SlotIndex}";

                _views.Add(slot.SlotIndex, view);
            }

            // 해제 때 꺼 둔 칸을 다시 쓴다 — 아래 Bind가 이전 값을 전부 덮는다
            view.gameObject.SetActive(true);

            // 캐시에 담긴 순서 그대로 왼쪽부터 채운다 — 나중에 배치한 칸이 뷰만 늦게 생겨도
            // 자리는 원래 순서를 지킨다.
            //
            // ⚠️ 이 순서는 'PlayerDataModel._workStationSlots'의 순서다. 로그인 스냅샷이
            //   슬롯 번호대로 오므로 지금은 번호 순과 같지만, 스냅샷에 없던 칸이 나중에
            //   병합되면 리스트 끝에 붙는다. 번호 순을 보장해야 할 일이 생기면
            //   그 보장은 캐시 쪽에서 해야 한다 — 여기서 다시 정렬하지 않는다.
            view.transform.SetSiblingIndex(activeCount - 1);
            int characterTid = _data.GetCharacterTid(slot.CharacterId);

            view.Bind(slot, characterTid);
            view.SetRarity(GameDataLoader.GetCharacterRarity(characterTid));
        }

        activeSlotText.text = $"{activeCount}/{_data.WorkStationSlots.Count}"; // '가동'은 앞의 아이콘이 말한다
    }

    // 배치가 풀린 칸을 끈다 ('Rebuild'에서 호출). 꺼진 칸은 레이아웃에서 빠져 뒤의 칸이 당겨진다.
    // 파괴하지 않는다 — 같은 칸에 다시 배치되면 그대로 켜서 쓴다.
    private void RemoveView(int slotIndex)
    {
        if (_views.TryGetValue(slotIndex, out var view) && view != null)
        {
            view.gameObject.SetActive(false); // Update는 꺼진 칸을 건너뛴다
        }
    }

    // 채취 결과 푸시 도착 — 그 칸의 게이지를 처음으로 되돌리고 얻은 아이템을 띄운다 (GatherResultReceived 구독).
    //
    // ⚠️ 이 패킷에는 진행도가 없다(SlotIndex · JudgeCount · ItemChanges뿐이다).
    //   게이지의 실제 동기화는 'S_WorkStationSlotSyncResponse' → 'WorkStationSlotsChanged'가 한다.
    //   그래서 이 구독이 하는 일은 "어느 칸에서 무엇을 얻었나"를 아는 것이다.
    private void OnGatherResultReceived(S_GatherResultResponse res)
    {
        if (_views.TryGetValue(res.SlotIndex, out var view) && view.isActiveAndEnabled)
        {
            ItemGainEffectView.ReadGains(res.ItemChanges, _gains);
            view.MarkHarvested(_gains);
        }
    }

    #endregion
}

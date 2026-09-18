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
// 데이터 갱신은 이벤트(옵저버) — 'CurrencyChanged' · 'WorkStationSlotsChanged' · 'GatherResultReceived'.
// 시간 진행은 이 클래스의 'Update' 하나. 칸마다 Update를 두면 상시 실행 앱에서
// 비용이 슬롯 수만큼 곱해진다 ('WorkStationListPresenter'와 같은 판단).
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
public class WidgetPresenter : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("열기/닫기 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button toggleButton = null!;

    [CenterHeader("상단 줄")]
    [SerializeField, Tooltip("골드 보유량")]
    private TMP_Text goldText = null!;

    [SerializeField, Tooltip("가동 슬롯 — 돌고 있는 칸 / 전체 칸")]
    private TMP_Text activeSlotText = null!;

    // ⏸ 아래 둘은 참조만 잡아 둔다 — 산출 정의(무엇을 얼마로 환산하는가)가 기획에서
    //    아직 안 정해졌다. 지금 임의로 채우면 틀린 숫자를 사용자가 믿게 된다.
    //    → GameDesign/design/ui/README.md 6장
    [SerializeField, Tooltip("⏸ 시간당 산출. 기획 미정이라 아직 연결하지 않는다 — 씬의 더미 문자열이 그대로 보인다")]
    private TMP_Text perHourText = null!;

    [SerializeField, Tooltip("⏸ 누적 수확. 기획 미정이라 아직 연결하지 않는다 — 씬의 더미 문자열이 그대로 보인다")]
    private TMP_Text totalText = null!;

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

    // 참조 확보 → 구독 → 초기화 순서로 진행한다 (클라 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다(MonoService 주석).
    private void Start()
    {
        this.RequireRef(toggleButton,   nameof(toggleButton));
        this.RequireRef(goldText,       nameof(goldText));
        this.RequireRef(activeSlotText, nameof(activeSlotText));
        this.RequireRef(perHourText,    nameof(perHourText));
        this.RequireRef(totalText,      nameof(totalText));
        this.RequireRef(miniSlotPrefab, nameof(miniSlotPrefab));
        this.RequireRef(stripParent,    nameof(stripParent));

        _data = Services.Get<PlayerDataModel>();
        toggleButton.onClick.AddListener(Services.Get<UIManager>().ToggleAll);

        Subscribe();
        RefreshGold();
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

        foreach (var slot in _data.WorkStationSlots)
        {
            if (!_views.TryGetValue(slot.SlotIndex, out var view) || !view.IsRunning)
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
    }

    #endregion

    #region 표시

    // 골드를 현재 값으로 갱신한다 (CurrencyChanged 구독 · Start · OnEnable)
    private void RefreshGold()
    {
        goldText.text = _data.Gold.ToString("N0"); // 천 단위 구분
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

            // 캐시에 담긴 순서 그대로 왼쪽부터 채운다 — 나중에 배치한 칸이 뷰만 늦게 생겨도
            // 자리는 원래 순서를 지킨다.
            //
            // ⚠️ 이 순서는 'PlayerDataModel._workStationSlots'의 순서다. 로그인 스냅샷이
            //   슬롯 번호대로 오므로 지금은 번호 순과 같지만, 스냅샷에 없던 칸이 나중에
            //   병합되면 리스트 끝에 붙는다. 번호 순을 보장해야 할 일이 생기면
            //   그 보장은 캐시 쪽에서 해야 한다 — 여기서 다시 정렬하지 않는다.
            view.transform.SetSiblingIndex(activeCount - 1);
            view.Bind(slot);
            view.SetRarity(GameDataLoader.GetCharacterRarity(_data.GetCharacterTid(slot.CharacterId)));
        }

        activeSlotText.text = $"가동 {activeCount}/{_data.WorkStationSlots.Count}";
    }

    // 배치가 풀린 칸을 지운다 ('Rebuild'에서 호출). 레이아웃이 뒤의 칸을 당겨 빈자리를 없앤다.
    private void RemoveView(int slotIndex)
    {
        if (!_views.TryGetValue(slotIndex, out var view))
        {
            return;
        }

        _views.Remove(slotIndex); // Update가 죽은 뷰를 만지지 않도록 먼저 뺀다

        if (view != null)
        {
            Destroy(view.gameObject);
        }
    }

    // 채취 결과 푸시 도착 — 그 칸의 게이지를 처음으로 되돌린다 (GatherResultReceived 구독).
    //
    // ⚠️ 이 패킷에는 진행도가 없다(SlotIndex · JudgeCount · ItemChanges뿐이다).
    //   게이지의 실제 동기화는 'S_WorkStationSlotSyncResponse' → 'WorkStationSlotsChanged'가 한다.
    //   그래서 이 구독이 하는 일은 "어느 칸에서 수확이 났는가"를 아는 것이고,
    //   ⏸ 나중에 수확 표시가 떠오르는 연출이 붙을 자리다.
    private void OnGatherResultReceived(S_GatherResultResponse res)
    {
        if (_views.TryGetValue(res.SlotIndex, out var view))
        {
            view.MarkHarvested();
        }
    }

    #endregion
}

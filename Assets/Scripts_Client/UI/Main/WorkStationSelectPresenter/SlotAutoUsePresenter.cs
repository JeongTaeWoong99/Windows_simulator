using System.Collections.Generic;
using GameData;
using MikaNetwork;
using MikaProtocol;
using UnityEngine;

// 작업슬롯 설정의 **자동 사용 칸** — 슬롯마다 영약 종류별 한 칸 (이슈 #113 · T-194 · 2026-11-14 사용자 결정).
//
// ■ 무엇을 하나
// 영약을 칸에 넣어 두면, 그 영약의 버프가 끝날 때 서버가 칸에서 1개씩 꺼내 쓴다. 칸은 넣기·꺼내기만 한다.
// 좌클릭 = 넣기(수량 팝업 — 인벤토리·창고 보유량까지) · 우클릭 = 꺼내기(수량 팝업 — 넣어 둔 만큼).
// 넣을 수 있는 영약은 'ItemUseTable'에서 읽는다('ItemUseLabel.IsAutoUsable' — 지금은 신속·풍요). 종류가 늘면 칸도 는다.
//
// ■ 왜 'WorkStationSelectPresenter'에 넣지 않았나
// 그 화면은 이미 산업·레벨·캐릭터·장비·효율을 다 들고 있다. 이 칸은 슬롯 번호 하나만 받으면 혼자 돈다 —
// 그래서 세팅 패널 안에 따로 두고 번호만 넘겨받는다('SetSlot'). 세팅 패널이 꺼지면 이것도 꺼져 구독이 끊긴다.
//
// ■ 칸 내용의 진실은 슬롯 캐시다
// 성공하면 'S_WorkStationSlotSyncResponse'가 응답보다 먼저 와 'WorkStationSlotsChanged'로 다시 그린다.
// 이 화면은 개수를 계산하지 않는다 — 한도(MaxStack)·꺼낼 자리 부족은 서버가 거절한다('AutoUseSlotFull' · 'StorageFull').
public class SlotAutoUsePresenter : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("칸 견본 — 이 오브젝트 아래(칸 줄)에 꺼 둔다. 넣을 수 있는 영약 수만큼 복제한다")]
    private AutoUseCellView cellTemplate = null!;

    [SerializeField, Tooltip("'자동 사용' 머리 글씨 — 올리면 동작 안내 툴팁을 띄운다")]
    private TooltipTrigger labelTooltip = null!;

    private PlayerDataModel   _data    = null!;
    private UIManager         _ui      = null!;
    private NetworkManager    _network = null!;
    private ServerWaitManager _wait    = null!;

    private readonly List<AutoUseCellView> _cells = new List<AutoUseCellView>();

    private int               _slotIndex = -1;
    private ServerWaitHandle? _waitHandle;
    private bool              _isReady;
    private bool              _isSubscribed;

    // 참조 확보 → 배선 → 구독 → 초기화 (클라 공통 규약)
    private void Start()
    {
        this.RequireRef(cellTemplate, nameof(cellTemplate));
        this.RequireRef(labelTooltip, nameof(labelTooltip));

        _data    = Services.Get<PlayerDataModel>();
        _ui      = Services.Get<UIManager>();
        _network = NetworkManager.Instance;
        _wait    = Services.Get<ServerWaitManager>();

        cellTemplate.gameObject.SetActive(false);
        labelTooltip.SetProvider(() => new TooltipContent(Lang.Get("UI.WorkStation.AutoUse")).Row(Lang.Get("UI.WorkStation.AutoUseHint"), ""));
        BuildCells();

        _isReady = true;
        Subscribe();
        Refresh();
    }

    // 다시 켜지면 구독을 잇고 다시 그린다 (Unity 메시지). 처음 켜질 때는 Start가 한다
    private void OnEnable()
    {
        if (!_isReady)
        {
            return;
        }

        Subscribe();
        Refresh();
    }

    // 꺼지면 구독을 끊고 대기 손잡이를 버린다 (Unity 메시지) — 남은 대기는 매니저가 타임아웃으로 정리한다
    private void OnDisable()
    {
        Unsubscribe();
        _waitHandle = null;
    }

    // 어느 슬롯의 칸인가 ('WorkStationSelectPresenter.ShowSetting'이 호출). 꺼져 있어도 번호만 받아 둔다
    public void SetSlot(int slotIndex)
    {
        _slotIndex  = slotIndex;
        _waitHandle = null; // 다른 슬롯이다 — 지난 대기를 들고 들어가지 않는다

        if (_isReady && isActiveAndEnabled)
        {
            Refresh();
        }
    }

    #region 구독

    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed                  = true;
        _data.WorkStationSlotsChanged += Refresh;
        _data.InventoryChanged        += Refresh;
        _data.SlotAutoUseResponded    += OnResponded;
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed                  = false;
        _data.WorkStationSlotsChanged -= Refresh;
        _data.InventoryChanged        -= Refresh;
        _data.SlotAutoUseResponded    -= OnResponded;
    }

    #endregion

    #region 표시

    // 넣을 수 있는 영약마다 칸을 만든다 (Start에서 한 번) — 표가 정하므로 실행 중에는 바뀌지 않는다
    private void BuildCells()
    {
        var tids = new List<int>();

        foreach (ItemUseTableRow row in GameTable.ItemUseTable.All)
        {
            if (ItemUseLabel.IsAutoUsable(row.UseEffect))
            {
                tids.Add(row.ItemTID);
            }
        }

        tids.Sort(); // 신속 → 풍요 — TID 순이 엑셀 순이다

        foreach (int tid in tids)
        {
            AutoUseCellView cell = Instantiate(cellTemplate, cellTemplate.transform.parent);

            cell.name = $"Auto Use Cell {tid}";
            cell.gameObject.SetActive(true);
            cell.LeftClicked  += OnCellLeftClicked;
            cell.RightClicked += OnCellRightClicked;
            cell.Bind(tid, 0, () => null);
            _cells.Add(cell);
        }
    }

    // 칸마다 넣어 둔 개수를 다시 그린다 (슬롯·인벤토리 변경 · 열기 · 응답)
    private void Refresh()
    {
        if (!_isReady)
        {
            return;
        }

        WorkStationSlotInfo? slot = FindSlot();
        bool                 idle = _waitHandle == null;

        foreach (AutoUseCellView cell in _cells)
        {
            int tid = cell.ItemTid;

            cell.Bind(tid, StoredCount(slot, tid), () => BuildTooltip(tid));
            cell.SetInteractable(idle && slot != null);
        }
    }

    // 칸 툴팁 — 이름 / 효과 / 자동 사용 칸에 n개 / 보유 n개 / 조작 안내 (칸의 TooltipTrigger가 띄우는 순간에 호출)
    private TooltipContent BuildTooltip(int tid)
    {
        int stored = StoredCount(FindSlot(), tid);

        return new TooltipContent(GameDataLoader.GetItemName(tid))
              .Row(ItemUseLabel.Effect(tid), "")
              .Row(Lang.Format("UI.WorkStation.AutoUseInSlot", $"{stored:N0}"), "")
              .Row(Lang.Get("UI.WorkStation.AutoUseHint"), "");
    }

    private WorkStationSlotInfo? FindSlot()
    {
        foreach (WorkStationSlotInfo slot in _data.WorkStationSlots)
        {
            if (slot.SlotIndex == _slotIndex)
            {
                return slot;
            }
        }

        return null;
    }

    private static int StoredCount(WorkStationSlotInfo? slot, int tid)
    {
        if (slot?.AutoUse == null)
        {
            return 0;
        }

        foreach (SlotAutoUseInfo auto in slot.AutoUse)
        {
            if (auto.ItemTID == tid)
            {
                return auto.Count;
            }
        }

        return 0;
    }

    #endregion

    #region 넣기 · 꺼내기

    // 좌클릭 — 넣기. 보유량이 0이면 묻지 않고 칸을 흔든다 (칸 LeftClicked 구독)
    private void OnCellLeftClicked(AutoUseCellView cell)
    {
        int owned = _data.GetItemCount(cell.ItemTid);

        if (owned <= 0)
        {
            _wait.RaiseRefusal(Lang.Format("UI.WorkStation.AutoUseNoItem", GameDataLoader.GetItemName(cell.ItemTid)), cell.transform);

            return;
        }

        if (owned == 1)
        {
            Send(cell.ItemTid, 1, take: false);

            return;
        }

        _ui.AskAmount(cell.ItemTid, owned, Lang.Get("UI.WorkStation.AskAutoPut"), amount => Send(cell.ItemTid, amount, take: false));
    }

    // 우클릭 — 꺼내기. 넣어 둔 것이 없으면 아무것도 하지 않는다 (칸 RightClicked 구독)
    private void OnCellRightClicked(AutoUseCellView cell)
    {
        int stored = StoredCount(FindSlot(), cell.ItemTid);

        if (stored <= 0)
        {
            return;
        }

        if (stored == 1)
        {
            Send(cell.ItemTid, 1, take: true);

            return;
        }

        _ui.AskAmount(cell.ItemTid, stored, Lang.Get("UI.WorkStation.AskAutoTake"), amount => Send(cell.ItemTid, amount, take: true));
    }

    // 넣기·꺼내기 요청을 보낸다 (좌·우클릭 · 수량 팝업 확인에서 호출)
    private void Send(int itemTid, int count, bool take)
    {
        if (_waitHandle != null || _slotIndex < 0)
        {
            return;
        }

        if (!_data.IsLoggedIn)
        {
            ClientLogger.Warn(ClientLogger.Send, "자동 사용 칸 요청을 보내지 않았다 — 로그인이 먼저다(서버가 응답 없이 버린다)");

            return;
        }

        _network.Send(new C_SlotAutoUseRequest
        {
            SlotIndex = _slotIndex,
            ItemTID   = itemTid,
            Count     = count,
            Take      = take,
        });

        ClientLogger.Info(ClientLogger.Send, $"자동 사용 칸 {(take ? "꺼내기" : "넣기")} 요청 — 슬롯={_slotIndex}, TID={itemTid}, {count}개");

        _waitHandle = _wait.Begin(Lang.Get("UI.WorkStation.WaitAutoUse"), onClosed: OnWaitClosed);
        Refresh();
    }

    // 응답 도착 — 성공이면 조용히, 거절이면 사유를 알린다 (PlayerDataModel.SlotAutoUseResponded 구독).
    // 대기 손잡이가 없으면 이 화면이 보낸 것이 아니다(지난 슬롯에서 보낸 것의 늦은 응답).
    private void OnResponded(EResultCode code)
    {
        if (_waitHandle == null)
        {
            return;
        }

        if (code == EResultCode.Ok)
        {
            _waitHandle.Succeed();
        }
        else
        {
            _waitHandle.Fail(ResultMessages.ToText(code));
        }
    }

    // 대기가 끝났다(성공·실패·타임아웃 공통) — 다시 누를 수 있게 푼다 (ServerWaitManager.Begin의 onClosed)
    private void OnWaitClosed()
    {
        _waitHandle = null;
        Refresh();
    }

    #endregion
}

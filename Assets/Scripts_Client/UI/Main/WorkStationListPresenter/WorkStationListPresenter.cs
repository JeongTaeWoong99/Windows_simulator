using System.Collections.Generic;
using System.Text;
using MikaNetwork;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 작업슬롯 목록 패널. 서버 스냅샷만큼 'WorkStationSlotView'를 만들고,
// 카운트다운을 여기 한 곳에서 계산해 각 뷰에 넘긴다.
//
// ■ 두 축을 섞지 않는다
// 데이터 갱신은 이벤트(옵저버) — 'PlayerDataModel.WorkStationSlotsChanged'.
// 시간 진행은 이 클래스의 'Update' 하나.
// 슬롯마다 Update를 두면 상시 실행 앱에서 비용이 슬롯 수만큼 곱해진다.
//
// ⚠️ 칸을 누를 때는 번호를 먼저 넣고 자리를 넘긴다 — 선택 화면은 평소 꺼져 있어
// 이 목록을 구독할 수 없다. 살아 있는 쪽이 넘긴다.
//
// selectPresenter.Open(slotIndex)                    번호를 먼저 넣는다
// ui.ShowMainScreen(MainScreen.WorkStationSelect)    자리를 넘긴다 (이 패널은 여기서 꺼진다)
//
// 카운트다운 계산은 'WorkStationProgress'가 한다 — 상주 위젯의 스트립이 같은 값을 그려서,
// 계산식이 두 벌이 되면 한쪽만 고쳐진다. 계산식의 근거는 '패킷 레퍼런스.md',
// 화면 전환 흐름은 'Main 규칙.md'의 "전환 층은 하나다" 절 참조.
//
// ■ 칸은 세 상태다 — 잠김 / 열린 빈 칸 / 배치됨
// 잠김 여부는 'WorkSlotTable'의 'UnlockTID'를 'PlayerDataModel.IsUnlocked'에 묻는다.
// 잠긴 칸은 프레임 라벨에 해금 조건을 적고, 누르면 배치 화면이 아니라 해금 흐름으로 간다.
public class WorkStationListPresenter : MonoBehaviour
{
    // 열린 빈 칸의 라벨 — 프레임 프리팹('WorkSlotFrame')에 적힌 문구와 같다
    private const string EmptyLabel = "비어있음.";

    [CenterHeader("참조")]
    [SerializeField, Tooltip("슬롯 한 칸 프리팹 (WorkStationSlotView 포함). 빈 프레임 안에 생성된다")]
    private WorkStationSlotView slotPrefab = null!;

    [SerializeField, Tooltip("칸 프레임(Slot)들이 들어 있는 부모 — Viewport > Content")]
    private Transform slotParent = null!;

    [SerializeField, Tooltip("칸을 누르면 갈아 끼울 화면 — WorkStation Select Presenter")]
    private WorkStationSelectPresenter selectPresenter = null!;

    // 슬롯 번호 → 뷰. 스냅샷이 다시 와도 같은 칸을 재사용해 깜빡임을 막는다.
    private readonly Dictionary<int, WorkStationSlotView> _views = new Dictionary<int, WorkStationSlotView>();

    // 칸 번호 → 프레임 라벨("비어있음." 자리). 잠긴 칸은 여기에 해금 조건을 적는다.
    private readonly Dictionary<int, TMP_Text> _frameLabels = new Dictionary<int, TMP_Text>();

    private PlayerDataModel   _data = null!;
    private UIManager         _ui   = null!;
    private ServerWaitManager _wait = null!;
    private bool            _isSubscribed;
    private bool            _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 내가 보낸 해금 요청의 대기. null이면 기다리는 요청이 없다 —
    // 이때 온 해금 결과는 치트·퀘스트처럼 서버가 연 것이라 문구를 띄우지 않는다.
    private ServerWaitHandle? _unlockWait;

    // 참조 확보 → 구독 → 초기화 순서로 진행한다 (클라 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다(MonoService 주석).
    private void Start()
    {
        this.RequireRef(slotPrefab,      nameof(slotPrefab));
        this.RequireRef(slotParent,      nameof(slotParent));
        this.RequireRef(selectPresenter, nameof(selectPresenter));

        _data = Services.Get<PlayerDataModel>();
        _ui   = Services.Get<UIManager>();
        _wait = Services.Get<ServerWaitManager>();

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
        {
            return;
        }

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
        {
            return;
        }

        foreach (var slot in _data.WorkStationSlots)
        {
            if (!_views.TryGetValue(slot.SlotIndex, out var view) || !view.IsRunning)
            {
                continue;
            }

            view.Tick(WorkStationProgress.CalculateProgress(slot), WorkStationProgress.CalculateRemainSeconds(slot));
        }
    }

    #region 구독

    // 슬롯 스냅샷 변경 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed                 = true;
        _data.WorkStationSlotsChanged += Rebuild;
        _data.UnlocksChanged          += Rebuild; // 열린 목록이 바뀌면 잠김 표시를 다시 그린다
        _data.UnlockCompleted         += OnUnlockCompleted;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed                 = false;
        _data.WorkStationSlotsChanged -= Rebuild;
        _data.UnlocksChanged          -= Rebuild;
        _data.UnlockCompleted         -= OnUnlockCompleted;
    }

    #endregion

    #region 칸 클릭

    // 칸 프레임의 버튼을 슬롯 번호와 묶는다 (Start에서 한 번).
    //
    // 버튼은 프레임에 붙어 있어야 한다 — 안에 생기는 뷰가 아니라.
    // 비어 있는 슬롯에는 뷰가 만들어지지 않는데, 빈 칸이야말로 눌러서 배치할 대상이다.
    //
    // 프레임 라벨도 여기서 잡아 둔다 — 슬롯 뷰가 생기기 전이라 프레임 안의 텍스트는 라벨 하나뿐이다.
    private void BindFrameButtons()
    {
        for (int i = 0; i < slotParent.childCount; i++)
        {
            var label = slotParent.GetChild(i).GetComponentInChildren<TMP_Text>(true);

            if (label != null)
            {
                _frameLabels[i] = label;
            }

            var button = slotParent.GetChild(i).GetComponent<Button>();

            if (button == null)
            {
                ClientLogger.Warn(ClientLogger.UI,
                    $"칸 프레임 {slotParent.GetChild(i).name}에 Button이 없어 클릭을 받을 수 없다.", this);

                continue;
            }

            // 반복 변수를 그대로 넘기면 모든 콜백이 마지막 값을 본다. 복사본을 캡처한다.
            int slotIndex = i;
            button.onClick.AddListener(() => OnFrameClicked(slotIndex));
        }
    }

    // 칸을 눌렀다 — 잠긴 칸이면 해금 흐름, 열린 칸이면 배치 화면 (칸 프레임 OnClick에 코드로 연결)
    private void OnFrameClicked(int slotIndex)
    {
        int unlockTid = GameDataLoader.GetWorkSlotUnlockTid(slotIndex);

        if (_data.IsUnlocked(unlockTid))
        {
            OpenSelect(slotIndex);

            return;
        }

        TryUnlock(slotIndex, unlockTid);
    }

    // 잠긴 칸의 해금을 시도한다 ('OnFrameClicked'에서 호출).
    // 순서는 서버 판정(기획 unlock 2.1)과 같다 — 무료 조건(선행)을 먼저 보고 골드는 마지막.
    //
    // ※ 클라 판정은 안내일 뿐이다. 통과해도 서버가 다시 검사한다(게임기획코어 P4).
    private void TryUnlock(int slotIndex, int unlockTid)
    {
        if (!GameDataLoader.TryGetUnlock(unlockTid, out var unlock))
        {
            ClientLogger.Warn(ClientLogger.UI, $"슬롯 {slotIndex}의 해금 {unlockTid}이 UnlockTable에 없다.", this);

            return;
        }

        if (_unlockWait != null)
        {
            return; // 응답 대기 중 — 같은 해금을 두 번 보내면 두 번째가 AlreadyUnlocked로 거절된다
        }

        foreach (int requiredTid in unlock.RequiredUnlockTIDs)
        {
            if (!_data.IsUnlocked(requiredTid))
            {
                _wait.RaiseNotice($"{DescribeUnlock(requiredTid)}을(를) 먼저 해금해야 합니다.");

                return;
            }
        }

        // 계정 레벨 조건. 작업슬롯 행은 지금 모두 0이라 늘 통과하지만, 곡선이 정해지면(T-012)
        // 시트만 채워도 여기서 막힌다 — 특성 노드가 같은 컬럼을 이미 쓰고 있다.
        if (_data.AccountLevel < unlock.AccountLevel)
        {
            _wait.RaiseNotice($"계정 레벨 {unlock.AccountLevel}이(가) 필요합니다. (지금 {_data.AccountLevel})");

            return;
        }

        if (_data.Gold < unlock.Gold)
        {
            _wait.RaiseNotice($"골드가 부족합니다. ({unlock.Gold:N0} 골드 필요)");

            return;
        }

        _ui.AskConfirm($"{unlock.Gold:N0} 골드가 필요합니다.\n해금하시겠습니까?",
                       () => RequestUnlock(unlockTid, unlock.Gold > 0 ? ECurrencyType.Gold : ECurrencyType.None));
    }

    // 해금 요청을 보내고 응답을 기다린다 (확인 팝업 콜백).
    //   currency : 지불 재화. 지불 컬럼이 없는 해금이면 None — 서버가 무시한다
    //
    // 결과는 'OnUnlockCompleted'가 받는다. 새 칸은 'S_WorkStationSlotSyncResponse' → 'Rebuild'로 따라온다.
    private void RequestUnlock(int unlockTid, ECurrencyType currency)
    {
        // 로그인 전에 보내면 서버가 응답 없이 버린다 — 'WorkStationSelectPresenter.CanSend'와 같은 이유
        if (!_data.IsLoggedIn)
        {
            ClientLogger.Warn(ClientLogger.Send, "해금 요청을 보내지 않았다 — 로그인이 먼저다(서버가 응답 없이 버린다)");

            return;
        }

        ClientLogger.Info(ClientLogger.Send, $"해금 요청 — {unlockTid}, 재화={currency}");

        NetworkManager.Instance.Send(new C_UnlockRequest { UnlockTID = unlockTid, Currency = currency });
        _unlockWait = _wait.Begin("작업슬롯 해금", onClosed: () => _unlockWait = null);
    }

    // 해금 결과 (PlayerDataModel.UnlockCompleted 구독).
    // 성공이면 표시는 이미 'UnlocksChanged → Rebuild'가 바꿨으므로 대기만 닫는다.
    private void OnUnlockCompleted(bool success, EResultCode code)
    {
        if (_unlockWait == null)
        {
            return; // 내가 보낸 요청이 아니다 (치트·퀘스트로 서버가 연 경우)
        }

        if (success)
        {
            _unlockWait.Succeed();
        }
        else
        {
            _unlockWait.Fail(ResultMessages.ToText(code));
        }
    }

    // 그 칸의 배치/해제 화면으로 갈아 끼운다 ('OnFrameClicked'에서 열린 칸일 때만 호출).
    // 배치 여부와 상관없이 열린다 — 빈 칸이면 배치, 찬 칸이면 해제가 뜬다.
    //
    // ⚠️ 번호를 먼저 넣고 화면을 넘긴다. 꺼져 있던 화면은 'Start()'가 아직 안 돌았을 수
    // 있어, 켠 뒤에 번호를 넣으면 초기화가 덮어쓴다.
    private void OpenSelect(int slotIndex)
    {
        selectPresenter.Open(slotIndex);
        _ui.ShowMainScreen(MainScreen.WorkStationSelect);
    }

    #endregion

    #region 목록 구성

    // 스냅샷대로 슬롯 뷰를 만들고 갱신한다 (WorkStationSlotsChanged 구독).
    // 슬롯 번호가 곧 프레임 순서다 — 슬롯 0은 'Content'의 첫 자식 프레임 안에 들어간다.
    // 인벤토리와 달리 번호가 고정이라 "빈 프레임 찾기"가 아니라 자리를 직접 고른다.
    //
    // ■ 배치된 칸에만 뷰를 둔다
    // 배치가 풀리면 뷰를 지운다. 남겨 두고 "대기"라고 적으면 빈 칸과 구분이 안 되고,
    // 무엇보다 뷰가 프레임 위를 덮어 칸을 눌러 배치 화면으로 들어가는 길을 막는다.
    private void Rebuild()
    {
        RefreshFrameLabels();

        foreach (var slot in _data.WorkStationSlots)
        {
            // 비어 있는 칸은 프레임만 남긴다 — 그래야 눌러서 배치할 수 있다
            if (!WorkStationProgress.IsAssigned(slot))
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
            view.SetRarity(GameDataLoader.GetCharacterRarity(_data.GetCharacterTid(slot.CharacterId)));
        }
    }

    // 프레임 라벨을 칸 상태대로 적는다 — 잠긴 칸은 해금 조건, 열린 칸은 "비어있음." ('Rebuild'에서 호출).
    // 배치된 칸은 위를 슬롯 뷰가 덮으므로 라벨 문구는 상관없다.
    //
    // ※ 조건은 고정 테이블 값이라 골드가 바뀌어도 다시 그릴 필요가 없다. 선행이 열리는 것은
    //   'UnlocksChanged'(T-038)가 알려 준다.
    private void RefreshFrameLabels()
    {
        foreach (var pair in _frameLabels)
        {
            int unlockTid = GameDataLoader.GetWorkSlotUnlockTid(pair.Key);

            pair.Value.text = _data.IsUnlocked(unlockTid) ? EmptyLabel : BuildLockedLabel(unlockTid);
        }
    }

    // 잠긴 칸의 조건 문구 — "잠김 / 500 골드 / 슬롯 2 해금 필요"처럼 조건마다 한 줄.
    // 이미 충족한 선행은 적지 않는다.
    private string BuildLockedLabel(int unlockTid)
    {
        var sb = new StringBuilder("잠김");

        if (!GameDataLoader.TryGetUnlock(unlockTid, out var unlock))
        {
            return sb.ToString();
        }

        if (unlock.Gold > 0)
        {
            sb.Append($"\n{unlock.Gold:N0} 골드");
        }

        if (unlock.AccountLevel > 0)
        {
            sb.Append($"\n계정 Lv.{unlock.AccountLevel}");
        }

        foreach (int requiredTid in unlock.RequiredUnlockTIDs)
        {
            if (!_data.IsUnlocked(requiredTid))
            {
                sb.Append($"\n{DescribeUnlock(requiredTid)} 해금 필요");
            }
        }

        return sb.ToString();
    }

    // 해금 TID를 사람이 읽는 이름으로 — 'UnlockTable.Name'("작업슬롯 2번").
    // 콘텐츠 테이블을 거꾸로 찾지 않는다 — 선행이 슬롯이 아닌 해금이어도 같은 방식으로 나와야 한다(기획 unlock #13).
    private static string DescribeUnlock(int unlockTid)
    {
        return GameDataLoader.TryGetUnlock(unlockTid, out var row) && row.Name.Length > 0 ? row.Name : $"해금 #{unlockTid}";
    }

    // 배치가 풀린 칸의 뷰를 지운다 ('Rebuild'에서 호출).
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

    // 프리팹을 프레임 안에 안착시킨다 — 위치를 0으로 맞춰 프레임 정중앙에 놓는다.
    // Instantiate 직후의 RectTransform은 프리팹에 저장된 좌표를 그대로 들고 온다.
    private static void SnapToFrame(RectTransform? rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchoredPosition3D = Vector3.zero;
        rect.localScale         = Vector3.one;
        rect.localRotation      = Quaternion.identity;
    }

    #endregion
}

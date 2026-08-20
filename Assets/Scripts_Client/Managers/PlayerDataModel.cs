using System;
using System.Collections.Generic;
using MikaNetwork;
using MikaProtocol;
using UnityEngine;

// UnityEngine에도 CharacterInfo(폰트 글리프 정보)가 있어 이름이 겹친다. 우리가 쓰는 건 패킷 쪽이다.
using CharacterInfo = MikaProtocol.CharacterInfo;

/// <summary>
/// 서버가 밀어준 내 계정 상태를 들고 있는 수신 전담 매니저 (서비스 로케이터 등록).
/// 수신 진입점('ServerPacketHandler')을 구독해 캐시를 채우고 가공된 변경 이벤트를 발행한다 —
/// UI는 서버 폴더가 아니라 이 매니저만 구독하면 된다.
/// </summary>
/// <remarks>
/// ⚠️ 송신은 하지 않는다 — 요청은 그 요청을 일으킨 Presenter가 직접 보낸다.
/// 'GameDataLoader'와의 역할 구분(고정 테이블 ↔ 내 계정 상태)은 'Managers 규칙.md' 2장,
/// 패킷별 수량 규약은 '서버 동작 이해.md', 로그인 1회 수신 세트는 '패킷 레퍼런스.md' 참조.
/// </remarks>
public class PlayerDataModel : MonoService<PlayerDataModel>
{
    // ─── 상태 캐시 ───
    private readonly List<ItemInfo>            _inventory        = new List<ItemInfo>();
    private readonly List<WorkStationSlotInfo> _workStationSlots = new List<WorkStationSlotInfo>();
    private readonly List<CharacterInfo>       _characters       = new List<CharacterInfo>();
    private readonly Dictionary<byte, long>    _currencies       = new Dictionary<byte, long>();

    // ─── 내부 상태 ───
    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    public long SessionId  { get; private set; }
    public bool IsLoggedIn { get; private set; }

    /// <summary>
    /// 로그인에 쓴 Id. 서버가 닉네임을 돌려주지 않아 표시에 대신 쓰는 임시값이다
    /// — 닉네임 패킷이 생기면 이 값과 'SetLoginId'는 함께 사라진다.
    /// </summary>
    public string LoginId { get; private set; } = "";

    public IReadOnlyList<ItemInfo>            Inventory        => _inventory;
    public IReadOnlyList<WorkStationSlotInfo> WorkStationSlots => _workStationSlots;

    /// <summary>
    /// 내가 가진 캐릭터들.
    ///
    /// ⚠️ 'CharacterInfo.CharacterId'는 개체 번호이고, 'CharacterInfo.CharacterTid'가
    /// 캐릭터 종류다. 같은 캐릭터를 여러 마리 가질 수 있어서 종류로는 하나를 특정하지 못한다.
    /// 슬롯 배치에 넣을 값은 개체 번호(CharacterId)다 — 종류(1001 같은 TID)를 보내면
    /// 서버가 'CharacterNotOwned'로 거절한다. 이름·적성은 TID로 테이블에서 읽는다.
    /// </summary>
    public IReadOnlyList<CharacterInfo> Characters => _characters;

    /// <summary>
    /// 첫 번째 보유 캐릭터의 개체 번호. 없으면 0(= 서버에선 배치 해제로 읽힌다).
    /// 캐릭터 선택 UI가 생기기 전까지 테스트 버튼들이 쓰는 임시 통로다.
    /// </summary>
    public long FirstCharacterId => _characters.Count > 0 ? _characters[0].CharacterId : 0L;

    /// <summary>
    /// 캐릭터 개체 번호로 표시 이름을 얻는다.
    ///
    /// 이름은 종류(TID)에 달린 값이라 개체 번호만으로는 못 찾는다 — 보유 목록에서 TID를 거쳐 간다.
    /// 'GameDataLoader.GetCharacterName'에 개체 번호를 그대로 넣으면 '?#2'가 나온다.
    /// </summary>
    public string GetCharacterName(long characterId)
    {
        foreach (var character in _characters)
        {
            if (character.CharacterId == characterId)
                return GameDataLoader.GetCharacterName(character.CharacterTid);
        }

        return $"?#{characterId}"; // 아직 목록을 못 받았거나 서버가 모르는 개체
    }

    /// <summary>
    /// 캐릭터 개체 번호로 그 산업의 적성(0~10)을 얻는다. 모르는 개체·산업이면 0
    /// (= 그 산업을 다루지 못한다. 서버가 배치를 'NoAptitude'로 거절한다).
    /// ⚠️ 'CharacterTable'을 직접 읽지 않는다 — 값의 주인은 서버다
    /// (근거는 '패킷 레퍼런스.md' 적성 절).
    /// </summary>
    public byte GetAptitude(long characterId, EIndustryType industry)
    {
        foreach (var character in _characters)
        {
            if (character.CharacterId != characterId)
                continue;

            foreach (var aptitude in character.Aptitudes)
            {
                if (aptitude.Industry == industry)
                    return aptitude.Value;
            }

            return 0; // 1차 산업 5종이 전부 실려 오므로 여기 오면 산업 쪽이 이상한 것이다
        }

        return 0;
    }

    /// <summary>재화 보유량을 조회한다. 아직 통지받지 못한 종류는 0이다.</summary>
    public long GetCurrency(byte currencyType) => _currencies.TryGetValue(currencyType, out long amount) ? amount : 0L;

    /// <summary>골드 보유량 (GameData.CurrencyType.Gold = 1).</summary>
    public long Gold => GetCurrency((byte)GameData.CurrencyType.Gold);

    /// <summary>
    /// 로그인 요청에 쓴 Id를 표시용으로 기억한다 (로그인을 보낸 UI가 호출).
    ///
    /// 이 매니저는 송신을 모르므로 "무엇으로 로그인했는가"를 스스로 알 수 없다.
    /// 서버가 닉네임을 돌려주기 시작하면 'LoginId'와 함께 지운다.
    /// </summary>
    public void SetLoginId(string id) => LoginId = id;

    // ─── 가공 이벤트 (UI가 구독) ───
    // ※ 완료 이벤트에는 성공 여부와 함께 결과 코드를 싣는다 — 실패 사유를 화면에 보여 주려면
    //   "실패했다"만으로는 부족하고 왜인지(EResultCode)가 필요하다.
    //   받은 Presenter가 'ResultMessages'로 문구를 만들어 'ServerWaitManager'에 넘긴다.
    public event Action<bool, EResultCode>?     LoginCompleted;   // 로그인 완료 (성공 여부·결과 코드)
    public event Action?                        InventoryChanged; // 인벤토리 갱신됨 (스냅샷 반영 후)
    public event Action<List<GachaRewardInfo>>? GachaCompleted;   // 가챠 성공 (뽑힌 보상 목록)
    public event Action<EResultCode>?           GachaFailed;      // 가챠 실패 (거절 사유)

    public event Action?                         CharactersChanged;          // 보유 캐릭터 캐시 갱신됨
    public event Action<bool, EResultCode>?      WorkStationAssignCompleted; // 슬롯 변경 완료 (성공 여부·결과 코드)
    public event Action?                         WorkStationSlotsChanged;    // 슬롯 캐시 갱신됨
    public event Action<S_GatherResultResponse>? GatherResultReceived;       // 채취 결과 푸시 도착
    public event Action?                         CurrencyChanged;            // 재화 캐시 갱신됨

    // ─── Unity 메시지 ───

    // 구독 → 초기화 순서로 진행한다 (매니저 공통 규약 — 이 매니저는 확보할 참조가 없다)
    private void Start()
    {
        Subscribe();
        _isReady = true;
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    private void OnEnable()
    {
        if (_isReady)
            Subscribe();
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
    }

    #region 구독

    // 수신 진입점 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
            return;

        _isSubscribed = true;

        ServerPacketHandler.LoginResponded           += OnLoginResponded;
        ServerPacketHandler.InventoryReceived        += OnInventoryReceived;
        ServerPacketHandler.GachaDrawn               += OnGachaDrawn;
        ServerPacketHandler.CharacterListReceived    += OnCharacterListReceived;
        ServerPacketHandler.WorkStationAssigned      += OnWorkStationAssigned;
        ServerPacketHandler.WorkStationSlotsReceived += OnWorkStationSlotsReceived;
        ServerPacketHandler.GatherResultReceived     += OnGatherResultReceived;
        ServerPacketHandler.WorkStationSlotSynced    += OnWorkStationSlotSynced;
        ServerPacketHandler.CurrencyReceived         += OnCurrencyReceived;
        ServerPacketHandler.ItemUpdated              += OnItemUpdated;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
            return;

        _isSubscribed = false;

        ServerPacketHandler.LoginResponded           -= OnLoginResponded;
        ServerPacketHandler.InventoryReceived        -= OnInventoryReceived;
        ServerPacketHandler.GachaDrawn               -= OnGachaDrawn;
        ServerPacketHandler.CharacterListReceived    -= OnCharacterListReceived;
        ServerPacketHandler.WorkStationAssigned      -= OnWorkStationAssigned;
        ServerPacketHandler.WorkStationSlotsReceived -= OnWorkStationSlotsReceived;
        ServerPacketHandler.GatherResultReceived     -= OnGatherResultReceived;
        ServerPacketHandler.WorkStationSlotSynced    -= OnWorkStationSlotSynced;
        ServerPacketHandler.CurrencyReceived         -= OnCurrencyReceived;
        ServerPacketHandler.ItemUpdated              -= OnItemUpdated;
    }

    #endregion

    #region 응답 처리 (ServerPacketHandler 구독)

    // 로그인 응답 — 세션 상태 갱신 후 이벤트 발행
    private void OnLoginResponded(S_LoginResponse res)
    {
        IsLoggedIn = res.Result == EResultCode.Ok;
        SessionId  = res.SessionId;

        if (!IsLoggedIn)
            ClientLogger.Warn(ClientLogger.Recv, $"로그인 실패 — 결과={res.Result}");

        LoginCompleted?.Invoke(IsLoggedIn, res.Result);
    }

    // 인벤토리 스냅샷 — 캐시 교체 후 이벤트 발행
    // ★ 로그인 시 자동으로 1회 수신.
    private void OnInventoryReceived(S_InventoryResponse res)
    {
        _inventory.Clear();
        if (res.Items != null)
            _inventory.AddRange(res.Items);

        InventoryChanged?.Invoke();
    }

    // 가챠 응답 — 인벤토리 반영 후 보상 이벤트 발행
    // ★ 한 패킷에 두 가지가 실려 온다. 용도가 다르니 섞어 쓰지 않는다.
    //   ItemChangeInfos = 갱신 후 누적 총량 → 인벤토리 반영 (다른 경로와 같은 규칙)
    //   Rewards         = 이번에 뽑힌 개별 항목(델타) → 연출 전용
    private void OnGachaDrawn(S_GachaDrawResponse res)
    {
        if (res.Result != EResultCode.Ok)
        {
            ClientLogger.Warn(ClientLogger.Recv, $"가챠 실패 — 결과={res.Result}");
            GachaFailed?.Invoke(res.Result);
            return;
        }

        ApplyItemChanges(res.ItemChangeInfos);
        GachaCompleted?.Invoke(res.Rewards ?? new List<GachaRewardInfo>());
    }

    // 보유 캐릭터 스냅샷 — 캐시 교체 후 이벤트 발행
    // ★ 로그인 시 자동으로 1회 수신. 여기 실린 CharacterId(개체 번호)가 슬롯 배치에 넣을 값이다.
    private void OnCharacterListReceived(S_CharacterListResponse res)
    {
        _characters.Clear();
        if (res.Characters != null)
            _characters.AddRange(res.Characters);

        if (_characters.Count == 0)
            ClientLogger.Warn(ClientLogger.Recv, "보유 캐릭터가 0마리다 — 작업슬롯 배치가 전부 거절된다(서버 지급 로직 확인)");

        CharactersChanged?.Invoke();
    }

    // 작업슬롯 스냅샷 — 캐시 교체 후 이벤트 발행
    // ★ 로그인 시 자동으로 1회 수신. 슬롯 조회 요청 패킷이 없어 전체 스냅샷은 이때뿐이고,
    //   이후에는 배치 응답으로 한 칸씩만 갱신된다.
    private void OnWorkStationSlotsReceived(S_WorkStationSlotsResponse res)
    {
        _workStationSlots.Clear();
        if (res.Slots != null)
            _workStationSlots.AddRange(res.Slots);

        WorkStationSlotsChanged?.Invoke();
    }

    // 슬롯 배치 응답 — 변경된 슬롯 하나만 오므로 캐시에 병합한다.
    // 스냅샷을 다시 받지 않으므로 여기서 반영하지 않으면 캐시가 서버 상태와 어긋난다.
    //
    // ※ 배치였는지 해제였는지는 알리지 않는다 — 실패 응답에는 슬롯이 실려 오지 않아(Slot=null)
    //   수신만으로는 구분할 수 없다. 그건 요청을 보낸 UI가 안다.
    private void OnWorkStationAssigned(S_WorkStationAssignResponse res)
    {
        var  changed = res.Slot;
        bool success = res.Result == EResultCode.Ok;

        // 실패 사유는 결과 코드에만 들어 있다(미보유 캐릭터·적성 0·없는 슬롯…).
        // 여기서 남기지 않으면 UI는 "실패했다"까지만 알고 왜인지는 서버 콘솔을 봐야 안다.
        if (!success)
            ClientLogger.Warn(ClientLogger.Recv, $"작업슬롯 변경 실패 — 결과={res.Result}");

        if (success && changed != null)
        {
            int index = _workStationSlots.FindIndex(slot => slot.SlotIndex == changed.SlotIndex);
            if (index >= 0)
                _workStationSlots[index] = changed;
            else
                _workStationSlots.Add(changed);

            WorkStationSlotsChanged?.Invoke();
        }

        WorkStationAssignCompleted?.Invoke(success, res.Result);
    }

    // 채취 결과 푸시 — 판정이 완성될 때마다 요청 없이 도착한다(수확이 없으면 오지 않는다).
    private void OnGatherResultReceived(S_GatherResultResponse res)
    {
        ApplyItemChanges(res.ItemChanges);
        GatherResultReceived?.Invoke(res);
    }

    // 슬롯 1칸 동기화 — 정산·속도 변경 후 도착한다. 카운트다운 기준점이 매번 교정된다.
    private void OnWorkStationSlotSynced(S_WorkStationSlotSyncResponse res)
    {
        var synced = res.Slot;
        if (synced == null)
            return;

        int index = _workStationSlots.FindIndex(slot => slot.SlotIndex == synced.SlotIndex);
        if (index >= 0)
            _workStationSlots[index] = synced;
        else
            _workStationSlots.Add(synced);

        WorkStationSlotsChanged?.Invoke();
    }

    // 아이템 증감 푸시 (가챠·즉시 지급 등)
    private void OnItemUpdated(S_UpdateItemResponse res)
    {
        ApplyItemChanges(res.ItemChangeInfos);
    }

    // 재화 통지 — 스냅샷과 변경이 같은 패킷이라 종류별로 덮어쓰기만 하면 된다.
    private void OnCurrencyReceived(S_CurrencyResponse res)
    {
        if (res.Currencies == null)
            return;

        foreach (var currency in res.Currencies)
            _currencies[currency.CurrencyType] = currency.Amount;

        CurrencyChanged?.Invoke();
    }

    #endregion
    
    #region 인벤토리 반영

    /// <summary>
    /// 아이템 변경분을 인벤토리 캐시에 반영하고 'InventoryChanged'를 발행한다.
    /// Count는 델타가 아니라 갱신 후 누적 총량이다 — 더하지 말고 덮어쓴다
    /// ('PacketInfo.ItemChangeInfo' 주석).
    ///
    /// 아이템이 늘어나는 모든 경로가 이 하나를 쓴다(채취·즉시 지급·가챠).
    /// 수량은 전부 서버가 정한 값이고 클라는 계산하지 않는다 — 경로가 늘어도 규칙은 그대로다.
    /// </summary>
    private void ApplyItemChanges(List<ItemChangeInfo>? changes)
    {
        if (changes == null || changes.Count == 0)
            return;

        foreach (var change in changes)
        {
            int index = _inventory.FindIndex(item => item.ItemId == change.ItemId);

            if (index >= 0)
                _inventory[index].Count = change.Count;
            else
                _inventory.Add(new ItemInfo { ItemId = change.ItemId, Count = change.Count });
        }

        InventoryChanged?.Invoke();
    }

    #endregion
}

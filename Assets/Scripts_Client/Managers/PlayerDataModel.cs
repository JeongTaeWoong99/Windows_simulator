using System;
using System.Collections.Generic;
using MikaNetwork;
using MikaProtocol;
using UnityEngine;

// UnityEngine에도 CharacterInfo(폰트 글리프 정보)가 있어 이름이 겹친다. 우리가 쓰는 건 패킷 쪽이다.
using CharacterInfo = MikaProtocol.CharacterInfo;

// 서버가 밀어준 내 계정 상태를 들고 있는 수신 전담 매니저 (서비스 로케이터 등록).
// 수신 진입점('ServerPacketHandler')을 구독해 캐시를 채우고 가공된 변경 이벤트를 발행한다 —
// UI는 서버 폴더가 아니라 이 매니저만 구독하면 된다.
//
// ⚠️ 송신은 하지 않는다 — 요청은 그 요청을 일으킨 Presenter가 직접 보낸다.
// 'GameDataLoader'와의 역할 구분(고정 테이블 ↔ 내 계정 상태)은 'Managers 규칙.md' 2장,
// 패킷별 수량 규약은 '서버 동작 이해.md', 로그인 1회 수신 세트는 '패킷 레퍼런스.md' 참조.
public class PlayerDataModel : MonoService<PlayerDataModel>
{
    // ─── 상태 캐시 ───
    private readonly List<ItemInfo>            _inventory        = new List<ItemInfo>();
    private readonly List<WorkStationSlotInfo> _workStationSlots = new List<WorkStationSlotInfo>();
    private readonly List<CharacterInfo>       _characters       = new List<CharacterInfo>();
    private readonly List<EquipInfo>           _equips           = new List<EquipInfo>();
    private readonly HashSet<int>              _unlockedTids     = new HashSet<int>(); // 열린 해금 — 영구라 줄지 않는다

    // ─── 내부 상태 ───
    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    public long SessionId  { get; private set; }
    public bool IsLoggedIn { get; private set; }

    // 로그인에 쓴 Id. 서버가 닉네임을 돌려주지 않아 표시에 대신 쓰는 임시값이다
    // — 닉네임 패킷이 생기면 이 값과 'SetLoginId'는 함께 사라진다.
    public string LoginId { get; private set; } = "";

    public IReadOnlyList<ItemInfo>            Inventory        => _inventory;
    public IReadOnlyList<WorkStationSlotInfo> WorkStationSlots => _workStationSlots;

    // 아이템 하나의 보유 수량. 없으면 0이다.
    //
    // 서버가 0개가 된 아이템도 목록에 실어 보내므로(감소도 같은 경로로 온다) 캐시에 남아 있다.
    // 판매 카트와 창고 격자가 **같은 판정을 봐야 한다** — 각자 'Inventory'를 훑으면
    // 한쪽만 고쳐졌을 때 화면과 요청이 다른 수량을 말한다.
    public int GetItemCount(int itemId)
    {
        foreach (var item in _inventory)
        {
            if (item.ItemId == itemId)
            {
                return item.Count;
            }
        }

        return 0;
    }

    // 내가 가진 캐릭터들.
    //
    // ⚠️ 'CharacterInfo.CharacterId'는 개체 번호이고, 'CharacterInfo.CharacterTid'가
    // 캐릭터 종류다. 같은 캐릭터를 여러 마리 가질 수 있어서 종류로는 하나를 특정하지 못한다.
    // 슬롯 배치에 넣을 값은 개체 번호(CharacterId)다 — 종류(1001 같은 TID)를 보내면
    // 서버가 'CharacterNotOwned'로 거절한다. 이름·적성은 TID로 테이블에서 읽는다.
    public IReadOnlyList<CharacterInfo> Characters => _characters;

    // 첫 번째 보유 캐릭터의 개체 번호. 없으면 0(= 서버에선 배치 해제로 읽힌다).
    // 캐릭터 선택 UI가 생기기 전까지 테스트 버튼들이 쓰는 임시 통로다.
    public long FirstCharacterId => _characters.Count > 0 ? _characters[0].CharacterId : 0L;

    // 캐릭터 개체 번호로 표시 이름을 얻는다.
    //
    // 이름은 종류(TID)에 달린 값이라 개체 번호만으로는 못 찾는다 — 보유 목록에서 TID를 거쳐 간다.
    // 'GameDataLoader.GetCharacterName'에 개체 번호를 그대로 넣으면 '?#2'가 나온다.
    public string GetCharacterName(long characterId)
    {
        foreach (var character in _characters)
        {
            if (character.CharacterId == characterId)
            {
                return GameDataLoader.GetCharacterName(character.CharacterTid);
            }
        }

        return $"?#{characterId}"; // 아직 목록을 못 받았거나 서버가 모르는 개체
    }

    // 캐릭터 개체 번호로 레벨을 얻는다. 모르는 개체면 0.
    public int GetCharacterLevel(long characterId)
    {
        foreach (var character in _characters)
        {
            if (character.CharacterId == characterId)
            {
                return character.Level;
            }
        }

        return 0;
    }

    // 캐릭터 개체 번호로 경험치 진행률(0~1)을 얻는다. 만렙이면 1, 모르는 개체면 0.
    //
    // 분모는 **다음 레벨의 필요치**('RequiredExp(Level + 1)')다 — 'Exp'가 누적이 아니라
    // 현재 레벨에서 쌓은 양이라서다(레벨업하면 서버가 필요치를 뺀 나머지로 줄여 보낸다).
    public float GetExpProgress(long characterId)
    {
        foreach (var character in _characters)
        {
            if (character.CharacterId != characterId)
            {
                continue;
            }

            bool hasNext = GameDataLoader.TryGetRequiredExp(character.Level + 1, out int required);

            if (!hasNext || required <= 0)
            {
                return 1f; // 만렙 — 다음 행이 없다
            }

            return Mathf.Clamp01((float)character.Exp / required);
        }

        return 0f;
    }

    // 캐릭터 개체 번호로 종류(TID)를 얻는다. 모르는 개체면 0.
    //
    // 등급처럼 종류에 달린 값을 개체 번호만 들고 있는 화면이 찾을 때 거쳐 간다('GetCharacterName'과 같은 이유).
    public int GetCharacterTid(long characterId)
    {
        foreach (var character in _characters)
        {
            if (character.CharacterId == characterId)
            {
                return character.CharacterTid;
            }
        }

        return 0;
    }

    // 캐릭터 개체 번호로 그 산업의 적성(0~10)을 얻는다. 모르는 개체·산업이면 0
    // (= 그 산업을 다루지 못한다. 서버가 배치를 'NoAptitude'로 거절한다).
    // ⚠️ 'CharacterTable'을 직접 읽지 않는다 — 값의 주인은 서버다
    // (근거는 '패킷 레퍼런스.md' 적성 절).
    public byte GetAptitude(long characterId, EIndustryType industry)
    {
        foreach (var character in _characters)
        {
            if (character.CharacterId != characterId)
            {
                continue;
            }

            foreach (var aptitude in character.Aptitudes)
            {
                if (aptitude.Industry == industry)
                {
                    return aptitude.Value;
                }
            }

            return 0; // 1차 산업 5종이 전부 실려 오므로 여기 오면 산업 쪽이 이상한 것이다
        }

        return 0;
    }

    // 이 캐릭터가 배치된 작업슬롯 번호. 배치돼 있지 않으면 -1.
    //
    // 창고 캐릭터 탭과 작업슬롯 선택 화면이 **같은 판정을 봐야 한다** — 각자 'WorkStationSlots'를
    // 훑으면 한쪽만 고쳐졌을 때 두 화면이 다른 말을 한다. 그래서 여기 한 번 두고 양쪽이 부른다.
    // ⚠️ 리스트 순번이 아니라 'SlotIndex'를 돌려준다 — 열린 슬롯만 실려 오므로 둘이 어긋난다.
    public int FindSlotIndexOf(long characterId)
    {
        if (characterId == 0L)
        {
            return -1; // 0은 '비어 있음'이라 빈 슬롯 전부와 맞아 버린다
        }

        foreach (var slot in _workStationSlots)
        {
            if (slot.CharacterId == characterId)
            {
                return slot.SlotIndex;
            }
        }

        return -1;
    }

    // 내가 가진 장비들. 창고에 있든 캐릭터가 끼고 있든 전부 여기 있다.
    //
    // ⚠️ 캐릭터와 같은 모양이다 — 'EquipId'가 개체 번호이고 'EquipTid'가 종류다.
    //   이름·등급·효과는 TID로 'EquipTable'에서 읽는다.
    // ※ 장착해도 목록에서 빠지지 않는다 — 서버가 장착 시 'SlotPosition'(창고 칸)을 그대로 두므로
    //   창고에 남아 있고, 'IsEquipped'로 구분한다.
    public IReadOnlyList<EquipInfo> Equips => _equips;

    // 장비 개체 번호로 종류(TID)를 얻는다. 모르는 개체면 0.
    public int GetEquipTid(long equipId)
    {
        foreach (var equip in _equips)
        {
            if (equip.EquipId == equipId)
            {
                return equip.EquipTid;
            }
        }

        return 0;
    }

    // 이 장비를 캐릭터가 끼고 있는가. 창고에 있으면(또는 모르는 개체면) false.
    // ※ 서버의 'Equip.IsEquipped'와 같은 판정이다 — 'EquippedCharacterId = 0'이 창고다.
    public bool IsEquipped(long equipId)
    {
        foreach (var equip in _equips)
        {
            if (equip.EquipId == equipId)
            {
                return equip.EquippedCharacterId != 0L;
            }
        }

        return false;
    }

    // 해금('UnlockTable')이 열렸는가. 'UnlockTID = 0'은 조건이 없는 것이라 항상 열려 있다.
    // 서버 'User.IsUnlocked'와 같은 모양이다 — 콘텐츠는 "내 UnlockTID가 열렸나"만 묻는다.
    // 조건(골드·선행·레벨)을 여기서 다시 보지 않는다 — 목록에 있다는 것이 서버가 판정해 열어 줬다는 뜻이다.
    public bool IsUnlocked(int unlockTid)
    {
        return unlockTid == 0 || _unlockedTids.Contains(unlockTid);
    }

    // 재화 보유량. 종류마다 필드다 — 서버가 행이 아니라 컬럼으로 싣기 때문이다
    // (DB 't_user_currency'도 같은 축이다). 재화가 늘면 패킷에 필드가 하나 늘고 여기도 하나 는다.
    //
    // ※ 예전에는 'Dictionary<byte, long>'에 'CurrencyType'을 키로 담았다. 그 축이 없어졌는데
    //   사전만 채우는 식으로 두면 패킷과 캐시가 다시 어긋난다 — 사전째 걷어냈다.
    public long Gold { get; private set; }  // 무료 재화
    public long Dia  { get; private set; }  // 유료 재화. ⏸ 지급·차감 경로가 아직 없어 늘 0이다

    // 계정 레벨 — 캐릭터가 얻은 경험치가 그대로 계정 경험치가 된다(캐릭터가 만렙이어도 계정은 자란다).
    // 재화와 같은 관례로 스냅샷이 통째로 온다 — 로그인 직후 · 경험치가 오를 때 · 특성 포인트를 쓸 때.
    //
    // ※ 특성을 찍은 기록은 여기 없다 — 열린 해금 목록('IsUnlocked')으로 온다.
    //   노드 TID = UnlockTID라 특성 전용 보유 목록이 따로 없다.
    public int  AccountLevel { get; private set; } = 1;
    public long AccountExp   { get; private set; }  // 현재 레벨에서 쌓은 양. 곡선이 캐릭터의 8배라 long이다
    public int  TraitPoint   { get; private set; }  // 남은(안 쓴) 특성 포인트

    // 로그인 요청에 쓴 Id를 표시용으로 기억한다 (로그인을 보낸 UI가 호출).
    //
    // 이 매니저는 송신을 모르므로 "무엇으로 로그인했는가"를 스스로 알 수 없다.
    // 서버가 닉네임을 돌려주기 시작하면 'LoginId'와 함께 지운다.
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
    public event Action?                         EquipsChanged;              // 보유 장비 캐시 갱신됨 (지급·장착·해제 전부)
    public event Action<bool, EResultCode>?      EquipCompleted;             // 장착·해제 완료 (성공 여부·결과 코드)
    public event Action<bool, EResultCode>?      WorkStationAssignCompleted; // 슬롯 변경 완료 (성공 여부·결과 코드)
    public event Action?                         WorkStationSlotsChanged;    // 슬롯 캐시 갱신됨
    public event Action<S_GatherResultResponse>? GatherResultReceived;       // 채취 결과 푸시 도착
    public event Action?                         CurrencyChanged;            // 재화 캐시 갱신됨

    public event Action<long>?        ItemSellCompleted; // 판매 성공 (이번에 번 골드 — 잔액은 'CurrencyChanged'로 따로 온다)
    public event Action<EResultCode>? ItemSellFailed;    // 판매 실패 (거절 사유)

    public event Action<List<GachaRewardInfo>>? ItemUseCompleted; // 아이템 사용 성공 (얻은 보상 목록 — 지금은 상자 개봉뿐)
    public event Action<EResultCode>?           ItemUseFailed;    // 아이템 사용 실패 (거절 사유)

    public event Action?                   UnlocksChanged;  // 열린 해금 목록 갱신됨 (로그인 목록 · 해금 성공 후)
    public event Action<bool, EResultCode>? UnlockCompleted; // 해금 결과 (성공 여부·결과 코드)

    public event Action?                    AccountLevelChanged; // 계정 레벨·경험치·특성 포인트 갱신됨
    public event Action<bool, EResultCode>? TraitLearnCompleted; // 특성 찍기 결과 (성공 여부·결과 코드)

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
        {
            Subscribe();
        }
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
        {
            return;
        }

        _isSubscribed = true;

        ServerPacketHandler.LoginResponded           += OnLoginResponded;
        ServerPacketHandler.InventoryReceived        += OnInventoryReceived;
        ServerPacketHandler.GachaDrawn               += OnGachaDrawn;
        ServerPacketHandler.CharacterListReceived    += OnCharacterListReceived;
        ServerPacketHandler.CharacterSynced          += OnCharacterSynced;
        ServerPacketHandler.EquipListReceived        += OnEquipListReceived;
        ServerPacketHandler.EquipSynced              += OnEquipSynced;
        ServerPacketHandler.EquipResponded           += OnEquipResponded;
        ServerPacketHandler.WorkStationAssigned      += OnWorkStationAssigned;
        ServerPacketHandler.WorkStationSlotsReceived += OnWorkStationSlotsReceived;
        ServerPacketHandler.GatherResultReceived     += OnGatherResultReceived;
        ServerPacketHandler.WorkStationSlotSynced    += OnWorkStationSlotSynced;
        ServerPacketHandler.CurrencyReceived         += OnCurrencyReceived;
        ServerPacketHandler.ItemUpdated              += OnItemUpdated;
        ServerPacketHandler.ItemSold                 += OnItemSold;
        ServerPacketHandler.ItemUsed                 += OnItemUsed;
        ServerPacketHandler.UnlockListReceived       += OnUnlockListReceived;
        ServerPacketHandler.UnlockResponded          += OnUnlockResponded;
        ServerPacketHandler.AccountLevelReceived     += OnAccountLevelReceived;
        ServerPacketHandler.UserTraitLearnResponded  += OnUserTraitLearnResponded;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed = false;

        ServerPacketHandler.LoginResponded           -= OnLoginResponded;
        ServerPacketHandler.InventoryReceived        -= OnInventoryReceived;
        ServerPacketHandler.GachaDrawn               -= OnGachaDrawn;
        ServerPacketHandler.CharacterListReceived    -= OnCharacterListReceived;
        ServerPacketHandler.CharacterSynced          -= OnCharacterSynced;
        ServerPacketHandler.EquipListReceived        -= OnEquipListReceived;
        ServerPacketHandler.EquipSynced              -= OnEquipSynced;
        ServerPacketHandler.EquipResponded           -= OnEquipResponded;
        ServerPacketHandler.WorkStationAssigned      -= OnWorkStationAssigned;
        ServerPacketHandler.WorkStationSlotsReceived -= OnWorkStationSlotsReceived;
        ServerPacketHandler.GatherResultReceived     -= OnGatherResultReceived;
        ServerPacketHandler.WorkStationSlotSynced    -= OnWorkStationSlotSynced;
        ServerPacketHandler.CurrencyReceived         -= OnCurrencyReceived;
        ServerPacketHandler.ItemUpdated              -= OnItemUpdated;
        ServerPacketHandler.ItemSold                 -= OnItemSold;
        ServerPacketHandler.ItemUsed                 -= OnItemUsed;
        ServerPacketHandler.UnlockListReceived       -= OnUnlockListReceived;
        ServerPacketHandler.UnlockResponded          -= OnUnlockResponded;
        ServerPacketHandler.AccountLevelReceived     -= OnAccountLevelReceived;
        ServerPacketHandler.UserTraitLearnResponded  -= OnUserTraitLearnResponded;
    }

    #endregion

    #region 응답 처리 (ServerPacketHandler 구독)

    // 로그인 응답 — 세션 상태 갱신 후 이벤트 발행
    private void OnLoginResponded(S_LoginResponse res)
    {
        IsLoggedIn = res.Result == EResultCode.Ok;
        SessionId  = res.SessionId;

        if (!IsLoggedIn)
        {
            ClientLogger.Warn(ClientLogger.Recv, $"로그인 실패 — 결과={res.Result}");
        }

        LoginCompleted?.Invoke(IsLoggedIn, res.Result);
    }

    // 인벤토리 스냅샷 — 캐시 교체 후 이벤트 발행
    // ★ 로그인 시 자동으로 1회 수신.
    private void OnInventoryReceived(S_InventoryResponse res)
    {
        _inventory.Clear();
        if (res.Items != null)
        {
            _inventory.AddRange(res.Items);
        }

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
        {
            _characters.AddRange(res.Characters);
        }

        if (_characters.Count == 0)
        {
            ClientLogger.Warn(ClientLogger.Recv, "보유 캐릭터가 0마리다 — 작업슬롯 배치가 전부 거절된다(서버 지급 로직 확인)");
        }

        CharactersChanged?.Invoke();
    }

    // 캐릭터 1개체 동기화 — 판정 정산으로 레벨·경험치가 바뀌면 요청 없이 도착한다.
    //
    // ★ 증감이 아니라 확정값이라 통째로 **교체**한다 (레벨업하면 'Exp'가 줄어드는 것도 그대로 받는다).
    // ⚠️ 목록에 없는 개체는 추가하지 않는다 — 보유 목록의 원본은 로그인 스냅샷이다.
    //   여기서 넣기 시작하면 스냅샷과 푸시 중 무엇이 진실인지 흐려진다.
    private void OnCharacterSynced(S_CharacterSyncResponse res)
    {
        var synced = res.Character;

        if (synced == null)
        {
            return;
        }

        int index = _characters.FindIndex(character => character.CharacterId == synced.CharacterId);

        if (index < 0)
        {
            ClientLogger.Warn(ClientLogger.Recv, $"보유 목록에 없는 캐릭터 동기화 — 개체={synced.CharacterId} (무시)");

            return;
        }

        CharacterInfo previous = _characters[index];

        _characters[index] = synced;

        if (synced.Level > previous.Level)
        {
            // TODO: 레벨업 연출 — 연출 리소스가 오면 여기서 알린다 (T-052 🎨).
            //       이벤트(예: 'CharacterLeveledUp(long characterId, int level)')를 열고
            //       창고 칸 반짝임·토스트가 구독한다. 한 정산에 여러 레벨이 오를 수 있어 'previous.Level'도 함께 넘길 것.
            ClientLogger.Info(ClientLogger.Recv,
                $"캐릭터 레벨업 — 개체={synced.CharacterId} Lv.{previous.Level} → Lv.{synced.Level}");
        }

        CharactersChanged?.Invoke();
    }

    // 보유 장비 스냅샷 — 캐시 교체 후 이벤트 발행
    // ★ 로그인 시 자동으로 1회 수신(캐릭터 목록 뒤·작업슬롯 앞). 창고에 있든 끼고 있든 전부 실려 온다.
    private void OnEquipListReceived(S_EquipListResponse res)
    {
        _equips.Clear();
        _equips.AddRange(res.Equips);

        EquipsChanged?.Invoke();
    }

    // 바뀐 장비 개체들 — 지급·장착·해제·자동 이동. 'EquipId'로 찾아 통째로 교체한다(확정값이다).
    //
    // ⚠️ 캐릭터('OnCharacterSynced')와 **반대로, 목록에 없는 개체는 추가한다.**
    //   캐릭터는 보유 목록의 원본이 로그인 스냅샷 하나뿐이라 푸시로 늘리지 않지만,
    //   장비는 이 패킷이 **지급 경로 그 자체**다(치트·가챠로 새 개체가 여기로 들어온다).
    private void OnEquipSynced(S_EquipSyncResponse res)
    {
        foreach (var synced in res.Equips)
        {
            int index = _equips.FindIndex(equip => equip.EquipId == synced.EquipId);

            if (index >= 0)
            {
                _equips[index] = synced;
            }
            else
            {
                _equips.Add(synced);
            }
        }

        EquipsChanged?.Invoke();
    }

    // 장착·해제 응답 — 결과만 알린다.
    //
    // ※ **바뀐 값은 여기로 오지 않는다.** 개체는 'S_EquipSyncResponse'가, 슬롯 속도는
    //   'S_WorkStationSlotSyncResponse'가 따로 싣고 온다 — 이 응답은 "받아들여졌는가"뿐이다.
    //   그래서 화면은 결과 문구에만 이걸 쓰고, 그리는 것은 'EquipsChanged'로 한다.
    private void OnEquipResponded(S_EquipResponse res)
    {
        EquipCompleted?.Invoke(res.Result == EResultCode.Ok, res.Result);
    }

    // 작업슬롯 스냅샷 — 캐시 교체 후 이벤트 발행
    // ★ 로그인 시 자동으로 1회 수신. 슬롯 조회 요청 패킷이 없어 전체 스냅샷은 이때뿐이고,
    //   이후에는 배치 응답으로 한 칸씩만 갱신된다.
    private void OnWorkStationSlotsReceived(S_WorkStationSlotsResponse res)
    {
        _workStationSlots.Clear();
        if (res.Slots != null)
        {
            _workStationSlots.AddRange(res.Slots);
        }

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
        {
            ClientLogger.Warn(ClientLogger.Recv, $"작업슬롯 변경 실패 — 결과={res.Result}");
        }

        if (success && changed != null)
        {
            int index = _workStationSlots.FindIndex(slot => slot.SlotIndex == changed.SlotIndex);

            if (index >= 0)
            {
                _workStationSlots[index] = changed;
            }
            else
            {
                _workStationSlots.Add(changed);
            }

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
        {
            return;
        }

        int index = _workStationSlots.FindIndex(slot => slot.SlotIndex == synced.SlotIndex);

        if (index >= 0)
        {
            _workStationSlots[index] = synced;
        }
        else
        {
            _workStationSlots.Add(synced);
        }

        WorkStationSlotsChanged?.Invoke();
    }

    // 아이템 증감 푸시 (가챠·즉시 지급 등)
    private void OnItemUpdated(S_UpdateItemResponse res)
    {
        ApplyItemChanges(res.ItemChangeInfos);
    }

    // 판매 응답 — 인벤토리 반영 후 결과 이벤트 발행. 'OnGachaDrawn'과 같은 모양이다.
    //
    // ★ 골드를 여기서 건드리지 않는다 — 'GainedGold'는 이번에 번 금액이고,
    //   확정 잔액은 뒤이어 오는 'S_CurrencyResponse'가 내려준다. 여기서 더하면 두 번 오른다.
    // ★ 전부 되거나 전혀 안 된다 — 실패면 인벤토리도 그대로다.
    private void OnItemSold(S_ItemSellResponse res)
    {
        if (res.Result != EResultCode.Ok)
        {
            ClientLogger.Warn(ClientLogger.Recv, $"판매 실패 — 결과={res.Result}");
            ItemSellFailed?.Invoke(res.Result);

            return;
        }

        ApplyItemChanges(res.ItemChangeInfos);
        ItemSellCompleted?.Invoke(res.GainedGold);
    }

    // 아이템 사용 응답 — 인벤토리 반영 후 보상 이벤트 발행. 'OnGachaDrawn'과 같은 모양이다.
    //
    // ★ 한 패킷에 두 가지가 실려 오는 것도 가챠와 같다.
    //   ItemChangeInfos = 갱신 후 누적 총량(쓴 상자의 차감과 받은 아이템) → 인벤토리 반영
    //   Rewards         = 이번에 얻은 개별 항목(델타) → 연출 전용
    // ★ 골드와 장비는 여기서 건드리지 않는다 — 골드는 'S_CurrencyResponse',
    //   장비 개체는 'S_EquipSyncResponse'로 따로 온다. 보상 목록의 골드는 **보여 주기 위한 값**이다.
    private void OnItemUsed(S_ItemUseResponse res)
    {
        if (res.Result != EResultCode.Ok)
        {
            ClientLogger.Warn(ClientLogger.Recv, $"아이템 사용 실패 — TID={res.ItemTID}, 결과={res.Result}");
            ItemUseFailed?.Invoke(res.Result);

            return;
        }

        ApplyItemChanges(res.ItemChangeInfos);
        ItemUseCompleted?.Invoke(res.Rewards ?? new List<GachaRewardInfo>());
    }

    // 재화 통지 — 스냅샷과 변경이 같은 패킷이라 덮어쓰기만 하면 된다.
    //
    // ★ 한쪽만 바뀌어도 서버는 둘 다 실어 보낸다. 값이 델타가 아니라 확정 잔액이라 안전하다.
    private void OnCurrencyReceived(S_CurrencyResponse res)
    {
        Gold = res.Gold;
        Dia  = res.Dia;

        CurrencyChanged?.Invoke();
    }

    // 열린 해금 전체 — 로그인 직후 1회. 스냅샷이라 비우고 채운다.
    // 서버가 슬롯 스냅샷보다 **먼저** 보내므로 목록 화면이 칸을 그릴 때는 이미 채워져 있다.
    private void OnUnlockListReceived(S_UnlockListResponse res)
    {
        _unlockedTids.Clear();
        _unlockedTids.UnionWith(res.UnlockTIDs);

        UnlocksChanged?.Invoke();
    }

    // 해금 결과 — 성공이면 목록에 더하고 알린다.
    //
    // ★ 내가 보낸 요청이 아니어도 온다(치트·퀘스트로 서버가 직접 연 경우). 그래서 목록 갱신은 요청 여부와
    //   상관없이 하고, "내 요청의 결과인가"는 받는 Presenter가 대기 핸들로 가린다.
    // ★ 골드·슬롯은 여기서 건드리지 않는다 — 차감 잔액은 'S_CurrencyResponse',
    //   새 칸은 'S_WorkStationSlotSyncResponse'가 뒤이어 온다('OnItemSold'와 같은 이유).
    private void OnUnlockResponded(S_UnlockResponse res)
    {
        bool success = res.Result == EResultCode.Ok;

        if (success)
        {
            _unlockedTids.Add(res.UnlockTID);
            UnlocksChanged?.Invoke();
        }
        else
        {
            ClientLogger.Warn(ClientLogger.Recv, $"해금 실패 — {res.UnlockTID}, 결과={res.Result}");
        }

        UnlockCompleted?.Invoke(success, res.Result);
    }

    // 계정 레벨 스냅샷 — 재화와 같은 관례로 통째로 덮어쓴다(델타가 아니다).
    // 로그인 직후 1회 + 경험치가 오를 때 + 특성 포인트를 쓸 때 온다.
    private void OnAccountLevelReceived(S_AccountLevelResponse res)
    {
        AccountLevel = res.Level;
        AccountExp   = res.Exp;
        TraitPoint   = res.TraitPoint;

        AccountLevelChanged?.Invoke();
    }

    // 특성 찍기 결과 — 결과 코드만 나른다.
    //
    // ★ 캐시를 여기서 건드리지 않는다. 찍힌 기록은 앞서 온 'S_UnlockResponse'가
    //   열린 해금 목록에 넣었고, 남은 포인트는 뒤따라 오는 'S_AccountLevelResponse'가 채운다
    //   ('OnUnlockResponded'가 골드를 건드리지 않는 것과 같은 이유).
    private void OnUserTraitLearnResponded(S_UserTraitLearnResponse res)
    {
        bool success = res.Result == EResultCode.Ok;

        if (!success)
        {
            ClientLogger.Warn(ClientLogger.Recv, $"특성 찍기 실패 — {res.UserTraitTID}, 결과={res.Result}");
        }

        TraitLearnCompleted?.Invoke(success, res.Result);
    }

    #endregion

    #region 인벤토리 반영

    // 아이템 변경분을 인벤토리 캐시에 반영하고 'InventoryChanged'를 발행한다.
    // Count는 델타가 아니라 갱신 후 누적 총량이다 — 더하지 말고 덮어쓴다
    // ('PacketInfo.ItemChangeInfo' 주석).
    //
    // 아이템이 늘어나는 모든 경로가 이 하나를 쓴다(채취·즉시 지급·가챠).
    // 수량은 전부 서버가 정한 값이고 클라는 계산하지 않는다 — 경로가 늘어도 규칙은 그대로다.
    private void ApplyItemChanges(List<ItemChangeInfo>? changes)
    {
        if (changes == null || changes.Count == 0)
        {
            return;
        }

        foreach (var change in changes)
        {
            int index = _inventory.FindIndex(item => item.ItemId == change.ItemId);

            if (index >= 0)
            {
                _inventory[index].Count = change.Count;
            }
            else
            {
                _inventory.Add(new ItemInfo { ItemId = change.ItemId, Count = change.Count });
            }
        }

        InventoryChanged?.Invoke();
    }

    #endregion
}

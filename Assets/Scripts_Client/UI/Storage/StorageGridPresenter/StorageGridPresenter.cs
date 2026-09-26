using System.Collections.Generic;
using MikaNetwork;
using MikaProtocol;
using UnityEngine;

// 창고의 칸 격자. 탭이 무엇이든 **같은 격자 하나**가 그린다 —
// 지금 켜진 탭의 공급자('StorageSlotSource')에게 칸 배치를 받아 그대로 옮긴다.
//
// ■ 왜 탭마다 격자를 두지 않는가
// 자원·캐릭터·장비의 격자·스크롤·레이아웃이 완전히 같다. 같아야 할 것을 세 벌로 두면
// 한쪽만 고쳐지고, 씬 오브젝트도 세 배가 된다('Storage 규칙.md'의 "탭이 달라도 격자는 하나다").
// 탭마다 다른 것은 **데이터와 표시 문구뿐**이라 그쪽만 공급자로 갈라 두었다.
//
// ■ 칸 프레임은 씬에 미리 깔려 있다
//   'Content' 아래의 'Slot (N)' 들이 프레임이고, 아이템 프리팹은 그 프레임의 자식으로
//   들어간다. Content 직속으로 만들면 프레임을 벗어나 레이아웃이 무너진다.
//   프레임은 코드가 만들지도 지우지도 않는다.
//
// ■ 격자는 자리를 정하지 않는다 — i번째 칸에 i번째 프레임을 쓸 뿐이다
//   어느 개체가 몇 번 칸인지도, 어디가 빈 칸인지도 공급자가 정한다('StorageSlotSource.Arrange').
//   [정렬]도 공급자 안에서 끝난다. 한때 격자가 'ItemId → 프레임'을 직접 붙들었는데,
//   그러면 처음 들어온 순서로 칸이 영구히 굳어 **정렬을 넣을 자리가 없었다.**
//
// ■ 배치·장착 중인 개체도 **창고에 남는다** (2026-09-25)
//   나가 있다고 목록에서 빼지 않는다 — 딤 처리 + '배' 마크로 구분하고, [정렬]에서만 맨 뒤로 민다.
//   나가 있는지는 공급자가 답한다('StorageSlotSource.IsAway') — 뜻이 탭마다 다르기 때문이다.
//
// ■ 빈 칸은 'Get'이 null로 답한다 (2026-09-25 · T-044)
//   장착·배치·판매로 개체가 빠져도 **그 칸은 비워 둔다** — 뒤의 것을 당겨 오지 않는다.
//   당겨 오면 장비 하나를 끼울 때마다 창고 전체가 한 칸씩 밀려 보던 자리를 잃는다.
//
// ■ 칸은 파괴하지 않고 풀로 되돌린다
//   탭을 오갈 때마다 200개를 만들고 부수면 상주 앱에서 GC가 쌓인다.
//   남는 칸은 'Clear()' 후 꺼 두었다가 다음 탭에서 다시 쓴다.
//
// ■ 칸의 표시 중 탭을 타는 것은 격자가 정한다
//   적성 스트립 · 레벨 배지 · 경험치 게이지는 캐릭터 탭에서만, 판매 담김 표시는 자원 탭에서만 켜진다.
//   '배' 마크는 캐릭터 탭에서 '배치 중', 장비 탭에서 '장착 중'으로 뜻이 갈린다 — 판정도 탭마다 다르다.
//   칸은 이 판단을 모른다 — 공급자가 만드는 완성값(`SlotData`)에 캐릭터 전용 필드를 끼우면
//   자원·가챠 칸까지 따라 두꺼워지므로, 탭을 아는 격자가 읽어서 넘긴다.
//
// ■ 우클릭 = 판매 목록에 담기 · 빼기 (자원 탭에서만)
//   칸('SlotView')은 우클릭을 이벤트로 던지기만 하고 무슨 뜻인지 모른다.
//   그것을 판매로 읽는 것이 여기다 — 서버 판매 패킷이 아이템 TID 축이라 캐릭터는 담을 수 없어서
//   자원 탭이 아니면 무시한다 (동선은 'Storage 규칙.md').
//
// ■ 좌클릭 = 상자 개봉 (자원 탭의 상자 칸에서만)
//   우클릭과 같은 구조다. 판매와 **입력 축을 나눠 쓰는 것**이 요점이다 —
//   한 조작에 두 뜻을 겹치면 눌러 보기 전에는 무엇이 일어날지 알 수 없다.
public class StorageGridPresenter : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("칸 프리팹 (SlotView 포함) — 자원·캐릭터 어느 탭이든 같은 칸이다. 빈 프레임 안에 생성된다")]
    private SlotView slotPrefab = null!;

    [SerializeField, Tooltip("칸 프레임(Slot)들이 들어 있는 부모 — Inventory Scroll View Panel > Viewport > Content")]
    private Transform slotParent = null!;

    // 씬에 깔린 칸 프레임들. 개수·순서가 고정이라 매번 훑지 않고 한 번만 모아 둔다.
    private readonly List<Transform> _frames = new List<Transform>();

    // 서버 창고 한도 — 서버 'User.StorageCapacity'와 같은 값이어야 한다(탭마다 따로 200칸).
    // ⚠️ 서버가 한도를 내려 주지 않아 사본을 둔다. 값의 거처가 정해지면 조회로 바꾼다(T-085).
    //   그 전까지는 프레임 수와 대조만 한다('CacheFrames') — 어긋나면 로그로 드러난다.
    private const int ServerStorageCapacity = 200;

    // 프레임 i 안에 만들어 둔 칸. 아직 안 만들었으면 null이고, 안 쓰는 동안에는 꺼 둔다.
    private readonly List<SlotView?> _views = new List<SlotView?>();

    // 캐릭터 칸에 넘길 적성 5종. 칸마다 새 배열을 만들지 않으려고 하나를 들고 재사용한다
    // — 칸이 받아 그리는 즉시 쓰임이 끝나므로 들고 있는 쪽이 하나여도 된다.
    private readonly byte[] _aptitudes = new byte[SlotView.AptitudeCount];

    // 적성 칸의 순서 = 산업. 'EIndustryType'의 None 제외 순서와 같고, 배치 화면의 산업 버튼도
    // 같은 순서로 만들어진다('WorkStationSelectPresenter.BuildIndustryList') — 두 화면이 맞아야 한다.
    // ※ 칸 툴팁의 적성 줄('CharacterSlotSource.BuildTooltip')도 이 순서를 탄다 — 스트립과 위아래가 같게 읽힌다.
    public static readonly EIndustryType[] StripIndustries =
    {
        EIndustryType.Farming,
        EIndustryType.Fishing,
        EIndustryType.Mining,
        EIndustryType.Logging,
        EIndustryType.Hunting,
    };

    // 탭별 공급자. 여기 없는 탭은 "아직 데이터가 없는 탭"이고, 탭 줄이 그 버튼을 잠근다.
    private readonly Dictionary<StorageTab, StorageSlotSource> _sources =
        new Dictionary<StorageTab, StorageSlotSource>();

    private StorageSlotSource? _current;

    // 지금 열린 탭. 우클릭을 판매로 읽어도 되는 탭인지 여기서 가른다.
    private StorageTab _currentTab = StorageTab.Resource;

    private PlayerDataModel   _data    = null!;
    private SellCartModel     _cart    = null!;
    private UIManager         _ui      = null!;
    private NetworkManager    _network = null!;
    private ServerWaitManager _wait    = null!;

    // 진행 중인 개봉 대기의 손잡이. 응답이 오면 결과를 보고하고, 무응답이면 스스로 타임아웃된다.
    private ServerWaitHandle? _waitHandle;

    private bool _isOpeningBox;   // 개봉 응답을 기다리는 중인가 — 연타로 두 번 나가면 상자가 두 번 빠진다
    private bool _isCartSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 이 탭의 칸을 팔 수 있나. 서버 판매 패킷이 아이템 TID 축이라 자원만 담긴다.
    private bool IsSellableTab => _currentTab == StorageTab.Resource;

    // 이 탭의 칸이 캐릭터인가. 적성 스트립·레벨 배지·경험치 게이지는 여기서만 켜진다.
    private bool IsCharacterTab => _currentTab == StorageTab.Character;

    // 이 탭의 'Key'가 아이템 TID인가. 상자 개봉은 여기서만 연다.
    //
    // ⚠️ 지금 'IsSellableTab'과 값이 같지만 **합치지 않는다.** 판매 축이 캐릭터·장비로 넓어지면
    //   ('C_ItemSellRequest'가 TID 축이라 서버 패킷이 먼저다) 그쪽이 true가 되면서 상자 개봉까지
    //   따라 열린다. 캐릭터·장비 탭의 'Key'는 **개체 PK**라 상자 TID와 우연히 겹칠 수 있다.
    private bool IsItemKeyTab => _currentTab == StorageTab.Resource;

    // 참조 확보 → 공급자 등록 (클라 공통 규약)
    private void Start()
    {
        EnsureInitialized();

        // 탭 줄이 먼저 돌아 이미 탭을 정해 줬으면 그 화면이 그려져 있다. 아직이면 빈 격자로 시작한다.
        if (_current == null)
        {
            Redraw();
        }
    }

    // 공급자 등록까지 끝났음을 보장한다 (자기 Start · 탭 줄이 먼저 물어볼 때).
    //
    // ⚠️ 'Start' 실행 순서는 보장되지 않는다 — 탭 줄이 먼저 돌면 공급자가 아직 비어 있어
    //   'HasSource'가 전부 false를 돌려주고 **모든 탭이 잠긴 채로 굳는다.**
    //   그래서 양쪽에서 부를 수 있게 열어 두고, 두 번 불려도 한 번만 돌게 막는다.
    // ※ 서비스 조회를 Awake에 두지 않는 이유는 그쪽은 등록 순서가 보장되지 않아서다(MonoService 주석).
    //   이 메서드는 언제나 Start 단계 이후에 불린다.
    public void EnsureInitialized()
    {
        if (_isReady)
        {
            return;
        }

        // 필수 참조 검증 — 미연결이면 여기서 멈춘다. 안 그러면 아이템이 처음 들어오는 순간
        // Instantiate에서 NRE가 나는데, 그때는 원인이 인스펙터라는 게 드러나지 않는다.
        this.RequireRef(slotPrefab, nameof(slotPrefab));
        this.RequireRef(slotParent, nameof(slotParent));

        CacheFrames();

        var data = Services.Get<PlayerDataModel>();

        _data    = data;
        _cart    = Services.Get<SellCartModel>();
        _ui      = Services.Get<UIManager>();
        _network = NetworkManager.Instance;
        _wait    = Services.Get<ServerWaitManager>();

        // ★ 탭을 하나 채우는 일은 여기 한 줄로 끝난다 — 공급자를 만들어 등록하면
        //   전환·잠금·격자는 그대로다 ('Storage 규칙.md'의 "탭 하나를 채우는 절차").
        //
        // ⏸ Trait — 기획은 있으나 서버 구현·패킷이 없다 (T-043).
        _sources.Add(StorageTab.Resource,  new ResourceSlotSource(data));
        _sources.Add(StorageTab.Character, new CharacterSlotSource(data));
        _sources.Add(StorageTab.Equipment, new EquipSlotSource(data));

        // ★ 끝까지 왔을 때만 세운다 — 위에서 예외가 나면(참조 미연결·서비스 미등록) 플래그가
        //   안 켜져 다음 호출이 다시 시도하고, 원인도 매번 같은 예외로 드러난다.
        //   먼저 세우면 초기화가 중단됐는데도 격자가 조용히 빈 채로 굳는다.
        _isReady = true;

        // ※ 로그인은 창고가 닫혀 있어도 알아야 해서 켜고 끌 때 풀지 않는다 — 파괴될 때만 푼다.
        _data.LoginCompleted += OnLoginCompleted;

        // ※ 개봉 결과도 같은 이유로 켜고 끌 때 풀지 않는다 — 응답이 늦게 오는 사이 창고를 닫으면
        //   대기 손잡이가 영영 안 닫혀 로딩이 남는다.
        _data.ItemUseCompleted += OnItemUseCompleted;
        _data.ItemUseFailed    += OnItemUseFailed;

        // ※ 카트 구독은 여기서 시작한다 — 'OnEnable'은 'EnsureInitialized'보다 먼저 돌 수 있어
        //   (탭 줄의 Start가 우리를 깨우는 경로) 거기에만 두면 첫 판이 구독을 놓친다.
        SubscribeCart();
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    //
    // ★ 재구독만으로는 부족하다 — 창고를 닫아 둔 사이 채취·가챠로 수량이 바뀌었을 수 있다.
    //   'Subscribe'가 목록을 다시 채우므로 여기서는 다시 그리기만 하면 맞는다.
    private void OnEnable()
    {
        if (!_isReady)
        {
            return;
        }

        SubscribeCart();

        if (_current == null)
        {
            return;
        }

        _current.Subscribe();
        Redraw();
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        UnsubscribeCart();

        if (_current == null)
        {
            return;
        }

        _current.Unsubscribe();
    }

    // 로그인 구독 해제 (Unity 메시지)
    private void OnDestroy()
    {
        if (!_isReady)
        {
            return;
        }

        _data.LoginCompleted   -= OnLoginCompleted;
        _data.ItemUseCompleted -= OnItemUseCompleted;
        _data.ItemUseFailed    -= OnItemUseFailed;
    }

    #region 구독

    // 판매 목록 변경 구독 (EnsureInitialized · OnEnable에서 호출).
    //
    // 담김 표시는 칸이 아니라 카트가 주인이다 — 격자가 자체 플래그를 들면 정보 칸에서
    // 뺐을 때 표시만 남는다.
    private void SubscribeCart()
    {
        if (_isCartSubscribed)
        {
            return;
        }

        _isCartSubscribed = true;
        _cart.Changed    += Redraw;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void UnsubscribeCart()
    {
        if (!_isCartSubscribed)
        {
            return;
        }

        _isCartSubscribed = false;
        _cart.Changed    -= Redraw;
    }

    #endregion

    #region 탭 전환

    // 이 탭의 데이터가 준비돼 있나 (탭 줄이 버튼을 잠글지 정할 때 호출).
    //
    // 잠금을 탭마다 손으로 켜고 끄지 않는 이유 — 공급자가 생기면 그 탭은 저절로 열려야 한다.
    // 두 곳에 적어 두면 공급자를 붙이고도 버튼이 잠긴 채 남는다.
    public bool HasSource(StorageTab tab)
    {
        EnsureInitialized();

        return _sources.ContainsKey(tab);
    }

    // 이 탭의 내용으로 갈아 끼운다 ('StorageTabPresenter'가 호출).
    public void ShowTab(StorageTab tab)
    {
        EnsureInitialized();

        // ★ 아래 조기 반환보다 먼저 기억한다 — 공급자가 없는 탭끼리 오가면
        //   'next'가 둘 다 null이라 조기 반환에 걸리는데, 그때도 지금 탭은 바뀌어 있다.
        _currentTab = tab;

        _sources.TryGetValue(tab, out StorageSlotSource? next);

        // 공급자가 없는 탭에서는 격자가 통째로 물러난다 — 그 자리는 전용 화면이 쓴다(특성 탭).
        // ★ 조기 반환보다 먼저 한다. 그리고 칸 200개를 하나씩 끄지 않는다 —
        //   프레임을 개별로 토글하면 탭을 옮길 때마다 레이아웃 리빌드가 200번 돈다
        //   ('Storage 규칙.md'). 격자 오브젝트 하나만 끄면 리빌드는 다시 켤 때 한 번이다.
        gameObject.SetActive(next != null);

        if (_current == next)
        {
            return;
        }

        if (_current != null)
        {
            _current.Unsubscribe();
            _current.Changed -= Redraw;
        }

        _current = next;

        if (_current != null)
        {
            _current.Changed += Redraw;
            _current.Subscribe(); // 목록도 여기서 채워진다
        }

        Redraw();
    }

    #endregion

    #region 정렬

    // 지금 탭을 규칙대로 줄 세운다 ('StorageToolPresenter'의 화살표 버튼이 호출).
    //
    // 규칙과 기억은 공급자가 쥔다 — 격자는 공급자가 알리는 'Changed'로 다시 그리기만 한다.
    public void SortCurrent(StorageSortOrder order)
    {
        EnsureInitialized();

        if (_current == null)
        {
            return; // 공급자가 없는 탭 — 그릴 것이 없으니 방향만 바뀐 채 다음 탭에서 반영된다
        }

        _current.Sort(order);
    }

    // 로그인했다 — 지난 세션에 기억한 정렬 자리를 버린다 (PlayerDataModel.LoginCompleted 구독)
    //
    // 목록이 서버 순서로 새로 오므로, 들고 있던 자리가 다른 상태를 덮지 않게 한다.
    // ⚠️ 칸 위치가 서버로 옮겨 가면(T-058 · T-044) 이 기억과 함께 걷어낸다.
    private void OnLoginCompleted(bool success, EResultCode code)
    {
        if (!success)
        {
            return;
        }

        foreach (StorageSlotSource source in _sources.Values)
        {
            source.ClearOrder();
        }
    }

    #endregion

    #region 칸 그리기

    // 지금 탭의 목록을 칸에 반영한다 (공급자 Changed 구독 · 탭 전환 · OnEnable).
    private void Redraw()
    {
        int count = _current == null ? 0 : _current.Count;

        for (int i = 0; i < _frames.Count; i++)
        {
            // 빈 칸이면 내용을 비우고 프레임만 남긴다 — 뒤의 것을 당겨 오지 않는다.
            // 장착·배치로 빠진 자리가 그대로 보여야 "어디서 빠졌는지"가 읽힌다('StorageSlotSource' 주석).
            SlotData? cell = i < count ? _current!.Get(i) : null;

            if (cell != null)
            {
                SlotView? view = GetOrCreateView(i);

                if (view == null)
                {
                    continue;
                }

                SlotData data = cell.Value;

                view.gameObject.SetActive(true);
                view.Bind(data);

                // 담김 표시의 주인은 카트다 — 자원 탭이 아니면 담길 수 없으므로 항상 꺼진다.
                view.SetSellMark(IsSellableTab && _cart.Contains((int)data.Key));

                // 지금 창고 밖에 나가 있나 — 캐릭터는 작업슬롯 배치 중, 장비는 장착 중.
                // **딤과 '배' 마크를 짝으로** 켠다: 딤만 두면 왜 어두운지 알 수 없고,
                // 마크만 두면 칸 200개 안에서 작은 배지가 묻힌다.
                //
                // ★ 판정을 격자가 하지 않는다 — 뜻이 탭마다 달라서, 여기서 분기하면
                //   딤·마크·[정렬] 세 군데에 같은 분기가 흩어진다('StorageSlotSource.IsAway').
                bool isAway = _current.IsAway(data.Key);

                view.SetAssignMark(isAway);
                view.SetDimmed(isAway);

                // 적성 스트립도 캐릭터 탭에서만이다 — 자원에는 적성이라는 개념이 없다.
                // 자원 탭에서 null을 넘기면 칸이 스트립을 끄고 수량 문구에게 자리를 돌려준다.
                view.SetAptitudes(IsCharacterTab ? ReadAptitudes(data.Key) : null);

                // 레벨 배지 · 경험치 게이지도 캐릭터 탭에서만이다.
                view.SetLevelBadge(IsCharacterTab ? ReadLevelLabel(data.Key) : null);
                view.SetExpGauge(IsCharacterTab ? _data.GetExpProgress(data.Key) : null);

                continue;
            }

            HideView(i);
        }

        if (count > _frames.Count)
        {
            ClientLogger.Warn(ClientLogger.UI,
                $"칸 프레임이 {_frames.Count}개인데 표시할 것이 {count}개다 — 뒤쪽이 잘렸다. 프레임을 늘려야 한다.", this);
        }
    }

    // 이 캐릭터의 적성 5종을 스트립 순서대로 담아 돌려준다 (Redraw에서 호출).
    //
    // 값의 주인은 서버다 — 'CharacterTable'을 직접 읽지 않는다('PlayerDataModel.GetAptitude' 주석).
    // ※ 돌려주는 배열은 재사용되는 하나다. 칸이 받아 그리는 즉시 쓰임이 끝나므로 들고 있어도 된다 —
    //   나중에 보관해 두는 쪽이 생기면 그때는 복사해야 한다.
    private byte[] ReadAptitudes(long characterId)
    {
        for (int i = 0; i < _aptitudes.Length; i++)
        {
            _aptitudes[i] = _data.GetAptitude(characterId, StripIndustries[i]);
        }

        return _aptitudes;
    }

    // 이 캐릭터의 레벨 배지 문구 — 'LV.19', 만렙이면 'LV.MAX' (Redraw에서 호출).
    //
    // ※ 'LV.' 접두를 뗄 수 없다 — 초상화 형태가 제각각이라 숫자만 두면 '2'가 무엇인지 드러나지 않는다.
    //   글자가 줄던 문제는 이름 줄에서 배지로 떼어 내 풀었다(한때 'LV.19 폭스파스크'로 이름 줄에 붙였다).
    // ※ 문구 규칙은 칸 툴팁과 함께 쓴다('CharacterSlotSource.GetLevelLabel').
    private string ReadLevelLabel(long characterId)
        => CharacterSlotSource.GetLevelLabel(_data.GetCharacterLevel(characterId));

    // i번째 프레임의 칸을 얻는다. 아직 없으면 그 프레임 안에 만든다 (Redraw에서 호출).
    private SlotView? GetOrCreateView(int index)
    {
        SlotView? view = _views[index];

        if (view != null)
        {
            return view;
        }

        view = Instantiate(slotPrefab, _frames[index]);
        SnapToFrame(view.transform as RectTransform);

        // 칸은 파괴하지 않고 풀로 되돌리므로 만들 때 한 번만 구독한다 — 다시 걸면 중복으로 쌓인다.
        view.RightClicked += OnSlotRightClicked;
        view.LeftClicked  += OnSlotLeftClicked;

        AttachTooltip(view);

        _views[index] = view;

        return view;
    }

    // 칸에 툴팁을 단다 — 올리면 칸에 다 못 담은 정보가 옆에 뜬다 (GetOrCreateView에서 호출, T-050).
    //
    // ■ 프리팹이 아니라 여기서 붙인다
    //   가챠 결과 팝업이 같은 칸 프리팹을 쓰는데, 그쪽은 결과를 보여 주는 자리라 툴팁 대상이 아니다.
    //   창고 칸을 만드는 곳이 여기 하나뿐이라 붙이는 곳도 하나다.
    // ■ 칸을 기억하지 않고 **띄우는 순간 'view.Key'를 읽는다**
    //   칸은 풀로 돌며 다른 개체로 다시 묶인다. 만들 때의 Key를 잡아 두면 정렬 한 번에 엉뚱한 것을 보인다.
    // ※ 내용은 지금 탭의 공급자가 만든다('StorageSlotSource.BuildTooltip') — 탭이 바뀌면 저절로 따라간다.
    // ※ 입력을 가로채지 않는다 — 툴팁은 레이캐스트 결과를 읽기만 하므로 우클릭 담기·좌클릭 개봉이 그대로 돈다.
    private void AttachTooltip(SlotView view)
    {
        var trigger = view.gameObject.AddComponent<TooltipTrigger>();

        trigger.SetProvider(() => view.IsEmpty || _current == null ? null : _current.BuildTooltip(view.Key));
    }

    // i번째 칸을 비우고 꺼 둔다 — 파괴하지 않고 풀로 되돌린다 (Redraw에서 호출).
    private void HideView(int index)
    {
        SlotView? view = _views[index];

        if (view == null)
        {
            return;
        }

        view.Clear();
        view.gameObject.SetActive(false);
    }

    #endregion

    #region 판매 담기

    // 칸을 우클릭했다 — 자원이면 판매 목록에 담거나 뺀다 (SlotView.RightClicked 구독)
    //
    // 이미 담긴 칸을 다시 누르면 뺀다(토글). 수량을 고치려면 뺐다가 다시 담는다 —
    // 한 조작에 '담기'와 '수량 바꾸기'를 겹쳐 두면 눌러 보기 전에는 무엇이 일어날지 알 수 없다.
    private void OnSlotRightClicked(SlotView view)
    {
        if (!IsSellableTab || view.IsEmpty)
        {
            return;
        }

        int itemId = (int)view.Key;

        if (_cart.Contains(itemId))
        {
            _cart.Remove(itemId);

            return;
        }

        int owned = _data.GetItemCount(itemId);

        if (owned <= 0)
        {
            return; // 화면이 아직 낡았다 — 뒤이어 올 InventoryChanged가 이 칸을 지운다
        }

        // 1개짜리는 물어볼 것이 없다. 팝업을 띄워 봐야 확인을 한 번 더 누르게 할 뿐이다.
        if (owned == 1)
        {
            _cart.Add(itemId, 1);

            return;
        }

        // ※ 팝업을 직접 들지 않고 'UIManager'를 거친다 — 팝업이 '!System Canvas'에 살아서다.
        //   화면 전체를 막아야 하는데 열 캔버스는 Sorting Order가 전부 0인 형제라
        //   창고 안에 두면 다른 열이 그대로 눌린다('System 규칙.md').
        _ui.AskAmount(itemId, owned, "몇 개를 팔까?", amount => _cart.Add(itemId, amount));
    }

    #endregion

    #region 상자 개봉

    // 칸을 좌클릭했다 — 상자면 몇 개 열지 묻고 개봉을 요청한다 (SlotView.LeftClicked 구독)
    //
    // 상자가 아닌 칸은 아무 일도 하지 않는다. 좌클릭에 다른 뜻이 붙기 전까지는 그게 맞는 반응이다.
    // 수량을 묻는 동선은 판매 담기와 **똑같이** 간다 — 1개면 묻지 않고, 2개 이상이면 팝업을 띄운다.
    private void OnSlotLeftClicked(SlotView view)
    {
        if (!IsItemKeyTab || view.IsEmpty)
        {
            return;
        }

        int itemId = (int)view.Key;

        if (!GameDataLoader.IsBox(itemId))
        {
            return;
        }

        if (_isOpeningBox)
        {
            return; // 앞 요청의 응답을 기다리는 중 — 연타 방지
        }

        int owned = _data.GetItemCount(itemId);

        if (owned <= 0)
        {
            return; // 화면이 아직 낡았다 — 뒤이어 올 InventoryChanged가 이 칸을 지운다
        }

        // 1개짜리는 물어볼 것이 없다. 팝업을 띄워 봐야 확인을 한 번 더 누르게 할 뿐이다.
        if (owned == 1)
        {
            OpenBox(itemId, 1);

            return;
        }

        // ※ 팝업을 직접 들지 않고 'UIManager'를 거치는 이유는 판매 담기와 같다('!System Canvas').
        _ui.AskAmount(itemId, owned, "몇 개를 열까?", amount => OpenBox(itemId, amount));
    }

    // 상자를 'count'개 연다 (OnSlotLeftClicked · 수량 팝업 확인에서 호출).
    //
    // 로그인 전에 보내면 서버가 User를 못 찾아 조용히 버린다 — 클라 입장에선 응답도 오류도
    // 없어서 "눌렀는데 아무 일도 안 일어난다"로만 보인다. 보내기 전에 여기서 끊고 이유를 남긴다.
    //
    // ⚠️ 수량 팝업이 뜬 사이 채취·판매로 보유량이 바뀔 수 있다 — 그 사이 모자라졌으면
    //   서버가 'NotEnoughItem'으로 거절한다. **클라가 다시 재지 않는다**(거절은 서버가 한다).
    private void OpenBox(int itemId, int count)
    {
        if (_isOpeningBox)
        {
            return;
        }

        if (!_data.IsLoggedIn)
        {
            ClientLogger.Warn(ClientLogger.Send, "상자 개봉 요청을 보내지 않았다 — 로그인이 먼저다(서버가 응답 없이 버린다)");

            return;
        }

        _network.Send(new C_ItemUseRequest
        {
            ItemTID = itemId,
            Count   = count
        });

        ClientLogger.Info(ClientLogger.Send, $"상자 개봉 요청 — TID={itemId}, {count}개");

        // 대기 시작 — 로딩 표시·무응답 감시·알림은 ServerWaitManager가 공통으로 처리한다.
        _isOpeningBox = true;
        _waitHandle   = _wait.Begin($"상자 개봉 {count}개", onClosed: OnWaitClosed);
    }

    // 개봉 성공 도착 — 대기를 조용히 닫는다. 보상 표시는 'GachaResultPresenter'가 맡는다
    // (PlayerDataModel.ItemUseCompleted 구독)
    private void OnItemUseCompleted(List<GachaRewardInfo> rewards)
    {
        _waitHandle?.Succeed();
    }

    // 개봉 실패 도착 — 사유를 사람이 읽을 문구로 옮겨 알림에 띄운다 (PlayerDataModel.ItemUseFailed 구독)
    private void OnItemUseFailed(EResultCode code)
    {
        _waitHandle?.Fail(ResultMessages.ToText(code));
    }

    // 대기가 끝났다(성공·실패·타임아웃 공통) — 다시 열 수 있게 푼다 (ServerWaitManager.Begin의 onClosed)
    private void OnWaitClosed()
    {
        _isOpeningBox = false;
        _waitHandle   = null;
    }

    #endregion

    #region 보조

    // 씬에 깔린 프레임을 모아 둔다 (EnsureInitialized에서 호출).
    //
    // ※ 앞서 예외로 초기화가 끊겼으면 다시 불릴 수 있다 — 비우고 시작해야 중복으로 쌓이지 않는다.
    private void CacheFrames()
    {
        _frames.Clear();
        _views.Clear();

        foreach (Transform frame in slotParent)
        {
            _frames.Add(frame);
            _views.Add(null);
        }

        if (_frames.Count == 0)
        {
            ClientLogger.Error(ClientLogger.UI,
                "칸 프레임이 하나도 없다 — Slot Parent가 Content를 가리키는지 확인할 것.", this);
        }
        else if (_frames.Count != ServerStorageCapacity)
        {
            // 한도가 프레임보다 크면 뒤쪽 칸이 보이지 않아 "아이템이 사라졌다"로 읽히고,
            // 작으면 서버가 거절할 자리를 그린다. 어느 쪽이든 조용히 지나가면 못 찾는다.
            ClientLogger.Error(ClientLogger.UI,
                $"칸 프레임이 {_frames.Count}개인데 서버 창고 한도는 {ServerStorageCapacity}칸이다 — 둘을 맞출 것.", this);
        }
    }

    // 프리팹을 프레임 안에 안착시킨다 — 프레임을 **꽉 채운다**.
    // Instantiate 직후의 RectTransform은 프리팹에 저장된 좌표를 그대로 들고 오므로,
    // 이걸 하지 않으면 프레임 밖으로 삐져나간다.
    //
    // ★ 크기도 프레임을 따른다 — 프레임은 'FlexibleGridLayoutGroup'이 창 폭에 맞춰 늘리는데(예: 115px)
    //   프리팹은 100×100 고정이라, 위치만 맞추면 사방에 빈 테두리가 생겼다(2026-09-16).
    //   프리팹 루트를 스트레치로 바꾸지 않는 이유 — 가챠 결과 팝업이 같은 프리팹을 레이아웃에 넣어 쓴다.
    private static void SnapToFrame(RectTransform? rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin          = Vector2.zero;
        rect.anchorMax          = Vector2.one;
        rect.pivot              = new Vector2(0.5f, 0.5f);
        rect.sizeDelta          = Vector2.zero;
        rect.anchoredPosition3D = Vector3.zero;
        rect.localScale         = Vector3.one;
        rect.localRotation      = Quaternion.identity;
    }

    #endregion
}

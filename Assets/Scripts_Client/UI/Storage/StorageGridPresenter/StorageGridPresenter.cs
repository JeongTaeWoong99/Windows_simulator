using System.Collections.Generic;
using UnityEngine;

// 창고의 칸 격자. 탭이 무엇이든 **같은 격자 하나**가 그린다 —
// 지금 켜진 탭의 공급자('StorageSlotSource')에게 목록을 받아 앞 칸부터 채운다.
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
// ■ i번째 항목이 i번째 프레임에 들어간다
//   예전에는 'ItemId → 프레임'을 고정해 두고 빈 프레임을 앞에서부터 찾았다. 그러면 아이템이
//   처음 들어온 순서로 칸이 영구히 고정돼 **정렬을 넣을 자리가 없다.**
//   순서의 주인을 공급자로 옮겼기 때문에, 정렬·걸러 내기는 공급자만 고치면 된다(T-015).
//
// ■ 칸은 파괴하지 않고 풀로 되돌린다
//   탭을 오갈 때마다 200개를 만들고 부수면 상주 앱에서 GC가 쌓인다.
//   남는 칸은 'Clear()' 후 꺼 두었다가 다음 탭에서 다시 쓴다.
//
// ■ 우클릭 = 판매 목록에 담기 · 빼기 (자원 탭에서만)
//   칸('InventorySlotView')은 우클릭을 이벤트로 던지기만 하고 무슨 뜻인지 모른다.
//   그것을 판매로 읽는 것이 여기다 — 서버 판매 패킷이 아이템 TID 축이라 캐릭터는 담을 수 없어서
//   자원 탭이 아니면 무시한다 (동선은 'Storage 규칙.md').
public class StorageGridPresenter : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("아이템 한 칸 프리팹 (InventorySlotView 포함). 빈 프레임 안에 생성된다")]
    private InventorySlotView slotPrefab = null!;

    [SerializeField, Tooltip("칸 프레임(Slot)들이 들어 있는 부모 — Inventory Scroll View Panel > Viewport > Content")]
    private Transform slotParent = null!;

    // 씬에 깔린 칸 프레임들. 개수·순서가 고정이라 매번 훑지 않고 한 번만 모아 둔다.
    private readonly List<Transform> _frames = new List<Transform>();

    // 프레임 i 안에 만들어 둔 칸. 아직 안 만들었으면 null이고, 안 쓰는 동안에는 꺼 둔다.
    private readonly List<InventorySlotView?> _views = new List<InventorySlotView?>();

    // 탭별 공급자. 여기 없는 탭은 "아직 데이터가 없는 탭"이고, 탭 줄이 그 버튼을 잠근다.
    private readonly Dictionary<StorageTab, StorageSlotSource> _sources =
        new Dictionary<StorageTab, StorageSlotSource>();

    private StorageSlotSource? _current;

    // 지금 열린 탭. 우클릭을 판매로 읽어도 되는 탭인지 여기서 가른다.
    private StorageTab _currentTab = StorageTab.Resource;

    private PlayerDataModel _data = null!;
    private SellCartModel   _cart = null!;
    private UIManager       _ui   = null!;

    private bool _isCartSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 이 탭의 칸을 팔 수 있나. 서버 판매 패킷이 아이템 TID 축이라 자원만 담긴다.
    private bool IsSellableTab => _currentTab == StorageTab.Resource;

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

        _data = data;
        _cart = Services.Get<SellCartModel>();
        _ui   = Services.Get<UIManager>();

        // ★ 탭을 하나 채우는 일은 여기 한 줄로 끝난다 — 공급자를 만들어 등록하면
        //   전환·잠금·격자는 그대로다 ('Storage 규칙.md'의 "탭 하나를 채우는 절차").
        //
        // ⏸ Equipment — 장비 데이터가 없다. 게다가 'ItemTable.ItemType'은 산업 축(농사·낚시…)이라
        //    장비를 담을 칸이 테이블에 없다. 컬럼 축부터 정해야 한다 (T-043 · T-002).
        // ⏸ Trait      — 기획은 있으나 서버 구현·패킷이 없다 (T-043).
        _sources.Add(StorageTab.Resource,  new ResourceSlotSource(data));
        _sources.Add(StorageTab.Character, new CharacterSlotSource(data));

        // ★ 끝까지 왔을 때만 세운다 — 위에서 예외가 나면(참조 미연결·서비스 미등록) 플래그가
        //   안 켜져 다음 호출이 다시 시도하고, 원인도 매번 같은 예외로 드러난다.
        //   먼저 세우면 초기화가 중단됐는데도 격자가 조용히 빈 채로 굳는다.
        _isReady = true;

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

        // ★ 아래 조기 반환보다 먼저 기억한다 — 공급자가 없는 탭끼리(장비↔특성) 오가면
        //   'next'가 둘 다 null이라 조기 반환에 걸리는데, 그때도 지금 탭은 바뀌어 있다.
        _currentTab = tab;

        _sources.TryGetValue(tab, out StorageSlotSource? next);

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

    #region 칸 그리기

    // 지금 탭의 목록을 칸에 반영한다 (공급자 Changed 구독 · 탭 전환 · OnEnable).
    private void Redraw()
    {
        int count = _current == null ? 0 : _current.Count;

        for (int i = 0; i < _frames.Count; i++)
        {
            if (i < count)
            {
                InventorySlotView? view = GetOrCreateView(i);

                if (view == null)
                {
                    continue;
                }

                StorageSlotData data = _current!.Get(i);

                view.gameObject.SetActive(true);
                view.Bind(data);

                // 담김 표시의 주인은 카트다 — 자원 탭이 아니면 담길 수 없으므로 항상 꺼진다.
                view.SetSellMark(IsSellableTab && _cart.Contains((int)data.Key));

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

    // i번째 프레임의 칸을 얻는다. 아직 없으면 그 프레임 안에 만든다 (Redraw에서 호출).
    private InventorySlotView? GetOrCreateView(int index)
    {
        InventorySlotView? view = _views[index];

        if (view != null)
        {
            return view;
        }

        view = Instantiate(slotPrefab, _frames[index]);
        SnapToFrame(view.transform as RectTransform);

        // 칸은 파괴하지 않고 풀로 되돌리므로 만들 때 한 번만 구독한다 — 다시 걸면 중복으로 쌓인다.
        view.RightClicked += OnSlotRightClicked;

        _views[index] = view;

        return view;
    }

    // i번째 칸을 비우고 꺼 둔다 — 파괴하지 않고 풀로 되돌린다 (Redraw에서 호출).
    private void HideView(int index)
    {
        InventorySlotView? view = _views[index];

        if (view == null)
        {
            return;
        }

        view.Clear();
        view.gameObject.SetActive(false);
    }

    #endregion

    #region 판매 담기

    // 칸을 우클릭했다 — 자원이면 판매 목록에 담거나 뺀다 (InventorySlotView.RightClicked 구독)
    //
    // 이미 담긴 칸을 다시 누르면 뺀다(토글). 수량을 고치려면 뺐다가 다시 담는다 —
    // 한 조작에 '담기'와 '수량 바꾸기'를 겹쳐 두면 눌러 보기 전에는 무엇이 일어날지 알 수 없다.
    private void OnSlotRightClicked(InventorySlotView view)
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
        _ui.AskAmount(itemId, owned, amount => _cart.Add(itemId, amount));
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
    }

    // 프리팹을 프레임 안에 안착시킨다 — 위치를 0으로 맞춰 프레임 정중앙에 놓는다.
    // Instantiate 직후의 RectTransform은 프리팹에 저장된 좌표를 그대로 들고 오므로,
    // 이걸 하지 않으면 프레임 밖으로 삐져나간다.
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

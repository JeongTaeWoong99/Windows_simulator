using System.Collections.Generic;
using MikaNetwork;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 창고 하단의 정보 칸 — 지금은 **판매 목록**을 그린다.
//
// ■ 무엇을 보여 주나
// 자원 칸을 우클릭해 담은 것들('SellCartModel')의 줄 목록·합계 골드·판매 버튼이다.
// 담고 → 판매 버튼, 이 두 단계가 곧 확인 절차라 별도 확인 모달을 두지 않는다
// (화면 중앙 팝업을 최소화한다 — 기획 P1).
//
// ■ 담긴 내용을 여기서 들고 있지 않다
// 격자('StorageGridPresenter')도 같은 목록을 봐야 담김 표시를 켤 수 있다. 둘 중 하나가
// 들고 있으면 패널끼리 서로를 참조하게 되므로, 상태는 'SellCartModel'에 두고 양쪽이 구독한다
// ('Storage 규칙.md').
//
// ⚠️ 클래스 이름이 역할과 어긋나 있다 — 원래는 고른 항목의 상세를 띄우는 자리였다.
//   'SellCartPresenter'가 맞지만 씬 오브젝트 이름·문서가 함께 가는 개명이라 따로 다룬다.
public class StorageInformationPresenter : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("판매 줄 프리팹 (SellCartRowView 포함). 담긴 수만큼 만들어 재사용한다")]
    private SellCartRowView rowPrefab = null!;

    [SerializeField, Tooltip("줄이 쌓이는 부모 — Sell Scroll View Panel > Viewport > Content")]
    private RectTransform rowParent = null!;

    [SerializeField, Tooltip("담긴 것이 없을 때의 안내 문구. 빈 칸은 고장과 구분되지 않는다")]
    private TMP_Text emptyText = null!;

    [SerializeField, Tooltip("합계 골드")]
    private TMP_Text totalText = null!;

    [SerializeField, Tooltip("상위 등급이 담겼을 때만 켜지는 경고. 오판매를 막는 자리다")]
    private GameObject highRarityWarning = null!;

    [SerializeField, Tooltip("판매 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button sellButton = null!;

    // 만들어 둔 줄. 담고 빼기를 반복하므로 파괴하지 않고 꺼 두었다가 다시 쓴다.
    private readonly List<SellCartRowView> _rows = new List<SellCartRowView>();

    private PlayerDataModel   _data    = null!;
    private SellCartModel     _cart    = null!;
    private NetworkManager    _network = null!;
    private ServerWaitManager _wait    = null!;

    // 진행 중인 판매 대기의 손잡이. 응답이 오면 결과를 보고하고, 무응답이면 스스로 타임아웃돼 잠금을 푼다.
    private ServerWaitHandle? _waitHandle;

    // 응답을 기다리는 중인가 — 연타로 두 번 나가면 두 번 팔린다.
    private bool _isWaiting;

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 배선 → 초기화 순서로 진행한다 (클라 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다.
    private void Start()
    {
        this.RequireRef(rowPrefab,         nameof(rowPrefab));
        this.RequireRef(rowParent,         nameof(rowParent));
        this.RequireRef(emptyText,         nameof(emptyText));
        this.RequireRef(totalText,         nameof(totalText));
        this.RequireRef(highRarityWarning, nameof(highRarityWarning));
        this.RequireRef(sellButton,        nameof(sellButton));

        _data    = Services.Get<PlayerDataModel>();
        _cart    = Services.Get<SellCartModel>();
        _network = NetworkManager.Instance;
        _wait    = Services.Get<ServerWaitManager>();

        Subscribe();
        sellButton.onClick.AddListener(OnSellClicked);
        Refresh(); // 창고를 닫아 둔 사이에 담긴 것이 있을 수 있다

        _isReady = true;
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    //
    // ★ 재구독만으로는 부족하다 — 닫혀 있는 동안 채취로 수량이 줄어 카트가 깎였을 수 있다.
    private void OnEnable()
    {
        if (_isReady)
        {
            Subscribe();
            Refresh();
        }
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
    }

    #region 구독

    // 판매 목록 변경·판매 응답 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed            = true;
        _cart.Changed           += Refresh;
        _data.ItemSellCompleted += OnSellCompleted;
        _data.ItemSellFailed    += OnSellFailed;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed            = false;
        _cart.Changed           -= Refresh;
        _data.ItemSellCompleted -= OnSellCompleted;
        _data.ItemSellFailed    -= OnSellFailed;
    }

    #endregion

    #region 목록 그리기

    // 담긴 목록·합계·경고·버튼 상태를 화면에 반영한다 (Start · OnEnable · SellCartModel.Changed 구독).
    private void Refresh()
    {
        int count = _cart.Count;

        for (int i = 0; i < count; i++)
        {
            ItemInfo        entry = _cart.Entries[i];
            SellCartRowView row   = GetOrCreateRow(i);

            row.gameObject.SetActive(true);
            row.Bind(entry.ItemId,
                     GameDataLoader.GetItemName(entry.ItemId),
                     entry.Count,
                     (long)GameDataLoader.GetItemPrice(entry.ItemId) * entry.Count);
        }

        HideRowsFrom(count);

        emptyText.gameObject.SetActive(count == 0);
        totalText.text = $"합계 {_cart.TotalPrice:N0} G";

        // 상위 등급은 다시 모으기 어렵다. 목록에서 빼지 않고 눈에 띄게만 알린다 —
        // 빼 버리면 "왜 안 담기지"가 되고, 팔 자유는 남겨 둔다.
        highRarityWarning.SetActive(_cart.HasHighRarity);

        ApplySellButton();

        // 방금 만든 줄은 아직 프리팹에 저장된 크기 그대로다 — uGUI의 레이아웃 계산은
        // 이 프레임 **맨 끝**(Canvas.willRenderCanvases)에 돌기 때문이다.
        // 그 사이 'WidgetPositionLayout.VerifyNoOverflow'가 'LateUpdate'에서 훑고 지나가
        // "자식이 부모보다 넓다" → 다음 검사에서 "해소됐다"가 왕복으로 찍힌다.
        // 여기서 미리 태워 두면 아무도 반쯤 놓인 줄을 보지 않는다.
        // ※ 프리팹 크기를 부모보다 작게 저장해 피하는 방법은 자식 하나만 커도 다시 터진다.
        LayoutRebuilder.ForceRebuildLayoutImmediate(rowParent);
    }

    // 'index'번째 줄을 돌려준다. 아직 없으면 그때 만든다 (Refresh에서 호출).
    private SellCartRowView GetOrCreateRow(int index)
    {
        if (index < _rows.Count)
        {
            return _rows[index];
        }

        SellCartRowView row = Instantiate(rowPrefab, rowParent);

        // 줄은 파괴하지 않고 재사용하므로 만들 때 한 번만 구독한다 — 다시 걸면 중복으로 쌓인다.
        row.RemoveClicked += OnRowRemoveClicked;

        _rows.Add(row);

        return row;
    }

    // 이번에 쓰이지 않은 줄을 비우고 꺼 둔다 (Refresh에서 호출).
    private void HideRowsFrom(int startIndex)
    {
        for (int i = startIndex; i < _rows.Count; i++)
        {
            _rows[i].Clear();
            _rows[i].gameObject.SetActive(false);
        }
    }

    // 판매 버튼을 열고 닫는다 — 담긴 것이 없거나 응답을 기다리는 중이면 잠근다.
    private void ApplySellButton()
    {
        sellButton.interactable = !_isWaiting && _cart.Count > 0;
    }

    #endregion

    #region 판매 요청

    // 줄의 빼기를 눌렀다 (SellCartRowView.RemoveClicked 구독)
    private void OnRowRemoveClicked(SellCartRowView row)
    {
        if (_isWaiting)
        {
            return; // 이미 나간 요청의 목록을 바꾸면 화면과 요청이 어긋난다
        }

        _cart.Remove(row.ItemId);
    }

    // 담긴 것을 한 번에 판다 (sellButton OnClick에 코드로 연결).
    //
    // 로그인 전에 보내면 서버가 User를 못 찾아 조용히 버린다 — 클라 입장에선 응답도 오류도
    // 없어서 "눌렀는데 아무 일도 안 일어난다"로만 보인다. 보내기 전에 여기서 끊고 이유를 남긴다.
    private void OnSellClicked()
    {
        if (_isWaiting || _cart.Count == 0)
        {
            return;
        }

        if (!_data.IsLoggedIn)
        {
            ClientLogger.Warn(ClientLogger.Send, "판매 요청을 보내지 않았다 — 로그인이 먼저다(서버가 응답 없이 버린다)");

            return;
        }

        _network.Send(new C_ItemSellRequest
        {
            Items = _cart.ToRequestItems()
        });

        ClientLogger.Info(ClientLogger.Send, $"판매 요청 — {_cart.Count}종, 예상 {_cart.TotalPrice:N0} G");

        // 대기 시작 — 로딩 표시·무응답 감시·알림은 ServerWaitManager가 공통으로 처리한다.
        _isWaiting  = true;
        ApplySellButton();
        _waitHandle = _wait.Begin("판매", onClosed: OnWaitClosed);
    }

    // 대기가 끝났다(성공·실패·타임아웃 공통) — 버튼 잠금을 푼다 (ServerWaitManager.Begin의 onClosed)
    private void OnWaitClosed()
    {
        _isWaiting  = false;
        _waitHandle = null;
        ApplySellButton();
    }

    // 판매 성공 — 목록을 비운다 (PlayerDataModel.ItemSellCompleted 구독)
    //
    // ※ 골드 표시는 상태바가 'CurrencyChanged'로 따로 갱신한다. 여기서 잔액을 만지지 않는다.
    private void OnSellCompleted(long gainedGold)
    {
        ClientLogger.Info(ClientLogger.Recv, $"판매 완료 — {gainedGold:N0} G 획득");

        _waitHandle?.Succeed();
        _cart.Clear(); // Changed가 발행되어 목록·합계가 다시 그려진다
    }

    // 판매 실패 — 사유를 사람이 읽을 문구로 옮겨 알림에 띄운다 (PlayerDataModel.ItemSellFailed 구독)
    //
    // ★ 목록을 비우지 않는다. 전부 되거나 전혀 안 되므로 아무것도 팔리지 않았고,
    //   담아 둔 것을 다시 고르게 하면 조작을 처음부터 반복시키는 셈이다.
    private void OnSellFailed(EResultCode code)
    {
        _waitHandle?.Fail(ResultMessages.ToText(code));
    }

    #endregion
}

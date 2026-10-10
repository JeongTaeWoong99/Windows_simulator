using System;
using System.Collections.Generic;
using GameData;
using MikaNetwork;
using MikaProtocol;
using UnityEngine;

// 가챠 패널 — 거래 열의 뽑기 화면. 풀마다 카드 한 장(1회 · 10회)을 둔다 (T-143).
//
// ■ 왜 거래 열인가
// 가챠는 기획상 상점에 속한다 — 가챠 티켓이 골드 상점 품목이다.
// → GameDesign/기획/거래/README.md 3.2
//
// ■ 이름과 비용은 코드에 박지 않는다
// 'GachaInfoTable'(Gacha.xlsx)의 Name·CostSingle·CostMulti를 읽어 채운다.
// ⚠️ 10연차가 단차 x 10이라는 보장이 없다 — 컬럼이 따로다. 곱해서 만들지 않는다.
// 카드의 괄호·종수·대표 아이콘·[P] 확률은 'GachaPoolSummary'가 가챠 시트에서 센다.
//
// ■ 결과 내용은 여기서 보지 않는다
// 뽑힌 보상·인벤토리 반영은 'PlayerDataModel'가 처리하고, 결과 팝업은 최상단의
// 'GachaResultPresenter'('!Overlay Canvas')가 스스로 구독해서 띄운다 — 여기 자식으로 두지 않는다.
// 다만 성공/실패 도착 여부는 여기서 구독한다 — 요청 중 로딩·버튼 잠금·실패 알림을 위해서다.
//
// ■ 결과 팝업의 [n회 더 뽑기]도 여기로 들어온다 (2026-10-09)
// 팝업이 요청을 직접 보내면 대기·연타 방지·실패 알림을 두 벌 갖게 된다. 마지막에 누른 버튼을 기억해 두고
// 'DrawAgain'으로 같은 버튼을 다시 누른 것처럼 처리한다. 팝업은 'DrawStateChanged'를 듣고 버튼 잠금을 맞춘다.
public class GachaPresenter : MonoBehaviour
{
    // 카드 한 장 — 어느 풀인가. 인스펙터에서 짝지어 넣는다.
    [Serializable]
    private struct PoolEntry
    {
        [Tooltip("뽑을 풀 Id (= GachaInfoTable의 GachaInfoTID). 2=캐릭터 · 3=무기 · 4=장신구 · 5=보석")]
        public int gachaId;

        [Tooltip("이 풀의 카드")]
        public GachaPoolCardView card;
    }

    // 버튼 하나의 요청 — 카드마다 1회·10회 두 개. 비용은 버튼 잠금 판정이 매 재화 변경마다 도므로 미리 읽어 둔다.
    private readonly struct DrawRequest
    {
        public readonly int  GachaId;
        public readonly int  DrawCount;
        public readonly long Cost;

        public DrawRequest(int gachaId, int drawCount, long cost)
        {
            GachaId   = gachaId;
            DrawCount = drawCount;
            Cost      = cost;
        }
    }

    // ※ NonReorderable로 두 가지를 동시에 얻는다 —
    //   [1] 목록 순서를 화면과 나란히 두어야 사람이 한눈에 대조할 수 있다.
    //   [2] reorderable list로 그려지면 Unity가 그 위의 [CenterHeader]를 건너뛴다 ('UI 규칙.md'의 "공통 작성 규약")
    [CenterHeader("참조")]
    [SerializeField, NonReorderable, Tooltip("풀 카드들. 화면과 같은 순서로 넣는다 (캐릭터 → 무기 → 장신구 → 보석)")]
    private PoolEntry[] pools = new PoolEntry[0];

    // 버튼별 요청 — 카드마다 1회·10회 두 개씩, '_cards'와 같은 순서. 테이블에 없는 풀은 넣지 않는다(카드를 끈다).
    private readonly List<DrawRequest> _requests = new List<DrawRequest>();

    // 요청이 만들어진 카드 — _cards[k]의 1회가 _requests[k * 2], 10회가 [k * 2 + 1]. Refresh가 카드마다 잠금을 맞춘다.
    private readonly List<GachaPoolCardView> _cards = new List<GachaPoolCardView>();

    private PlayerDataModel   _data    = null!;
    private NetworkManager    _network = null!;
    private ServerWaitManager _wait    = null!;

    // 진행 중인 가챠 대기의 손잡이. 응답이 오면 결과를 보고하고, 무응답이면 스스로 타임아웃돼 잠금을 푼다.
    private ServerWaitHandle? _waitHandle;

    // 응답을 기다리는 중인가 — 연타로 두 번 나가면 인벤토리·재화가 꼬이므로 버튼을 잠근다.
    private bool _isWaiting;

    // 마지막으로 요청한 버튼(_requests의 번호). 아직 뽑은 적이 없으면 -1 — [n회 더 뽑기]가 이 버튼을 다시 누른다.
    private int _lastIndex = -1;

    // [n회 더 뽑기]를 지금 누를 수 있는지가 바뀌었다 — 대기 시작/종료 · 재화 변경 · 패널 켜짐/꺼짐.
    public event Action? DrawStateChanged;

    // 마지막으로 뽑은 횟수 (1 · 10). 아직 뽑은 적이 없으면 0.
    public int LastDrawCount => _lastIndex >= 0 ? _requests[_lastIndex].DrawCount : 0;

    // 마지막 버튼을 지금 다시 뽑을 수 있는가 — 패널이 켜져 있고(꺼지면 응답 구독이 풀려 대기를 닫을 수 없다),
    // 대기 중이 아니고, 골드가 그 버튼의 비용 이상이어야 한다. 'Refresh'의 버튼 잠금과 같은 판정이다.
    public bool CanDrawAgain => isActiveAndEnabled
                                && _lastIndex >= 0
                                && !_isWaiting
                                && _data.Gold >= _requests[_lastIndex].Cost;

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 배선 → 초기화 순서로 진행한다 (클라 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다(MonoService 주석).
    private void Start()
    {
        _data    = Services.Get<PlayerDataModel>();
        _network = NetworkManager.Instance;
        _wait    = Services.Get<ServerWaitManager>();

        BuildCards();

        Subscribe();
        Refresh(); // 로그인 재화가 이미 와 있을 수 있다

        _isReady = true;
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    //
    // ★ 재구독만으로는 부족하다 — 닫혀 있는 동안 판매·채취로 골드가 바뀌었을 수 있다.
    private void OnEnable()
    {
        if (_isReady)
        {
            Subscribe();
            Refresh();
        }
    }

    // 구독 해제 (Unity 메시지) — 꺼진 패널로는 다시 뽑을 수 없으니 팝업에도 알린다
    private void OnDisable()
    {
        Unsubscribe();
        DrawStateChanged?.Invoke();
    }

    #region 구독

    // 가챠 성공·실패 도착과 재화 변경 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed         = true;
        _data.GachaCompleted += OnGachaCompleted;
        _data.GachaFailed    += OnGachaFailed;
        _data.CurrencyChanged += Refresh;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed         = false;
        _data.GachaCompleted -= OnGachaCompleted;
        _data.GachaFailed    -= OnGachaFailed;
        _data.CurrencyChanged -= Refresh;
    }

    #endregion

    #region 초기화

    // 카드마다 풀을 읽어 그리고 버튼을 배선한다 (Start에서 한 번).
    //
    // 빠지거나 어긋난 카드는 끈다 — 켜 두면 버튼을 눌러도 아무 일이 없어서 고장 난 것처럼 보인다.
    // 원인이 인스펙터·테이블이라는 걸 드러내려고 여기서 먼저 알린다.
    private void BuildCards()
    {
        _requests.Clear();
        _cards.Clear();

        if (pools.Length == 0)
        {
            ClientLogger.Error(ClientLogger.UI, "가챠 카드가 하나도 없다 — Gacha Presenter의 Pools를 확인할 것.", this);
        }

        foreach (PoolEntry entry in pools)
        {
            if (entry.card == null)
            {
                ClientLogger.Error(ClientLogger.UI, $"가챠 카드가 비어 있다 (풀 {entry.gachaId}).", this);

                continue;
            }

            if (!GameDataLoader.TryGetGachaInfo(entry.gachaId, out GachaInfoTableRow info))
            {
                ClientLogger.Error(ClientLogger.UI,
                    $"GachaInfoTable에 없는 풀 {entry.gachaId}다 — 서버가 InvalidGachaId로 거절한다. 카드를 끈다.", this);
                entry.card.gameObject.SetActive(false);

                continue;
            }

            // 재화 축은 골드뿐이다(다이아 폐지 — #41). 잔액 비교(Refresh)도 골드로만 한다.
            if (info.CostCurrency != CurrencyType.Gold)
            {
                ClientLogger.Warn(ClientLogger.UI,
                    $"풀 {entry.gachaId}의 비용 재화가 골드가 아니다({info.CostCurrency}) — 버튼 잠금이 골드로만 판정된다.", this);
            }

            BindCard(entry.gachaId, info, entry.card);
        }
    }

    // 카드 한 장을 그리고 요청 두 개(1회·10회)를 등록한다 (BuildCards에서 호출).
    // ⚠️ 10연차 비용은 단차 x 10이 아니다. 컬럼이 따로라 각각 읽는다.
    private void BindCard(int gachaId, GachaInfoTableRow info, GachaPoolCardView card)
    {
        GachaPoolSummary summary = GachaPoolSummary.Build(gachaId, info.Name);

        string subtitle = summary.Subtitle;
        string title    = subtitle.Length > 0
            ? $"{info.Name} {UIRichText.Small(UIRichText.Label($"({subtitle})"))}"
            : info.Name;

        // 상수는 long으로 생성되지만 패킷의 'DrawCount'가 int다 — 서버가 1·10만 받는다.
        int single = (int)Constants.GachaDrawSingle;
        int multi  = (int)Constants.GachaDrawMulti;

        card.Bind(title, summary.KindText, summary.Icon,
                  single, info.CostSingle,
                  multi,  info.CostMulti,
                  summary.BuildRateTooltip);

        // 반복 변수를 그대로 넘기면 모든 콜백이 마지막 값을 본다. 복사본을 캡처한다.
        int singleIndex = _requests.Count;

        _requests.Add(new DrawRequest(gachaId, single, info.CostSingle));
        _requests.Add(new DrawRequest(gachaId, multi,  info.CostMulti));
        _cards.Add(card);

        card.DrawClicked += isMulti => Draw(isMulti ? singleIndex + 1 : singleIndex);
    }

    #endregion

    #region 뽑기 요청

    // 'index'번째 버튼으로 뽑기를 요청한다 (카드의 DrawClicked에 코드로 연결).
    //
    // 로그인 전에 보내면 서버가 User를 못 찾아 조용히 버린다 — 클라 입장에선 응답도 오류도
    // 없어서 "눌렀는데 아무 일도 안 일어난다"로만 보인다. 보내기 전에 여기서 끊고 이유를 남긴다.
    private void Draw(int index)
    {
        if (_isWaiting)
        {
            return; // 앞 요청의 응답을 기다리는 중 — 연타 방지
        }

        if (!_data.IsLoggedIn)
        {
            ClientLogger.Warn(ClientLogger.Send, "가챠 요청을 보내지 않았다 — 로그인이 먼저다(서버가 응답 없이 버린다)");

            return;
        }

        DrawRequest request = _requests[index];

        _lastIndex = index;

        _network.Send(new C_GachaDrawRequest
        {
            GachaId   = request.GachaId,
            DrawCount = request.DrawCount
        });

        ClientLogger.Info(ClientLogger.Send, $"가챠 요청 — 풀={request.GachaId}, {request.DrawCount}회");

        // 대기 시작 — 로딩 표시·무응답 감시·알림은 ServerWaitManager가 공통으로 처리한다.
        _isWaiting  = true;
        Refresh();
        _waitHandle = _wait.Begin($"가챠 {request.DrawCount}회", onClosed: OnWaitClosed);
    }

    // 결과 팝업의 [n회 더 뽑기] — 마지막에 누른 버튼을 다시 누른다 (GachaResultPresenter에서 호출).
    // 잠금 판정은 'CanDrawAgain' 하나로 한다 — 팝업 버튼이 늦게 갱신됐어도 여기서 한 번 더 막는다.
    public void DrawAgain()
    {
        if (!CanDrawAgain)
        {
            return;
        }

        Draw(_lastIndex);
    }

    // 대기가 끝났다(성공·실패·타임아웃 공통) — 버튼을 다시 연다 (ServerWaitManager.Begin의 onClosed)
    private void OnWaitClosed()
    {
        _isWaiting  = false;
        _waitHandle = null;
        Refresh();
    }

    // 가챠 성공 도착 — 대기를 조용히 닫는다. 보상 표시는 'GachaResultPresenter'가 맡는다 (PlayerDataModel.GachaCompleted 구독)
    private void OnGachaCompleted(List<GachaRewardInfo> rewards)
    {
        _waitHandle?.Succeed();
    }

    // 가챠 실패 도착 — 사유를 사람이 읽을 문구로 옮겨 알림에 띄운다 (PlayerDataModel.GachaFailed 구독)
    private void OnGachaFailed(EResultCode code)
    {
        _waitHandle?.Fail(ResultMessages.ToText(code));
    }

    #endregion

    #region 표시 갱신

    // 버튼 잠금을 지금 상태에 맞춘다 (Start · OnEnable · 재화 변경 · 대기 시작/종료).
    //
    // 두 축을 함께 본다 — 대기 중이면 전부 잠기고, 대기가 풀려도 골드가 모자란 버튼은 잠긴 채로 남는다.
    // 한 축만 보면 대기가 끝나는 순간 살 수 없는 버튼까지 함께 열린다.
    //
    // ※ 클라 판단은 표시용일 뿐이다. 실제 거절은 서버가 하고('NotEnoughCurrency'),
    //   그 사유는 'ResultMessages'를 거쳐 알림으로 뜬다.
    private void Refresh()
    {
        for (int i = 0; i < _cards.Count; i++)
        {
            _cards[i].SetState(_data.Gold, _isWaiting);
        }

        DrawStateChanged?.Invoke();
    }

    #endregion
}

using System;
using System.Collections.Generic;
using GameData;
using MikaNetwork;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 가챠 패널 — 거래 열의 뽑기 화면. 풀마다 단차·10연차 버튼을 갖는다.
//
// ■ 왜 거래 열인가
// 가챠는 기획상 상점에 속한다 — 가챠 티켓이 골드 상점 품목이다.
// → GameDesign/기획/거래/README.md 3.2
//
// ■ 이름과 비용은 코드에 박지 않는다
// 'GachaInfoTable'(Gacha.xlsx)의 Name·CostSingle·CostMulti를 읽어 채운다.
// ⚠️ 10연차가 단차 x 10이라는 보장이 없다 — 컬럼이 따로다. 곱해서 만들지 않는다.
//
// ■ 결과 내용은 여기서 보지 않는다
// 뽑힌 보상·인벤토리 반영은 'PlayerDataModel'가 처리하고, 결과 팝업은 최상단의
// 'GachaResultPresenter'('!System Canvas')가 스스로 구독해서 띄운다 — 여기 자식으로 두지 않는다.
// 다만 성공/실패 도착 여부는 여기서 구독한다 — 요청 중 로딩·버튼 잠금·실패 알림을 위해서다.
public class GachaPresenter : MonoBehaviour
{
    // 뽑기 버튼 하나 — 어느 풀을 몇 회 뽑는가. 인스펙터에서 짝지어 넣는다.
    [Serializable]
    private struct DrawEntry
    {
        [Tooltip("뽑을 풀 Id (= GachaInfoTable의 GachaInfoTID). 1=구슬 상자 · 2=캐릭터 소환")]
        public int gachaId;

        [Tooltip("한 번에 뽑을 횟수. 서버가 1과 10만 받는다")]
        public int drawCount;

        [Tooltip("이 줄의 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
        public Button button;

        [Tooltip("풀 이름 문구. 테이블에서 읽어 채운다")]
        public TMP_Text nameText;

        [Tooltip("필요 골드 문구. 테이블에서 읽어 채운다")]
        public TMP_Text costText;
    }

    // ※ NonReorderable로 두 가지를 동시에 얻는다 —
    //   [1] 목록 순서를 화면과 나란히 두어야 사람이 한눈에 대조할 수 있다.
    //   [2] reorderable list로 그려지면 Unity가 그 위의 [CenterHeader]를 건너뛴다 ('UI 규칙.md'의 "공통 작성 규약")
    [CenterHeader("참조")]
    [SerializeField, NonReorderable, Tooltip("뽑기 버튼들. 화면과 같은 순서로 넣는다 (자원 1회·10회 · 캐릭터 1회·10회)")]
    private DrawEntry[] draws = new DrawEntry[0];

    // draws[i]의 비용. 버튼 잠금 판정이 매 재화 변경마다 도므로 테이블을 다시 뒤지지 않는다.
    private readonly List<long> _costs = new List<long>();

    private PlayerDataModel   _data    = null!;
    private NetworkManager    _network = null!;
    private ServerWaitManager _wait    = null!;

    // 진행 중인 가챠 대기의 손잡이. 응답이 오면 결과를 보고하고, 무응답이면 스스로 타임아웃돼 잠금을 푼다.
    private ServerWaitHandle? _waitHandle;

    // 응답을 기다리는 중인가 — 연타로 두 번 나가면 인벤토리·재화가 꼬이므로 버튼을 잠근다.
    private bool _isWaiting;

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 배선 → 초기화 순서로 진행한다 (클라 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다(MonoService 주석).
    private void Start()
    {
        _data    = Services.Get<PlayerDataModel>();
        _network = NetworkManager.Instance;
        _wait    = Services.Get<ServerWaitManager>();

        ValidateDraws();

        Subscribe();
        BindButtons();
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

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
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

    // 인스펙터 배선이 테이블과 맞는지 보고, 이름·비용 문구를 채운다 (Start에서 한 번).
    //
    // 빠지거나 어긋난 줄은 조용히 안 열린다 — 버튼을 눌러도 아무 일이 없어서 버튼이 고장 난 것처럼
    // 보인다. 원인이 인스펙터·테이블이라는 걸 드러내려고 여기서 먼저 알린다.
    private void ValidateDraws()
    {
        _costs.Clear();

        if (draws.Length == 0)
        {
            ClientLogger.Error(ClientLogger.UI, "가챠 버튼이 하나도 없다 — Gacha Presenter의 Draws를 확인할 것.", this);
        }

        foreach (DrawEntry entry in draws)
        {
            _costs.Add(ResolveCost(entry));
        }
    }

    // 한 줄의 비용을 정하고 문구를 채운다. 테이블에 없으면 0을 돌려주고 알린다 (ValidateDraws에서 호출).
    private long ResolveCost(DrawEntry entry)
    {
        if (entry.button == null)
        {
            ClientLogger.Error(ClientLogger.UI, $"가챠 버튼이 비어 있다 (풀 {entry.gachaId}, {entry.drawCount}회).", this);

            return 0L;
        }

        if (entry.drawCount != 1 && entry.drawCount != 10)
        {
            ClientLogger.Error(ClientLogger.UI,
                $"허용되지 않는 뽑기 횟수 {entry.drawCount} — 서버가 1과 10만 받는다 (풀 {entry.gachaId}).", this);
        }

        if (!GameDataLoader.TryGetGachaInfo(entry.gachaId, out GachaInfoTableRow info))
        {
            ClientLogger.Error(ClientLogger.UI,
                $"GachaInfoTable에 없는 풀 {entry.gachaId}다 — 서버가 InvalidGachaId로 거절한다.", this);

            return 0L;
        }

        // ⚠️ 10연차 비용은 단차 x 10이 아니다. 컬럼이 따로라 각각 읽는다.
        long cost = entry.drawCount == 1 ? info.CostSingle : info.CostMulti;

        // 지금 재화 축은 골드뿐이다. 다이아 비용이 생기면 잔액 비교(ApplyButtons)도 함께 갈라야 한다.
        if (info.CostCurrency != CurrencyType.Gold)
        {
            ClientLogger.Warn(ClientLogger.UI,
                $"풀 {entry.gachaId}의 비용 재화가 골드가 아니다({info.CostCurrency}) — 버튼 잠금이 골드로만 판정된다.", this);
        }

        if (entry.nameText != null)
        {
            entry.nameText.text = $"{info.Name} {entry.drawCount}회";
        }

        if (entry.costText != null)
        {
            entry.costText.text = $"{cost:N0} G";
        }

        return cost;
    }

    // 버튼을 배선한다 (Start에서 호출).
    private void BindButtons()
    {
        for (int i = 0; i < draws.Length; i++)
        {
            if (draws[i].button == null)
            {
                continue;
            }

            // 반복 변수를 그대로 넘기면 모든 콜백이 마지막 값을 본다. 복사본을 캡처한다.
            int index = i;
            draws[i].button.onClick.AddListener(() => Draw(index));
        }
    }

    #endregion

    #region 뽑기 요청

    // 'index'번째 줄로 뽑기를 요청한다 (버튼 OnClick에 코드로 연결).
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

        DrawEntry entry = draws[index];

        _network.Send(new C_GachaDrawRequest
        {
            GachaId   = entry.gachaId,
            DrawCount = entry.drawCount
        });

        ClientLogger.Info(ClientLogger.Send, $"가챠 요청 — 풀={entry.gachaId}, {entry.drawCount}회");

        // 대기 시작 — 로딩 표시·무응답 감시·알림은 ServerWaitManager가 공통으로 처리한다.
        _isWaiting  = true;
        Refresh();
        _waitHandle = _wait.Begin($"가챠 {entry.drawCount}회", onClosed: OnWaitClosed);
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
    // 두 축을 함께 본다 — 대기 중이면 전부 잠기고, 대기가 풀려도 골드가 모자란 줄은 잠긴 채로 남는다.
    // 한 축만 보면 대기가 끝나는 순간 살 수 없는 버튼까지 함께 열린다.
    //
    // ※ 클라 판단은 표시용일 뿐이다. 실제 거절은 서버가 하고('NotEnoughCurrency'),
    //   그 사유는 'ResultMessages'를 거쳐 알림으로 뜬다.
    private void Refresh()
    {
        for (int i = 0; i < draws.Length; i++)
        {
            if (draws[i].button == null)
            {
                continue;
            }

            long cost = i < _costs.Count ? _costs[i] : 0L;

            draws[i].button.interactable = !_isWaiting && _data.Gold >= cost;
        }
    }

    #endregion
}

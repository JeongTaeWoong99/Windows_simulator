using System;
using System.Collections.Generic;
using GameData;
using MikaNetwork;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 특성 트리 화면 — 창고 열의 '특성' 탭에서만 켜진다.
//
// ■ 창고 안에 있지만 격자가 아니다
// 자원·캐릭터·장비 셋은 'StorageGridPresenter' 하나가 공급자만 갈아 끼워 그리는데,
// 특성은 칸 목록이 아니라 **선으로 이어진 트리**라 그 격자에 들어가지 않는다.
// 그래서 이 탭에서는 격자·도구 줄·판매 목록이 꺼지고 이 패널이 대신 켜진다
// (근거와 예외 규칙은 'Storage 규칙.md'의 "탭이 달라도 격자는 하나다").
//
// ■ 노드 45개를 씬에 깔지 않는다
// 프리팹 하나를 찍어 풀로 쓴다 — 탭마다 줄 수가 다르고(속도 5줄 · 레벨 4줄),
// 칸을 만들고 부수기를 반복하면 상시 실행 앱에서 GC가 쌓인다(캐릭터 줄과 같은 판단).
//
// ■ 트리 모양은 데이터가 정한다
// 열 = 산업('UserTraitTableRow.Industry'), 줄 = 같은 산업 안의 TID 순서다.
// **TID 규칙을 여기 베끼지 않는다** — 시트에 단이 하나 늘면 트리도 저절로 한 줄 는다.
//
// ■ 찍었는지는 열린 해금 목록이 말한다
// 노드 TID = UnlockTID라 특성 전용 보유 목록이 없다. 조건(계정 레벨·선행)도
// **같은 TID의 'UnlockTable' 행**에 있어, 한 칸을 그리려면 두 테이블을 함께 읽는다.
public class TraitPresenter : MonoBehaviour
{
    // 이 화면 안의 두 구역. 산업 축은 같고 세로로 쌓이는 것이 다르다.
    private enum TraitTab
    {
        // 산업별 작업속도 가산 (+10%씩 누적)
        Speed,

        // 산업 레벨 해금 (Lv2~) — 찍으면 작업슬롯에서 그 레벨을 고를 수 있다
        Level,
    }

    // 트리의 열 = 산업. 'EIndustryType'의 None을 뺀 순서이고, 화면의 왼쪽부터다.
    private static readonly EIndustryType[] Columns =
    {
        EIndustryType.Farming,
        EIndustryType.Fishing,
        EIndustryType.Mining,
        EIndustryType.Logging,
        EIndustryType.Hunting,
    };

    // 탭 버튼 하나와 그 버튼이 여는 구역. 인스펙터에서 짝지어 넣는다.
    [Serializable]
    private struct TabEntry
    {
        [Tooltip("이 화면 안의 탭 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
        public Button button;

        [Tooltip("이 버튼이 여는 구역")]
        public TraitTab tab;
    }

    [CenterHeader("참조")]
    [SerializeField, Tooltip("노드 한 칸 프리팹 (TraitNodeView 포함). Content 아래에 런타임 생성된다")]
    private TraitNodeView nodePrefab = null!;

    [SerializeField, Tooltip("노드가 들어가는 부모 — Scroll View > Viewport > Content (GridLayoutGroup 5열)")]
    private Transform nodeParent = null!;

    [SerializeField, Tooltip("남은 특성 포인트 문구")]
    private TMP_Text pointText = null!;

    [SerializeField, Tooltip("창고 탭 줄. 특성 탭일 때만 이 패널이 켜진다")]
    private StorageTabPresenter storageTabs = null!;

    // ※ NonReorderable — reorderable list 로 그려지면 Unity 가 그 위의 [CenterHeader] 를 건너뛴다
    //   ('UI 규칙.md'의 "공통 작성 규약").
    [CenterHeader("구역 탭")]
    [SerializeField, NonReorderable, Tooltip("구역 탭 버튼들. TraitTab 값마다 정확히 한 줄씩, 화면과 같은 순서로 넣는다")]
    private TabEntry[] tabs = new TabEntry[0];

    [CenterHeader("색")]
    [SerializeField, Tooltip("지금 열린 구역의 탭 색 — 창고 탭 줄과 같은 금색")]
    private Color selectedTabColor = new Color(0.839f, 0.682f, 0.067f);

    [SerializeField, Tooltip("열리지 않은 구역의 탭 색")]
    private Color normalTabColor = Color.white;

    // 만들어 둔 노드 칸. 파괴하지 않고 재사용한다 (남는 칸은 꺼 둔다).
    private readonly List<TraitNodeView> _nodes = new List<TraitNodeView>();

    private PlayerDataModel   _data = null!;
    private UIManager         _ui   = null!;
    private ServerWaitManager _wait = null!;

    private TraitTab _currentTab = TraitTab.Speed;
    private bool     _isSubscribed;
    private bool     _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 내가 보낸 찍기 요청의 대기. null이면 기다리는 요청이 없다.
    private ServerWaitHandle? _learnWait;

    // 참조 확보 → 구독 → 초기화 순서로 진행한다 (클라 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다(MonoService 주석).
    private void Start()
    {
        this.RequireRef(nodePrefab,  nameof(nodePrefab));
        this.RequireRef(nodeParent,  nameof(nodeParent));
        this.RequireRef(pointText,   nameof(pointText));
        this.RequireRef(storageTabs, nameof(storageTabs));

        _data = Services.Get<PlayerDataModel>();
        _ui   = Services.Get<UIManager>();
        _wait = Services.Get<ServerWaitManager>();

        ValidateTabs();
        BindTabButtons();

        // ⚠️ 창고 탭 구독만 Start/OnDestroy에 건다 — 이 패널은 자기 오브젝트를 끄기 때문이다.
        //    OnDisable에서 풀면 다시 켤 신호를 받을 길이 사라져 특성 탭에 영영 못 돌아온다
        //    (도구 줄이 같은 이유로 그렇게 한다 → 'Storage 규칙.md').
        storageTabs.TabChanged += ApplyStorageTab;

        Subscribe();
        ShowTab(_currentTab);

        _isReady = true;

        // 씬에는 켜진 채로 저장한다(꺼진 채면 이 Start가 영영 안 돈다).
        // 배선이 끝난 지금 현재 탭을 보고 스스로 물러난다.
        ApplyStorageTab(storageTabs.CurrentTab);
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    //
    // ★ 재구독만으로는 부족하다 — 닫혀 있는 동안 온 해금·레벨 변경을 놓쳤기 때문이다.
    //   캐시(PlayerDataModel)는 계속 살아 있으므로 다시 그리기만 하면 즉시 맞는다.
    private void OnEnable()
    {
        if (!_isReady)
        {
            return;
        }

        Subscribe();
        Redraw();
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
    }

    // 창고 탭 구독 해제 (Unity 메시지). 자기 오브젝트를 끄므로 여기서만 푼다.
    private void OnDestroy()
    {
        storageTabs.TabChanged -= ApplyStorageTab;
    }

    #region 구독

    // 해금·계정 레벨 변경 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed              = true;
        _data.UnlocksChanged      += Redraw;      // 찍힌 표시는 열린 해금 목록이 바꾼다
        _data.AccountLevelChanged += Redraw;      // 남은 포인트·계정 레벨 조건
        _data.TraitLearnCompleted += OnTraitLearnCompleted;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed              = false;
        _data.UnlocksChanged      -= Redraw;
        _data.AccountLevelChanged -= Redraw;
        _data.TraitLearnCompleted -= OnTraitLearnCompleted;
    }

    #endregion

    #region 탭

    // 인스펙터 배선이 'TraitTab'과 맞는지 본다 (Start에서 한 번).
    // 빠진 구역은 조용히 안 열린다 — 버튼을 눌러도 아무 일이 없어 고장처럼 보인다.
    private void ValidateTabs()
    {
        foreach (TraitTab tab in Enum.GetValues(typeof(TraitTab)))
        {
            int count = 0;

            foreach (TabEntry entry in tabs)
            {
                if (entry.tab == tab && entry.button != null)
                {
                    count++;
                }
            }

            if (count != 1)
            {
                ClientLogger.Error(ClientLogger.UI,
                    $"특성 구역 '{tab}'의 버튼이 {count}개다 (정확히 1개여야 한다). " +
                    $"Trait Presenter의 Tabs를 확인할 것.", this);
            }
        }
    }

    // 구역 탭 버튼을 배선한다 (Start에서 한 번).
    //
    // ⚠️ 반복 변수를 람다에 그대로 넘기면 모든 콜백이 마지막 값을 본다. 복사본을 캡처한다.
    private void BindTabButtons()
    {
        foreach (TabEntry entry in tabs)
        {
            if (entry.button == null)
            {
                continue;
            }

            TraitTab tab = entry.tab;
            entry.button.onClick.AddListener(() => ShowTab(tab));
        }
    }

    // 그 구역을 열고 나머지 탭의 선택 표시를 끈다 (Start · 탭 버튼).
    private void ShowTab(TraitTab tab)
    {
        _currentTab = tab;

        RefreshTabSelection();
        Redraw();
    }

    // 지금 열린 구역의 탭만 선택 색으로 칠한다 (ShowTab에서 호출).
    //
    // ⚠️ 'Image.color'가 아니라 'ColorBlock'이다 — 창고 탭 줄과 같은 이유.
    private void RefreshTabSelection()
    {
        foreach (TabEntry entry in tabs)
        {
            if (entry.button == null)
            {
                continue;
            }

            Color target = entry.tab == _currentTab ? selectedTabColor : normalTabColor;

            ColorBlock colors = entry.button.colors;

            colors.normalColor   = target;
            colors.selectedColor = target;
            entry.button.colors  = colors;
        }
    }

    // 창고 탭이 바뀌었다 ('StorageTabPresenter.TabChanged' 구독).
    // 특성 탭일 때만 이 패널이 보인다 — 도구 줄·격자·판매 목록이 같은 방식으로 자기를 끈다.
    private void ApplyStorageTab(StorageTab tab)
    {
        gameObject.SetActive(tab == StorageTab.Trait);
    }

    #endregion

    #region 그리기

    // 지금 구역의 트리를 다시 그린다 (탭 전환 · 해금·레벨 변경 구독).
    private void Redraw()
    {
        pointText.text = $"특성 포인트 {_data.TraitPoint}";

        // 열마다 세로로 쌓을 노드를 모은다. 줄 수는 산업마다 다를 수 있으므로 가장 긴 열에 맞춘다.
        var columnNodes = new List<UserTraitTableRow>[Columns.Length];
        int rowCount    = 0;

        for (int column = 0; column < Columns.Length; column++)
        {
            columnNodes[column] = CollectColumn(Columns[column]);
            rowCount            = Mathf.Max(rowCount, columnNodes[column].Count);
        }

        // 격자는 형제 순서대로 채워지므로 **줄 단위(왼쪽→오른쪽)** 로 넘긴다.
        int used = 0;

        for (int row = 0; row < rowCount; row++)
        {
            for (int column = 0; column < Columns.Length; column++)
            {
                TraitNodeView view = GetNode(used++);
                var           list = columnNodes[column];

                // 이 열이 다른 열보다 짧으면 빈 칸을 둔다 — 칸을 빼면 아래 줄이 왼쪽으로 당겨져
                // 산업 열이 어긋난다(지금 데이터는 다섯 열이 모두 같은 길이다).
                if (row >= list.Count)
                {
                    BindEmpty(view);

                    continue;
                }

                BindNode(view, list[row], hasLink: row > 0);
            }
        }

        // 남는 칸은 파괴하지 않고 꺼 둔다 (속도 5줄 ↔ 레벨 4줄을 오간다).
        for (int i = used; i < _nodes.Count; i++)
        {
            _nodes[i].Clear();
            _nodes[i].gameObject.SetActive(false);
        }
    }

    // 이 산업의 노드를 지금 구역에 맞게 골라 TID 순으로 돌려준다 (Redraw에서 호출).
    //
    // ※ 구역을 'EffectType'으로 가른다 — 속도는 'SpeedAdd', 산업 레벨은 효과가 없고('None')
    //   **UnlockTID가 열리는 것 자체가 결과**다(그 TID를 'IndustryLevelTable'이 참조한다).
    private List<UserTraitTableRow> CollectColumn(EIndustryType industry)
    {
        var result = new List<UserTraitTableRow>();

        foreach (var row in GameDataLoader.UserTraits)
        {
            if ((byte)row.Industry != (byte)industry)
            {
                continue;
            }

            bool isSpeed = row.EffectType == UserTraitEffect.SpeedAdd;

            if (isSpeed == (_currentTab == TraitTab.Speed))
            {
                result.Add(row);
            }
        }

        result.Sort((a, b) => a.UserTraitTID.CompareTo(b.UserTraitTID));

        return result;
    }

    // 칸 하나를 노드에 묶는다 (Redraw에서 호출).
    private void BindNode(TraitNodeView view, UserTraitTableRow trait, bool hasLink)
    {
        view.gameObject.SetActive(true);

        bool learned = _data.IsUnlocked(trait.UserTraitTID);
        bool ready   = learned || MeetsConditions(trait.UserTraitTID);

        TraitNodeView.NodeState state = learned  ? TraitNodeView.NodeState.Learned
                                      : ready    ? TraitNodeView.NodeState.Available
                                                 : TraitNodeView.NodeState.Locked;

        view.Bind(trait.UserTraitTID, trait.Name, BuildDetail(trait, learned), state, hasLink);
        view.Clicked -= OnNodeClicked; // 재사용 칸이라 중복 구독을 먼저 끊는다
        view.Clicked += OnNodeClicked;
    }

    // 열 길이를 맞추기 위한 빈 칸 (Redraw에서 호출)
    private void BindEmpty(TraitNodeView view)
    {
        view.gameObject.SetActive(true);
        view.Bind(0, "", "", TraitNodeView.NodeState.Locked, hasLink: false);
    }

    // 칸 아래 한 줄 — 잠겼으면 **조건**을, 아니면 **효과**를 적는다.
    //
    // ※ 잠긴 노드를 숨기지 않는다(해금 규칙). 무엇이 모자란지가 여기 나와야
    //   "왜 안 눌리지"가 화면에서 끝난다.
    private string BuildDetail(UserTraitTableRow trait, bool learned)
    {
        if (!learned && GameDataLoader.TryGetUnlock(trait.UserTraitTID, out var unlock))
        {
            foreach (int requiredTid in unlock.RequiredUnlockTIDs)
            {
                if (!_data.IsUnlocked(requiredTid))
                {
                    return "앞 단계 먼저";
                }
            }

            if (_data.AccountLevel < unlock.AccountLevel)
            {
                return $"계정 Lv{unlock.AccountLevel} 필요";
            }
        }

        return DescribeEffect(trait);
    }

    // 이 노드가 주는 것 한 줄 (BuildDetail · 확인 문구에서 호출).
    //
    // ※ 산업 레벨 노드는 효과가 없다 — 여는 레벨의 이름('밭')을 대신 적는다.
    //   그게 이 노드의 실제 결과이고, 트리만 보고도 무엇이 열리는지 알 수 있어야 한다.
    private static string DescribeEffect(UserTraitTableRow trait)
    {
        if (trait.EffectType == UserTraitEffect.SpeedAdd)
        {
            return $"작업속도 +{trait.EffectValue / 10f:0.#}%"; // EffectValue는 천분율
        }

        if (GameDataLoader.TryGetIndustryLevelByUnlockTid(trait.UserTraitTID, out var level))
        {
            return $"Lv{level.Level} {level.Name}";
        }

        return "";
    }

    // 칸을 꺼내 온다. 모자라면 프리팹을 하나 더 찍는다 (Redraw에서 호출).
    //
    // ⚠️ 'LayoutGroup'이 배치하는 프리팹은 만든 자리에서 바로 태운다 — 부모를 나중에 옮기면
    //    한 프레임 동안 엉뚱한 자리에 그려진다('UI 규칙.md' 3장).
    private TraitNodeView GetNode(int index)
    {
        while (_nodes.Count <= index)
        {
            _nodes.Add(Instantiate(nodePrefab, nodeParent));
        }

        return _nodes[index];
    }

    #endregion

    #region 찍기

    // 노드를 눌렀다 (TraitNodeView.Clicked 구독).
    //
    // 순서는 서버 판정과 같다 — 선행 → 계정 레벨 → 포인트.
    // ※ 클라 판정은 안내일 뿐이다. 통과해도 서버가 다시 검사한다(게임기획코어 P4).
    private void OnNodeClicked(TraitNodeView view)
    {
        if (_learnWait != null)
        {
            return; // 응답 대기 중 — 같은 노드를 두 번 보내면 두 번째가 AlreadyUnlocked로 거절된다
        }

        if (!GameDataLoader.TryGetUserTrait(view.UserTraitTid, out var trait))
        {
            return; // 빈 칸(열 맞추기용)이거나 테이블에 없는 TID
        }

        if (_data.IsUnlocked(trait.UserTraitTID))
        {
            _wait.RaiseNotice("이미 배운 특성입니다.");

            return;
        }

        if (!MeetsConditions(trait.UserTraitTID, notice: true))
        {
            return;
        }

        if (_data.TraitPoint < trait.TraitPoint)
        {
            _wait.RaiseNotice($"특성 포인트가 부족합니다. ({trait.TraitPoint}점 필요)");

            return;
        }

        _ui.AskConfirm($"특성 포인트 {trait.TraitPoint}점을 사용합니다.\n'{trait.Name}'을(를) 배우시겠습니까?",
                       () => RequestLearn(trait.UserTraitTID));
    }

    // 계정 레벨·선행을 만족하는가. 'notice'면 못 만족한 이유를 알린다 (그리기 · 클릭에서 호출).
    private bool MeetsConditions(int userTraitTid, bool notice = false)
    {
        if (!GameDataLoader.TryGetUnlock(userTraitTid, out var unlock))
        {
            return true; // 조건 행이 없으면 조건이 없는 것이다
        }

        foreach (int requiredTid in unlock.RequiredUnlockTIDs)
        {
            if (!_data.IsUnlocked(requiredTid))
            {
                if (notice)
                {
                    _wait.RaiseNotice($"{DescribeTrait(requiredTid)}을(를) 먼저 배워야 합니다.");
                }

                return false;
            }
        }

        if (_data.AccountLevel < unlock.AccountLevel)
        {
            if (notice)
            {
                _wait.RaiseNotice($"계정 레벨 {unlock.AccountLevel}이(가) 필요합니다. (지금 {_data.AccountLevel})");
            }

            return false;
        }

        return true;
    }

    // 특성 TID를 사람이 읽는 이름으로 ('UserTraitTable.Name').
    private static string DescribeTrait(int userTraitTid)
    {
        return GameDataLoader.TryGetUserTrait(userTraitTid, out var row) && row.Name.Length > 0
            ? row.Name
            : $"특성 #{userTraitTid}";
    }

    // 찍기 요청을 보내고 응답을 기다린다 (확인 팝업 콜백).
    //
    // 결과는 'OnTraitLearnCompleted'가 받는다. 찍힌 표시는 앞서 오는 'S_UnlockResponse'
    // → 'UnlocksChanged' → 'Redraw'가 이미 바꾼다.
    private void RequestLearn(int userTraitTid)
    {
        // 로그인 전에 보내면 서버가 응답 없이 버린다 — 'WorkStationSelectPresenter.CanSend'와 같은 이유
        if (!_data.IsLoggedIn)
        {
            ClientLogger.Warn(ClientLogger.Send, "특성 찍기 요청을 보내지 않았다 — 로그인이 먼저다(서버가 응답 없이 버린다)");

            return;
        }

        ClientLogger.Info(ClientLogger.Send, $"특성 찍기 요청 — {userTraitTid}");

        NetworkManager.Instance.Send(new C_UserTraitLearnRequest { UserTraitTID = userTraitTid });
        _learnWait = _wait.Begin("특성 찍기", onClosed: () => _learnWait = null);
    }

    // 찍기 결과 (PlayerDataModel.TraitLearnCompleted 구독).
    private void OnTraitLearnCompleted(bool success, EResultCode code)
    {
        if (_learnWait == null)
        {
            return; // 내가 보낸 요청이 아니다
        }

        if (success)
        {
            _learnWait.Succeed();
        }
        else
        {
            _learnWait.Fail(ResultMessages.ToText(code));
        }
    }

    #endregion
}

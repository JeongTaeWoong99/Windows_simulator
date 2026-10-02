using System.Collections.Generic;
using GameData;
using MikaNetwork;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// 특성 화면 — 인벤토리 열의 '특성' 탭에서만 켜진다.
//
// ■ 인벤토리 안에 있지만 격자가 아니다
// 자원·캐릭터·장비 셋은 'InventoryGridPresenter' 하나가 공급자만 갈아 끼워 그리는데,
// 특성은 칸 목록이 아니라 **산업 × 종류 표**라 그 격자에 들어가지 않는다.
// 그래서 이 탭에서는 격자·도구 줄·판매 목록이 꺼지고 이 패널이 대신 켜진다
// (근거와 예외 규칙은 'Inventory 규칙.md'의 "탭이 달라도 격자는 하나다").
//
// ■ 산업 × 종류 표다 — 탭이 없다 (2026-10-02 · T-116 A안)
// 줄 = 공통 · 농사 · 낚시 · 채굴 · 벌목 · 사냥 (왼쪽에 산업 이름), 열 = 개척 · 속도 · 산출량 (위에 고정 머리 줄).
// 포인트로 전부 올릴 수 없어(130점 중 99점) **"어느 산업을 키울까"가 곧 선택**이다 —
// 그 비교는 한 산업의 세 특성이 같은 줄에 놓여야 된다. 예전엔 노드 사슬을 구역 탭 둘로 갈랐다.
//
// ■ 표 모양은 데이터가 정한다
// 칸 = (줄의 산업, 열의 효과)에 맞는 'UserTraitTable' 행이다. 없으면 빈 칸으로 자리만 지킨다.
// **TID 규칙을 여기 베끼지 않는다** — 효과는 'EffectType', 산업은 'Industry'로 찾는다.
//
// ■ 특성은 레벨형이다
// 특성 하나가 기본 레벨 → 최대 레벨을 1씩 오른다. 레벨은 'PlayerDataModel.GetTraitLevel',
// 레벨마다의 조건(계정 레벨)은 'UserTraitLevelTable', 비용·효과는 레벨마다 같다('UserTraitTable').
public class TraitPresenter : MonoBehaviour
{
    // 표의 줄 = 산업. 'None'은 전 산업에 붙는 공통 특성 줄이다.
    private static readonly EIndustryType[] Rows =
    {
        EIndustryType.None,
        EIndustryType.Farming,
        EIndustryType.Fishing,
        EIndustryType.Mining,
        EIndustryType.Logging,
        EIndustryType.Hunting,
    };

    // 표의 열 = 효과. 줄 프리팹의 칸 수(3)와 같아야 한다.
    private static readonly UserTraitEffect[] Columns =
    {
        UserTraitEffect.IndustryUnlock,
        UserTraitEffect.SpeedAdd,
        UserTraitEffect.YieldAdd,
    };

    // 열 이름 — 머리 줄과 칸 이름이 함께 쓴다. 'Columns'와 같은 순서다.
    private static readonly string[] ColumnNames = { "개척", "속도", "산출량" };

    [CenterHeader("참조")]
    [SerializeField, Tooltip("표 한 줄 프리팹 (TraitRowView — 산업 이름 + 칸 셋). Content 아래에 런타임 생성된다")]
    private TraitRowView rowPrefab = null!;

    [SerializeField, Tooltip("줄이 들어가는 부모 — Scroll View > Viewport > Content (VerticalLayoutGroup)")]
    private Transform rowParent = null!;

    [SerializeField, NonReorderable, Tooltip("머리 줄의 열 이름 셋 — 개척 · 속도 · 산출량 순. 스크롤 밖에 고정이다")]
    private TMP_Text[] headerTexts = new TMP_Text[0];

    [SerializeField, Tooltip("인벤토리 탭 줄. 특성 탭일 때만 이 패널이 켜진다")]
    [FormerlySerializedAs("storageTabs")]
    private InventoryTabPresenter inventoryTabs = null!;

    [CenterHeader("정보 영역")]
    [SerializeField, Tooltip("고른 특성의 이름 + 'Lv 2 / 5'")]
    private TMP_Text detailNameText = null!;

    [SerializeField, Tooltip("고른 특성의 종류 설명 — 무엇이 좋아지는지 (산출량 오해 방지)")]
    private TMP_Text detailDescText = null!;

    [SerializeField, Tooltip("지금 ▶ 다음 비교 상자")]
    private TraitDetailStatsView stats = null!;

    [SerializeField, Tooltip("버튼 바로 위 한 줄 — '조건 계정 Lv35 · 보유 포인트 13'. 고른 것이 없어도 보유 포인트는 보인다")]
    private TMP_Text requirementText = null!;

    [SerializeField, Tooltip("아무것도 고르지 않았을 때의 안내 문구. 빈 칸은 고장과 구분되지 않는다")]
    private TMP_Text detailEmptyText = null!;

    [SerializeField, Tooltip("고른 특성을 1레벨 올리는 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button confirmButton = null!;

    [SerializeField, Tooltip("버튼 글씨 — '레벨 올리기 (1점)'. 비용을 코드가 채운다")]
    private TMP_Text confirmLabel = null!;

    [CenterHeader("색")]
    [SerializeField, Tooltip("[레벨 올리기] — 지금 올릴 수 있을 때")]
    private UIThemeRole confirmReadyRole = UIThemeRole.ButtonPrimary;

    [SerializeField, Tooltip("[레벨 올리기] — 계정 Lv · 포인트가 모자랄 때. 눌리기는 한다(누르면 이유 알림)")]
    private UIThemeRole confirmBlockedRole = UIThemeRole.ButtonDisabled;

    // 만들어 둔 줄. 파괴하지 않고 재사용한다 (남는 것은 꺼 둔다).
    private readonly List<TraitRowView> _rows = new List<TraitRowView>();

    private PlayerDataModel   _data = null!;
    private UIManager         _ui   = null!;
    private ServerWaitManager _wait = null!;

    private int  _selectedTraitTid; // 정보 영역에 펼친 특성. 0이면 고른 것이 없다
    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 내가 보낸 레벨 올리기 요청의 대기. null이면 기다리는 요청이 없다.
    private ServerWaitHandle? _learnWait;

    // 참조 확보 → 구독 → 초기화 순서로 진행한다 (클라 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다(MonoService 주석).
    private void Start()
    {
        this.RequireRef(rowPrefab,     nameof(rowPrefab));
        this.RequireRef(rowParent,     nameof(rowParent));
        this.RequireRef(inventoryTabs, nameof(inventoryTabs));

        this.RequireRef(detailNameText,    nameof(detailNameText));
        this.RequireRef(detailDescText,    nameof(detailDescText));
        this.RequireRef(stats,             nameof(stats));
        this.RequireRef(requirementText,   nameof(requirementText));
        this.RequireRef(detailEmptyText,   nameof(detailEmptyText));
        this.RequireRef(confirmButton,     nameof(confirmButton));
        this.RequireRef(confirmLabel,      nameof(confirmLabel));

        _data = Services.Get<PlayerDataModel>();
        _ui   = Services.Get<UIManager>();
        _wait = Services.Get<ServerWaitManager>();

        confirmButton.onClick.AddListener(OnConfirmClicked);
        BindHeader();

        // ⚠️ 인벤토리 탭 구독만 Start/OnDestroy에 건다 — 이 패널은 자기 오브젝트를 끄기 때문이다.
        //    OnDisable에서 풀면 다시 켤 신호를 받을 길이 사라져 특성 탭에 영영 못 돌아온다
        //    (도구 줄이 같은 이유로 그렇게 한다 → 'Inventory 규칙.md').
        inventoryTabs.TabChanged += ApplyInventoryTab;

        Subscribe();
        Redraw();

        _isReady = true;

        // 배선이 끝난 지금 현재 탭을 보고 스스로 물러난다.
        // ※ 꺼진 채로 저장돼도 된다 — 탭 줄이 'ShowTab' 전에 한 번 켜 주므로 이 Start는 반드시 돈다
        //   ('InventoryTabPresenter.WakeTabScreens').
        ApplyInventoryTab(inventoryTabs.CurrentTab);
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    //
    // ★ 재구독만으로는 부족하다 — 닫혀 있는 동안 온 특성·계정 레벨 변경을 놓쳤기 때문이다.
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

    // 인벤토리 탭 구독 해제 (Unity 메시지). 자기 오브젝트를 끄므로 여기서만 푼다.
    private void OnDestroy()
    {
        inventoryTabs.TabChanged -= ApplyInventoryTab;
    }

    #region 구독

    // 특성·계정 레벨 변경 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed              = true;
        _data.TraitsChanged       += Redraw;      // 특성 레벨 (로그인 목록 · 올리기 응답)
        _data.AccountLevelChanged += Redraw;      // 남은 포인트 · 다음 레벨의 계정 레벨 조건
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
        _data.TraitsChanged       -= Redraw;
        _data.AccountLevelChanged -= Redraw;
        _data.TraitLearnCompleted -= OnTraitLearnCompleted;
    }

    // 인벤토리 탭이 바뀌었다 ('InventoryTabPresenter.TabChanged' 구독).
    // 특성 탭일 때만 이 패널이 보인다 — 도구 줄·격자·판매 목록이 같은 방식으로 자기를 끈다.
    private void ApplyInventoryTab(InventoryTab tab)
    {
        gameObject.SetActive(tab == InventoryTab.Trait);
    }

    #endregion

    #region 표

    // 머리 줄에 열 이름을 적는다 (Start에서 한 번) — 열 순서의 주인이 이 클래스라 이름도 여기서 준다.
    private void BindHeader()
    {
        for (int i = 0; i < headerTexts.Length && i < ColumnNames.Length; i++)
        {
            headerTexts[i].text = ColumnNames[i];
        }
    }

    // 표와 정보 영역을 다시 그린다 (칸 선택 · 특성·계정 레벨 변경 구독).
    //
    // ※ 정보 영역도 여기서 함께 그린다 — 올린 직후 옛 레벨이 남아 있으면 안 되고,
    //   특성·계정 레벨 변경은 전부 이 함수를 거친다.
    private void Redraw()
    {
        int used = 0;

        foreach (EIndustryType industry in Rows)
        {
            var traits = new UserTraitTableRow?[Columns.Length];

            if (!CollectRow(industry, traits))
            {
                continue; // 이 산업에 특성이 하나도 없다 — 빈 줄을 그리지 않는다
            }

            TraitRowView row = GetRow(used++);

            row.gameObject.SetActive(true);
            row.Bind(industry == EIndustryType.None ? "공통" : IndustryLabel.Get(industry));

            for (int column = 0; column < Columns.Length && column < row.CellCount; column++)
            {
                TraitNodeView cell  = row.GetCell(column);
                var           trait = traits[column];

                if (trait == null)
                {
                    cell.BindBlank(); // 열을 맞추는 빈 칸 — 공통 줄의 개척·속도 자리

                    continue;
                }

                // 칸 이름은 열 이름이다 — 줄이 산업을 말한다. 공통 줄만 특성 이름 그대로('공통 산출량').
                BindCell(cell, trait, industry == EIndustryType.None ? trait.Name : ColumnNames[column]);
            }
        }

        // 남는 줄은 파괴하지 않고 꺼 둔다.
        for (int i = used; i < _rows.Count; i++)
        {
            _rows[i].gameObject.SetActive(false);
        }

        RedrawDetail();
    }

    // 이 산업 줄의 칸을 열 순서대로 채운다. 하나라도 있으면 true (Redraw에서 호출).
    private static bool CollectRow(EIndustryType industry, UserTraitTableRow?[] row)
    {
        bool any = false;

        foreach (var trait in GameDataLoader.UserTraits)
        {
            if ((byte)trait.Industry != (byte)industry)
            {
                continue;
            }

            int column = System.Array.IndexOf(Columns, trait.EffectType);

            if (column < 0 || row[column] != null)
            {
                continue; // 표에 열이 없는 효과 · 같은 칸에 두 번째 특성 — 첫 것만 그린다
            }

            row[column] = trait;
            any         = true;
        }

        return any;
    }

    // 칸 하나를 특성에 묶는다 (Redraw에서 호출).
    private void BindCell(TraitNodeView cell, UserTraitTableRow trait, string displayName)
    {
        int level = _data.GetTraitLevel(trait.UserTraitTID);

        cell.Bind(trait.UserTraitTID, displayName, level, trait.MaxLevel,
                  GetNodeState(trait, level), isSelected: trait.UserTraitTID == _selectedTraitTid);
    }

    // 칸의 색 — 최대 레벨 · 올릴 수 있음 · 다음 레벨의 계정 레벨 미달.
    //
    // ※ 포인트 부족은 색으로 가르지 않는다 — 모든 칸에 똑같이 걸려 전부 회색이 되면 아무것도 읽히지 않는다.
    //   누르면 알림으로 말한다.
    private TraitNodeView.NodeState GetNodeState(UserTraitTableRow trait, int level)
    {
        if (level >= trait.MaxLevel)
        {
            return TraitNodeView.NodeState.MaxLevel;
        }

        return _data.AccountLevel >= GetRequiredAccountLevel(trait, level + 1)
            ? TraitNodeView.NodeState.Available
            : TraitNodeView.NodeState.Locked;
    }

    // 줄을 꺼내 온다. 모자라면 프리팹을 하나 더 찍는다 (Redraw에서 호출).
    //
    // ⚠️ 'LayoutGroup'이 배치하는 프리팹은 만든 자리에서 바로 태운다 — 부모를 나중에 옮기면
    //    한 프레임 동안 엉뚱한 자리에 그려진다('UI 규칙.md' 3장).
    private TraitRowView GetRow(int index)
    {
        while (_rows.Count <= index)
        {
            var row = Instantiate(rowPrefab, rowParent);

            for (int column = 0; column < row.CellCount; column++)
            {
                row.GetCell(column).Clicked += OnCellClicked; // 줄은 파괴하지 않으므로 한 번만 건다
            }

            _rows.Add(row);
        }

        return _rows[index];
    }

    #endregion

    #region 정보 영역

    // 고른 특성을 정보 영역에 펼친다 (Redraw에서 호출).
    //
    // 이름(+ 레벨) · 설명 · 지금 ▶ 다음 비교 상자 · (빈 공간) · 조건 줄 · [레벨 올리기 (n점)].
    //
    // ※ 변화(상자)와 조건(글자 한 줄)은 모양을 일부러 다르게 한다(2026-10-03 사용자 피드백) — 같은 칸 모양으로
    //   위아래에 두면 세로선이 맞길 기대하게 되고, 성격이 다른 둘이 한 표처럼 읽힌다.
    //   조건은 "버튼을 누를 수 있나"의 정보라 버튼 바로 위에 둔다.
    // ※ 보유 포인트도 이 줄이 말한다 — 표 위에 따로 두던 '특성 포인트 n'은 걷어냈다(같은 숫자가 두 번).
    //
    // ※ 레벨별 계정 Lv 조건(사다리)은 두지 않는다(2026-10-02 사용자 피드백) — 다음 레벨 조건이 칩에 있으면 충분하고,
    //   한 줄을 더 읽게 할 만큼 계획에 쓰이지 않았다.
    // [레벨 올리기]는 **최대 레벨에서만** 숨긴다. 조건 미달·포인트 부족이면 회색이지만 눌리게 둔다 —
    // 누르면 이유("계정 레벨 n이 필요합니다" 등)가 알림으로 뜬다.
    private void RedrawDetail()
    {
        bool hasSelection = GameDataLoader.TryGetUserTrait(_selectedTraitTid, out var trait);

        detailEmptyText.gameObject.SetActive(!hasSelection);
        detailNameText.gameObject.SetActive(hasSelection);
        detailDescText.gameObject.SetActive(hasSelection);
        stats.gameObject.SetActive(hasSelection);

        if (!hasSelection)
        {
            requirementText.text = DescribePoint(null);
            confirmButton.gameObject.SetActive(false);

            return;
        }

        int  level = _data.GetTraitLevel(trait.UserTraitTID);
        bool isMax = level >= trait.MaxLevel;

        // 최대 레벨은 이름 줄에서 작게 말한다 — 큰 '최대 레벨' 상자는 보기 싫다는 피드백(2026-10-02).
        detailNameText.text = isMax
            ? $"{trait.Name}  <size=70%><color=#{Hex(UIThemeRole.ButtonSelected)}>Lv {level} / {trait.MaxLevel} · 최대</color></size>"
            : $"{trait.Name}  <size=70%><color=#{Hex(UIThemeRole.TextSub)}>Lv {level} / {trait.MaxLevel}</color></size>";
        detailDescText.text = DescribeKind(trait);

        bool levelOk = isMax || _data.AccountLevel >= GetRequiredAccountLevel(trait, level + 1);
        bool pointOk = _data.TraitPoint >= trait.TraitPoint;

        BindStats(trait, level, isMax);

        requirementText.text = isMax
            ? DescribePoint(null)
            : $"조건 {Paint($"계정 Lv{GetRequiredAccountLevel(trait, level + 1)}", levelOk)}"
              + (levelOk ? "" : $" <size=85%><color=#{Hex(UIThemeRole.TextSub)}>(지금 {_data.AccountLevel})</color></size>")
              + $"   ·   {DescribePoint(pointOk)}";

        confirmButton.gameObject.SetActive(!isMax);
        confirmLabel.text = $"레벨 올리기 ({trait.TraitPoint}점)";
        PaintConfirm(levelOk && pointOk);
    }

    // 비교 상자를 채운다 (RedrawDetail에서 호출).
    //
    // ※ '→'를 쓰지 않는다 — 폰트(neodgm_pro SDF)가 Static 아틀라스라 그 글리프가 없어 □로 나온다. '▶'를 쓴다.
    private void BindStats(UserTraitTableRow trait, int level, bool isMax)
    {
        if (isMax)
        {
            stats.BindMax("현재 효과", DescribeEffect(trait, level));

            return;
        }

        int next = level + 1;

        stats.BindNext($"지금 (Lv{level})", DescribeEffect(trait, level),
                       $"다음 (Lv{next})",  DescribeNextEffect(trait, next));
    }

    // '보유 포인트 13' (조건 줄). ok가 있으면 숫자를 충족/미달 색으로, null이면(고른 것 없음 · 최대 레벨) 그냥 적는다.
    // ※ 충족 여부는 글자색(Positive · Negative)으로 말한다 — '✓'는 아틀라스에 없다.
    private string DescribePoint(bool? ok)
        => ok == null
            ? $"보유 포인트 {_data.TraitPoint}"
            : $"보유 포인트 {Paint(_data.TraitPoint.ToString(), ok.Value)}";

    // 충족이면 Positive, 아니면 Negative 색으로 감싼다 (조건 줄).
    private static string Paint(string text, bool ok)
        => $"<color=#{Hex(ok ? UIThemeRole.Positive : UIThemeRole.Negative)}>{text}</color>";

    // [레벨 올리기] 색 — 지금 올릴 수 있으면 강조색, 아니면 회색 (RedrawDetail에서 호출).
    //
    // ※ 'interactable'은 끄지 않는다 — 눌러서 이유를 들을 수 있어야 한다. 그래서 색만 'ColorBlock'으로 바꾼다
    //   ('UIThemeColor'가 구운 값과 같은 공식 — 바탕은 흰색, 틴트가 역할 색).
    private void PaintConfirm(bool ready)
    {
        Color      color  = UIThemePalette.Of(ready ? confirmReadyRole : confirmBlockedRole);
        ColorBlock colors = confirmButton.colors;

        colors.normalColor      = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.15f);
        colors.pressedColor     = Color.Lerp(color, Color.black, 0.2f);
        colors.selectedColor    = color;

        confirmButton.colors = colors;
    }

    // 테마 색을 리치 텍스트용 16진수로 — 색을 코드에 박지 않고 팔레트를 따른다.
    private static string Hex(UIThemeRole role) => ColorUtility.ToHtmlStringRGB(UIThemePalette.Of(role));

    // 지금 레벨의 효과 한 마디 — 정보 줄 "효과"의 앞쪽.
    //
    // ※ 효과 = 레벨당 값 × (지금 레벨 − 기본 레벨)이다(서버 'User.SumTraitEffect').
    //   개척은 수치가 아니라 **지금 열린 산업 레벨**을 적는다 — 그게 실제 결과다.
    private static string DescribeEffect(UserTraitTableRow trait, int level)
    {
        switch (trait.EffectType)
        {
            case UserTraitEffect.IndustryUnlock:
                return GameDataLoader.TryGetIndustryLevel((EIndustryType)(byte)trait.Industry, level, out var row)
                    ? $"Lv{level} {row.Name}까지"
                    : $"Lv{level}까지";

            case UserTraitEffect.SpeedAdd:
                return $"속도 {FormatPermille(trait.EffectValue * (level - trait.BaseLevel))}";

            case UserTraitEffect.YieldAdd:
                return $"산출량 {FormatPermille(trait.EffectValue * (level - trait.BaseLevel))}";

            default:
                return "";
        }
    }

    // 다음 레벨에서 바뀌는 것 — 정보 줄 "효과"의 뒤쪽. 개척은 **새로 열리는 산업 레벨**을 적는다.
    private static string DescribeNextEffect(UserTraitTableRow trait, int next)
    {
        if (trait.EffectType == UserTraitEffect.IndustryUnlock)
        {
            return GameDataLoader.TryGetIndustryLevel((EIndustryType)(byte)trait.Industry, next, out var row)
                ? $"Lv{next} {row.Name} 열림"
                : $"Lv{next} 열림";
        }

        return DescribeEffect(trait, next);
    }

    // 이 종류의 특성이 무엇을 좋게 하는지 (정보 영역 이름 아래).
    //
    // ■ 산출량은 이름만으로 뜻이 안 읽힌다 (2026-10-02 사용자 요청)
    // "좋은 게 나올 확률이 오르나?"로 오해되기 쉽다 — 실제로는 **같은 자원이 더** 나오고 희귀도 확률은 그대로다.
    // 속도와 갈리는 점(상자·경험치는 늘지 않는다)까지 적어야 둘 중 무엇을 찍을지 고를 수 있다.
    private static string DescribeKind(UserTraitTableRow trait)
    {
        switch (trait.EffectType)
        {
            case UserTraitEffect.IndustryUnlock:
                return "그 산업의 다음 레벨 작업지를 엽니다. 작업슬롯에서 고를 수 있게 됩니다.";

            case UserTraitEffect.SpeedAdd:
                return "작업 주기가 짧아집니다. 자원 · 상자 · 경험치가 모두 더 빨리 쌓입니다.";

            case UserTraitEffect.YieldAdd:
                return "판정 1회에 같은 자원이 더 나옵니다 (120%면 1개 + 20% 확률로 1개 더). "
                     + "좋은 자원이 나올 확률은 그대로이고, 상자 · 경험치는 늘지 않습니다."
                     + (trait.Industry == IndustryType.None ? " 모든 산업에 붙습니다." : "");

            default:
                return "";
        }
    }

    // 이 특성을 'level'로 올릴 때의 계정 레벨 조건. 조건 행이 없으면 0(조건 없음).
    private static int GetRequiredAccountLevel(UserTraitTableRow trait, int level)
        => GameDataLoader.TryGetUserTraitLevel(trait.UserTraitTID, level, out var row) ? row.AccountLevel : 0;

    // 천분율 가산을 "+15%"로 적는다 (EffectValue는 천분율).
    private static string FormatPermille(int permille)
        => $"+{permille / 10f:0.#}%";

    #endregion

    #region 레벨 올리기

    // 칸을 눌렀다 — **고르기만 한다** (TraitNodeView.Clicked 구독).
    //
    // 누르는 즉시 팝업을 띄우면 읽고 결정할 자리가 없다(T-079). 정보 영역에 펼치고,
    // 올리기는 [레벨 올리기]가 한다.
    private void OnCellClicked(TraitNodeView view)
    {
        if (view.UserTraitTid == 0)
        {
            return; // 빈 칸(열 맞추기용)
        }

        _selectedTraitTid = view.UserTraitTid;

        Redraw();
    }

    // [레벨 올리기]를 눌렀다 — 고른 특성을 1레벨 올린다 (confirmButton.onClick).
    //
    // 순서는 서버 판정과 같다 — 최대 레벨 → 계정 레벨 → 포인트.
    // ※ 클라 판정은 안내일 뿐이다. 통과해도 서버가 다시 검사한다(게임기획코어 P4).
    private void OnConfirmClicked()
    {
        if (_learnWait != null)
        {
            return; // 응답 대기 중 — 연타하면 두 번 올라간다
        }

        if (!GameDataLoader.TryGetUserTrait(_selectedTraitTid, out var trait))
        {
            return; // 고른 것이 없거나 테이블에 없는 TID
        }

        int level = _data.GetTraitLevel(trait.UserTraitTID);

        if (level >= trait.MaxLevel)
        {
            _wait.RaiseNotice("이미 최대 레벨입니다.");

            return;
        }

        int required = GetRequiredAccountLevel(trait, level + 1);

        if (_data.AccountLevel < required)
        {
            _wait.RaiseNotice($"계정 레벨 {required}이(가) 필요합니다. (지금 {_data.AccountLevel})");

            return;
        }

        if (_data.TraitPoint < trait.TraitPoint)
        {
            _wait.RaiseNotice($"특성 포인트가 부족합니다. ({trait.TraitPoint}점 필요)");

            return;
        }

        _ui.AskConfirm($"특성 포인트 {trait.TraitPoint}점을 사용합니다.\n'{trait.Name}'을(를) Lv{level + 1}로 올리시겠습니까?",
                       () => RequestLearn(trait.UserTraitTID));
    }

    // 레벨 올리기 요청을 보내고 응답을 기다린다 (확인 팝업 콜백).
    //
    // 결과는 'OnTraitLearnCompleted'가 받는다. 레벨은 응답('S_UserTraitLearnResponse.Level')
    // → 'TraitsChanged' → 'Redraw'가 바꾼다.
    private void RequestLearn(int userTraitTid)
    {
        // 로그인 전에 보내면 서버가 응답 없이 버린다 — 'WorkStationSelectPresenter.CanSend'와 같은 이유
        if (!_data.IsLoggedIn)
        {
            ClientLogger.Warn(ClientLogger.Send, "특성 레벨 올리기 요청을 보내지 않았다 — 로그인이 먼저다(서버가 응답 없이 버린다)");

            return;
        }

        ClientLogger.Info(ClientLogger.Send, $"특성 레벨 올리기 요청 — {userTraitTid}");

        NetworkManager.Instance.Send(new C_UserTraitLearnRequest { UserTraitTID = userTraitTid });
        _learnWait = _wait.Begin("특성 레벨 올리기", onClosed: () => _learnWait = null);
    }

    // 레벨 올리기 결과 (PlayerDataModel.TraitLearnCompleted 구독).
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

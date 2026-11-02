using System.Collections.Generic;
using GameData;
using MikaNetwork;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 건물 창의 특성 탭 — 그 건물의 트리(노드 + 선)와 고른 노드의 정보 영역. (T-183 · T-182)
//
// ■ 트리 모양은 데이터가 정한다
// 노드 자리는 'UserTraitTable.Column' · 'Row', 선은 'RequiredTraitTIDs'다 — 노드가 늘어도 이 코드를 고치지 않는다.
// **TID 규칙을 여기 베끼지 않는다** — 효과는 'EffectType', 산업은 'Industry'로 읽는다.
//
// ■ 포인트는 건물마다 따로다
// 건물 Lv1당 그 건물 포인트 1점 — 'PlayerDataModel.GetBuildingPoints'가 레벨에서 센다. 서버가 보내 주지 않는다.
//
// ■ 특성은 레벨형이다
// 노드 하나가 기본 레벨 → 최대 레벨을 1씩 오른다. 레벨은 'PlayerDataModel.GetTraitLevel'.
// 비용·효과는 레벨마다 같다('UserTraitTable'). 조건은 앞 노드를 최대까지 찍는 것과 건물 포인트다.
public class TraitPresenter : MonoBehaviour
{
    // 열 이름의 번역 키 — 도움말의 종류 설명이 쓴다. 개척 · 속도 · 산출량 순서다.
    private static readonly string[] KindNameKeys = { "UI.WorkStation.PioneerFallback", "UI.Trait.Speed", "UI.WorkStation.Yield" };

    [CenterHeader("트리")]
    [SerializeField, Tooltip("노드 프리팹 (TraitNodeView). 트리 영역 아래에 런타임 생성된다")]
    private TraitNodeView nodePrefab = null!;

    [SerializeField, Tooltip("노드가 놓이는 영역. 앵커를 비율로 잡아 판 크기가 바뀌어도 자리가 유지된다")]
    private RectTransform nodeArea = null!;

    [SerializeField, Tooltip("앞 노드와 잇는 선 프리팹 (Image — 가로 막대). 노드보다 뒤에 그린다")]
    private Image linkPrefab = null!;

    [SerializeField, Tooltip("선이 놓이는 영역 — 노드 영역과 같은 자리 · 노드보다 앞 형제")]
    private RectTransform linkArea = null!;

    [SerializeField, Tooltip("트리 위의 도움말 아이콘(HelpIcon 프리팹) 툴팁 트리거 — 포인트 · 앞 노드 조건 · 종류별 효과")]
    private TooltipTrigger helpTooltip = null!;

    [CenterHeader("정보 영역")]
    [SerializeField, Tooltip("고른 특성의 이름 + 'Lv 2 / 5'")]
    private TMP_Text detailNameText = null!;

    [SerializeField, Tooltip("고른 특성의 종류 설명 — 무엇이 좋아지는지 (산출량 오해 방지)")]
    private TMP_Text detailDescText = null!;

    [SerializeField, Tooltip("지금 ▶ 다음 비교 상자")]
    private TraitDetailStatsView stats = null!;

    [SerializeField, Tooltip("버튼 바로 위 한 줄 — '조건 농사 개척 최대 · 보유 포인트 13'. 고른 것이 없어도 보유 포인트는 보인다")]
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

    [SerializeField, Tooltip("[레벨 올리기] — 앞 노드 · 포인트가 모자랄 때. 눌리기는 한다(누르면 이유 알림)")]
    private UIThemeRole confirmBlockedRole = UIThemeRole.ButtonDisabled;

    [SerializeField, Tooltip("앞 노드를 다 찍은 선")]
    private UIThemeRole linkOpenRole = UIThemeRole.ButtonSelected;

    [SerializeField, Tooltip("앞 노드를 아직 다 찍지 않은 선")]
    private UIThemeRole linkLockedRole = UIThemeRole.Border;

    // 만들어 둔 노드 · 선. 파괴하지 않고 재사용한다 (남는 것은 꺼 둔다).
    private UIRowList<TraitNodeView> _nodes = null!;
    private UIRowList<Image>         _links = null!;

    // 이 건물의 노드 — 'Show'에서 표를 걸러 둔다 (표는 적재 후 불변이다).
    private readonly List<UserTraitTableRow> _treeNodes = new List<UserTraitTableRow>();

    private PlayerDataModel   _data = null!;
    private UIManager         _ui   = null!;
    private ServerWaitManager _wait = null!;

    private int  _buildingTid;
    private int  _selectedTraitTid; // 정보 영역에 펼친 특성. 0이면 고른 것이 없다
    private int  _columnCount;
    private int  _rowCount;
    private bool _isWired;
    private bool _isSubscribed;

    // 내가 보낸 레벨 올리기 요청의 대기. null이면 기다리는 요청이 없다.
    private ServerWaitHandle? _learnWait;

    // 그 요청의 특성 — 성공하면 이 칸을 번쩍인다
    private int _learningTid;

    // 이 건물의 트리로 화면을 켠다 ('OutpostBuildingPresenter'가 호출).
    public void Show(int buildingTid)
    {
        Wire();

        if (_buildingTid != buildingTid)
        {
            _selectedTraitTid = 0;
        }

        _buildingTid = buildingTid;

        CollectTree();
        gameObject.SetActive(true);
        Subscribe();
        Redraw();

        // ⚠️ 선 길이는 영역의 실제 크기로 잰다 — 방금 켜졌으면 레이아웃이 아직 돌지 않았다.
        //    이 화면의 크기는 부모의 세로 배치가 정하므로 부모부터 다시 잰다.
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform.parent);
        DrawLinks();
    }

    // 화면을 끈다 ('OutpostBuildingPresenter'가 호출).
    public void Hide()
    {
        gameObject.SetActive(false);
    }

    // 참조 확보 → 배선 (Show에서 한 번). 평소 꺼져 있어 'Start'를 기다리면 첫 표시에 늦는다.
    private void Wire()
    {
        if (_isWired)
        {
            return;
        }

        _isWired = true;

        this.RequireRef(nodePrefab,  nameof(nodePrefab));
        this.RequireRef(nodeArea,    nameof(nodeArea));
        this.RequireRef(linkPrefab,  nameof(linkPrefab));
        this.RequireRef(linkArea,    nameof(linkArea));
        this.RequireRef(helpTooltip, nameof(helpTooltip));

        this.RequireRef(detailNameText,  nameof(detailNameText));
        this.RequireRef(detailDescText,  nameof(detailDescText));
        this.RequireRef(stats,           nameof(stats));
        this.RequireRef(requirementText, nameof(requirementText));
        this.RequireRef(detailEmptyText, nameof(detailEmptyText));
        this.RequireRef(confirmButton,   nameof(confirmButton));
        this.RequireRef(confirmLabel,    nameof(confirmLabel));

        _data = Services.Get<PlayerDataModel>();
        _ui   = Services.Get<UIManager>();
        _wait = Services.Get<ServerWaitManager>();

        _nodes = new UIRowList<TraitNodeView>(nodePrefab, nodeArea, node => node.Clicked += OnNodeClicked);
        _links = new UIRowList<Image>(linkPrefab, linkArea);

        confirmButton.onClick.AddListener(OnConfirmClicked);
        helpTooltip.SetProvider(BuildHelpTooltip);
    }

    // 다시 켜졌을 때의 재구독 (Unity 메시지).
    private void OnEnable()
    {
        if (_isWired)
        {
            Subscribe();
        }
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
    }

    // 판 크기가 바뀌면 선을 다시 잰다 — 노드는 앵커 비율이라 따라가지만 선 길이는 픽셀이다 (Unity 메시지)
    private void OnRectTransformDimensionsChange()
    {
        if (_isWired && isActiveAndEnabled)
        {
            DrawLinks();
        }
    }

    #region 구독

    // 특성·건물 레벨 변경 구독 (Show · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed              = true;
        _data.TraitsChanged       += Redraw;      // 특성 레벨 (로그인 목록 · 올리기 응답)
        _data.BuildingsChanged    += Redraw;      // 건물 레벨이 곧 포인트다
        _data.TraitLearnCompleted += OnTraitLearnCompleted;
        Lang.Changed              += Redraw;
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
        _data.BuildingsChanged    -= Redraw;
        _data.TraitLearnCompleted -= OnTraitLearnCompleted;
        Lang.Changed              -= Redraw;
    }

    #endregion

    #region 트리

    // 이 건물의 노드를 거르고 트리 크기(열 · 줄 수)를 잰다 (Show에서 호출).
    private void CollectTree()
    {
        _treeNodes.Clear();

        _columnCount = 1;
        _rowCount    = 1;

        foreach (var node in GameDataLoader.UserTraits)
        {
            if (node.BuildingTID != _buildingTid)
            {
                continue;
            }

            _treeNodes.Add(node);

            _columnCount = Mathf.Max(_columnCount, node.Column);
            _rowCount    = Mathf.Max(_rowCount, node.Row + 1);
        }
    }

    // 노드와 정보 영역을 다시 그린다 (노드 선택 · 특성·건물 레벨 변경 구독).
    //
    // ※ 정보 영역도 여기서 함께 그린다 — 올린 직후 옛 레벨이 남아 있으면 안 되고,
    //   특성·건물 레벨 변경은 전부 이 함수를 거친다.
    private void Redraw()
    {
        for (int i = 0; i < _treeNodes.Count; i++)
        {
            var           trait = _treeNodes[i];
            TraitNodeView node  = _nodes.Get(i);
            int           level = _data.GetTraitLevel(trait.UserTraitTID);

            PlaceNode((RectTransform)node.transform, trait);

            node.Bind(trait.UserTraitTID, Loc.Get(trait.NameLocKey), level, trait.MaxLevel,
                      GetNodeState(trait, level), isSelected: trait.UserTraitTID == _selectedTraitTid);
        }

        _nodes.HideFrom(_treeNodes.Count);

        DrawLinks();
        RedrawDetail();
    }

    // 노드 자리 — 영역을 열 · 줄 수로 나눈 칸의 가운데. 열은 1부터, 줄은 0부터다(표 그대로).
    private void PlaceNode(RectTransform node, UserTraitTableRow trait)
    {
        Vector2 anchor = AnchorOf(trait);

        node.anchorMin        = anchor;
        node.anchorMax        = anchor;
        node.pivot            = new Vector2(0.5f, 0.5f);
        node.anchoredPosition = Vector2.zero;
    }

    private Vector2 AnchorOf(UserTraitTableRow trait)
        => new Vector2((trait.Column - 0.5f) / _columnCount, 1f - (trait.Row + 0.5f) / _rowCount);

    // 앞 노드와 잇는 선을 그린다 — 앞 노드가 최대면 밝은 색이다 (Redraw · 크기 변경).
    private void DrawLinks()
    {
        int     used = 0;
        Vector2 size = linkArea.rect.size;

        foreach (var trait in _treeNodes)
        {
            foreach (int requiredTid in trait.RequiredTraitTIDs)
            {
                if (!GameDataLoader.TryGetUserTrait(requiredTid, out var required))
                {
                    continue;
                }

                Vector2 from = Vector2.Scale(AnchorOf(required), size);
                Vector2 to   = Vector2.Scale(AnchorOf(trait), size);

                bool isOpen = _data.GetTraitLevel(requiredTid) >= required.MaxLevel;

                DrawLink(_links.Get(used++), from, to, isOpen);
            }
        }

        _links.HideFrom(used);
    }

    // 선 하나 — 가로 막대를 두 점 사이 길이로 늘려 돌린다.
    private void DrawLink(Image link, Vector2 from, Vector2 to, bool isOpen)
    {
        var     rect  = (RectTransform)link.transform;
        Vector2 delta = to - from;

        rect.anchorMin        = Vector2.zero;
        rect.anchorMax        = Vector2.zero;
        rect.pivot            = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = (from + to) * 0.5f;
        rect.sizeDelta        = new Vector2(delta.magnitude, rect.sizeDelta.y);
        rect.localRotation    = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

        link.color = UIThemePalette.Of(isOpen ? linkOpenRole : linkLockedRole);
    }

    // 칸의 색 — 최대 레벨 · 올릴 수 있음 · 앞 노드 미완.
    //
    // ※ 포인트 부족은 색으로 가르지 않는다 — 모든 칸에 똑같이 걸려 전부 회색이 되면 아무것도 읽히지 않는다.
    //   누르면 알림으로 말한다.
    private TraitNodeView.NodeState GetNodeState(UserTraitTableRow trait, int level)
    {
        if (level >= trait.MaxLevel)
        {
            return TraitNodeView.NodeState.MaxLevel;
        }

        return _data.TryGetUnmetPrerequisite(trait, out _)
            ? TraitNodeView.NodeState.Locked
            : TraitNodeView.NodeState.Available;
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
    // ※ 보유 포인트도 이 줄이 말한다.
    // [레벨 올리기]는 **최대 레벨에서만** 숨긴다. 조건 미달·포인트 부족이면 회색이지만 눌리게 둔다 —
    // 누르면 이유(앞 노드 · 포인트)가 알림으로 뜬다.
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
            ? $"{Loc.Get(trait.NameLocKey)}  <size=70%><color=#{Hex(UIThemeRole.ButtonSelected)}>Lv {level} / {trait.MaxLevel} · {Lang.Get("UI.WorkStation.Max")}</color></size>"
            : $"{Loc.Get(trait.NameLocKey)}  <size=70%><color=#{Hex(UIThemeRole.TextSub)}>Lv {level} / {trait.MaxLevel}</color></size>";
        detailDescText.text = DescribeKind(trait);

        bool hasUnmet = _data.TryGetUnmetPrerequisite(trait, out var unmet);
        bool pointOk  = _data.GetBuildingPoints(_buildingTid) >= trait.TraitPoint;

        BindStats(trait, level, isMax);

        requirementText.text = isMax || trait.RequiredTraitTIDs.Length == 0
            ? DescribePoint(isMax ? (bool?)null : pointOk)
            : $"{Lang.Get("UI.Trait.Condition")} {Paint(DescribeRequired(trait, hasUnmet ? unmet : null), !hasUnmet)}   ·   {DescribePoint(pointOk)}";

        confirmButton.gameObject.SetActive(!isMax);
        confirmLabel.text = Lang.Format("UI.Trait.LevelUp", trait.TraitPoint);
        PaintConfirm(!hasUnmet && pointOk);
    }

    // 비교 상자를 채운다 (RedrawDetail에서 호출).
    //
    // ※ 지금 ▶ 다음은 '▶'로 잇는다 (옛 폰트에 '→'가 없어 정한 표기).
    private void BindStats(UserTraitTableRow trait, int level, bool isMax)
    {
        if (isMax)
        {
            stats.BindMax(Lang.Get("UI.Trait.CurrentEffect"), DescribeEffect(trait, level));

            return;
        }

        int next = level + 1;

        stats.BindNext(Lang.Format("UI.Trait.NowLevel", level), DescribeEffect(trait, level),
                       Lang.Format("UI.Trait.NextLevel", next), DescribeNextEffect(trait, next));
    }

    // '보유 포인트 13' (조건 줄). ok가 있으면 숫자를 충족/미달 색으로, null이면(고른 것 없음 · 최대 레벨) 그냥 적는다.
    // ※ 충족 여부는 글자색(Positive · Negative)으로 말한다 — '✓'는 아틀라스에 없다.
    private string DescribePoint(bool? ok)
    {
        int points = _data.GetBuildingPoints(_buildingTid);

        return ok == null
            ? Lang.Format("UI.Trait.Points", points)
            : Lang.Format("UI.Trait.Points", Paint(points.ToString(), ok.Value));
    }

    // 조건 줄의 앞 노드 — '농사 개척 최대'. 다 찍었으면(unmet = null) 첫 앞 노드를 충족으로 보여 준다.
    private static string DescribeRequired(UserTraitTableRow trait, UserTraitTableRow? unmet)
    {
        if (unmet == null && !GameDataLoader.TryGetUserTrait(trait.RequiredTraitTIDs[0], out unmet))
        {
            return "";
        }

        return Lang.Format("UI.Trait.RequiredNode", Loc.Get(unmet.NameLocKey));
    }

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
                    ? Lang.Format("UI.Trait.UpTo", $"Lv{level} {Loc.Get(row.NameLocKey)}")
                    : Lang.Format("UI.Trait.UpTo", $"Lv{level}");

            case UserTraitEffect.SpeedAdd:
                return $"{Lang.Get("UI.Trait.Speed")} {FormatPermille(trait.EffectValue * (level - trait.BaseLevel))}";

            case UserTraitEffect.YieldAdd:
                return $"{Lang.Get("UI.WorkStation.Yield")} {FormatPermille(trait.EffectValue * (level - trait.BaseLevel))}";

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
                ? Lang.Format("UI.Trait.Opens", $"Lv{next} {Loc.Get(row.NameLocKey)}")
                : Lang.Format("UI.Trait.Opens", $"Lv{next}");
        }

        return DescribeEffect(trait, next);
    }

    // 이 종류의 특성이 무엇을 좋게 하는지 (정보 영역 이름 아래).
    //
    // ■ 산출량은 이름만으로 뜻이 안 읽힌다 (2026-10-02 사용자 요청)
    // "좋은 게 나올 확률이 오르나?"로 오해되기 쉽다 — 실제로는 **같은 자원이 더** 나오고 희귀도 확률은 그대로다.
    // 속도와 갈리는 점(상자·경험치는 늘지 않는다)까지 적어야 둘 중 무엇을 찍을지 고를 수 있다.
    // 계산 예(120%)·희귀도 확률은 도움말 아이콘으로 옮겼다 — 여기는 한 줄 요약만 둔다(T-144).
    private static string DescribeKind(UserTraitTableRow trait)
    {
        switch (trait.EffectType)
        {
            case UserTraitEffect.IndustryUnlock:
                return Lang.Get("UI.Trait.DescUnlock");

            case UserTraitEffect.SpeedAdd:
                return Lang.Get("UI.Trait.DescSpeed");

            case UserTraitEffect.YieldAdd:
                return Lang.Get("UI.Trait.DescYield")
                     + (trait.Industry == IndustryType.None ? " " + Lang.Get("UI.Trait.DescYieldAll") : "");

            default:
                return "";
        }
    }

    // 트리 위 도움말 아이콘 — 포인트 얻는 법 · 앞 노드 조건 · 종류별 효과 (T-144).
    private static TooltipContent BuildHelpTooltip()
    {
        var content = new TooltipContent(Lang.Get("UI.Trait.HelpTitle"))
            .Row(Lang.Get("UI.Trait.HelpPoint"), "")
            .Row(Lang.Get("UI.Trait.HelpLevel"), "")
            .Row(Lang.Get("UI.Trait.HelpChoose"), "")
            .Header(Lang.Get("UI.Trait.HelpKinds"));

        content.Row($"{Lang.Get(KindNameKeys[0])} — {Lang.Get("UI.Trait.DescUnlock")}", "")
               .Row($"{Lang.Get(KindNameKeys[1])} — {Lang.Get("UI.Trait.DescSpeed")}", "")
               .Row($"{Lang.Get(KindNameKeys[2])} — {Lang.Get("UI.Trait.DescYield")}", "")
               .Row(Lang.Get("UI.Trait.HelpYieldCalc"), "")
               .Row(Lang.Get("UI.Trait.HelpYieldRarity"), "")
               .Row($"{Lang.Get("UI.Trait.Common")} — {Lang.Get("UI.Trait.DescYieldAll")}", "");

        return content;
    }

    // 천분율 가산을 "+15%"로 적는다 (EffectValue는 천분율).
    private static string FormatPermille(int permille)
        => $"+{permille / 10f:0.#}%";

    #endregion

    #region 레벨 올리기

    // 노드를 눌렀다 — **고르기만 한다** (TraitNodeView.Clicked 구독).
    //
    // 누르는 즉시 팝업을 띄우면 읽고 결정할 자리가 없다(T-079). 정보 영역에 펼치고,
    // 올리기는 [레벨 올리기]가 한다.
    private void OnNodeClicked(TraitNodeView view)
    {
        if (view.UserTraitTid == 0)
        {
            return;
        }

        _selectedTraitTid = view.UserTraitTid;

        Redraw();
    }

    // [레벨 올리기]를 눌렀다 — 고른 특성을 1레벨 올린다 (confirmButton.onClick).
    //
    // 순서는 서버 판정과 같다 — 최대 레벨 → 앞 노드 → 포인트.
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
            _wait.RaiseRefusal(Lang.Get("UI.Trait.AlreadyMax"));

            return;
        }

        if (_data.TryGetUnmetPrerequisite(trait, out var unmet))
        {
            _wait.RaiseRefusal(Lang.Format("UI.Trait.NeedPrerequisite", Loc.Get(unmet.NameLocKey)));

            return;
        }

        if (_data.GetBuildingPoints(_buildingTid) < trait.TraitPoint)
        {
            _wait.RaiseRefusal(Lang.Format("UI.Trait.NotEnoughPoints", trait.TraitPoint));

            return;
        }

        _ui.AskConfirm(Lang.Format("UI.Trait.ConfirmLearn", trait.TraitPoint, Loc.Get(trait.NameLocKey), level + 1),
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
        _learningTid = userTraitTid;
        _learnWait   = _wait.Begin(Lang.Get("UI.Trait.WaitLearn"), onClosed: () => _learnWait = null);
    }

    // 레벨 올리기 결과 (PlayerDataModel.TraitLearnCompleted 구독).
    //
    // ※ 칸의 새 레벨은 이미 그려져 있다 — 'PlayerDataModel'이 'TraitsChanged'(→ 'Redraw')를 먼저 쏘고 이 결과를 쏜다.
    private void OnTraitLearnCompleted(bool success, EResultCode code)
    {
        if (_learnWait == null)
        {
            return; // 내가 보낸 요청이 아니다
        }

        if (success)
        {
            _learnWait.Succeed();
            FindNode(_learningTid)?.PlayLevelUp();
        }
        else
        {
            _learnWait.Fail(ResultMessages.ToText(code));
        }
    }

    // 이 특성을 그리는 노드 — 트리에 없으면 null (OnTraitLearnCompleted에서 호출)
    private TraitNodeView? FindNode(int userTraitTid)
    {
        foreach (TraitNodeView node in _nodes.All)
        {
            if (node.gameObject.activeSelf && node.UserTraitTid == userTraitTid)
            {
                return node;
            }
        }

        return null;
    }

    #endregion
}

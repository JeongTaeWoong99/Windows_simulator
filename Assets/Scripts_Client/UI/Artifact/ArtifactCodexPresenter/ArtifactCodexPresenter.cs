using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using GameData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 아티팩트 도감 — 번호 순 · 등급별 묶음으로 전 종류를 늘어놓는다. **보기만 한다** (#81 합의 · 목업 'GameDesign/design/ui/아티팩트도감장착목업.html').
//
// ■ 칸 상태 셋
//   장착 · 켜짐   : 그대로.
//   장착 · 꺼짐   : 어둡게('SetDimmed') — 효과가 없다.
//   미장착       : 흑백('SetFilteredOut'). 갖고 있으면 이름과 '보유 n'을, 본 적도 없으면 '???'를 적는다.
// ■ 장착 · 판매는 여기서 하지 않는다 — 인벤토리 장비 탭의 아티팩트 칸이 한다(2026-10-25 사용자 결정).
//   좌클릭은 고르기만 한다('ArtifactModel.Select') — 켜고 끄기는 상세 칸의 버튼이 한다.
// ■ 장착하면 반짝인다 — 인벤토리 아티팩트 창이 장착에 성공하면 이 화면을 열고('UIManager.ShowArtifact') 번호를 맡긴다('ArtifactModel.Highlight').
//   그 칸까지 스크롤하고 획득 반짝임('SlotGainShine')을 두 번 + 칸을 한 번 튕긴다. 화면이 막 켜지는 중이면 그릴 준비가 된 뒤 꺼낸다('TakeHighlight').
// ■ 효과 합계는 적지 않는다(#81 합의) — "장착 n / 전체"만 적는다.
//
// ※ 칸은 처음 한 번만 만든다 — 종류 수(테이블 행)는 플레이 중에 바뀌지 않는다. 그 뒤로는 다시 그리기만 한다.
public class ArtifactCodexPresenter : MonoBehaviour
{
    // 필터 — 버튼 순서와 같다.
    private enum CodexFilter
    {
        All,      // 전체
        Equipped, // 장착한 것만
    }

    [CenterHeader("필터 줄")]
    [SerializeField, Tooltip("[전체] 버튼. OnClick은 코드가 연결한다")]
    private Button allButton = null!;

    [SerializeField, Tooltip("[장착] 버튼. OnClick은 코드가 연결한다")]
    private Button equippedButton = null!;

    [SerializeField, Tooltip("'장착 n / 전체' 글자. 문구는 코드가 쓴다")]
    private TMP_Text countText = null!;

    [CenterHeader("격자")]
    [SerializeField, Tooltip("등급 묶음 원본 — 꺼 둔다. 등급 수만큼 복제해 Group Parent 아래에 둔다")]
    private ArtifactCodexGroupView groupTemplate = null!;

    [SerializeField, Tooltip("묶음이 쌓이는 부모 — Scroll View > Viewport > Content")]
    private Transform groupParent = null!;

    [SerializeField, Tooltip("칸 프리팹 (SlotView) — 인벤토리 격자와 같은 칸이다")]
    private SlotView slotPrefab = null!;

    [SerializeField, Tooltip("필터를 바꾸면 맨 위로 올린다")]
    private ScrollRect scrollRect = null!;

    [CenterHeader("필터 버튼 색")]
    [SerializeField, Tooltip("고른 필터 버튼의 색 역할 — 경매장 탭 줄과 같다")]
    private UIThemeRole selectedRole = UIThemeRole.ButtonSelected;

    [SerializeField, Tooltip("고르지 않은 필터 버튼의 색 역할")]
    private UIThemeRole normalRole = UIThemeRole.Button;

    // 칸 하나와 그 번호 — 칸은 한 번호에 묶여 다시 묶이지 않는다.
    private readonly struct CodexCell
    {
        public readonly SlotView Slot;
        public readonly int      Tid;

        public CodexCell(SlotView slot, int tid)
        {
            Slot = slot;
            Tid  = tid;
        }
    }

    // 등급 묶음 — 등급 순서. 묶음마다 자기 칸을 '_groupCells'로 안다.
    private readonly List<ArtifactCodexGroupView> _groups      = new List<ArtifactCodexGroupView>();
    private readonly List<GlobalRarity>           _groupRarity = new List<GlobalRarity>();
    private readonly List<List<CodexCell>>        _groupCells  = new List<List<CodexCell>>();

    private int _cellCount; // 전 종류 수 — '장착 n / 전체'의 뒤

    // 반짝임 — 칸마다 처음 반짝일 때 붙인다(번호 → 반짝임).
    private readonly Dictionary<int, SlotGainShine> _shines = new Dictionary<int, SlotGainShine>();

    private CancellationTokenSource? _highlightCts;

    // 반짝이기 전 기다림(초) — 장착 응답 뒤 도감 스냅샷이 와서 칸이 '장착'으로 바뀌고, 막 켠 화면의 레이아웃이 자리 잡을 시간
    private const float HighlightDelay = 0.15f;

    // 두 번째 반짝임까지(초) · 칸 튕김 크기 · 길이(초)
    private const float SecondShineDelay = 0.55f;
    private const float PunchScale       = 0.18f;
    private const float PunchDuration    = 0.45f;

    private PlayerDataModel _data  = null!;
    private ArtifactModel   _model = null!;

    private CodexFilter _filter = CodexFilter.All;

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 배선 → 초기화 순서로 진행한다 (클라 공통 규약)
    private void Start()
    {
        this.RequireRef(allButton,      nameof(allButton));
        this.RequireRef(equippedButton, nameof(equippedButton));
        this.RequireRef(countText,      nameof(countText));
        this.RequireRef(groupTemplate,  nameof(groupTemplate));
        this.RequireRef(groupParent,    nameof(groupParent));
        this.RequireRef(slotPrefab,     nameof(slotPrefab));
        this.RequireRef(scrollRect,     nameof(scrollRect));

        _data  = Services.Get<PlayerDataModel>();
        _model = Services.Get<ArtifactModel>();

        groupTemplate.gameObject.SetActive(false);
        BuildSlots();

        Subscribe();

        allButton.onClick.AddListener(() => SetFilter(CodexFilter.All));
        equippedButton.onClick.AddListener(() => SetFilter(CodexFilter.Equipped));

        ApplyFilterButtons();
        Refresh();

        _isReady = true;

        TryHighlight(); // 장착하며 처음 연 화면 — 칸을 막 만들었다
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지) — 닫혀 있던 사이에 장착 · 감정이 있었을 수 있다
    private void OnEnable()
    {
        if (_isReady)
        {
            Subscribe();
            Refresh();
            TryHighlight();
        }
    }

    // 구독 해제 · 반짝임 기다림을 끊는다 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
        CancelHighlight();
    }

    #region 구독

    // 도감 · 칸 · 고름 · 언어 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed = true;

        _data.ArtifactsChanged     += Refresh;
        _model.SelectionChanged    += Refresh;
        _model.HighlightRequested  += TryHighlight;
        Lang.Changed               += Refresh;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed = false;

        _data.ArtifactsChanged     -= Refresh;
        _model.SelectionChanged    -= Refresh;
        _model.HighlightRequested  -= TryHighlight;
        Lang.Changed               -= Refresh;
    }

    #endregion

    #region 칸 만들기

    // 등급 순 → 번호 순으로 묶음과 칸을 만든다 (Start에서 한 번).
    private void BuildSlots()
    {
        var rows = new List<ArtifactTableRow>(GameTable.ArtifactTable.All);

        rows.Sort((a, b) => a.GlobalRarity != b.GlobalRarity ? a.GlobalRarity.CompareTo(b.GlobalRarity) : a.ArtifactTID.CompareTo(b.ArtifactTID));

        foreach (ArtifactTableRow row in rows)
        {
            int group = _groupRarity.IndexOf(row.GlobalRarity);

            if (group < 0)
            {
                ArtifactCodexGroupView view = Instantiate(groupTemplate, groupParent, false);

                view.name = $"Group ({row.GlobalRarity})";
                view.gameObject.SetActive(true);

                _groups.Add(view);
                _groupRarity.Add(row.GlobalRarity);
                _groupCells.Add(new List<CodexCell>());

                group = _groups.Count - 1;
            }

            SlotView slot = Instantiate(slotPrefab, _groups[group].SlotParent, false);
            int      tid  = row.ArtifactTID;

            slot.name = $"Artifact ({tid})";
            slot.SetSubVisible(true);
            slot.LeftClicked += _ => _model.Select(tid);

            // 툴팁은 올리는 순간에 만든다 — 장착 · 켜짐이 그 사이에 바뀌었을 수 있다
            slot.gameObject.AddComponent<TooltipTrigger>().SetProvider(() => BuildTooltip(tid));

            _groupCells[group].Add(new CodexCell(slot, tid));
            _cellCount++;
        }
    }

    #endregion

    #region 그리기

    // 칸 · 묶음 머리 · 장착 수를 지금 상태로 그린다 (Start · OnEnable · 도감 · 칸 · 고름 · 언어 변경).
    private void Refresh()
    {
        int equippedTotal = 0;

        for (int g = 0; g < _groups.Count; g++)
        {
            int equipped = 0;
            int shown    = 0;

            foreach (CodexCell cell in _groupCells[g])
            {
                SlotView slot       = cell.Slot;
                int      tid        = cell.Tid;
                bool     isEquipped = _data.ArtifactCodex.TryGetValue(tid, out bool active);
                bool     visible    = _filter == CodexFilter.All || isEquipped;

                if (isEquipped)
                {
                    equipped++;
                }

                if (slot.gameObject.activeSelf != visible)
                {
                    slot.gameObject.SetActive(visible);
                }

                if (!visible)
                {
                    continue;
                }

                shown++;
                DrawSlot(slot, tid, isEquipped, active);
            }

            equippedTotal += equipped;

            _groups[g].SetHead(Lang.Format("UI.Artifact.GroupHead",
                                           UIRichText.Paint(RarityLabel.Get(_groupRarity[g]), RarityPalette.Get(_groupRarity[g])),
                                           equipped.ToString("N0"),
                                           _groupCells[g].Count.ToString("N0")));

            // 장착 필터에서 하나도 없는 등급은 머리째 접는다 — 빈 머리만 줄지어 서면 고장처럼 보인다
            bool groupVisible = shown > 0;

            if (_groups[g].gameObject.activeSelf != groupVisible)
            {
                _groups[g].gameObject.SetActive(groupVisible);
            }
        }

        countText.text = Lang.Format("UI.Artifact.EquippedCount",
                                     UIRichText.Paint(equippedTotal.ToString("N0"), UIThemeRole.Highlight),
                                     _cellCount.ToString("N0"));
    }

    // 칸 하나 — 상태 셋(클래스 머리 주석)과 고름 표시.
    private void DrawSlot(SlotView slot, int tid, bool isEquipped, bool active)
    {
        int  owned   = _data.GetArtifactCount(tid);
        bool isKnown = isEquipped || owned > 0;

        string name = isKnown ? GameDataLoader.GetArtifactName(tid) : Lang.Get("UI.Artifact.Unknown");
        string sub  = isEquipped ? (active ? "" : Lang.Get("UI.Artifact.Off"))
                    : owned > 0  ? Lang.Format("UI.Artifact.OwnedShort", owned.ToString("N0"))
                    : "";

        // 본 적 없는 번호는 그림도 가린다 — 이름을 가리는 것과 같은 이유
        slot.Bind(new SlotData(tid, name, sub,
                               isKnown ? GameDataLoader.GetArtifactRarity(tid) : GlobalRarity.None,
                               isKnown ? VisualCatalog.ArtifactIconOf(tid) : null));
        slot.SetDimmed(isEquipped && !active);
        slot.SetFilteredOut(!isEquipped);

        // ※ 고름 표시는 응축 대상 테두리(파랑)를 빌려 쓴다 — 칸 프리팹에 "고름" 표시가 따로 없다.
        slot.SetCondenseMark(_model.SelectedTid == tid ? SlotCondenseMark.Target : SlotCondenseMark.None);
    }

    // 칸 툴팁 — 본 적 없는 번호는 이름을 가린다.
    private TooltipContent? BuildTooltip(int tid)
    {
        bool isKnown = _data.IsArtifactEquipped(tid) || _data.GetArtifactCount(tid) > 0;

        if (!isKnown)
        {
            return new TooltipContent(Lang.Get("UI.Artifact.Unknown")).Row(Lang.Get("UI.Artifact.UnknownHint"), "");
        }

        return ArtifactText.BuildTooltip(_data, tid, withControls: false);
    }

    #endregion

    #region 장착 반짝임

    // 맡겨진 번호가 있으면 반짝인다 (Start · OnEnable · ArtifactModel.HighlightRequested 구독).
    // ※ 꺼져 있으면 꺼내지 않는다 — 켜질 때(OnEnable) 다시 묻는다.
    private void TryHighlight()
    {
        if (!_isReady || !isActiveAndEnabled)
        {
            return;
        }

        int tid = _model.TakeHighlight();

        if (tid == 0)
        {
            return;
        }

        CancelHighlight();

        _highlightCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);

        HighlightAsync(tid, _highlightCts.Token).Forget();
    }

    // 그 칸이 보이게 하고 반짝인다 — 장착 필터에서 걸러져 있으면 전체로 돌린다 (TryHighlight에서 시작).
    private async UniTaskVoid HighlightAsync(int tid, CancellationToken ct)
    {
        if (_filter != CodexFilter.All)
        {
            SetFilter(CodexFilter.All);
        }

        if (await UniTask.Delay(TimeSpan.FromSeconds(HighlightDelay), cancellationToken: ct).SuppressCancellationThrow())
        {
            return;
        }

        SlotView? slot = FindSlot(tid);

        if (slot == null)
        {
            return;
        }

        ScrollTo((RectTransform)slot.transform);

        GlobalRarity rarity = GameDataLoader.GetArtifactRarity(tid);
        SlotGainShine shine = GetShine(tid, slot);

        slot.transform.DOKill(complete: true);
        _ = slot.transform.DOPunchScale(Vector3.one * PunchScale, PunchDuration, 6, 0.6f).SetLink(slot.gameObject); // 기다리지 않는다 — 반짝임과 함께 간다

        // 두 번 — 'Play'는 반짝이는 중이면 처음부터 다시라서 끝난 뒤에 한 번 더 건다
        shine.Play(tid, rarity, 0f);

        if (await UniTask.Delay(TimeSpan.FromSeconds(SecondShineDelay), cancellationToken: ct).SuppressCancellationThrow())
        {
            return;
        }

        shine.Play(tid, rarity, 0f);
    }

    // 반짝임 기다림을 끊는다 (OnDisable · 새 반짝임).
    private void CancelHighlight()
    {
        _highlightCts?.Cancel();
        _highlightCts?.Dispose();
        _highlightCts = null;
    }

    // 번호의 칸 — 없으면 null.
    private SlotView? FindSlot(int tid)
    {
        foreach (List<CodexCell> cells in _groupCells)
        {
            foreach (CodexCell cell in cells)
            {
                if (cell.Tid == tid)
                {
                    return cell.Slot;
                }
            }
        }

        return null;
    }

    // 칸의 반짝임 — 처음이면 붙인다('SlotGainShine.Attach' — 칸 프리팹은 인벤토리와 공유라 손대지 않는다).
    private SlotGainShine GetShine(int tid, SlotView slot)
    {
        if (!_shines.TryGetValue(tid, out SlotGainShine shine))
        {
            shine = SlotGainShine.Attach(slot.gameObject);
            _shines.Add(tid, shine);
        }

        return shine;
    }

    // 칸이 보이는 자리의 가운데쯤에 오도록 세로로 스크롤한다 — 내용이 보이는 폭보다 짧으면 그대로 둔다.
    private void ScrollTo(RectTransform cell)
    {
        Canvas.ForceUpdateCanvases();

        RectTransform content  = scrollRect.content;
        RectTransform viewport = scrollRect.viewport != null ? scrollRect.viewport : (RectTransform)scrollRect.transform;

        float contentHeight = content.rect.height;
        float viewHeight    = viewport.rect.height;

        if (contentHeight <= viewHeight)
        {
            return;
        }

        Vector3 local     = content.InverseTransformPoint(cell.TransformPoint(cell.rect.center));
        float   fromTop   = content.rect.yMax - local.y;
        float   scrollTop = Mathf.Clamp(fromTop - viewHeight * 0.5f, 0f, contentHeight - viewHeight);

        scrollRect.verticalNormalizedPosition = 1f - scrollTop / (contentHeight - viewHeight);
    }

    #endregion

    #region 필터

    // 필터를 바꾼다 — 맨 위로 올린다 (allButton · equippedButton OnClick)
    private void SetFilter(CodexFilter filter)
    {
        if (_filter == filter)
        {
            return;
        }

        _filter = filter;

        ApplyFilterButtons();
        Refresh();

        scrollRect.verticalNormalizedPosition = 1f;
    }

    // 고른 필터 버튼만 선택 색으로 — 색의 주인은 ColorBlock이다('AuctionTabPresenter.ShowTab'과 같다).
    private void ApplyFilterButtons()
    {
        Paint(allButton,      _filter == CodexFilter.All);
        Paint(equippedButton, _filter == CodexFilter.Equipped);
    }

    private void Paint(Button button, bool on)
    {
        Color target = UIThemePalette.Of(on ? selectedRole : normalRole);

        ColorBlock colors = button.colors;
        colors.normalColor   = target;
        colors.selectedColor = target;
        button.colors        = colors;
    }

    #endregion
}

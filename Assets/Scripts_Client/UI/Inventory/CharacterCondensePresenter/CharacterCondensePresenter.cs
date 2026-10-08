using System;
using System.Collections.Generic;
using DG.Tweening;
using MikaNetwork;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// UnityEngine에도 CharacterInfo(폰트 글리프 정보)가 있어 이름이 겹친다. 우리가 쓰는 건 패킷 쪽이다.
using CharacterInfo = MikaProtocol.CharacterInfo;

// 응축 창 — 같은 캐릭터를 재료로 녹여 대상의 ★을 올린다 (T-130 · 이슈 #59 · 캐릭터 기획 5.5).
//
// ■ 판매 목록 자리를 빌려 쓴다 — 큐브 창('EquipEnchantPresenter')과 같은 방식
// 캐릭터 탭에서 캐릭터를 **좌클릭**하면 판매 목록이 비워지며 물러나고 이 창이 그 자리에 열린다.
// 닫기 · 다른 탭 · 우클릭(판매 담기)이면 물러나고 판매 목록이 돌아온다.
//
// ■ 재료는 인벤토리 격자에서 고른다 — 팰월드처럼 목록을 따로 두지 않는다
// 열려 있는 동안 격자가 재료 고르기 화면이 된다. 칸이 무엇인지('GetMark')는 이 창이 답하고, 격자는 그대로 그린다.
//   대상 = 파랑 테두리 · 고른 재료 = 노랑 테두리 + 체크 · 못 넣는 칸(다른 캐릭터 · 배치·착용 중) = 흑백.
// 칸 순서는 그대로 둔다 — 같은 캐릭터만 앞으로 모으면 눈이 기억한 자리를 잃는다.
//
// ■ 규칙 (서버 'User.TryCondenseCharacter')
//   - 재료는 대상과 **같은 CharacterTid**만. 배치·착용 중인 재료는 거절된다. 대상은 배치·착용 중이어도 된다.
//   - 개체는 넣은 재료의 **누적 수**만 기억하고 ★은 표('CharacterStarTable')의 누적 기준으로 정해진다.
//   - 한 번에 넣는 수에 제한이 없다. 최고 ★을 넘는 몫도 **소모된다** — 화면이 넘치는 수를 미리 알린다.
//
// ※ '★' 글자를 쓰지 않는다 — 폰트 아틀라스에 없다('UI 규칙.md'). 그림(★ 스프라이트)과 'n성' 글씨로 말한다.
public class CharacterCondensePresenter : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("인벤토리 탭 줄. 캐릭터 탭을 떠나면 이 창이 닫힌다")]
    private InventoryTabPresenter inventoryTabs = null!;

    [SerializeField, Tooltip("대상 상반신 — 그림이 없으면 자리 표시 네모(프리팹 색)")]
    private Image portraitImage = null!;

    [SerializeField, Tooltip("대상 이름 — 등급색")]
    private TMP_Text nameText = null!;

    [SerializeField, Tooltip("이름 아래 한 줄 — 'Lv7 · 응축 누적 15 / 76'")]
    private TMP_Text infoText = null!;

    [SerializeField, NonReorderable, Tooltip("머리 줄의 ★ 그림 — 최고 ★ 수만큼. 얻은 ★ · 이번에 오를 ★ · 빈 ★을 색으로 가른다")]
    private Image[] headerStars = new Image[0];

    [SerializeField, Tooltip("★이 오르면 통통 튀는 묶음 — 머리 줄의 ★들을 담은 부모")]
    private RectTransform headerStarRow = null!;

    [SerializeField, Tooltip("판매 목록으로 돌아가는 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button closeButton = null!;

    [CenterHeader("비교 상자")]
    [SerializeField, Tooltip("왼쪽 상자 제목 — '지금 2성'")]
    private TMP_Text beforeTitleText = null!;

    [SerializeField, Tooltip("왼쪽 상자 값 — '작업속도 +20%'")]
    private TMP_Text beforeValueText = null!;

    [SerializeField, Tooltip("두 상자 사이 '▶' — ★이 오를 재료를 골랐을 때만 켜진다")]
    private GameObject arrow = null!;

    [SerializeField, Tooltip("오른쪽 상자 — ★이 오를 재료를 골랐을 때만 켜진다")]
    private GameObject afterBox = null!;

    [SerializeField, Tooltip("오른쪽 상자 제목 — '응축 후 3성'")]
    private TMP_Text afterTitleText = null!;

    [SerializeField, Tooltip("오른쪽 상자 값 — '작업속도 +30%'")]
    private TMP_Text afterValueText = null!;

    [CenterHeader("진행 바")]
    [SerializeField, NonReorderable, Tooltip("단계 칸 — ★1부터 순서대로. 최고 ★ 수만큼 있어야 한다")]
    private CondenseStageView[] stageViews = new CondenseStageView[0];

    [SerializeField, Tooltip("바 아래 왼쪽 — '누적 15 + 9 = 24'")]
    private TMP_Text captionLeftText = null!;

    [SerializeField, Tooltip("바 아래 오른쪽 — '3성까지 20'")]
    private TMP_Text captionRightText = null!;

    [SerializeField, Tooltip("알림 줄 — 넘치는 재료(빨강) · 못 넣는 재료 수(흐림)")]
    private TMP_Text warnText = null!;

    [CenterHeader("버튼")]
    [SerializeField, Tooltip("[다음 단계까지 채우기]. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button fillButton = null!;

    [SerializeField, Tooltip("[응축] 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button confirmButton = null!;

    [SerializeField, Tooltip("[응축] 글씨 — '응축 (9마리)'. 코드가 채운다")]
    private TMP_Text confirmLabel = null!;

    [CenterHeader("색")]
    [SerializeField, Tooltip("[응축] — 보낼 수 있을 때")]
    private UIThemeRole confirmReadyRole = UIThemeRole.ButtonPrimary;

    [SerializeField, Tooltip("[응축] — 재료가 없거나 최고 ★이거나 응답을 기다릴 때. 눌리기는 한다(누르면 이유 알림)")]
    private UIThemeRole confirmBlockedRole = UIThemeRole.ButtonDisabled;

    // 이번에 오를 ★의 투명도 — 얻은 ★과 같은 색을 옅게 칠해 "아직"을 말한다(목업의 깜빡이는 ★).
    private const float PreviewStarAlpha = 0.45f;

    // 이 레벨부터 재료로 쓰면 한 번 묻는다 — 키운 캐릭터를 실수로 녹이지 않게(T-130 목업 · 정할 것 1).
    private const int ConfirmMaterialLevel = 2;

    // 창이 열렸는가. 판매 목록이 이걸 보고 물러난다.
    public bool IsOpen => _targetId != 0L;

    // 열림이 바뀌었다 ('SellCartPresenter'가 구독 — 열리면 카트를 비우고 물러난다).
    public event Action<bool>? OpenChanged;

    // 대상·재료가 바뀌었다 ('InventoryGridPresenter'가 구독 — 칸 표시를 다시 그린다).
    public event Action? SelectionChanged;

    // 고른 재료 — 고른 순서대로. 판정('Contains')은 집합으로 한다.
    private readonly List<long>    _materials   = new List<long>();
    private readonly HashSet<long> _materialSet = new HashSet<long>();

    // [다음 단계까지 채우기]가 후보를 줄 세우는 버퍼 — 누를 때마다 새로 만들지 않는다.
    private readonly List<CharacterInfo> _candidates = new List<CharacterInfo>();

    private PlayerDataModel   _data    = null!;
    private UIManager         _ui      = null!;
    private NetworkManager    _network = null!;
    private ServerWaitManager _wait    = null!;

    private long _targetId; // 창에 띄운 대상 개체. 0이면 닫혀 있다

    // 진행 중인 응축 요청. null이면 기다리는 것이 없다.
    private ServerWaitHandle? _waitHandle;
    private int               _sentStar; // 보낼 때의 ★ — 응답에서 올랐는지 비교한다

    private Tween? _starPunch;

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 배선 → 초기화 순서로 진행한다 (클라 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다.
    private void Start()
    {
        this.RequireRef(inventoryTabs,    nameof(inventoryTabs));
        this.RequireRef(portraitImage,    nameof(portraitImage));
        this.RequireRef(nameText,         nameof(nameText));
        this.RequireRef(infoText,         nameof(infoText));
        this.RequireRef(headerStarRow,    nameof(headerStarRow));
        this.RequireRef(closeButton,      nameof(closeButton));
        this.RequireRef(beforeTitleText,  nameof(beforeTitleText));
        this.RequireRef(beforeValueText,  nameof(beforeValueText));
        this.RequireRef(arrow,            nameof(arrow));
        this.RequireRef(afterBox,         nameof(afterBox));
        this.RequireRef(afterTitleText,   nameof(afterTitleText));
        this.RequireRef(afterValueText,   nameof(afterValueText));
        this.RequireRef(captionLeftText,  nameof(captionLeftText));
        this.RequireRef(captionRightText, nameof(captionRightText));
        this.RequireRef(warnText,         nameof(warnText));
        this.RequireRef(fillButton,       nameof(fillButton));
        this.RequireRef(confirmButton,    nameof(confirmButton));
        this.RequireRef(confirmLabel,     nameof(confirmLabel));

        if (headerStars.Length == 0 || stageViews.Length == 0)
        {
            throw new InvalidOperationException($"{name}: 'headerStars'·'stageViews'가 비어 있다 — 인스펙터에 넣을 것.");
        }

        EnsureServices();

        if (headerStars.Length < GameDataLoader.CondenseMaxStar || stageViews.Length < GameDataLoader.CondenseMaxStar)
        {
            ClientLogger.Warn(ClientLogger.UI,
                $"응축 창의 ★ 그림 {headerStars.Length}개 · 단계 칸 {stageViews.Length}개 — 최고 ★ {GameDataLoader.CondenseMaxStar}보다 적다.", this);
        }

        closeButton.onClick.AddListener(Close);
        fillButton.onClick.AddListener(OnFillClicked);
        confirmButton.onClick.AddListener(OnConfirmClicked);

        // ⚠️ 탭·응답 구독은 Start/OnDestroy에 건다 — 이 창은 자기 오브젝트를 끈다('EquipEnchantPresenter'와 같은 이유).
        inventoryTabs.TabChanged         += OnTabChanged;
        _data.CharacterCondenseCompleted += OnCondenseCompleted;

        _isReady = true;

        Subscribe();

        // 닫힌 채 깨어났으면(씬에 켜진 채 저장됨) 바로 물러난다 — 판매 목록 자리를 비워 두면 안 된다.
        if (!IsOpen)
        {
            gameObject.SetActive(false);

            return;
        }

        Redraw();
    }

    // 서비스를 확보한다 (Start · Open에서 호출).
    //
    // ⚠️ 꺼진 채 저장된 창이라 처음 여는 순간에는 'Start'가 아직 안 돌았다 — 켜도 'Start'는 다음 프레임이다.
    //   그 사이 격자가 'GetMark'로 칸을 물으므로, 여기서 서비스를 먼저 잡아 두지 않으면 첫 클릭에서 NRE가 난다.
    // ※ 'Open'은 언제나 사용자 클릭(= 모든 Start 이후)에서 불리므로 서비스 등록 순서 문제가 없다.
    private void EnsureServices()
    {
        if (_data != null)
        {
            return;
        }

        _data    = Services.Get<PlayerDataModel>();
        _ui      = Services.Get<UIManager>();
        _network = NetworkManager.Instance;
        _wait    = Services.Get<ServerWaitManager>();
    }

    // 탭·응답 구독 해제 (Unity 메시지). 자기 오브젝트를 끄므로 여기서만 푼다.
    private void OnDestroy()
    {
        if (!_isReady)
        {
            return;
        }

        inventoryTabs.TabChanged         -= OnTabChanged;
        _data.CharacterCondenseCompleted -= OnCondenseCompleted;
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    // ★ 닫혀 있는 동안 캐릭터가 바뀌었을 수 있다 — 다시 그린다.
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

    #region 격자가 묻는 것

    // 이 칸이 응축 창에서 무엇인가 (인벤토리 격자가 캐릭터 칸을 그릴 때마다 호출).
    public SlotCondenseMark GetMark(long characterId)
    {
        if (!IsOpen)
        {
            return SlotCondenseMark.None;
        }

        if (characterId == _targetId)
        {
            return SlotCondenseMark.Target;
        }

        if (_materialSet.Contains(characterId))
        {
            return SlotCondenseMark.Material;
        }

        return CanBeMaterial(characterId) ? SlotCondenseMark.Candidate : SlotCondenseMark.Excluded;
    }

    // 캐릭터 칸을 좌클릭했다 (인벤토리 격자가 캐릭터 탭에서 호출).
    //
    // 닫혀 있으면 그 캐릭터로 연다 · 같은 캐릭터면 재료로 담고 뺀다 · 다른 캐릭터면 대상을 바꾼다.
    // ※ 다른 캐릭터 칸이 흑백이어도 누르면 대상이 바뀐다 — "이걸 올리고 싶다"를 칸에서 바로 말할 수 있어야 한다.
    public void OnCharacterClicked(long characterId)
    {
        EnsureServices();

        if (!IsOpen || IsWaiting)
        {
            if (!IsWaiting)
            {
                Open(characterId);
            }

            return;
        }

        if (characterId == _targetId)
        {
            return;
        }

        if (_data.GetCharacterTid(characterId) != _data.GetCharacterTid(_targetId))
        {
            Open(characterId);

            return;
        }

        if (_materialSet.Contains(characterId))
        {
            RemoveMaterial(characterId);
            NotifySelection();

            return;
        }

        if (IsTargetMaxed)
        {
            _wait.RaiseNotice("이미 최고 단계입니다 — 더 넣을 수 없습니다.");

            return;
        }

        if (_data.IsCharacterBusy(characterId))
        {
            _wait.RaiseNotice("배치됐거나 장비를 낀 캐릭터는 재료로 넣을 수 없습니다.");

            return;
        }

        AddMaterial(characterId);
        NotifySelection();
    }

    #endregion

    #region 열고 닫기

    // 이 캐릭터로 창을 연다 — 고른 재료는 비운다 (OnCharacterClicked).
    //
    // ※ 아직 'Start'가 안 돌았을 수 있다(꺼진 채 저장된 창) — 대상만 기억하고 켜면 'Start'가 그린다.
    private void Open(long characterId)
    {
        if (characterId == 0L || characterId == _targetId)
        {
            return;
        }

        EnsureServices();

        bool wasOpen = IsOpen;

        _targetId = characterId;
        ClearMaterials();

        gameObject.SetActive(true);

        if (_isReady)
        {
            Redraw();
        }

        if (!wasOpen)
        {
            OpenChanged?.Invoke(true);
        }

        NotifySelection();
    }

    // 창을 닫고 판매 목록에 자리를 돌려준다 (닫기 버튼 · 탭 전환 · 격자의 우클릭 판매 담기 · 대상이 사라졌을 때).
    // ※ 응답을 기다리는 중이어도 닫는다 — 대기 손잡이는 응답 구독(Start/OnDestroy)이 닫는다.
    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        _targetId = 0L;
        ClearMaterials();

        gameObject.SetActive(false);
        OpenChanged?.Invoke(false);
        NotifySelection();
    }

    // 캐릭터 탭을 떠나면 닫는다 (InventoryTabPresenter.TabChanged 구독).
    private void OnTabChanged(InventoryTab tab)
    {
        if (tab != InventoryTab.Character)
        {
            Close();
        }
    }

    #endregion

    #region 구독

    // 캐릭터·슬롯·장비 변경 구독 (Start · OnEnable에서 호출) — 재료가 배치·착용되거나 팔려 사라질 수 있다.
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed                  = true;
        _data.CharactersChanged       += OnDataChanged;
        _data.WorkStationSlotsChanged += OnDataChanged;
        _data.EquipsChanged           += OnDataChanged;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed                  = false;
        _data.CharactersChanged       -= OnDataChanged;
        _data.WorkStationSlotsChanged -= OnDataChanged;
        _data.EquipsChanged           -= OnDataChanged;
    }

    // 보유 캐릭터가 바뀌었다 — 못 쓰게 된 재료를 빼고 다시 그린다 (캐릭터·슬롯·장비 변경 구독).
    // ※ 재료가 빠졌으면 격자에도 알린다 — 칸의 체크가 남으면 "골랐는데 안 들어갔다"가 된다.
    private void OnDataChanged()
    {
        if (!IsOpen)
        {
            return;
        }

        int removed = _materials.RemoveAll(id => !CanBeMaterial(id));

        if (removed > 0)
        {
            _materialSet.Clear();
            _materialSet.UnionWith(_materials);
            NotifySelection();
        }

        Redraw();
    }

    #endregion

    #region 그리기

    // 창 전체를 다시 그린다 (Open · OnEnable · 데이터 변경 · 재료 고르기 · 응답).
    // ※ 'Start' 전에는 그리지 않는다 — 켜진 뒤 'Start'가 그린다.
    private void Redraw()
    {
        if (!IsOpen || !_isReady)
        {
            return;
        }

        CharacterInfo? target = _data.FindCharacter(_targetId);

        // 팔았거나 경매에 올려 사라졌다 — 비어 있는 창을 남기지 않는다.
        if (target == null)
        {
            Close();

            return;
        }

        int count  = target.CondenseCount;
        int after  = Mathf.Min(count + _materials.Count, GameDataLoader.GetCondenseThreshold(GameDataLoader.CondenseMaxStar));
        int now    = target.Star;
        int then   = Mathf.Max(now, GameDataLoader.GetCondenseStarAt(after));

        DrawHeader(target, now, then);
        DrawCompare(now, then);
        DrawBar(count, after);
        DrawCaption(count, now, then);
        DrawWarn(target, count);
        DrawConfirm(now);
    }

    // 머리 — 상반신 · 이름(등급색) · 레벨·누적 한 줄 · ★ 그림.
    private void DrawHeader(CharacterInfo target, int now, int then)
    {
        Sprite? portrait = VisualCatalog.PortraitOf(target.CharacterTid);

        portraitImage.sprite         = portrait;
        portraitImage.preserveAspect = portrait != null;
        portraitImage.color          = portrait != null ? Color.white : UIThemePalette.Of(UIThemeRole.Slot);

        nameText.text  = GameDataLoader.GetCharacterName(target.CharacterTid);
        nameText.color = RarityPalette.Get(GameDataLoader.GetCharacterRarity(target.CharacterTid));

        int max = GameDataLoader.CondenseMaxStar;

        infoText.text = $"{CharacterSlotSource.GetLevelLabel(target.Level)}{UIRichText.Dot}응축 누적 {target.CondenseCount} / "
                      + $"{GameDataLoader.GetCondenseThreshold(max)}";

        Color filled = UIThemePalette.Of(UIThemeRole.Highlight);
        Color empty  = UIThemePalette.Of(UIThemeRole.TextDisabled);

        for (int i = 0; i < headerStars.Length; i++)
        {
            headerStars[i].gameObject.SetActive(i < max);
            headerStars[i].color = i < now  ? filled
                                 : i < then ? new Color(filled.r, filled.g, filled.b, PreviewStarAlpha)
                                 : empty;
        }
    }

    // 비교 상자 — 지금 ★의 작업속도, 고른 재료로 ★이 오르면 '▶ 응축 후'.
    private void DrawCompare(int now, int then)
    {
        beforeTitleText.text = $"지금 {now}성";
        beforeValueText.text = $"작업속도 +{GameDataLoader.GetStarSpeedAdd(now) / 10f:0.#}%";

        bool rises = then > now;

        arrow.SetActive(rises);
        afterBox.SetActive(rises);

        if (!rises)
        {
            return;
        }

        afterTitleText.text = $"응축 후 {then}성";
        afterValueText.text = $"작업속도 +{GameDataLoader.GetStarSpeedAdd(then) / 10f:0.#}%";
    }

    // 진행 바 — ★마다 한 칸, 칸마다 그 단계 몫만큼 채운다.
    private void DrawBar(int count, int after)
    {
        for (int i = 0; i < stageViews.Length; i++)
        {
            int star = i + 1;

            if (star > GameDataLoader.CondenseMaxStar)
            {
                stageViews[i].gameObject.SetActive(false);

                continue;
            }

            int low      = GameDataLoader.GetCondenseThreshold(star - 1);
            int required = GameDataLoader.GetCondenseThreshold(star) - low;

            stageViews[i].gameObject.SetActive(true);
            stageViews[i].Bind(Mathf.Clamp(count - low, 0, required), Mathf.Clamp(after - low, 0, required), required, star);
        }
    }

    // 바 아래 — 왼쪽 '누적 15 + 9 = 24', 오른쪽 '3성까지 20' · '최고 단계'.
    private void DrawCaption(int count, int now, int then)
    {
        int n   = _materials.Count;
        int max = GameDataLoader.CondenseMaxStar;

        captionLeftText.text = n > 0
            ? $"누적 {count} + {Colorize(n.ToString(), UIThemePalette.Of(UIThemeRole.Highlight))} = {count + n}"
            : $"누적 {count}";

        if (now >= max)
        {
            captionRightText.text = "최고 단계";
        }
        else if (then >= max)
        {
            captionRightText.text = $"{max}성 도달";
        }
        else
        {
            captionRightText.text = $"{then + 1}성까지 {GameDataLoader.GetCondenseThreshold(then + 1) - (count + n)}";
        }
    }

    // 알림 줄 — 최고 단계 · 넘치는 재료(빨강) · 재료가 될 같은 캐릭터가 없음 · 못 넣는 같은 캐릭터 수(흐림) 중 하나.
    // ※ "없음"을 창에 먼저 적는 이유 — 다른 캐릭터도 같은 그림을 쓰는 동안은 흑백 칸이 왜 안 담기는지 칸만 봐서는 모른다.
    private void DrawWarn(CharacterInfo target, int count)
    {
        int maxCount = GameDataLoader.GetCondenseThreshold(GameDataLoader.CondenseMaxStar);
        int overflow = Mathf.Max(0, count + _materials.Count - maxCount);

        if (target.Star >= GameDataLoader.CondenseMaxStar)
        {
            SetWarn("이미 최고 단계입니다 — 더 넣을 수 없습니다.", UIThemeRole.Negative);
        }
        else if (overflow > 0)
        {
            SetWarn($"넘치는 재료 {overflow}마리는 효과 없이 사라집니다.", UIThemeRole.Negative);
        }
        else if (_materials.Count == 0 && !HasAnyCandidate())
        {
            int busy = CountBusySameCharacters(target);

            SetWarn(busy > 0 ? $"재료로 넣을 {nameText.text}이(가) 없습니다 — {busy}마리는 배치·착용 중입니다."
                             : $"재료로 넣을 {nameText.text}이(가) 없습니다 — 같은 캐릭터끼리만 응축됩니다.",
                    UIThemeRole.TextSub);
        }
        else
        {
            int busy = CountBusySameCharacters(target);

            SetWarn(busy > 0 ? $"배치·착용 중인 {nameText.text} {busy}마리는 재료로 넣을 수 없습니다." : "같은 캐릭터 칸을 눌러 재료로 담으세요.",
                    UIThemeRole.TextSub);
        }
    }

    // [응축] — 글씨와 색.
    private void DrawConfirm(int now)
    {
        bool maxed = now >= GameDataLoader.CondenseMaxStar;
        int  n     = _materials.Count;

        confirmLabel.text = IsWaiting ? "응답을 기다리는 중"
                          : maxed     ? "최고 단계"
                          : n == 0    ? "재료를 고르세요"
                          : $"응축 ({n}마리)";

        PaintConfirm(!IsWaiting && !maxed && n > 0);
    }

    private void SetWarn(string text, UIThemeRole role)
    {
        warnText.text  = text;
        warnText.color = UIThemePalette.Of(role);
    }

    // [응축] 색 — 'EquipEnchantPresenter.PaintConfirm'과 같은 공식. 'interactable'은 끄지 않는다(눌러서 이유를 듣는다).
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

    // ★이 올랐다 — 머리 줄의 ★을 통통 튀게 한다 (응축 성공 응답에서 호출).
    private void PunchStars()
    {
        _starPunch?.Kill(true);
        headerStarRow.localScale = Vector3.one;

        _starPunch = headerStarRow.DOPunchScale(Vector3.one * 0.35f, 0.45f, 6, 0.6f)
                                  .SetLink(gameObject, LinkBehaviour.KillOnDisable);
    }

    private static string Colorize(string text, Color color) => $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";

    #endregion

    #region 재료

    private bool IsWaiting => _waitHandle != null;

    // 대상이 이미 최고 ★인가.
    private bool IsTargetMaxed => _data.GetCharacterStar(_targetId) >= GameDataLoader.CondenseMaxStar;

    // 이 캐릭터를 지금 재료로 넣을 수 있나 — 대상과 같은 종류 · 대상 자신 아님 · 배치·착용 중 아님 · 대상이 최고 ★ 아님.
    // ※ 서버 'TryCondenseCharacter'의 거절 조건과 같다. 판정은 서버가 한 번 더 한다.
    private bool CanBeMaterial(long characterId)
    {
        if (characterId == _targetId || IsTargetMaxed)
        {
            return false;
        }

        int tid = _data.GetCharacterTid(characterId);

        return tid != 0 && tid == _data.GetCharacterTid(_targetId) && !_data.IsCharacterBusy(characterId);
    }

    // 인벤토리에 지금 재료로 넣을 수 있는 캐릭터가 하나라도 있나 (DrawWarn에서 호출).
    private bool HasAnyCandidate()
    {
        foreach (CharacterInfo character in _data.Characters)
        {
            if (character.Container == EContainer.Inventory && CanBeMaterial(character.CharacterId))
            {
                return true;
            }
        }

        return false;
    }

    // 대상과 같은 캐릭터 중 배치·착용 중이라 못 넣는 수 (DrawWarn에서 호출).
    private int CountBusySameCharacters(CharacterInfo target)
    {
        int busy = 0;

        foreach (CharacterInfo character in _data.Characters)
        {
            if (character.CharacterId != target.CharacterId
                && character.CharacterTid == target.CharacterTid
                && _data.IsCharacterBusy(character.CharacterId))
            {
                busy++;
            }
        }

        return busy;
    }

    private void AddMaterial(long characterId)
    {
        if (_materialSet.Add(characterId))
        {
            _materials.Add(characterId);
        }
    }

    private void RemoveMaterial(long characterId)
    {
        if (_materialSet.Remove(characterId))
        {
            _materials.Remove(characterId);
        }
    }

    private void ClearMaterials()
    {
        _materials.Clear();
        _materialSet.Clear();
    }

    // 대상·재료가 바뀌었음을 격자에 알리고 창을 다시 그린다.
    private void NotifySelection()
    {
        Redraw();
        SelectionChanged?.Invoke();
    }

    // [다음 단계까지 채우기] — 다음 ★에 딱 닿는 수만큼 **낮은 레벨부터** 다시 담는다 (fillButton OnClick에 코드로 연결).
    //
    // ※ 이미 고른 것은 비우고 다시 고른다 — 넘치지 않는 최소 묶음을 만드는 버튼이라, 손으로 고른 것을 더하면 넘칠 수 있다.
    // ※ 낮은 레벨부터 고르는 이유 — 키운 캐릭터를 재료로 먼저 쓰면 레벨·경험치가 사라진다.
    private void OnFillClicked()
    {
        if (!IsOpen || IsWaiting)
        {
            return;
        }

        CharacterInfo? target = _data.FindCharacter(_targetId);

        if (target == null)
        {
            return;
        }

        if (target.Star >= GameDataLoader.CondenseMaxStar)
        {
            _wait.RaiseNotice("이미 최고 단계입니다 — 더 넣을 수 없습니다.");

            return;
        }

        _candidates.Clear();

        foreach (CharacterInfo character in _data.Characters)
        {
            if (character.Container == EContainer.Inventory && CanBeMaterial(character.CharacterId))
            {
                _candidates.Add(character);
            }
        }

        if (_candidates.Count == 0)
        {
            _wait.RaiseNotice($"재료로 넣을 {nameText.text}이(가) 없습니다.\n같은 캐릭터끼리만 응축됩니다.");

            return;
        }

        _candidates.Sort((a, b) => a.Level != b.Level ? a.Level.CompareTo(b.Level)
                                 : a.Exp   != b.Exp   ? a.Exp.CompareTo(b.Exp)
                                 : a.CharacterId.CompareTo(b.CharacterId));

        int need = GameDataLoader.GetCondenseThreshold(target.Star + 1) - target.CondenseCount;

        ClearMaterials();

        for (int i = 0; i < _candidates.Count && i < need; i++)
        {
            AddMaterial(_candidates[i].CharacterId);
        }

        NotifySelection();
    }

    #endregion

    #region 응축

    // [응축]을 눌렀다 (confirmButton OnClick에 코드로 연결).
    //
    // 판정 순서 — 대기 중이면 무시 → 최고 단계·재료 없음이면 알림 → 키운 재료·넘침이 있으면 한 번 묻는다 → 보낸다.
    private void OnConfirmClicked()
    {
        if (IsWaiting || !IsOpen)
        {
            return;
        }

        if (!_data.IsLoggedIn)
        {
            ClientLogger.Warn(ClientLogger.Send, "응축 요청을 보내지 않았다 — 로그인이 먼저다(서버가 응답 없이 버린다)");

            return;
        }

        CharacterInfo? target = _data.FindCharacter(_targetId);

        if (target == null)
        {
            return;
        }

        if (target.Star >= GameDataLoader.CondenseMaxStar)
        {
            _wait.RaiseNotice("이미 최고 단계입니다 — 더 넣을 수 없습니다.");

            return;
        }

        if (_materials.Count == 0)
        {
            _wait.RaiseNotice("같은 캐릭터 칸을 눌러 재료를 먼저 고르세요.");

            return;
        }

        string? caution = BuildCaution(target);

        if (caution == null)
        {
            RequestCondense();

            return;
        }

        _ui.AskConfirm(caution, RequestCondense);
    }

    // 묻고 보낼 이유 — 레벨을 올린 재료 · 넘치는 재료. 없으면 null (OnConfirmClicked에서 호출).
    private string? BuildCaution(CharacterInfo target)
    {
        int grown = 0;

        foreach (long id in _materials)
        {
            if (_data.GetCharacterLevel(id) >= ConfirmMaterialLevel)
            {
                grown++;
            }
        }

        int maxCount = GameDataLoader.GetCondenseThreshold(GameDataLoader.CondenseMaxStar);
        int overflow = Mathf.Max(0, target.CondenseCount + _materials.Count - maxCount);

        if (grown == 0 && overflow == 0)
        {
            return null;
        }

        string text = $"재료 {_materials.Count}마리를 녹여 {nameText.text}의 단계를 올립니다.";

        if (grown > 0)
        {
            text += $"\nLv{ConfirmMaterialLevel} 이상 재료 {grown}마리의 레벨·경험치는 사라집니다.";
        }

        if (overflow > 0)
        {
            text += $"\n넘치는 {overflow}마리는 효과 없이 사라집니다.";
        }

        return text;
    }

    // 응축을 보낸다 (OnConfirmClicked · 확인 팝업에서 호출).
    // ※ 팝업이 떠 있는 사이 재료가 배치·판매됐을 수 있다 — 'OnDataChanged'가 이미 걸러 두었으니 남은 것만 보낸다.
    private void RequestCondense()
    {
        if (IsWaiting || !IsOpen || _materials.Count == 0)
        {
            return;
        }

        var materials = new List<long>(_materials);

        _data.BeginCondense(materials);
        _network.Send(new C_CharacterCondenseRequest { CharacterId = _targetId, MaterialIds = materials });

        ClientLogger.Info(ClientLogger.Send, $"응축 — 대상 #{_targetId}, 재료 {materials.Count}마리");

        _sentStar   = _data.GetCharacterStar(_targetId);
        _waitHandle = _wait.Begin("응축", onClosed: OnWaitClosed);
        Redraw();
    }

    // 응축 결과 (PlayerDataModel.CharacterCondenseCompleted 구독 — 캐시는 이미 반영돼 있다).
    // ※ 창을 닫았어도 대기는 닫는다.
    private void OnCondenseCompleted(S_CharacterCondenseResponse res)
    {
        if (_waitHandle == null)
        {
            return;
        }

        if (res.Result != EResultCode.Ok)
        {
            _waitHandle.Fail(ResultMessages.ToText(res.Result));

            return;
        }

        ClearMaterials();

        if (res.Character != null && res.Character.CharacterId == _targetId && res.Character.Star > _sentStar && gameObject.activeInHierarchy)
        {
            PunchStars();
        }

        _waitHandle.Succeed();
        NotifySelection();
    }

    // 대기가 끝났다(성공·실패·타임아웃 공통) — 버튼을 푼다 (ServerWaitManager.Begin의 onClosed)
    private void OnWaitClosed()
    {
        _waitHandle = null;
        Redraw();
    }

    #endregion
}

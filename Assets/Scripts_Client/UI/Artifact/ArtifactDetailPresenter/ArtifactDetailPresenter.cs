using GameData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 도감에서 고른 아티팩트의 상세 — 이름 · 등급 · 상태 · 효과 + [도감에 장착] (T-151).
//
// ■ 인벤토리 아티팩트 창과 같은 모양·같은 버튼이다 (2026-11-21 사용자 — "도감에서도 바로 등록").
//   창의 [닫기] · [도감 보기]만 뺐다. 장착 요청은 같은 'ArtifactEquipRequester'를 쓴다.
//   성공하면 그 칸을 반짝인다(창은 도감 화면을 여는데, 여기는 이미 도감 화면이다).
// ■ 켜고 끄기가 없다 (#90 · 2026-10-28 서버 반영) — 장착하면 효과가 항상 붙는다.
//   본 적 없는 번호는 이름을 가리고 버튼을 잠근다.
// ■ 판매 버튼을 두지 않는다 (2026-10-25 사용자 결정) — 좋은 것은 경매장에서 즉시 판매가보다 비싸게 팔 수 있다.
//   파는 길은 장비 탭 아티팩트 칸의 우클릭(즉시 판매) · Ctrl+우클릭(경매) 둘이고, 고르는 일은 사람이 한다.
public class ArtifactDetailPresenter : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("고른 것이 없을 때의 안내")]
    private TMP_Text emptyText = null!;

    [SerializeField, Tooltip("고른 것이 있을 때 켜지는 묶음 — 아이콘 · 글자")]
    private GameObject contentRoot = null!;

    [SerializeField, Tooltip("아이콘 칸 (ItemIconView 프리팹)")]
    private ItemIconView iconView = null!;

    [SerializeField, Tooltip("이름 — 등급색")]
    private TMP_Text nameText = null!;

    [SerializeField, Tooltip("정보 한 줄 — 등급 · 도감 상태 · 보유")]
    private TMP_Text infoText = null!;

    [SerializeField, Tooltip("효과 한 줄")]
    private TMP_Text effectText = null!;

    [SerializeField, Tooltip("안내 — 장착 규칙 · 남는 것을 파는 법")]
    private TMP_Text hintText = null!;

    [SerializeField, Tooltip("[도감에 장착]. OnClick은 코드가 연결한다")]
    private Button equipButton = null!;

    [SerializeField, Tooltip("[도감에 장착] 글자 — 이미 장착했으면 '이미 도감에 장착함'. 코드가 쓴다(LocalizedText를 붙이지 않는다)")]
    private TMP_Text equipButtonText = null!;

    private PlayerDataModel _data  = null!;
    private ArtifactModel   _model = null!;

    private ArtifactEquipRequester _equip = null!; // 장착 요청 — 인벤토리 아티팩트 창과 같은 것을 쓴다

    private TMP_Text _sourceText = null!; // 얻는 곳 · 확률 — 효과 칸을 복제해 바로 아래에 둔다(CreateSourceBox)

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 초기화 순서로 진행한다 (클라 공통 규약)
    private void Start()
    {
        this.RequireRef(emptyText,   nameof(emptyText));
        this.RequireRef(contentRoot, nameof(contentRoot));
        this.RequireRef(iconView,    nameof(iconView));
        this.RequireRef(nameText,    nameof(nameText));
        this.RequireRef(infoText,    nameof(infoText));
        this.RequireRef(effectText,  nameof(effectText));
        this.RequireRef(hintText,        nameof(hintText));
        this.RequireRef(equipButton,     nameof(equipButton));
        this.RequireRef(equipButtonText, nameof(equipButtonText));

        _data  = Services.Get<PlayerDataModel>();
        _model = Services.Get<ArtifactModel>();
        _equip = new ArtifactEquipRequester(_data, Services.Get<ServerWaitManager>(), OnEquipSucceeded, Refresh);

        _sourceText = CreateSourceBox();

        Subscribe();

        equipButton.onClick.AddListener(OnEquipClicked);

        Refresh();

        _isReady = true;
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
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

    // 장착 결과 구독 해제 (Unity 메시지) — 켜고 끄기와 상관없이 살아 있는 동안 듣는다
    private void OnDestroy()
    {
        _equip?.Dispose();
    }

    #region 구독

    // 고름 · 도감 · 칸 · 언어 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed = true;

        _model.SelectionChanged += Refresh;
        _data.ArtifactsChanged  += Refresh;
        Lang.Changed            += Refresh;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed = false;

        _model.SelectionChanged -= Refresh;
        _data.ArtifactsChanged  -= Refresh;
        Lang.Changed            -= Refresh;
    }

    #endregion

    #region 그리기

    // 고른 번호의 상세를 그린다 (Start · OnEnable · 고름 · 도감 · 칸 · 언어 변경).
    private void Refresh()
    {
        int tid = _model.SelectedTid;

        bool has = tid != 0 && GameDataLoader.TryGetArtifact(tid, out _);

        emptyText.gameObject.SetActive(!has);
        contentRoot.SetActive(has);

        if (!has || !GameDataLoader.TryGetArtifact(tid, out ArtifactTableRow row))
        {
            return;
        }

        int  owned   = _data.GetArtifactCount(tid);
        bool isKnown = _data.IsArtifactEquipped(tid) || owned > 0;

        bool isEquipped = _data.IsArtifactEquipped(tid);

        equipButtonText.text     = Lang.Get(isEquipped ? "UI.Artifact.AlreadyEquipped" : "UI.Artifact.Equip");
        equipButton.interactable = _equip.CanEquip(tid);

        // 얻는 곳은 본 적 없어도 보여 준다 — 무엇을 노릴지 고르는 데 쓴다 (T-150)
        string sources = ArtifactSources.Build(tid);

        _sourceText.transform.parent.gameObject.SetActive(sources.Length > 0);
        _sourceText.text = sources.Length > 0 ? $"{UIRichText.Label(Lang.Get("UI.Artifact.Sources"))}\n{sources}" : "";

        if (!isKnown)
        {
            iconView.Bind(new ItemIconContent(GlobalRarity.None, "?", "", null));
            nameText.text   = Lang.Get("UI.Artifact.Unknown");
            nameText.color  = RarityPalette.OnDark(GlobalRarity.None);
            infoText.text   = UIRichText.Label(Lang.Get("UI.Artifact.UnknownHint"));
            effectText.text = "";
            hintText.text   = "";

            return;
        }

        // 아티팩트 창과 같은 글자 — 어두운 패널 위라 'OnDark'
        iconView.Bind(ItemIconContent.ForArtifact(tid, owned));
        nameText.text   = Loc.Get(row.NameLocKey);
        nameText.color  = RarityPalette.OnDark(row.GlobalRarity);
        infoText.text   = $"{UIRichText.Paint(RarityLabel.Get(row.GlobalRarity), RarityPalette.OnDark(row.GlobalRarity))}" +
                          $"{UIRichText.Dot}{ArtifactText.State(_data, tid)}" +
                          $"{UIRichText.Dot}{UIRichText.Label(Lang.Get("UI.Inventory.Owned"))} {Lang.Format("UI.Inventory.CountUnitShort", owned.ToString("N0"))}";
        effectText.text = ArtifactText.Effect(row);
        hintText.text   = UIRichText.Label(Lang.Get(isEquipped ? "UI.Artifact.SellHint" : "UI.Artifact.EquipHint"));
    }

    // 효과 칸(배경 상자 + 글자)을 복제해 바로 아래에 '얻는 곳' 칸을 만든다.
    // ■ 씬을 고치지 않으려고 실행 중에 만든다 — 씬을 에디터로 저장하면 레이아웃 계산값이 수천 줄 바뀐다(2026-11-24).
    //   같은 상자를 복제하므로 바탕 · 여백 · 글꼴이 효과 칸과 같다.
    private TMP_Text CreateSourceBox()
    {
        var effectBox = effectText.transform.parent;
        var box       = Instantiate(effectBox.gameObject, effectBox.parent);

        box.name = "Source Box (runtime)";
        box.transform.SetSiblingIndex(effectBox.GetSiblingIndex() + 1);

        var text = box.GetComponentInChildren<TMP_Text>(true);

        // 여러 줄이라 높이를 글자에 맡긴다 — 효과 칸은 한 줄 고정 높이(58) · 자동 축소 · 줄바꿈 없음이다.
        // 자동 축소를 두면 줄 수만큼 글자가 작아진다 — 효과 칸의 최대 크기로 고정하고 상자가 늘어나게 한다.
        if (box.TryGetComponent(out LayoutElement boxLayout))
        {
            boxLayout.preferredHeight = -1f;
        }

        // 상세 칸은 높이 300 고정(그 아래 오른쪽 패널은 비어 있다) — 최소 300은 두고 내용만큼 늘어나게 한다
        if (TryGetComponent(out LayoutElement selfLayout))
        {
            selfLayout.preferredHeight = -1f;
        }

        text.enableAutoSizing = false;
        text.fontSize         = effectText.fontSizeMax;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode     = TextOverflowModes.Overflow;

        return text;
    }

    #endregion

    #region 장착

    // [도감에 장착] (equipButton OnClick)
    private void OnEquipClicked()
    {
        _equip.Send(_model.SelectedTid);
        equipButton.interactable = _equip.CanEquip(_model.SelectedTid);
    }

    // 장착 성공 — 그 칸을 반짝인다 (ArtifactEquipRequester의 onSucceeded)
    private void OnEquipSucceeded(int tid)
    {
        _model.Highlight(tid);
    }

    #endregion
}

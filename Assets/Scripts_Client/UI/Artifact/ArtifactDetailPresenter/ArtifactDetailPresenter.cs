using GameData;
using TMPro;
using UnityEngine;

// 도감에서 고른 아티팩트의 상세 — 이름 · 등급 · 상태 · 효과 (T-151). **보기만 한다** — 버튼이 없다.
//
// ■ 켜고 끄기가 없다 (#90 · 2026-10-28 서버 반영) — 장착하면 효과가 항상 붙는다.
//   장착은 인벤토리 장비 탭의 아티팩트 창이 한다('ArtifactEquipPresenter' · 2026-10-25 사용자 결정).
//   본 적 없는 번호는 이름을 가린다.
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

    private PlayerDataModel _data  = null!;
    private ArtifactModel   _model = null!;

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

        _data  = Services.Get<PlayerDataModel>();
        _model = Services.Get<ArtifactModel>();

        Subscribe();

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

        if (!isKnown)
        {
            iconView.Bind(new ItemIconContent(GlobalRarity.None, "?", "", null));
            nameText.text   = Lang.Get("UI.Artifact.Unknown");
            nameText.color  = RarityPalette.Get(GlobalRarity.None);
            infoText.text   = UIRichText.Label(Lang.Get("UI.Artifact.UnknownHint"));
            effectText.text = "";

            return;
        }

        iconView.Bind(ItemIconContent.ForArtifact(tid, 0));
        nameText.text   = Loc.Get(row.NameLocKey);
        nameText.color  = RarityPalette.Get(row.GlobalRarity);
        infoText.text   = $"{UIRichText.Paint(RarityLabel.Get(row.GlobalRarity), RarityPalette.Get(row.GlobalRarity))}" +
                          $"{UIRichText.Dot}{ArtifactText.State(_data, tid)}" +
                          $"{UIRichText.Dot}{UIRichText.Label(Lang.Get("UI.Inventory.Owned"))} {Lang.Format("UI.Inventory.CountUnitShort", owned.ToString("N0"))}";
        effectText.text = ArtifactText.Effect(row);
    }

    #endregion
}

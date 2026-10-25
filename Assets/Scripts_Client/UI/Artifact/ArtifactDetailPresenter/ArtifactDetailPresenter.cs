using GameData;
using MikaNetwork;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 도감에서 고른 아티팩트의 상세 — 이름 · 등급 · 상태 · 효과 (T-151).
//
// ■ [효과 끄기]는 두지 않는다 (2026-10-25 사용자 결정) — 장착하면 계속 켜 둔다. 서버에 끄기 제거를 요청했다.
//   이미 꺼 둔 번호만 [효과 켜기]가 남는다 — 되살릴 길이 없으면 영영 꺼진 채로 남기 때문이다.
//   장착하지 않은 번호는 버튼이 없다 — 장착은 인벤토리 장비 탭의 아티팩트 창이 한다('ArtifactEquipPresenter' · 2026-10-25 사용자 결정).
//   본 적 없는 번호는 이름을 가린다.
// ■ 판매 버튼을 두지 않는다 (2026-10-25 사용자 결정) — 좋은 것은 경매장에서 즉시 판매가보다 비싸게 팔 수 있다.
//   파는 길은 장비 탭 아티팩트 칸의 우클릭(즉시 판매) · Ctrl+우클릭(경매) 둘이고, 고르는 일은 사람이 한다.
public class ArtifactDetailPresenter : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("고른 것이 없을 때의 안내")]
    private TMP_Text emptyText = null!;

    [SerializeField, Tooltip("고른 것이 있을 때 켜지는 묶음 — 아이콘 · 글자 · 버튼")]
    private GameObject contentRoot = null!;

    [SerializeField, Tooltip("아이콘 칸 (ItemIconView 프리팹)")]
    private ItemIconView iconView = null!;

    [SerializeField, Tooltip("이름 — 등급색")]
    private TMP_Text nameText = null!;

    [SerializeField, Tooltip("정보 한 줄 — 등급 · 도감 상태 · 보유")]
    private TMP_Text infoText = null!;

    [SerializeField, Tooltip("효과 한 줄")]
    private TMP_Text effectText = null!;

    [SerializeField, Tooltip("[효과 끄기/켜기]. OnClick은 코드가 연결한다")]
    private Button actionButton = null!;

    [SerializeField, Tooltip("버튼 글자 — 코드가 쓴다(LocalizedText를 붙이지 않는다)")]
    private TMP_Text actionButtonText = null!;

    private PlayerDataModel   _data    = null!;
    private ArtifactModel     _model   = null!;
    private NetworkManager    _network = null!;
    private ServerWaitManager _wait    = null!;

    // 진행 중인 요청의 대기 — 연타로 두 번 나가지 않게 버튼을 잠근다.
    private ServerWaitHandle? _waitHandle;

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 배선 → 초기화 순서로 진행한다 (클라 공통 규약)
    private void Start()
    {
        this.RequireRef(emptyText,        nameof(emptyText));
        this.RequireRef(contentRoot,      nameof(contentRoot));
        this.RequireRef(iconView,         nameof(iconView));
        this.RequireRef(nameText,         nameof(nameText));
        this.RequireRef(infoText,         nameof(infoText));
        this.RequireRef(effectText,       nameof(effectText));
        this.RequireRef(actionButton,     nameof(actionButton));
        this.RequireRef(actionButtonText, nameof(actionButtonText));

        _data    = Services.Get<PlayerDataModel>();
        _model   = Services.Get<ArtifactModel>();
        _network = NetworkManager.Instance;
        _wait    = Services.Get<ServerWaitManager>();

        Subscribe();

        actionButton.onClick.AddListener(OnActionClicked);

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

    // 고름 · 도감 · 칸 · 결과 · 언어 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed = true;

        _model.SelectionChanged       += Refresh;
        _data.ArtifactsChanged        += Refresh;
        _data.ArtifactActiveCompleted += OnActiveCompleted;
        Lang.Changed                  += Refresh;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed = false;

        _model.SelectionChanged       -= Refresh;
        _data.ArtifactsChanged        -= Refresh;
        _data.ArtifactActiveCompleted -= OnActiveCompleted;
        Lang.Changed                  -= Refresh;
    }

    #endregion

    #region 그리기

    // 고른 번호의 상세를 그린다 (Start · OnEnable · 고름 · 도감 · 칸 · 언어 변경 · 대기 종료).
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

        bool isEquipped = _data.ArtifactCodex.TryGetValue(tid, out bool active);
        int  owned      = _data.GetArtifactCount(tid);
        bool isKnown    = isEquipped || owned > 0;

        if (!isKnown)
        {
            iconView.Bind(new ItemIconContent(GlobalRarity.None, "?", "", null));
            nameText.text  = Lang.Get("UI.Artifact.Unknown");
            nameText.color = RarityPalette.Get(GlobalRarity.None);
            infoText.text  = UIRichText.Label(Lang.Get("UI.Artifact.UnknownHint"));
            effectText.text = "";
            actionButton.gameObject.SetActive(false);

            return;
        }

        iconView.Bind(ItemIconContent.ForArtifact(tid, 0));
        nameText.text  = Loc.Get(row.NameLocKey);
        nameText.color = RarityPalette.Get(row.GlobalRarity);
        infoText.text  = $"{UIRichText.Paint(RarityLabel.Get(row.GlobalRarity), RarityPalette.Get(row.GlobalRarity))}" +
                         $"{UIRichText.Dot}{ArtifactText.State(_data, tid)}" +
                         $"{UIRichText.Dot}{UIRichText.Label(Lang.Get("UI.Inventory.Owned"))} {Lang.Format("UI.Inventory.CountUnitShort", owned.ToString("N0"))}";

        // 꺼 둔 번호는 효과를 흐리게 — 지금 붙지 않는다
        string effect = ArtifactText.Effect(row);
        effectText.text = isEquipped && !active ? UIRichText.Paint(effect, UIThemeRole.TextDisabled) : effect;

        actionButton.gameObject.SetActive(isEquipped && !active);
        actionButtonText.text     = Lang.Get("UI.Artifact.TurnOn");
        actionButton.interactable = _waitHandle == null;
    }

    #endregion

    #region 요청

    // 켜고 끄기 (actionButton OnClick).
    private void OnActionClicked()
    {
        int tid = _model.SelectedTid;

        if (_waitHandle != null || tid == 0 || !_data.ArtifactCodex.TryGetValue(tid, out bool active))
        {
            return;
        }

        _network.Send(new C_ArtifactActiveRequest { ArtifactTid = tid, IsActive = !active });
        ClientLogger.Info(ClientLogger.Send, $"아티팩트 켜고 끄기 요청 — 번호 {tid} → {(!active ? "켜기" : "끄기")}");

        _waitHandle = _wait.Begin(Lang.Get("UI.Artifact.WaitToggle"), onClosed: OnWaitClosed);
        actionButton.interactable = false;
    }

    // 켜고 끄기 결과 (PlayerDataModel.ArtifactActiveCompleted 구독)
    private void OnActiveCompleted(EResultCode code, bool isActive)
    {
        if (_waitHandle == null)
        {
            return; // 내가 보낸 것이 아니다
        }

        if (code != EResultCode.Ok)
        {
            _waitHandle?.Fail(ResultMessages.ToText(code));

            return;
        }

        _waitHandle?.Succeed();
    }

    // 대기가 끝났다(성공·실패·타임아웃 공통) — 버튼을 다시 연다 (ServerWaitManager.Begin의 onClosed)
    private void OnWaitClosed()
    {
        _waitHandle = null;

        if (_isReady && isActiveAndEnabled)
        {
            Refresh();
        }
    }

    #endregion
}

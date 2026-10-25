using System;
using GameData;
using MikaNetwork;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 아티팩트 창 — 장비 탭에서 아티팩트 칸을 좌클릭하면 판매 목록 자리에 열린다 (T-151 · 2026-10-25 사용자 결정).
//
// ■ 장비와 무엇이 다른가 — 좌클릭 하나
//   장비 칸 좌클릭은 큐브 창, 아티팩트 칸 좌클릭은 이 창이다. 우클릭(판매 담기) · Ctrl+우클릭(경매 등록)은 장비와 같다('InventoryGridPresenter').
//   이 창에서만 할 수 있는 일은 **도감에 장착** 하나다 — 칸에서 1개가 빠져 도감에 영구로 들어가고 효과가 켜진다.
// ■ 장착하면 도감 화면이 열리고 그 칸이 반짝인다 — 장착은 되돌릴 수 없어서, 무엇이 어디에 들어갔는지 그 자리에서 보여 준다.
//   열기는 'UIManager.ShowArtifact', 반짝임은 'ArtifactModel.Highlight' → 'ArtifactCodexPresenter'.
// ■ 이미 장착한 번호는 장착할 수 없다 — 버튼을 잠그고 '이미 도감에 장착함'으로 적는다. 남는 것은 팔거나 경매에 올린다.
// ■ 확인 창을 두지 않는다 — 번호마다 1개만 들어가고 남는 것은 그대로 칸에 있다(화면 중앙 팝업 최소화 · P1).
//
// ■ 판매 목록 자리를 빌려 쓴다 — 큐브 창과 같다. 판매 목록은 'OpenChanged'를 듣고 스스로 물러난다.
//   큐브 창과 달리 판매 목록을 비우지 않는다 — 이 창은 칸을 바꾸지 않아 담아 둔 것과 다툴 일이 없다.
//   닫기 · 장비 탭을 떠남 · 우클릭 담기 · 장비 칸 좌클릭(큐브 창)이면 물러난다.
public class ArtifactEquipPresenter : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("인벤토리 탭 줄. 장비 탭을 떠나면 이 창이 닫힌다")]
    private InventoryTabPresenter inventoryTabs = null!;

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

    [SerializeField, Tooltip("[도감 보기] — 아티팩트 화면을 이 번호로 연다. OnClick은 코드가 연결한다")]
    private Button codexButton = null!;

    [SerializeField, Tooltip("닫기 [X]. OnClick은 코드가 연결한다")]
    private Button closeButton = null!;

    private PlayerDataModel   _data    = null!;
    private ArtifactModel     _model   = null!;
    private UIManager         _ui      = null!;
    private NetworkManager    _network = null!;
    private ServerWaitManager _wait    = null!;

    private int _tid; // 창에 띄운 아티팩트 번호. 0이면 닫혀 있다

    // 진행 중인 장착 요청 — 연타로 두 번 나가지 않게 버튼을 잠근다.
    private ServerWaitHandle? _waitHandle;
    private int               _equippingTid;

    private bool _isReady; // Start 완료 여부

    public bool IsOpen => _tid != 0;

    // 창이 열리고 닫혔다 — 판매 목록이 자리를 비키고 돌아온다.
    public event Action<bool>? OpenChanged;

    // 참조 확보 → 구독 → 배선 → 초기화 순서로 진행한다 (클라 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다.
    private void Start()
    {
        this.RequireRef(inventoryTabs,   nameof(inventoryTabs));
        this.RequireRef(iconView,        nameof(iconView));
        this.RequireRef(nameText,        nameof(nameText));
        this.RequireRef(infoText,        nameof(infoText));
        this.RequireRef(effectText,      nameof(effectText));
        this.RequireRef(hintText,        nameof(hintText));
        this.RequireRef(equipButton,     nameof(equipButton));
        this.RequireRef(equipButtonText, nameof(equipButtonText));
        this.RequireRef(codexButton,     nameof(codexButton));
        this.RequireRef(closeButton,     nameof(closeButton));

        _data    = Services.Get<PlayerDataModel>();
        _model   = Services.Get<ArtifactModel>();
        _ui      = Services.Get<UIManager>();
        _network = NetworkManager.Instance;
        _wait    = Services.Get<ServerWaitManager>();

        // ⚠️ 구독은 Start/OnDestroy에 건다 — 이 창은 자기 오브젝트를 끈다(큐브 창과 같다).
        inventoryTabs.TabChanged     += OnTabChanged;
        _data.ArtifactsChanged       += OnArtifactsChanged;
        _data.ArtifactEquipCompleted += OnEquipCompleted;
        Lang.Changed                 += Redraw;

        equipButton.onClick.AddListener(OnEquipClicked);
        codexButton.onClick.AddListener(OnCodexClicked);
        closeButton.onClick.AddListener(Close);

        _isReady = true;

        if (IsOpen)
        {
            Redraw(); // 꺼진 채 저장된 창 — 'Open'이 번호만 기억하고 켰다
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    // 구독 해제 (Unity 메시지)
    private void OnDestroy()
    {
        if (!_isReady)
        {
            return;
        }

        inventoryTabs.TabChanged     -= OnTabChanged;
        _data.ArtifactsChanged       -= OnArtifactsChanged;
        _data.ArtifactEquipCompleted -= OnEquipCompleted;
        Lang.Changed                 -= Redraw;
    }

    #region 열고 닫기

    // 이 번호로 창을 연다 (인벤토리 격자가 아티팩트 칸 좌클릭에서 호출).
    // ※ 아직 'Start'가 안 돌았을 수 있다(꺼진 채 저장된 창) — 번호만 기억하고 켜면 'Start'가 그린다.
    public void Open(int artifactTid)
    {
        if (artifactTid == 0)
        {
            return;
        }

        bool wasOpen = IsOpen;

        _tid = artifactTid;

        gameObject.SetActive(true);

        if (_isReady)
        {
            Redraw();
        }

        if (!wasOpen)
        {
            OpenChanged?.Invoke(true);
        }
    }

    // 창을 닫고 판매 목록에 자리를 돌려준다 (닫기 · 탭 전환 · 우클릭 담기 · 큐브 창 · 칸이 다 빠졌을 때).
    // ※ 장착 응답을 기다리는 중이어도 닫는다 — 대기 손잡이는 응답 구독(Start/OnDestroy)이 닫는다.
    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        _tid = 0;

        gameObject.SetActive(false);
        OpenChanged?.Invoke(false);
    }

    // 장비 탭을 떠나면 닫는다 (InventoryTabPresenter.TabChanged 구독).
    private void OnTabChanged(InventoryTab tab)
    {
        if (tab != InventoryTab.Equipment)
        {
            Close();
        }
    }

    // 칸·도감이 바뀌었다 — 다 빠졌으면 닫고, 아니면 다시 그린다 (PlayerDataModel.ArtifactsChanged 구독).
    // ※ 마지막 1개를 장착했으면 칸이 사라진다 — 창도 함께 물러난다. 도감 화면이 그 자리를 이어받는다.
    private void OnArtifactsChanged()
    {
        if (!IsOpen)
        {
            return;
        }

        if (_data.GetArtifactCount(_tid) <= 0)
        {
            Close();

            return;
        }

        Redraw();
    }

    #endregion

    #region 그리기

    // 띄운 번호를 그린다 (Open · Start · 칸 변화 · 언어 변경 · 대기 종료).
    private void Redraw()
    {
        if (!IsOpen || !GameDataLoader.TryGetArtifact(_tid, out ArtifactTableRow row))
        {
            return;
        }

        bool isEquipped = _data.IsArtifactEquipped(_tid);
        int  owned      = _data.GetArtifactCount(_tid);

        iconView.Bind(ItemIconContent.ForArtifact(_tid, owned));
        nameText.text  = Loc.Get(row.NameLocKey);
        nameText.color = RarityPalette.Get(row.GlobalRarity);
        infoText.text  = $"{UIRichText.Paint(RarityLabel.Get(row.GlobalRarity), RarityPalette.Get(row.GlobalRarity))}" +
                         $"{UIRichText.Dot}{ArtifactText.State(_data, _tid)}" +
                         $"{UIRichText.Dot}{UIRichText.Label(Lang.Get("UI.Inventory.Owned"))} {Lang.Format("UI.Inventory.CountUnitShort", owned.ToString("N0"))}";
        effectText.text = ArtifactText.Effect(row);
        hintText.text   = UIRichText.Label(Lang.Get(isEquipped ? "UI.Artifact.SellHint" : "UI.Artifact.EquipHint"));

        equipButtonText.text     = Lang.Get(isEquipped ? "UI.Artifact.AlreadyEquipped" : "UI.Artifact.Equip");
        equipButton.interactable = !isEquipped && owned > 0 && _waitHandle == null;
    }

    #endregion

    #region 요청

    // 도감에 장착 (equipButton OnClick).
    private void OnEquipClicked()
    {
        if (_waitHandle != null || !IsOpen || _data.IsArtifactEquipped(_tid) || _data.GetArtifactCount(_tid) <= 0)
        {
            return;
        }

        if (!_data.IsLoggedIn)
        {
            ClientLogger.Warn(ClientLogger.Send, "아티팩트 장착 요청을 보내지 않았다 — 로그인이 먼저다(서버가 응답 없이 버린다)");

            return;
        }

        _equippingTid = _tid;
        _network.Send(new C_ArtifactEquipRequest { ArtifactTid = _tid });
        ClientLogger.Info(ClientLogger.Send, $"아티팩트 장착 요청 — 번호 {_tid}");

        _waitHandle              = _wait.Begin(Lang.Get("UI.Artifact.WaitEquip"), onClosed: OnWaitClosed);
        equipButton.interactable = false;
    }

    // 장착 결과 (PlayerDataModel.ArtifactEquipCompleted 구독) — 성공이면 도감 화면을 열고 그 칸을 반짝인다.
    // ※ 칸 · 도감은 뒤따르는 스냅샷이 바꾼다. 내가 보낸 장착이 아니면(다른 경로) 대기만 닫는다.
    private void OnEquipCompleted(EResultCode code, int tid)
    {
        if (_waitHandle == null || tid != _equippingTid)
        {
            return;
        }

        _equippingTid = 0;

        if (code != EResultCode.Ok)
        {
            _waitHandle.Fail(ResultMessages.ToText(code));

            return;
        }

        _waitHandle.Succeed();

        _ui.ShowArtifact(true);
        _model.Highlight(tid);
    }

    // [도감 보기] — 아티팩트 화면을 이 번호로 연다 (codexButton OnClick).
    private void OnCodexClicked()
    {
        if (!IsOpen)
        {
            return;
        }

        _ui.ShowArtifact(true);
        _model.Select(_tid);
    }

    // 대기가 끝났다(성공·실패·타임아웃 공통) — 버튼을 다시 연다 (ServerWaitManager.Begin의 onClosed)
    private void OnWaitClosed()
    {
        _waitHandle = null;

        if (IsOpen)
        {
            Redraw();
        }
    }

    #endregion
}

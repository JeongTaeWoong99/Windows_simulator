using GameData;
using MikaNetwork;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 건물 창의 업그레이드 탭 — 지금 레벨 ▶ 다음 레벨 · 필요한 골드 · 자원('보유 / 필요') · 얻는 것 · 못 올리는 이유 · [Lv n으로 올리기].
//
// 비용은 'BuildingLevelTable'의 다음 레벨 행이다. 자원 보유량은 인벤토리 + 창고 합산이다 — 서버가 그렇게 뺀다(#75 요청 3).
// 못 올려도 버튼은 눌린다 — 회색으로 칠하고, 누르면 이유를 알린다('TraitPresenter'의 [레벨 올리기]와 같다).
public class OutpostUpgradePresenter : MonoBehaviour
{
    [CenterHeader("레벨")]
    [SerializeField, Tooltip("지금 레벨 — 'Lv3'")]
    private TMP_Text fromLevelText = null!;

    [SerializeField, Tooltip("다음 레벨 — 'Lv4'. 최대 레벨이면 화살표째 꺼진다")]
    private TMP_Text toLevelText = null!;

    [SerializeField, Tooltip("두 레벨 사이 화살표")]
    private GameObject arrow = null!;

    [CenterHeader("비용")]
    [SerializeField, Tooltip("비용 한 줄 프리팹 (OutpostCostRowView)")]
    private OutpostCostRowView costRowPrefab = null!;

    [SerializeField, Tooltip("비용 줄이 들어가는 부모 (VerticalLayoutGroup)")]
    private Transform costRowParent = null!;

    [SerializeField, Tooltip("비용 묶음 — 최대 레벨이면 꺼진다")]
    private GameObject costBox = null!;

    [CenterHeader("얻는 것 · 이유")]
    [SerializeField, Tooltip("얻는 것 — '특성 포인트 +1'. 트리가 없는 건물은 준비 중 안내까지 적는다")]
    private TMP_Text gainText = null!;

    [SerializeField, Tooltip("못 올리는 이유 한 줄")]
    private TMP_Text reasonText = null!;

    [SerializeField, Tooltip("[Lv n으로 올리기]. OnClick은 코드가 연결한다")]
    private Button upgradeButton = null!;

    [SerializeField, Tooltip("버튼 글씨")]
    private TMP_Text upgradeLabel = null!;

    [CenterHeader("색")]
    [SerializeField, Tooltip("지금 올릴 수 있을 때")]
    private UIThemeRole readyRole = UIThemeRole.ButtonPrimary;

    [SerializeField, Tooltip("못 올릴 때 — 눌리기는 한다")]
    private UIThemeRole blockedRole = UIThemeRole.ButtonDisabled;

    private UIRowList<OutpostCostRowView> _rows = null!;

    private PlayerDataModel   _data = null!;
    private ServerWaitManager _wait = null!;

    private int  _buildingTid;
    private bool _isWired;
    private bool _isSubscribed;

    // 보낸 요청의 대기. null이면 기다리는 요청이 없다.
    private ServerWaitHandle? _upgradeWait;

    // 이 건물로 화면을 켠다 ('OutpostBuildingPresenter'가 호출).
    public void Show(int buildingTid)
    {
        Wire();

        _buildingTid = buildingTid;

        gameObject.SetActive(true);
        Subscribe();
        Redraw();
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

        this.RequireRef(fromLevelText, nameof(fromLevelText));
        this.RequireRef(toLevelText,   nameof(toLevelText));
        this.RequireRef(arrow,         nameof(arrow));
        this.RequireRef(costRowPrefab, nameof(costRowPrefab));
        this.RequireRef(costRowParent, nameof(costRowParent));
        this.RequireRef(costBox,       nameof(costBox));
        this.RequireRef(gainText,      nameof(gainText));
        this.RequireRef(reasonText,    nameof(reasonText));
        this.RequireRef(upgradeButton, nameof(upgradeButton));
        this.RequireRef(upgradeLabel,  nameof(upgradeLabel));

        _data = Services.Get<PlayerDataModel>();
        _wait = Services.Get<ServerWaitManager>();
        _rows = new UIRowList<OutpostCostRowView>(costRowPrefab, costRowParent);

        upgradeButton.onClick.AddListener(OnUpgradeClicked);
    }

    // 다시 켜졌을 때의 재구독 — 창을 닫았다 열면 'Show'보다 먼저 온다 (Unity 메시지)
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

    #region 구독

    // 보유량 · 상한 · 레벨이 바뀌면 다시 그린다.
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed = true;

        _data.BuildingsChanged         += Redraw;
        _data.CurrencyChanged          += Redraw;
        _data.InventoryChanged         += Redraw;
        _data.AccountLevelChanged      += Redraw;
        _data.BuildingUpgradeCompleted += OnUpgradeCompleted;
        Lang.Changed                   += Redraw;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed = false;

        _data.BuildingsChanged         -= Redraw;
        _data.CurrencyChanged          -= Redraw;
        _data.InventoryChanged         -= Redraw;
        _data.AccountLevelChanged      -= Redraw;
        _data.BuildingUpgradeCompleted -= OnUpgradeCompleted;
        Lang.Changed                   -= Redraw;
    }

    #endregion

    #region 그리기

    private void Redraw()
    {
        int level = _data.GetBuildingLevel(_buildingTid);
        int next  = level + 1;

        bool hasNext = GameDataLoader.TryGetBuildingLevel(_buildingTid, next, out var cost);

        fromLevelText.text = $"Lv{level}";
        toLevelText.text   = $"Lv{next}";

        arrow.SetActive(hasNext);
        toLevelText.gameObject.SetActive(hasNext);
        costBox.SetActive(hasNext);
        upgradeButton.gameObject.SetActive(hasNext);

        gainText.text = DescribeGain();

        EResultCode check = _data.CheckBuildingUpgrade(_buildingTid);

        reasonText.text = DescribeReason(check);

        if (!hasNext)
        {
            return;
        }

        DrawCost(cost);

        upgradeLabel.text = Lang.Format("UI.Outpost.UpgradeTo", next);
        PaintButton(check == EResultCode.Ok);
    }

    // 비용 줄 — 골드(있으면) + 자원. 자원 없는 첫 레벨은 골드 한 줄이다.
    private void DrawCost(BuildingLevelTableRow cost)
    {
        int used = 0;

        if (cost.Gold > 0)
        {
            _rows.Get(used++).Bind(ItemIconContent.ForGold(), Lang.Get("UI.Outpost.Gold"), DescribeAmount(_data.Gold, cost.Gold));
        }

        for (int i = 0; i < cost.ItemTIDs.Length && i < cost.ItemCounts.Length; i++)
        {
            int itemTid = cost.ItemTIDs[i];
            int count   = cost.ItemCounts[i];

            _rows.Get(used++).Bind(ItemIconContent.ForItem(itemTid, 0), GameDataLoader.GetItemName(itemTid), DescribeAmount(_data.GetItemCount(itemTid), count));
        }

        _rows.HideFrom(used);
    }

    // '보유 / 필요' — 보유를 충족 · 부족 색으로 칠한다.
    private static string DescribeAmount(long owned, long required)
        => $"{UIRichText.Paint($"{owned:N0}", owned >= required ? UIThemeRole.Positive : UIThemeRole.Negative)} / {required:N0}";

    // 얻는 것 — 포인트 1점. 트리가 없는 건물은 왜 지금 쓸 곳이 없는지 함께 적는다.
    private string DescribeGain()
        => GameDataLoader.HasBuildingNodes(_buildingTid)
            ? Lang.Get("UI.Outpost.GainPoint")
            : $"{Lang.Get("UI.Outpost.GainPoint")}\n{UIRichText.Label(Lang.Get("UI.Outpost.TraitPending"))}";

    // 지금 못 올리는 이유. 올릴 수 있으면 빈 칸이다.
    // ※ 골드·자원 부족은 비용 줄의 색이 이미 말하지만, 버튼 옆에서 한 번 더 말해야 왜 회색인지 읽힌다.
    private string DescribeReason(EResultCode check)
    {
        switch (check)
        {
            case EResultCode.Ok:
                return "";

            case EResultCode.BuildingAccountLevelCap:
                return Lang.Format("UI.Outpost.NeedAccount", _data.GetBuildingLevel(_buildingTid) + 1, _data.AccountLevel);

            default:
                return ResultMessages.ToText(check);
        }
    }

    // 버튼 색 — 'interactable'은 끄지 않는다. 눌러서 이유를 들을 수 있어야 한다.
    private void PaintButton(bool isReady)
    {
        Color      color  = UIThemePalette.Of(isReady ? readyRole : blockedRole);
        ColorBlock colors = upgradeButton.colors;

        colors.normalColor      = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.15f);
        colors.pressedColor     = Color.Lerp(color, Color.black, 0.2f);
        colors.selectedColor    = color;

        upgradeButton.colors = colors;
    }

    #endregion

    #region 요청

    // [Lv n으로 올리기]를 눌렀다 (upgradeButton.onClick).
    // ※ 클라 판정은 안내일 뿐이다. 통과해도 서버가 다시 검사한다(게임기획코어 P4).
    private void OnUpgradeClicked()
    {
        if (_upgradeWait != null)
        {
            return; // 응답 대기 중 — 연타하면 두 번 낸다
        }

        EResultCode check = _data.CheckBuildingUpgrade(_buildingTid);

        if (check != EResultCode.Ok)
        {
            _wait.RaiseRefusal(DescribeReason(check), upgradeButton.transform);

            return;
        }

        // 로그인 전에 보내면 서버가 응답 없이 버린다 — 'TraitPresenter.RequestLearn'과 같은 이유
        if (!_data.IsLoggedIn)
        {
            ClientLogger.Warn(ClientLogger.Send, "건물 업그레이드 요청을 보내지 않았다 — 로그인이 먼저다(서버가 응답 없이 버린다)");

            return;
        }

        ClientLogger.Info(ClientLogger.Send, $"건물 업그레이드 요청 — {_buildingTid}");

        NetworkManager.Instance.Send(new C_BuildingUpgradeRequest { BuildingTID = _buildingTid });
        _upgradeWait = _wait.Begin(Lang.Get("UI.Outpost.WaitUpgrade"), onClosed: () => _upgradeWait = null);
    }

    // 업그레이드 결과 (PlayerDataModel.BuildingUpgradeCompleted 구독).
    private void OnUpgradeCompleted(int buildingTid, EResultCode code)
    {
        if (_upgradeWait == null)
        {
            return; // 내가 보낸 요청이 아니다
        }

        if (code == EResultCode.Ok)
        {
            _upgradeWait.Succeed();
        }
        else
        {
            _upgradeWait.Fail(ResultMessages.ToText(code));
        }
    }

    #endregion
}

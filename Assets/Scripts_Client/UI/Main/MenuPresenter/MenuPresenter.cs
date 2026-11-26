using MikaProtocol;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// 작업슬롯 화면 하단의 메뉴 줄 — 좌우 열(인벤토리·길드)과 경매장 · 아티팩트 · 마을을 여는 버튼들. (기획 2.4)
// 'Title'과 함께 늘 켜져 있어, 가운데 세 화면이 무엇으로 바뀌든 그대로 남는다.
//
// ■ 이 줄이 특별 이벤트 자리다 (기획 2.4 · 2026-09-16 확정)
//   보스 · 대형 작업물처럼 채취 루프 밖의 콘텐츠가 들어오면 이 줄에 버튼이 는다. 높이가 고정이라 슬롯 영역은 밀리지 않는다.
//   ⚠️ 아직 없는 콘텐츠의 빈 칸·잠긴 버튼을 미리 두지 않는다 — "지금 못 하는 것"이 상시로 눈에 남는다.
//      예외 — [레이드]는 맨 오른쪽에 비활성으로 먼저 둔다(2026-11-16 사용자 결정). 씬에서 'interactable'을 꺼 두어 코드 배선이 없다.
//   ⚠️ 버튼에 배지·타이머·"오늘까지" 표시를 붙이지 않는다 — 상주 창이 재촉하는 물건이 된다(P1).
//      예외 — 빨간 점. 시간이 아니라 **지금 할 일 · 안 본 결과**가 있을 때만 켠다. 숫자는 적지 않는다.
//        [마을] 올릴 건물 · 찍을 특성(2026-11-22 사용자 요청)
//        [인벤토리] 상급 큐브 결과를 고르기 전 장비(T-145 · 2026-11-26)
//      경고 점('WarningDotView' — 느낌표 · 숨 쉬기 · 올리면 툴팁)은 갈래가 다르다 — 놔두면 손해인 일.
//        [인벤토리] 가득 참 — 채취물이 버려지는 중. 켜지면 빨간 점 대신이다(2026-11-27 · 위젯 알림 줄과 같은 사유)
//        [경매장] 안 본 거래 완료(낙찰 · 판매) — 완료 탭 배지와 같은 값(T-145)
//        [아티팩트] 도감에 안 넣은 번호가 칸에 있다(T-145)
//      넣지 않는다 — 상자 쌓임(1000개까지 정상) · 응축 가능(너무 자주 켜져 재촉이 된다).
//
// ⚠️ 열지 않고 뒤집는다 — 여는 일만 하면 이미 열려 있을 때 눌러도 변화가 없어
// 버튼이 고장 난 것처럼 보인다. 인벤토리·길드은 자리를 뺏지 않으므로 'MainScreen' 전환이 아니라
// 각각의 열 토글('ToggleInventory'·'ToggleGuild')을 쓴다.
//
// ※ 경매장 · 아티팩트 · 마을(코드 이름 Outpost)은 토글이 아니라 열기다('ShowAuction' · 'ShowArtifact' · 'ShowOutpost') — 열리면 화면 전체를 덮어 이 줄이 가려진다.
//   닫기는 그 화면 안 [X]다(T-147 · T-182).
public class MenuPresenter : MonoBehaviour
{
    // ※ 선택 참조다. 비워 두면 그 버튼이 아직 없는 것으로 보고 넘어간다.
    [CenterHeader("참조")]
    [SerializeField, Tooltip("인벤토리 열기 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    [FormerlySerializedAs("storageButton")]
    private Button? inventoryButton;

    // ※ FormerlySerializedAs — 거래(Market) → 거점(Hub, 2026-10-13 · T-147) → 길드(Guild, 2026-10-27 · T-182) 개명 전 씬 값을 잇는다.
    [SerializeField, Tooltip("길드 열기 버튼(뽑기·창고). OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    [FormerlySerializedAs("marketButton")]
    [FormerlySerializedAs("hubButton")]
    private Button? guildButton;

    [SerializeField, Tooltip("경매장 열기 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button? auctionButton;

    [SerializeField, Tooltip("아티팩트 화면(도감 · 보유 · 판매) 열기 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button? artifactButton;

    [SerializeField, Tooltip("마을 화면(건물 · 특성) 열기 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button? outpostButton;

    [SerializeField, Tooltip("[마을] 빨간 점 — 올릴 건물이나 찍을 특성이 있을 때 켜진다")]
    private GameObject? outpostNoticeDot;

    [SerializeField, Tooltip("[인벤토리] 빨간 점 — 상급 큐브 결과를 고르기 전 장비가 있을 때 켜진다")]
    private GameObject? inventoryNoticeDot;

    [SerializeField, Tooltip("[인벤토리] 경고 점 — 인벤토리가 가득 차 채취물이 버려지는 중. 켜지면 빨간 점 대신이다. 올리면 툴팁")]
    private WarningDotView? inventoryWarningDot;

    [SerializeField, Tooltip("[경매장] 빨간 점 — 안 본 거래 완료(낙찰 · 판매)가 있을 때 켜진다")]
    private GameObject? auctionNoticeDot;

    [SerializeField, Tooltip("[아티팩트] 빨간 점 — 도감에 안 넣은 번호가 칸에 있을 때 켜진다")]
    private GameObject? artifactNoticeDot;

    private PlayerDataModel? _data;
    private AuctionModel?    _auction;

    // 참조 확보 → 구독 → 배선 (클라 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다(MonoService 주석).
    private void Start()
    {
        var ui = Services.Get<UIManager>();

        _data    = Services.Get<PlayerDataModel>();
        _auction = Services.Get<AuctionModel>();

        _data.BuildingsChanged         += RedrawNotice;
        _data.TraitsChanged            += RedrawNotice;
        _data.CurrencyChanged          += RedrawNotice;
        _data.InventoryChanged         += RedrawNotice;
        _data.AccountLevelChanged      += RedrawNotice;
        _data.EquipsChanged            += RedrawNotice;
        _data.InventoryFullChanged     += RedrawNotice;
        _data.ArtifactsChanged         += RedrawNotice;
        _auction.HistoryBadgeChanged   += RedrawNotice;
        RedrawNotice();

        if (inventoryButton != null)
        {
            inventoryButton.onClick.AddListener(ui.ToggleInventory);
        }

        if (guildButton != null)
        {
            guildButton.onClick.AddListener(ui.ToggleGuild);
        }

        if (auctionButton != null)
        {
            auctionButton.onClick.AddListener(() => ui.ShowAuction(true));
        }

        if (artifactButton != null)
        {
            artifactButton.onClick.AddListener(() => ui.ShowArtifact(true));
        }

        if (outpostButton != null)
        {
            outpostButton.onClick.AddListener(() => ui.ShowOutpost(true));
        }
    }

    // 늘 켜진 줄이라 끝날 때만 푼다 (Unity 메시지)
    private void OnDestroy()
    {
        if (_data == null || _auction == null)
        {
            return;
        }

        _data.BuildingsChanged         -= RedrawNotice;
        _data.TraitsChanged            -= RedrawNotice;
        _data.CurrencyChanged          -= RedrawNotice;
        _data.InventoryChanged         -= RedrawNotice;
        _data.AccountLevelChanged      -= RedrawNotice;
        _data.EquipsChanged            -= RedrawNotice;
        _data.InventoryFullChanged     -= RedrawNotice;
        _data.ArtifactsChanged         -= RedrawNotice;
        _auction.HistoryBadgeChanged   -= RedrawNotice;
    }

    #region 빨간 점

    // 빨간 점 넷을 다시 그린다 (구독 · Start). 비워 둔 점은 건너뛴다.
    // ※ 한 번에 다 본다 — 이벤트마다 어느 점이 바뀌는지 가려 두면 한쪽 구독이 빠졌을 때 조용히 멈춘다. 판정이 다 가볍다.
    private void RedrawNotice()
    {
        if (_data == null || _auction == null)
        {
            return;
        }

        SetDot(outpostNoticeDot,   _data.HasVillageNotice());
        bool full = _data.IsInventoryFull;

        SetDot(inventoryNoticeDot, !full && HasPendingEnchant());
        if (inventoryWarningDot != null)
        {
            inventoryWarningDot.Show(full, () => WarningDotView.Content(Lang.Get("UI.Widget.AlertInventoryFull")));
        }
        SetDot(auctionNoticeDot,   _auction.HistoryUnseenCount > 0);
        SetDot(artifactNoticeDot,  HasUnequippedArtifact());
    }

    private static void SetDot(GameObject? dot, bool on)
    {
        if (dot != null && dot.activeSelf != on)
        {
            dot.SetActive(on);
        }
    }

    // 상급 큐브 결과를 고르기 전 장비가 있는가 — 인벤토리 [장비] 탭 점과 같은 판정
    private bool HasPendingEnchant()
    {
        foreach (EquipInfo equip in _data!.Equips)
        {
            if (equip.PendingEnchantGrade > 0)
            {
                return true;
            }
        }

        return false;
    }

    // 도감에 안 넣은 번호가 아티팩트 칸에 있는가
    private bool HasUnequippedArtifact()
    {
        foreach (ItemInfo stack in _data!.ArtifactStacks)
        {
            if (stack.Count > 0 && !_data.IsArtifactEquipped(stack.ItemId))
            {
                return true;
            }
        }

        return false;
    }

    #endregion
}

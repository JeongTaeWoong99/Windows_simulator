using System;
using System.Collections.Generic;
using UnityEngine;

// 위젯 알림 줄 — 3열을 다 접고 위젯만 띄워 둔 동안, 꼭 알아야 하는 일을 위젯 바로 위(위 칸이면 아래)에 띠로 쌓는다 (T-145 · B안).
// 목업: 'GameDesign/design/ui/위젯특수알림목업.html'
//
// ■ 레드닷과 갈래가 다르다
//   레드닷은 "열면 볼 것이 있다"(조용히 — 하단 메뉴 · 마을). 이 줄은 **"놔두면 손해 / 걸어 둔 결과가 나왔다"**만 부른다.
//   - 인벤토리 가득 참(경고) — 채취물이 버려지는 중. 'PlayerDataModel.IsInventoryFull'(수확 응답의 'InventoryFull' · 서버 T-157).
//   - 빈 작업슬롯(경고) — 열린 칸이 비었는데 쉬는 캐릭터가 있다. 그 칸은 아무것도 안 번다.
//   - 큐브 자동(진행 → 결과) — [내리기]로 큐브 창을 숨긴 동안만('EquipEnchantPresenter.IsMinimized').
//   같은 사유는 띠 하나다 — 풀리면 띠가 사라지고, 다시 생기면 다시 뜬다. 소리 · 흔들기는 없다(P1 — 주의를 뺏지 않는다).
//
// ■ 띠를 누르면 3열이 다 열린다 (2026-11-26 사용자 결정) — 'UIManager.OpenAllColumns'
//   가득 참이면 인벤토리 **자원 탭**(가득 차는 것은 자원뿐), 큐브는 'UIManager.Reopened'로 큐브 창이 그 상태 그대로 다시 뜬다.
//
// ■ 열이 하나라도 열려 있으면 띠를 숨긴다 — 그때는 **경고 점**이 그 자리에서 알린다 (2026-11-27 사용자)
//   열린 화면에는 띠를 둘 빈 자리가 위젯 높이(87px)뿐이라 띠가 쌓이면 옆 열을 덮는다.
//   그래서 열려 있는 동안은 사유가 생긴 곳에 경고 점('WarningDotView' — 올리면 툴팁)을 띄운다:
//   가득 참 → 하단 [인벤토리] 버튼('MenuPresenter') · 빈 칸 → 그 작업슬롯 칸('WorkStationListPresenter').
//   큐브 자동은 열면 큐브 창이 그대로 다시 뜨므로 점이 없다.
//   열림은 'UIManager.IsOpen'을 Update에서 본다 — 여닫기 경로가 여럿(하단 메뉴 · 경매장 · 위젯 버튼)이라 bool 하나를 보는 쪽이 새는 곳이 없다.
//
// ⚠️ '#Widget Canvas' 안 · 레이아웃 밖(앵커로만)에 둔다. 위젯 높이(87px)를 늘리면 3열 높이 계산이 어긋난다
//    ('WidgetPositionLayout'). 띠는 캔버스 사각형 밖으로 그려지지만, 레이캐스트는 그래픽 사각형으로 받으므로 눌린다.
//    FPS 글씨는 위젯과 다른 열 구석으로 비킨다('FpsTextPresenter') — 띠와 만나지 않는다.
public class WidgetAlertPresenter : MonoBehaviour
{
    // 띠 종류 — 위젯에 가까운 쪽부터 이 순서로 쌓인다(경고가 결과보다 먼저 보인다).
    private enum Alert
    {
        InventoryFull,
        IdleSlot,
        CubeAuto,
    }

    [CenterHeader("참조")]
    [SerializeField, Tooltip("띠 견본 — 꺼 둔 채 둔다. 종류마다 하나씩 복제해 이 오브젝트 아래에 쌓는다")]
    private WidgetAlertChipView chipTemplate = null!;

    [SerializeField, Tooltip("위젯 위치 — 위젯이 위 칸이면 띠를 아래로 쌓는다")]
    private WidgetPositionLayout positionLayout = null!;

    [SerializeField, Tooltip("큐브 창 — 내려 둔 자동의 진행 · 결과")]
    private EquipEnchantPresenter equipEnchant = null!;

    [SerializeField, Tooltip("인벤토리 탭 줄 — 가득 참 띠를 누르면 자원 탭으로")]
    private InventoryTabPresenter inventoryTabs = null!;

    private readonly Dictionary<Alert, WidgetAlertChipView> _chips = new Dictionary<Alert, WidgetAlertChipView>();

    private PlayerDataModel? _data;
    private UIManager?       _ui;
    private RectTransform    _rect = null!;
    private bool             _isShown;   // 띠가 보이는가 — 열이 다 닫혔을 때만
    private bool             _isLower;   // 위젯이 아래 칸인가 — 띠를 위로 쌓는다
    private WidgetPosition?  _placedFor; // 마지막으로 자리를 잡은 위젯 위치

    // 참조 확보 → 구독 → 초기화 (클라 공통 규약)
    // ※ 서비스 조회는 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다.
    // ※ 위젯 캔버스는 상주라 꺼지지 않는다 — 구독은 Start/OnDestroy에 건다.
    private void Start()
    {
        this.RequireRef(chipTemplate,   nameof(chipTemplate));
        this.RequireRef(positionLayout, nameof(positionLayout));
        this.RequireRef(equipEnchant,   nameof(equipEnchant));
        this.RequireRef(inventoryTabs,  nameof(inventoryTabs));

        _data = Services.Get<PlayerDataModel>();
        _ui   = Services.Get<UIManager>();
        _rect = (RectTransform)transform;

        chipTemplate.gameObject.SetActive(false);

        _data.InventoryFullChanged     += Redraw;
        _data.WorkStationSlotsChanged  += Redraw;
        _data.CharactersChanged        += Redraw;
        equipEnchant.AutoStatusChanged += Redraw;
        Lang.Changed                   += Redraw;

        _isShown = !_ui.IsOpen;
        PlaceStack();
    }

    // 구독 해제 (Unity 메시지)
    private void OnDestroy()
    {
        if (_data == null)
        {
            return;
        }

        _data.InventoryFullChanged     -= Redraw;
        _data.WorkStationSlotsChanged  -= Redraw;
        _data.CharactersChanged        -= Redraw;
        equipEnchant.AutoStatusChanged -= Redraw;
        Lang.Changed                   -= Redraw;
    }

    // 열림 · 위젯 위치가 바뀌면 띠를 보이거나 숨기고 쌓는 쪽을 다시 잡는다 (Unity 메시지)
    // ※ 자기 오브젝트는 끄지 않는다 — 꺼지면 Update가 안 돌아 다시 켜지지 못한다. 띠만 끈다.
    private void Update()
    {
        if (_ui == null)
        {
            return;
        }

        bool show = !_ui.IsOpen;

        if (show != _isShown || _placedFor != positionLayout.Position)
        {
            _isShown = show;
            PlaceStack();
        }
    }

    #region 그리기

    // 위젯이 아래 칸이면 위젯 위쪽 가장자리에서 위로, 위 칸이면 아래쪽 가장자리에서 아래로 쌓는다 (Start · Update)
    private void PlaceStack()
    {
        _placedFor = positionLayout.Position;
        _isLower   = (int)positionLayout.Position / 3 == 1;

        _rect.anchorMin        = new Vector2(0f, _isLower ? 1f : 0f);
        _rect.anchorMax        = new Vector2(1f, _isLower ? 1f : 0f);
        _rect.pivot            = new Vector2(0.5f, _isLower ? 0f : 1f);
        _rect.sizeDelta        = new Vector2(0f, _rect.sizeDelta.y);
        _rect.anchoredPosition = Vector2.zero;

        Redraw();
    }

    // 지금 사유대로 띠를 켜고 끄고 글씨를 쓴다 (사건 구독 · 언어 바뀜 · 열림)
    private void Redraw()
    {
        if (_data == null)
        {
            return;
        }

        Draw(Alert.InventoryFull, _data.IsInventoryFull, () => Lang.Get("UI.Widget.AlertInventoryFull"), UIThemeRole.Negative);

        int idle = CountIdleSlots();

        Draw(Alert.IdleSlot, idle > 0, () => Lang.Format("UI.Widget.AlertIdleSlot", idle), UIThemeRole.Negative);

        bool cube = equipEnchant.IsMinimized && (equipEnchant.IsAutoRunning || equipEnchant.HasAutoResult);

        Draw(Alert.CubeAuto, cube, DescribeCube, CubeRole());
        PlaceOrder();
    }

    // 띠 하나를 켜거나 끈다 — 처음 켤 때 견본을 복제한다. 글씨는 켤 때만 만든다.
    private void Draw(Alert alert, bool on, Func<string> message, UIThemeRole role)
    {
        on &= _isShown;

        if (!_chips.TryGetValue(alert, out WidgetAlertChipView chip))
        {
            if (!on)
            {
                return;
            }

            chip          = Instantiate(chipTemplate, transform);
            chip.name     = $"Alert Chip {alert}";
            chip.Clicked += () => OnChipClicked(alert);

            _chips.Add(alert, chip);
        }

        chip.gameObject.SetActive(on);

        if (on)
        {
            chip.Bind(message(), role);
        }
    }

    // 위젯에 가까운 쪽부터 경고 → 결과 순으로 (Redraw).
    // 세로 레이아웃은 위에서 아래로 놓으므로 — 위젯이 아래 칸이면(띠가 위로 쌓이면) 뒤 종류부터 뒤에 붙여 경고가 맨 아래(위젯 쪽)에 온다.
    // ※ 견본은 맨 앞에 꺼진 채 남는다 — 꺼진 자식은 레이아웃이 건너뛴다.
    private void PlaceOrder()
    {
        int last = (int)Alert.CubeAuto;

        for (int i = 0; i <= last; i++)
        {
            var alert = (Alert)(_isLower ? last - i : i);

            if (_chips.TryGetValue(alert, out WidgetAlertChipView chip))
            {
                chip.transform.SetAsLastSibling();
            }
        }
    }

    // 비어 있는 열린 칸 수 — 쉬는 캐릭터가 없으면 0(넣을 사람이 없으면 재촉하지 않는다).
    // ※ 'WorkStationSlots'에는 열린 칸만 실려 온다 — 잠긴 칸은 세지 않는다.
    // ※ 작업슬롯 칸의 경고 점('WorkStationListPresenter.RefreshFrameNotices')과 같은 기준이다.
    private int CountIdleSlots()
    {
        int empty = 0;

        foreach (var slot in _data!.WorkStationSlots)
        {
            if (!WorkStationProgress.IsAssigned(slot))
            {
                empty++;
            }
        }

        if (empty == 0)
        {
            return 0;
        }

        foreach (var character in _data.Characters)
        {
            if (_data.FindSlotIndexOf(character.CharacterId) < 0)
            {
                return empty;
            }
        }

        return 0;
    }

    // 큐브 띠 글씨 — 도는 중이면 진행, 끝났으면 끝난 이유
    private string DescribeCube()
    {
        if (equipEnchant.IsAutoRunning)
        {
            return Lang.Format("UI.Widget.AlertCubeRunning", $"{equipEnchant.AutoUsed:N0}", $"{equipEnchant.AutoCap:N0}", equipEnchant.AutoGoalText);
        }

        return Lang.Format("UI.Widget.AlertCubeDone", equipEnchant.AutoEndReason, $"{equipEnchant.AutoUsed:N0}");
    }

    // 큐브 띠 색 — 도는 중 노랑 · 목표 달성 초록 · 그 밖의 끝(상한 · 큐브 소진 · 거절) 빨강
    private UIThemeRole CubeRole()
    {
        if (equipEnchant.IsAutoRunning)
        {
            return UIThemeRole.Highlight;
        }

        return equipEnchant.AutoReached ? UIThemeRole.Positive : UIThemeRole.Negative;
    }

    #endregion

    #region 입력

    // 띠를 눌렀다 — 3열을 다 열고 사유에 맞는 화면을 보인다 (띠 Clicked 구독)
    private void OnChipClicked(Alert alert)
    {
        _ui!.OpenAllColumns(); // 큐브는 여기서 'Reopened'로 큐브 창이 다시 뜬다

        // 자원 탭 — 큐브 창이 장비 탭에 떠 있으면 바꾸지 않는다(탭을 떠나면 자동이 멈춘다)
        if (alert == Alert.InventoryFull && !equipEnchant.IsOpen)
        {
            inventoryTabs.RequestTab(InventoryTab.Resource);
        }
    }

    #endregion
}

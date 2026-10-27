using UnityEngine;

// 길드 열 — 뽑기 · 창고 2탭이 들어간다('GuildTabPresenter'). 탭마다 화면이 따로다('GachaPresenter' · 'WarehousePresenter').
//
// ※ 옛 이름은 거래 열(Market)이다. 경매장이 화면 전체를 쓰는 '#Auction Canvas'로 빠지며(T-147) 뽑기·창고만 남아
//   거점(Hub)으로 바꿨고, 거점이 지도 화면 이름이 되며(T-183) 길드(Guild)로 다시 바꿨다(T-182) — 경매장은 'AuctionCanvasView'.
//
// ⚠️ 여기 '창고'는 인벤토리가 아니다 — 인벤토리(옛 이름 창고/Storage)는 얻은 것이 바로 들어오는 왼쪽 열이고('InventoryCanvasView'),
//   창고는 두고두고 쓸 것을 맡겨 두는 곳이다.
public class GuildCanvasView : MonoBehaviour
{
    // 이 열을 열고 닫는다 (UIManager가 호출).
    // 닫힘은 연출이 끝난 뒤에 꺼진다('PanelTransition') — 열림 여부는 'IsShown'으로 읽는다.
    public void Show(bool on) => PanelTransition.SetActive(gameObject, on);

    // 열려 있는가 — 닫히는 중이면 false
    public bool IsShown => PanelTransition.IsShown(gameObject);
}

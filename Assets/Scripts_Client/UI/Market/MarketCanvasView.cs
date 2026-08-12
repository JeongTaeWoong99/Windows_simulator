using UnityEngine;

/// <summary>
/// 거래 열 — 거래소 · 상점 2탭이 들어간다. 결과물의 출구(거래소)와 입구(상점)다.
/// 아직 내용이 없어 자리만 잡고 있다.
///
/// ⚠️ 거래소·상점은 기획 수치가 전부 미정이다. 골드 상점이라는 방향만 잡혀 있고
/// 수수료·한도·품목·가격은 정해지지 않았다 — 임의로 정하지 않는다.
/// → GameDesign/design/trade/README.md 3.2
/// </summary>
public class MarketCanvasView : MonoBehaviour
{
    /// <summary>이 열을 열고 닫는다 (UIManager가 호출).</summary>
    public void Show(bool on)
    {
        gameObject.SetActive(on);
    }
}

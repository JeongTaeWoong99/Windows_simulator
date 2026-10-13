using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 시세 그래프의 평균선 — 점들을 이은 꺾은선 하나를 그린다. 'AuctionPriceChartView'가 점을 넘긴다.
//
// uGUI에는 선 그래픽이 없어 사각형(선분마다 두께만큼)을 직접 만든다. 점은 이 그래픽 사각형 안의 0~1 좌표다.
public class AuctionPriceLineGraphic : MaskableGraphic
{
    [SerializeField, Tooltip("선 두께(px)")]
    private float thickness = 3f;

    private readonly List<Vector2> _points = new List<Vector2>();

    // 이을 점들 — 왼쪽부터, 0~1 좌표. 둘보다 적으면 아무것도 그리지 않는다.
    public void SetPoints(IReadOnlyList<Vector2> points)
    {
        _points.Clear();
        _points.AddRange(points);
        SetVerticesDirty();
    }

    // 선분마다 두께만큼 벌린 사각형을 만든다 (Unity 콜백)
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (_points.Count < 2)
        {
            return;
        }

        Rect  rect = rectTransform.rect;
        float half = thickness * 0.5f;

        for (int i = 0; i < _points.Count - 1; i++)
        {
            Vector2 a = ToLocal(rect, _points[i]);
            Vector2 b = ToLocal(rect, _points[i + 1]);

            Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * half;
            int     start  = vh.currentVertCount;

            vh.AddVert(a - normal, color, Vector2.zero);
            vh.AddVert(a + normal, color, Vector2.zero);
            vh.AddVert(b + normal, color, Vector2.zero);
            vh.AddVert(b - normal, color, Vector2.zero);

            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }

    private static Vector2 ToLocal(Rect rect, Vector2 normalized)
        => new Vector2(rect.xMin + rect.width * normalized.x, rect.yMin + rect.height * normalized.y);
}

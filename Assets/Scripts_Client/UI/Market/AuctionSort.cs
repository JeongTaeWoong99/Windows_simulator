using System;
using System.Collections.Generic;

// 목록의 가격 정렬 — 낮은 가격순 ↔ 높은 가격순을 버튼 하나로 뒤집는다.
//
// ■ 로컬 정렬이다
//   서버가 준 목록은 그대로 두고 **화면에 그릴 사본만** 정렬한다. 받는 즉시 지금 방향으로 줄 세운다.
//   모델의 원본 순서를 바꾸면 안 된다 — 장비 검색의 [더 보기]는 원본의 마지막 매물을 커서로 쓴다.
//   ⚠️ 장비 검색은 **받은 페이지까지만** 정렬된다. 서버는 낮은 가격순으로 20개씩 주므로 높은 가격순은
//      [더 보기]를 끝까지 받아야 전체에서의 순서가 된다.
// ■ 정렬 상태는 각 Presenter의 화면 상태다(모델에 두지 않는다). 탭을 다시 열어도 유지되고, 게임을 껐다 켜면 낮은 가격순이다.
public enum AuctionSortOrder
{
    Ascending,  // 낮은 가격순 (기본)
    Descending, // 높은 가격순
}

public static class AuctionSort
{
    // 다음 정렬 — 누를 때마다 방향을 뒤집는다.
    public static AuctionSortOrder Next(AuctionSortOrder order)
        => order == AuctionSortOrder.Ascending ? AuctionSortOrder.Descending : AuctionSortOrder.Ascending;

    // 정렬 버튼 문구 — 지금 무엇으로 보고 있는지를 말한다.
    public static string GetLabel(AuctionSortOrder order)
        => order == AuctionSortOrder.Ascending ? "낮은 가격순 ▲" : "높은 가격순 ▼";

    // 'source'를 'order'대로 정렬한 새 목록을 만든다. 원본은 건드리지 않는다.
    //   price : 정렬 기준 가격. 0 이하(매물 없음)는 방향과 상관없이 맨 뒤로 보낸다
    //
    // ※ 같은 가격끼리는 받은 순서를 지킨다(안정 정렬) — List.Sort는 불안정해서 같은 값의 줄이 누를 때마다 뒤섞인다.
    public static List<T> Apply<T>(IReadOnlyList<T> source, AuctionSortOrder order, Func<T, long> price)
    {
        var indexed = new List<(T item, int index)>(source.Count);

        for (int i = 0; i < source.Count; i++)
        {
            indexed.Add((source[i], i));
        }

        int sign = order == AuctionSortOrder.Ascending ? 1 : -1;

        indexed.Sort((a, b) =>
        {
            long priceA = price(a.item);
            long priceB = price(b.item);

            bool hasA = priceA > 0L;
            bool hasB = priceB > 0L;

            if (hasA != hasB)
            {
                return hasA ? -1 : 1;
            }

            int byPrice = priceA.CompareTo(priceB) * sign;

            return byPrice != 0 ? byPrice : a.index.CompareTo(b.index);
        });

        var result = new List<T>(indexed.Count);

        foreach ((T item, int _) in indexed)
        {
            result.Add(item);
        }

        return result;
    }
}

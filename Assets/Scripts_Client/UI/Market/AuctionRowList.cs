using System;
using System.Collections.Generic;
using UnityEngine;

// 'AuctionRowView' 줄을 만들어 두고 다시 쓰는 목록 — 경매장 탭의 네 Presenter가 하나씩 갖는다.
//
// 줄 풀은 범용 'UIRowList<T>'(Common/object-pool)가 맡고, 여기는 경매 줄의 그리기(Bind)만 얹는다.
// 줄은 파괴하지 않는다 — 남는 줄은 꺼 두었다가 다음에 다시 켠다.
public class AuctionRowList
{
    private readonly UIRowList<AuctionRowView> _rows;

    //   prefab   : 줄 프리팹
    //   parent   : 줄이 쌓이는 Content (VLG + ContentSizeFitter)
    //   onAction : 어느 줄의 버튼이든 눌리면 그 줄의 키로 부른다
    public AuctionRowList(AuctionRowView prefab, Transform parent, Action<long> onAction)
    {
        // 줄은 재사용하므로 만들 때 한 번만 구독한다 — 다시 걸면 중복으로 쌓인다.
        _rows = new UIRowList<AuctionRowView>(prefab, parent, row => row.ActionClicked += onAction);
    }

    // 목록을 'contents'로 다시 그린다. 모자란 줄은 만들고 남는 줄은 끈다.
    public void Show(IReadOnlyList<AuctionRowContent> contents)
    {
        for (int i = 0; i < contents.Count; i++)
        {
            _rows.Get(i).Bind(contents[i]);
        }

        _rows.HideFrom(contents.Count);
    }
}

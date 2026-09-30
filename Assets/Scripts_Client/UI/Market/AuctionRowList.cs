using System;
using System.Collections.Generic;
using UnityEngine;

// 'AuctionRowView' 줄을 만들어 두고 다시 쓰는 목록 — 경매장 탭의 네 Presenter가 하나씩 갖는다.
//
// 네 목록이 줄 풀 코드를 각자 들면 같은 20줄이 네 번 복사된다('MailPresenter'의 줄 풀과 같은 모양).
// 줄은 파괴하지 않는다 — 남는 줄은 꺼 두었다가 다음에 다시 켠다.
public class AuctionRowList
{
    private readonly List<AuctionRowView> _rows = new List<AuctionRowView>();

    private readonly AuctionRowView _prefab;
    private readonly Transform      _parent;
    private readonly Action<long>   _onAction;

    //   prefab   : 줄 프리팹
    //   parent   : 줄이 쌓이는 Content (VLG + ContentSizeFitter)
    //   onAction : 어느 줄의 버튼이든 눌리면 그 줄의 키로 부른다
    public AuctionRowList(AuctionRowView prefab, Transform parent, Action<long> onAction)
    {
        _prefab   = prefab;
        _parent   = parent;
        _onAction = onAction;
    }

    // 목록을 'contents'로 다시 그린다. 모자란 줄은 만들고 남는 줄은 끈다.
    public void Show(IReadOnlyList<AuctionRowContent> contents)
    {
        for (int i = 0; i < contents.Count; i++)
        {
            AuctionRowView row = GetOrCreateRow(i);

            row.gameObject.SetActive(true);
            row.Bind(contents[i]);
        }

        for (int i = contents.Count; i < _rows.Count; i++)
        {
            _rows[i].gameObject.SetActive(false);
        }
    }

    // 'index'번째 줄을 돌려준다. 아직 없으면 그때 만든다 (Show에서 호출).
    private AuctionRowView GetOrCreateRow(int index)
    {
        if (index < _rows.Count)
        {
            return _rows[index];
        }

        AuctionRowView row = UnityEngine.Object.Instantiate(_prefab, _parent);

        // 줄은 재사용하므로 만들 때 한 번만 구독한다 — 다시 걸면 중복으로 쌓인다.
        row.ActionClicked += _onAction;

        _rows.Add(row);

        return row;
    }
}

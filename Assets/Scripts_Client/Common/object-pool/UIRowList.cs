using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

// 레이아웃 그룹 아래의 줄을 만들어 두고 다시 쓰는 목록 — 목록 UI의 "제자리 풀".
//
// ■ 'PrefabPool<T>'와 무엇이 다른가
//   줄은 부모를 옮기지 않는다. 한 번 만든 줄은 끝까지 같은 부모 아래 같은 순서에 있고,
//   남는 줄은 끄기만 한다. 그래서
//   - 레이아웃 그룹이 줄을 넣고 뺄 때마다 양쪽 부모를 다시 배치하지 않는다
//   - i번째 줄이 늘 i번째 형제다 — 줄 순서·툴팁 연결이 어긋나지 않는다
//   목록은 대개 "최대 길이만큼만" 자라므로 줄을 파괴할 일이 없다.
//
// ■ 쓰는 법
//   _rows = new UIRowList<RowView>(rowPrefab, rowParent, row => row.Clicked += OnRowClicked, row => row.Clear());
//   for (i …) _rows.Get(i).Bind(items[i]);
//   _rows.HideFrom(items.Count);
//
//   onCreated : 만들 때 한 번 — 이벤트 구독은 여기서만 한다 (다시 걸면 중복으로 쌓인다)
//   onHide    : 끄기 직전 — 이전 내용을 비운다 (다음에 켤 때 옛 값이 한 프레임 비치지 않게)
public sealed class UIRowList<T> where T : Component
{
    private readonly List<T>    _rows = new List<T>();
    private readonly T          _prefab;
    private readonly Transform  _parent;
    private readonly Action<T>? _onCreated;
    private readonly Action<T>? _onHide;

    // 지금까지 만든 줄 수 (꺼진 것 포함)
    public int Count => _rows.Count;

    // 만든 줄 전부 (꺼진 것 포함) — 잠금 같은 일괄 처리용
    public IReadOnlyList<T> All => _rows;

    public UIRowList(T prefab, Transform parent, Action<T>? onCreated = null, Action<T>? onHide = null)
    {
        _prefab    = prefab;
        _parent    = parent;
        _onCreated = onCreated;
        _onHide    = onHide;
    }

    // 'index'번째 줄을 켜서 돌려준다. 모자라면 그때 만든다.
    public T Get(int index)
    {
        while (_rows.Count <= index)
        {
            T created = Object.Instantiate(_prefab, _parent);

            _onCreated?.Invoke(created);
            _rows.Add(created);
        }

        T row = _rows[index];

        row.gameObject.SetActive(true);

        return row;
    }

    // 'startIndex'부터 뒤의 줄을 비우고 끈다. 'HideFrom(0)'은 전부 끈다.
    public void HideFrom(int startIndex)
    {
        for (int i = Math.Max(0, startIndex); i < _rows.Count; i++)
        {
            T row = _rows[i];

            _onHide?.Invoke(row);
            row.gameObject.SetActive(false);
        }
    }
}

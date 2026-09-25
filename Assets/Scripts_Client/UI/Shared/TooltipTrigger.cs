using System;
using UnityEngine;

// 마우스를 올리면 툴팁을 띄울 대상에 붙는다. "무엇을 띄울지"만 갖고, 띄우는 일은 'TooltipPresenter'가 한다.
//
// ■ 내용을 주는 길이 둘이다
// - 고정 문구 — 인스펙터 'text'. 아이콘 버튼의 이름("세팅")처럼 바뀌지 않는 것. 코드가 필요 없다.
// - 동적 내용 — Presenter가 'SetProvider'로 만드는 함수를 넘긴다. **띄우는 순간에 부른다** —
//   미리 만들어 두면 산업·해금이 바뀔 때마다 다시 넣어 줘야 한다.
// 둘 다 있으면 동적 내용이 이긴다.
//
// ■ 이 오브젝트나 자식이 레이캐스트를 받아야 뜬다
// 툴팁은 커서 밑 레이캐스트의 맨 위 결과에서 이 컴포넌트를 부모 방향으로 찾는다('TooltipPresenter.FindTrigger').
// 'raycastTarget'이 꺼진 그림 위에서는 뜨지 않는다. 버튼은 바탕 Image가 받으므로 따로 할 일이 없다.
public class TooltipTrigger : MonoBehaviour
{
    [SerializeField, TextArea, Tooltip("고정 문구. 코드가 SetProvider로 내용을 넘기면 그쪽이 이긴다")]
    private string text = "";

    private Func<TooltipContent?>? _provider;

    // 띄울 내용을 만드는 함수를 넘긴다 (Presenter가 배선할 때 호출). null을 돌려주면 툴팁이 뜨지 않는다.
    public void SetProvider(Func<TooltipContent?>? provider)
    {
        _provider = provider;
    }

    // 지금 띄울 내용 ('TooltipPresenter'가 띄우는 순간에 호출). 없으면 null.
    public TooltipContent? Build()
    {
        if (_provider != null)
        {
            return _provider();
        }

        return text.Length > 0 ? new TooltipContent(text) : null;
    }
}

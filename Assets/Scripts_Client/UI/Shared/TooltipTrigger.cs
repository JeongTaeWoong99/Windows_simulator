using System;
using TMPro;
using UnityEngine;

// 마우스를 올리면 툴팁을 띄울 대상에 붙는다. "무엇을 띄울지"만 갖고, 띄우는 일은 'TooltipPresenter'가 한다.
//
// ■ 내용을 주는 길이 둘이다
// - 고정 문구 — 인스펙터 'text'. 아이콘 버튼의 이름("세팅")처럼 바뀌지 않는 것. 코드가 필요 없다.
// - 동적 내용 — Presenter가 'SetProvider'로 만드는 함수를 넘긴다. **띄우는 순간에 부른다** —
//   미리 만들어 두면 산업·해금이 바뀔 때마다 다시 넣어 줘야 한다.
// - 숨긴 글씨 — 인스펙터 'label'. 글씨를 끄고 아이콘만 보이는 버튼(상태줄 · 메인 메뉴)은 그 글씨를 띄운다.
//   'LocalizedText'가 꺼진 글씨에도 언어를 바꿔 쓰므로 툴팁도 따라 바뀐다.
// 여럿이면 동적 내용 → 고정 문구 → 숨긴 글씨 순으로 이긴다.
//
// ■ 이 오브젝트나 자식이 레이캐스트를 받아야 뜬다
// 툴팁은 커서 밑 레이캐스트의 맨 위 결과에서 이 컴포넌트를 부모 방향으로 찾는다('TooltipPresenter.FindTrigger').
// 'raycastTarget'이 꺼진 그림 위에서는 뜨지 않는다. 버튼은 바탕 Image가 받으므로 따로 할 일이 없다.
public class TooltipTrigger : MonoBehaviour
{
    [SerializeField, TextArea, Tooltip("고정 문구. 코드가 SetProvider로 내용을 넘기면 그쪽이 이긴다")]
    private string text = "";

    [SerializeField, Tooltip("아이콘만 보이는 버튼의 숨긴 글씨 — 고정 문구가 비었을 때 이 글씨를 띄운다")]
    private TMP_Text? label;

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

        if (text.Length > 0)
        {
            return new TooltipContent(text);
        }

        string shown = LabelText();

        return shown.Length > 0 ? new TooltipContent(shown) : null;
    }

    // 숨긴 글씨 — 꺼진 오브젝트라 'LocalizedText'가 돌지 않는다. 키가 있으면 키로 직접 읽는다(씬에 남은 글씨는 낡았을 수 있다)
    private string LabelText()
    {
        if (label == null)
        {
            return "";
        }

        return label.TryGetComponent(out LocalizedText localized) && localized.Key.Length > 0 ? Lang.Get(localized.Key) : label.text;
    }
}

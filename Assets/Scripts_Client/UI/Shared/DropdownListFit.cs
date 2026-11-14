using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 드롭다운 목록 너비를 글자에 맞춘다 — 최소는 머리 너비, 넘치면 가장 긴 항목까지 (2026-11-14).
// 'TMP_Dropdown'에 붙인다. 'CheckDropdownView'는 목록을 직접 만들므로 아래 정적 메서드만 빌려 쓴다.
//
// ■ 'TMP_Dropdown'은 펼칠 때마다 템플릿을 복제해 'Dropdown List'를 새로 만든다 — 그래서 펼쳐진 것을 보고 그때 잰다.
//   'Show'를 가로챌 이벤트가 없어 LateUpdate에서 살핀다(펼친 프레임 안에 맞춰져 튀지 않는다).
// ■ 글자 밖 여백(체크 자리 · 틀 안쪽 · 스크롤바)은 펼쳐진 목록에서 '목록 너비 − 글자 칸 너비'로 잰다 — 템플릿마다 달라 상수로 두지 않는다.
[RequireComponent(typeof(TMP_Dropdown))]
public class DropdownListFit : MonoBehaviour
{
    // 가장 긴 글자가 딱 맞으면 소수점 차이로 줄임표가 붙는다 — 조금 더 둔다.
    private const float Slack = 6f;

    private TMP_Dropdown   _dropdown = null!;
    private RectTransform? _fitted;

    // 자기 컴포넌트만 잡는다 (Unity 메시지)
    private void Awake()
    {
        _dropdown = GetComponent<TMP_Dropdown>();
    }

    // 새로 펼쳐진 목록이 있으면 한 번 맞춘다 (Unity 메시지)
    private void LateUpdate()
    {
        if (!_dropdown.IsExpanded)
        {
            _fitted = null;

            return;
        }

        if (transform.Find("Dropdown List") is not RectTransform list || list == _fitted)
        {
            return;
        }

        _fitted = list;

        var labels = new List<TMP_Text>();

        foreach (TMP_Text text in list.GetComponentsInChildren<TMP_Text>())
        {
            if (text.name == "Item Label")
            {
                labels.Add(text);
            }
        }

        if (labels.Count == 0)
        {
            return;
        }

        float extra = list.rect.width - labels[0].rectTransform.rect.width;

        SetWidth(list, ((RectTransform)transform).rect.width, Widest(labels) + extra);
    }

    // 글자들 중 가장 긴 것의 너비 (자기 글꼴·크기 그대로 잰다)
    public static float Widest(IEnumerable<TMP_Text> labels)
    {
        float widest = 0f;

        foreach (TMP_Text label in labels)
        {
            widest = Mathf.Max(widest, label.GetPreferredValues(label.text).x);
        }

        return widest + Slack;
    }

    // 목록 너비를 머리 왼쪽 끝에 맞춰 정한다 — 머리보다 좁아지지는 않는다.
    //   list   : 머리의 자식이고 가로로 머리에 늘어붙은(앵커 0~1) 목록
    public static void SetWidth(RectTransform list, float headWidth, float wanted)
    {
        float width = Mathf.Max(headWidth, wanted);

        list.anchorMin = new Vector2(0f, list.anchorMin.y);
        list.anchorMax = new Vector2(1f, list.anchorMax.y);
        list.offsetMin = new Vector2(0f, list.offsetMin.y);
        list.offsetMax = new Vector2(width - headWidth, list.offsetMax.y);
    }
}

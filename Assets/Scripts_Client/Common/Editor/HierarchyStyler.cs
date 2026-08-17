using UnityEditor;
using UnityEngine;

// ── HierarchyStyler : HierarchyPalette 의 규칙대로 하이어라키를 실제로 그리는 쪽 ──
//   [InitializeOnLoad] → 에디터가 스크립트를 로드할 때 정적 생성자가 1회 자동 호출된다.
//   EditorApplication.hierarchyWindowItemOnGUI → 하이어라키의 "한 줄"을 그릴 때마다 불린다.
//                                                (오브젝트 하나당 매 리페인트 1회)
//
// ■ 하는 일은 두 줄이다
//   [1] 줄 전체를 배경색으로 덮는다   EditorGUI.DrawRect
//   [2] 접두 문자를 뗀 이름을 다시 그린다  EditorGUI.LabelField
//   Unity 가 이미 그려 둔 줄 위에 덮어쓰는 방식이라, Unity 가 표현하던 것도 함께 가려진다.
//   그래서 "비활성은 흐리게"를 직접 되살려야 한다 (아래 DisabledAlpha).
[InitializeOnLoad]
public static class HierarchyStyler
{
    // 비활성 오브젝트를 흐리게 만드는 비율.
    //
    // ★ 이게 없으면 "꺼진 것이 꺼져 보이지 않는다."
    //   Unity 는 비활성 오브젝트의 이름을 흐리게 그려 알려 주는데, 우리가 그 위를 불투명한
    //   배경색으로 덮으면 그 신호가 통째로 사라진다. 색칠한 줄만 활성·비활성 구분이 안 되는
    //   상태가 되어, 꺼 둔 화면을 켜 둔 줄 알고 한참 헤매게 된다.
    //   배경과 글자 알파를 함께 낮춰 Unity 의 관례와 같은 모습으로 되돌린다.
    private const float DisabledAlpha = 0.4f;

    // 찾아 둔 팔레트. 에셋을 지웠다 다시 만들면 Unity 의 가짜 null 이 되므로 매번 == null 로 본다.
    private static HierarchyPalette? _palette;

    static HierarchyStyler()
    {
        // ※ 팔레트가 없어도 일단 구독한다 — 나중에 만들면 에디터를 다시 켜지 않고 바로 먹는다.
        //   (정적 생성자 시점에는 AssetDatabase 가 아직 준비되지 않을 수 있어 조회를 미룬다)
        EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyItem;
    }

    /// <summary>프로젝트에서 팔레트를 찾는다 (처음 그릴 때 한 번). 없으면 'null'.</summary>
    private static HierarchyPalette? Palette
    {
        get
        {
            if (_palette != null)
                return _palette;

            // ※ 경로가 아니라 타입으로 찾는다 — 팔레트를 어디로 옮겨도 계속 동작한다.
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(HierarchyPalette));
            if (guids.Length == 0)
                return null;

            _palette = AssetDatabase.LoadAssetAtPath<HierarchyPalette>(AssetDatabase.GUIDToAssetPath(guids[0]));
            return _palette;
        }
    }

    // 하이어라키의 한 줄을 그린다 (Unity 가 줄마다 호출)
    private static void OnHierarchyItem(int instanceID, Rect rect)
    {
        var palette = Palette;
        if (palette == null)
            return;

#pragma warning disable CS0618 // hierarchyWindowItemOnGUI 가 주는 건 instanceID 뿐이라 이 조회를 대체할 수 없다
        var go = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
#pragma warning restore CS0618

        if (go == null)
            return;

        foreach (var rule in palette.rules)
        {
            if (string.IsNullOrEmpty(rule.prefix) || !go.name.StartsWith(rule.prefix))
                continue;

            // 부모가 꺼져 있으면 이 오브젝트도 화면에 없다 — activeSelf 가 아니라 activeInHierarchy 다.
            // Unity 의 흐리게 표시와 같은 기준이어야 눈으로 본 것과 실제가 어긋나지 않는다.
            bool visible = go.activeInHierarchy;

            Color background = rule.backgroundColor;
            Color text       = rule.textColor;

            if (!visible)
            {
                background.a *= DisabledAlpha;
                text.a       *= DisabledAlpha;
            }

            EditorGUI.DrawRect(rect, background);

            // ★ 이름을 대문자로 바꾸지 않는다.
            //   원래 이 도구는 ToUpper() 로 전부 대문자로 그렸는데, 그러면 하이어라키에 보이는 이름과
            //   씬·코드에 있는 실제 이름이 달라진다. 검색해도 안 맞고, 대문자로 단어를 끊어 읽던
            //   파스칼 표기의 경계가 뭉개진다.
            EditorGUI.LabelField(rect, go.name.Substring(rule.prefix.Length), new GUIStyle
            {
                alignment = rule.textAlignment,
                fontStyle = rule.fontStyle,
                normal    = { textColor = text },
            });

            return; // 처음 맞는 규칙 하나만 적용한다 — 겹쳐 그리면 색이 섞인다
        }
    }
}

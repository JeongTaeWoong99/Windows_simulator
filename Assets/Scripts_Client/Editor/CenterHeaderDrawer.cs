using UnityEditor;
using UnityEngine;

// CenterHeaderAttribute를 굵게 + 가운데 정렬로 그리는 데코레이터.
[CustomPropertyDrawer(typeof(CenterHeaderAttribute))]
public class CenterHeaderDrawer : DecoratorDrawer
{
    private const float TopPadding = 8f; // 위 섹션과 시각적으로 떨어뜨리는 여백

    public override void OnGUI(Rect position)
    {
        var centerHeader = (CenterHeaderAttribute)attribute;

        Rect rect = position;
        rect.yMin += TopPadding;

        var style = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
        };

        EditorGUI.LabelField(rect, centerHeader.text, style);
    }

    public override float GetHeight()
    {
        return EditorGUIUtility.singleLineHeight + TopPadding;
    }
}

using UnityEngine;

// 인스펙터에서 가운데 정렬되는 헤더. 기본 [Header]는 좌측 정렬만 지원하므로
// 별도 속성 + 전용 DecoratorDrawer(에디터)로 중앙 정렬을 강제한다.
public class CenterHeaderAttribute : PropertyAttribute
{
    public readonly string text;

    public CenterHeaderAttribute(string text)
    {
        this.text = text;
    }
}

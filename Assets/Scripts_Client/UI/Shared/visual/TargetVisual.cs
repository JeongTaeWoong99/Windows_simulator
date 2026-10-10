using System;
using UnityEngine;

// 산업 하나의 대상(왼쪽에서 다가오는 것) — 서 있는 그림 한 장 + 타격 섬광 + 레벨별 색.
//
// ■ 손으로 만들지 않는다
//   'Assets/Art/targets/<키>/'의 레시피를 에디터 도구가 가공해 채운다('CharacterVisual'과 같다).
//
// ■ 레벨은 색으로만 가른다 (임시)
//   레벨마다 그림이 생기면 'sprite'를 레벨별 배열로 바꾼다. 지금은 같은 그림에 'Image.color'를 곱한다.
//
// ■ 타격 섬광
//   곱하기 색으로는 하얗게 만들 수 없어서, 같은 모양의 흰 실루엣('flash')을 위에 겹쳐 알파를 줄인다.
//
// ■ 크기 배율은 사람 몫이다 (2026-10-10)
//   'scale'은 굽기가 건드리지 않는다('ArtBaker'는 그림·레벨 색만 쓴다). 발(아래 가운데)을 땅선에 둔 채 그림만 키운다.
//   멈추는 자리는 대상의 **오른쪽 끝** 기준이라 키워도 캐릭터와의 간격은 그대로다.
[CreateAssetMenu(menuName = "DesktopWindowControl/Visual/Target Visual", fileName = "TargetVisual")]
public class TargetVisual : ScriptableObject
{
    [SerializeField, Tooltip("서 있는 그림 (발이 아래 가운데)")]
    private Sprite? sprite;

    [SerializeField, Tooltip("같은 모양의 흰 실루엣 — 타격 순간 위에 겹친다")]
    private Sprite? flash;

    [SerializeField, Tooltip("산업 레벨 1부터 차례로. 1레벨은 흰색(원래 색)")]
    private Color[] levelTints = Array.Empty<Color>();

    [CenterHeader("개인 조정 (굽기가 덮지 않는다)")]
    [SerializeField, Range(0.25f, 4f), Tooltip("대상 그림 크기 배율 — 발 기준으로 키운다. 1 = 굽기 결과 그대로. 너무 작은 대상만 올린다")]
    private float scale = 1f;

    public Sprite? Sprite => sprite;
    public Sprite? Flash  => flash;
    public float   Scale  => scale;

    // 산업 레벨의 색. 표에 없는 레벨이면 가장 가까운 끝값 — 비어 있으면 원래 색.
    public Color GetTint(int level)
    {
        if (levelTints.Length == 0)
        {
            return Color.white;
        }

        return levelTints[Mathf.Clamp(level - 1, 0, levelTints.Length - 1)];
    }
}

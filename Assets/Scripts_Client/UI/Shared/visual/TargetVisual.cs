using System;
using UnityEngine;

// 산업 하나의 대상(왼쪽에서 다가오는 것) — 레벨별 서 있는 그림 + 타격 섬광.
//
// ■ 손으로 만들지 않는다
//   'Assets/Art/targets/<키>/'의 레시피를 에디터 도구가 가공해 채운다('CharacterVisual'과 같다).
//
// ■ 레벨마다 그림이 다르다 (2026-10-18)
//   'levels'가 레벨 1부터의 그림이다. 표보다 높은 레벨은 마지막 그림을 쓴다.
//   레벨 그림이 없는 대상만 예전처럼 한 장('sprite')에 레벨 색('levelTints')을 곱한다 — 그림이 있으면 색을 곱하지 않는다.
//
// ■ 타격 섬광
//   곱하기 색으로는 하얗게 만들 수 없어서, 같은 모양의 흰 실루엣('flash')을 위에 겹쳐 알파를 줄인다.
//
// ■ 크기 배율은 사람 몫이다 (2026-10-10)
//   'scale'은 굽기가 건드리지 않는다('ArtBaker'는 그림·레벨 색만 쓴다). 발(아래 가운데)을 땅선에 둔 채 그림만 키운다.
//   멈추는 자리는 대상의 **오른쪽 끝** 기준이라 키워도 캐릭터와의 간격은 그대로다.
//   레벨 그림마다 배율도 따로 있다('Level.scale' · 2026-10-18) — 화면 배율 = 'scale' × 레벨 배율('GetScale').
[CreateAssetMenu(menuName = "DesktopWindowControl/Visual/Target Visual", fileName = "TargetVisual")]
public class TargetVisual : ScriptableObject
{
    [Serializable]
    public struct Level
    {
        [Tooltip("서 있는 그림 (발이 아래 가운데)")]
        public Sprite? sprite;

        [Tooltip("같은 모양의 흰 실루엣")]
        public Sprite? flash;

        [Range(0.25f, 4f), Tooltip("이 레벨 그림만의 크기 배율 — 아래 'scale'에 곱한다. 굽기가 덮지 않는다(0은 1로 본다)")]
        public float scale;
    }

    [SerializeField, Tooltip("서 있는 그림 (발이 아래 가운데) — 레벨 그림이 있으면 1레벨 그림과 같다")]
    private Sprite? sprite;

    [SerializeField, Tooltip("같은 모양의 흰 실루엣 — 타격 순간 위에 겹친다")]
    private Sprite? flash;

    [SerializeField, Tooltip("산업 레벨 1부터 차례로의 그림. 비어 있으면 'sprite' 한 장에 레벨 색을 곱한다")]
    private Level[] levels = Array.Empty<Level>();

    [SerializeField, Tooltip("산업 레벨 1부터 차례로. 1레벨은 흰색(원래 색). 레벨 그림이 있으면 쓰지 않는다")]
    private Color[] levelTints = Array.Empty<Color>();

    [CenterHeader("개인 조정 (굽기가 덮지 않는다)")]
    [SerializeField, Range(0.25f, 4f), Tooltip("대상 그림 크기 배율(모든 레벨) — 발 기준으로 키운다. 1 = 굽기 결과 그대로. 레벨 하나만 바꾸려면 'levels'의 배율을 쓴다")]
    private float scale = 1f;

    public Sprite? Sprite => sprite;
    public Sprite? Flash  => flash;
    public float   Scale  => scale;

    // 이 레벨의 그림. 레벨 그림이 없으면 한 장짜리.
    public Sprite? GetSprite(int level) => levels.Length > 0 ? LevelAt(level).sprite : sprite;

    // 이 레벨의 크기 배율 — 전체 배율 × 레벨 배율. 레벨 그림이 없으면 전체 배율만.
    public float GetScale(int level)
    {
        float levelScale = levels.Length > 0 ? LevelAt(level).scale : 0f;

        return scale * (levelScale > 0f ? levelScale : 1f);
    }

    // 이 레벨의 섬광. 레벨 그림이 없으면 한 장짜리.
    public Sprite? GetFlash(int level) => levels.Length > 0 ? LevelAt(level).flash : flash;

    // 산업 레벨의 색. 레벨 그림이 있거나 표가 비어 있으면 원래 색 — 표에 없는 레벨이면 가장 가까운 끝값.
    public Color GetTint(int level)
    {
        if (levels.Length > 0 || levelTints.Length == 0)
        {
            return Color.white;
        }

        return levelTints[Mathf.Clamp(level - 1, 0, levelTints.Length - 1)];
    }

    private Level LevelAt(int level) => levels[Mathf.Clamp(level - 1, 0, levels.Length - 1)];
}

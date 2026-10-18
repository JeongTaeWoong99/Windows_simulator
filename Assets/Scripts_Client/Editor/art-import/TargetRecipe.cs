using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
    // 대상 하나를 어떻게 가공할지 — 레벨별 원본 스프라이트(또는 한 장 + 레벨별 색).
    // 'Assets/Art/targets/<키>/<키>_recipe.asset'에 둔다. **폴더 이름이 곧 키다**(지금은 산업 이름).
    //
    // ■ 레벨마다 그림이 다르면 'levels'를 채운다 (2026-10-18)
    //   레벨 1부터 차례로. 결과는 '<키>_L1.png' … 이고, 레벨 그림이 있으면 화면은 색을 곱하지 않는다.
    //   비어 있으면 예전처럼 'source' 한 장에 레벨 색을 곱한다.
    [CreateAssetMenu(menuName = "DesktopWindowControl/Art Recipe/Target", fileName = "recipe")]
    public class TargetRecipe : ScriptableObject
    {
        [System.Serializable]
        public struct LevelSource
        {
            public Sprite? sprite;

            [Min(1), Tooltip("정수 배 축소 — 원본마다 크기가 달라 레벨마다 정한다")]
            public int downscale;
        }

        [CenterHeader("원본")]
        [SerializeField, Tooltip("레벨별 그림이 없을 때 쓰는 한 장")]
        private Sprite? source;

        [SerializeField, Tooltip("산업 레벨 1부터 차례로. 차 있으면 'source'·레벨 색 대신 이 그림을 쓴다")]
        private LevelSource[] levels = System.Array.Empty<LevelSource>();

        [CenterHeader("가공")]
        [SerializeField, Min(1), Tooltip("정수 배 축소 — 가장 가까운 픽셀로 줄인다 ('source'용)")]
        private int downscale = 1;

        [SerializeField, Tooltip("산업 레벨 1부터 차례로 곱할 색. 1레벨은 흰색(원래 색). 레벨 그림이 있으면 쓰지 않는다")]
        private Color[] levelTints =
        {
            Color.white,
            new Color(1f,   0.85f, 0.35f), // 노랑
            new Color(0.5f, 0.8f,  1f),    // 하늘
            new Color(0.8f, 0.55f, 1f),    // 보라
            new Color(1f,   0.45f, 0.4f),  // 빨강
        };

        public Sprite?       Source     => source;
        public LevelSource[] Levels     => levels;
        public int           Downscale  => downscale;
        public Color[]       LevelTints => levelTints;

        [ContextMenu("이 레시피 굽기")]
        private void Bake() => ArtBaker.Bake(this);
    }
}

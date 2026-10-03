using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
    // 대상 하나를 어떻게 가공할지 — 원본 스프라이트 한 장과 레벨별 색.
    // 'Assets/Art/targets/<키>/<키>_recipe.asset'에 둔다. **폴더 이름이 곧 키다**(지금은 산업 이름).
    [CreateAssetMenu(menuName = "DesktopWindowControl/Art Recipe/Target", fileName = "recipe")]
    public class TargetRecipe : ScriptableObject
    {
        [CenterHeader("원본")]
        [SerializeField] private Sprite? source;

        [CenterHeader("가공")]
        [SerializeField, Min(1), Tooltip("정수 배 축소 — 가장 가까운 픽셀로 줄인다")]
        private int downscale = 1;

        [SerializeField, Tooltip("산업 레벨 1부터 차례로 곱할 색. 1레벨은 흰색(원래 색)")]
        private Color[] levelTints =
        {
            Color.white,
            new Color(1f,   0.85f, 0.35f), // 노랑
            new Color(0.5f, 0.8f,  1f),    // 하늘
            new Color(0.8f, 0.55f, 1f),    // 보라
            new Color(1f,   0.45f, 0.4f),  // 빨강
        };

        public Sprite?  Source     => source;
        public int      Downscale  => downscale;
        public Color[]  LevelTints => levelTints;

        [ContextMenu("이 레시피 굽기")]
        private void Bake() => ArtBaker.Bake(this);
    }
}

using System;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
    // 배경 하나를 어떻게 가공할지 — 원본 층 여러 장을 몇 개 묶음으로 합치고, 얼마나 잘라 줄일지.
    // 'Assets/Art/backgrounds/<키>/<키>_recipe.asset'에 둔다. **폴더 이름이 곧 키다.**
    //
    // 층을 합치는 이유: 슬롯마다 층 수만큼 RawImage가 생긴다. 같은 속도로 흐를 층은 미리 한 장으로 굽는다.
    [CreateAssetMenu(menuName = "DesktopWindowControl/Art Recipe/Background", fileName = "recipe")]
    public class BackgroundRecipe : ScriptableObject
    {
        [Serializable]
        public struct LayerGroup
        {
            [Tooltip("한 장으로 합칠 원본 층 — 뒤 → 앞 순서. 크기가 모두 같아야 한다")]
            public Texture2D[] sources;

            [Tooltip("땅(1.0) 대비 흐르는 속도")]
            public float speedRatio;
        }

        [CenterHeader("원본")]
        [SerializeField, Tooltip("결과 층 — 뒤 → 앞 순서")]
        private LayerGroup[] groups = Array.Empty<LayerGroup>();

        [CenterHeader("가공")]
        [SerializeField, Min(1), Tooltip("원본 아래에서부터 남길 높이 (원본 픽셀). 슬롯은 가로로 길어 위쪽 하늘은 버린다")]
        private int cropBottom = 256;

        [SerializeField, Min(1), Tooltip("정수 배 축소 — 칸 평균으로 줄인다")]
        private int downscale = 2;

        [SerializeField, Min(0), Tooltip("바닥에서 발이 서는 높이 (결과 픽셀)")]
        private int groundHeight;

        public LayerGroup[] Groups       => groups;
        public int          CropBottom   => cropBottom;
        public int          Downscale    => downscale;
        public int          GroundHeight => groundHeight;

        [ContextMenu("이 레시피 굽기")]
        private void Bake() => ArtBaker.Bake(this);
    }
}

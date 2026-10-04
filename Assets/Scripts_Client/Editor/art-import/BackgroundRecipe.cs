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

            [Tooltip("원본이 아틀라스 안의 스프라이트일 때 — 차 있으면 'sources' 대신 이것을 쓴다. 크기가 모두 같아야 한다")]
            public Sprite[] spriteSources;

            [Tooltip("땅(1.0) 대비 흐르는 속도")]
            public float speedRatio;
        }

        [CenterHeader("원본")]
        [SerializeField, Tooltip("결과 층 — 뒤 → 앞 순서")]
        private LayerGroup[] groups = Array.Empty<LayerGroup>();

        [SerializeField, Tooltip("땅 띠 — 이 타일들을 왼쪽 → 오른쪽으로 이어 붙여 맨 앞(속도 1.0) 층으로 깐다. 원본 배경에 땅이 없을 때만. 비우면 없음")]
        private Sprite[] groundTiles = Array.Empty<Sprite>();

        [SerializeField, Min(1), Tooltip("땅 띠의 정수 배 축소 — 배경('downscale')과 따로. 타일은 캐릭터와 같은 밀도(1)가 자연스럽다")]
        private int groundDownscale = 1;

        [CenterHeader("가공")]
        [SerializeField, Min(1), Tooltip("원본 아래에서부터 남길 높이 (원본 픽셀). 슬롯은 가로로 길어 위쪽 하늘은 버린다")]
        private int cropBottom = 256;

        [SerializeField, Min(0), Tooltip("원본 아래에서 먼저 버릴 높이 (원본 픽셀). 아래가 통짜 땅·단색이라 슬롯에 그것만 보일 때 줄인다")]
        private int skipBottom;

        [SerializeField, Min(1), Tooltip("정수 배 축소 — 칸 평균으로 줄인다")]
        private int downscale = 2;

        [SerializeField, Min(0), Tooltip("바닥에서 발이 서는 높이 (결과 픽셀)")]
        private int groundHeight;

        public LayerGroup[] Groups       => groups;
        public int          CropBottom   => cropBottom;
        public int          SkipBottom   => skipBottom;
        public int          Downscale    => downscale;
        public int          GroundHeight => groundHeight;
        public Sprite[]     GroundTiles     => groundTiles;
        public int          GroundDownscale => groundDownscale;

        [ContextMenu("이 레시피 굽기")]
        private void Bake() => ArtBaker.Bake(this);
    }
}

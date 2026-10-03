using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
    // 캐릭터 하나를 어떻게 가공할지 — 원본 팩의 무엇을 쓰고, 어느 방향을 보며, 어디를 잘라 낼지.
    // 'Assets/Art/characters/<키>/<키>_recipe.asset'에 둔다. **폴더 이름이 곧 키다.**
    //
    // 원본은 애니메이션 클립이나 스프라이트 배열 중 하나만 채운다. 배열이 차 있으면 배열이 이긴다
    // (클립이 없는 팩 대비). 같은 팩의 프레임은 크기·원점이 같다고 본다 — 다르면 굽기가 경고한다.
    //
    // 공격은 **연속 공격 순서대로** 여러 개를 넣는다(팩에 1·2·3타가 있으면 전부). 하나뿐이면 그것을 반복한다.
    [CreateAssetMenu(menuName = "DesktopWindowControl/Art Recipe/Character", fileName = "recipe")]
    public class CharacterRecipe : ScriptableObject
    {
        [System.Serializable]
        public struct AttackSource
        {
            public AnimationClip? clip;
            public Sprite[]       sprites;

            [Tooltip("타격 프레임을 직접 정한다. 0이면 사거리가 가장 크게 뻗는 프레임을 찾는다(첫 프레임은 타격이 될 수 없다)")]
            public int hitFrameOverride;
        }

        [CenterHeader("원본")]
        [SerializeField] private AnimationClip? runClip;
        [SerializeField] private Sprite[] runSprites = System.Array.Empty<Sprite>();

        [SerializeField, Tooltip("연속 공격 순서대로. 판정 순간에는 언제나 마지막 공격이 닿는다")]
        private AttackSource[] attacks = System.Array.Empty<AttackSource>();

        [SerializeField, Tooltip("발 위치와 크롭의 기준 — 대기 클립의 첫 프레임. 비우면 달리기 첫 프레임")]
        private AnimationClip? idleClip;

        [SerializeField, Tooltip("원본이 오른쪽을 보면 켠다 — 결과물은 언제나 왼쪽을 본다")]
        private bool sourceFacesRight;

        [CenterHeader("가공")]
        [SerializeField, Min(1), Tooltip("정수 배 축소. 1이면 원본 픽셀 그대로")]
        private int downscale = 1;

        [SerializeField, Tooltip("인벤토리 상반신 크롭 — 결과 칸 좌표(왼쪽 아래 0,0 · 발은 x=80). 정사각형. 너비 0이면 자동")]
        private RectInt portraitRect;

        [SerializeField, Tooltip("위젯 머리 크롭 — 결과 칸 좌표. 정사각형. 너비 0이면 자동")]
        private RectInt headRect;

        public AnimationClip? RunClip          => runClip;
        public Sprite[]       RunSprites       => runSprites;
        public AttackSource[] Attacks          => attacks;
        public AnimationClip? IdleClip         => idleClip;
        public bool           SourceFacesRight => sourceFacesRight;
        public int            Downscale        => downscale;
        public RectInt        PortraitRect     => portraitRect;
        public RectInt        HeadRect         => headRect;

        [ContextMenu("이 레시피 굽기")]
        private void Bake() => ArtBaker.Bake(this);
    }
}

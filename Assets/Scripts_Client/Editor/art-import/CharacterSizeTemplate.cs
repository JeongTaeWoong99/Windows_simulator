using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
    // 캐릭터 크기 등급(소·중·대)의 기본 세팅 — 굽기가 결과 SO('CharacterVisual')의 개인 조정 칸을 처음 채울 때 쓴다.
    // 'Assets/Art/characters/_templates/'에 둔다. 레시피의 'sizeTemplate'이 가리키고, 비어 있으면 키로 고른다('autoMaxHeight').
    //
    // ■ 한 번만 채운다 (2026-10-18)
    //   SO의 등급('CharacterVisual.sizeClass')이 비어 있을 때만 굽기가 채운다 — 그 뒤 사람이 다듬은 값은 다시 굽기가 덮지 않는다.
    //   등급을 바꾸려면 SO 인스펙터의 드롭다운('CharacterVisualEditor')을 쓴다 — 고르면 그 템플릿 값으로 다시 채우고,
    //   그 뒤 손댄 값은 '커스텀'으로 표시된다. 템플릿 값을 바꾼 뒤 모두 맞추려면 [이 템플릿 캐릭터 전부 다시 적용](덮는다).
    //
    // ■ 크기는 키로 맞춘다
    //   캐릭터 배율 = 'targetHeight' ÷ 몸 키(대기 첫 프레임). 옆으로 긴 그림은 'maxWidth'를 넘지 않게 줄인다.
    //   그래서 같은 등급끼리는 원본 팩의 크기와 상관없이 비슷한 키로 선다.
    //
    // ■ 공격 자리는 사거리로 맞춘다
    //   멈추는 거리 = 공격들의 타격 프레임 사거리 평균 × 캐릭터 배율 × 'reachRatio' (몸 앞끝보다는 멀리).
    //   전체 세팅의 'stopGap'과의 차이를 SO의 'stopGapOffset'에 적는다.
    [CreateAssetMenu(menuName = "DesktopWindowControl/Art Recipe/Character Size Template", fileName = "size_template")]
    public class CharacterSizeTemplate : ScriptableObject
    {
        [CenterHeader("고르기")]
        [SerializeField, Tooltip("이 템플릿의 등급 — SO 드롭다운·화면 표시(소·중·대)가 이 값으로 템플릿을 찾는다. 등급마다 템플릿 하나")]
        private ECharacterSize sizeClass = ECharacterSize.Medium;

        [SerializeField, Min(0), Tooltip("레시피에 템플릿이 비어 있을 때 몸 키(아트 픽셀)가 이 값 이하면 이 템플릿 — 가장 작은 값부터 본다. 0이면 자동으로 고르지 않는다(대형처럼 손으로만 고르는 것)")]
        private int autoMaxHeight;

        [CenterHeader("크기")]
        [SerializeField, Min(1), Tooltip("캐릭터 배율을 곱한 뒤의 몸 키(아트 픽셀) — 대상 그림과 같은 잣대다")]
        private int targetHeight = 52;

        [SerializeField, Min(1), Tooltip("캐릭터 배율을 곱한 뒤의 몸 너비 상한 — 옆으로 긴 그림(슬라임·쥐)이 너무 커지지 않게")]
        private int maxWidth = 70;

        [SerializeField, Range(0f, 4f), Tooltip("슬롯 무대 확대 배율('pixelScaleOverride'). 0이면 전체 세팅. 키 큰 등급일수록 낮춰 머리가 잘리지 않게")]
        private float pixelScale = 1.4f;

        [CenterHeader("공격 자리")]
        [SerializeField, Range(0.2f, 1f), Tooltip("사거리 중 대상 앞에 남기는 비율 — 낮을수록 무기가 대상 안으로 깊이 들어간다")]
        private float reachRatio = 0.55f;

        [CenterHeader("얼굴 크롭")]
        [SerializeField, Tooltip("자동 상반신·머리 크롭을 몸 전체로 — 얼굴이 따로 없는 작은 캐릭터(박쥐·슬라임)는 가까이 자르면 알아보기 어렵다")]
        private bool cropWholeBody;

        public ECharacterSize SizeClass => sizeClass;
        public int   AutoMaxHeight => autoMaxHeight;
        public int   TargetHeight  => targetHeight;
        public int   MaxWidth      => maxWidth;
        public float PixelScale    => pixelScale;
        public float ReachRatio    => reachRatio;
        public bool  CropWholeBody => cropWholeBody;

        [ContextMenu("이 템플릿 캐릭터 전부 다시 적용 (개인 조정을 덮는다)")]
        private void Reapply() => ArtBaker.ReapplySizeTemplate(this);
    }
}

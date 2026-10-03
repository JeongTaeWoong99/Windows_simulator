using System;
using UnityEngine;

// 슬롯 배경 한 벌 — 뒤에서 앞 순서의 패럴랙스 층.
//
// ■ 손으로 만들지 않는다
//   'Assets/Art/backgrounds/<키>/'의 레시피를 에디터 도구가 가공해 채운다('CharacterVisual'과 같다).
//
// ■ 그리는 방법
//   층마다 'RawImage' 하나 — 높이에 맞추고 가로는 'uvRect.x'를 밀어 반복한다(텍스처는 Repeat).
//   속도 비율 1.0이 땅이다. 대상이 다가오는 속도와 같아야 미끄러져 보이지 않는다.
[CreateAssetMenu(menuName = "DesktopWindowControl/Visual/Background Visual", fileName = "BackgroundVisual")]
public class BackgroundVisual : ScriptableObject
{
    [Serializable]
    public struct Layer
    {
        [Tooltip("가로로 이어 붙여도 이음매가 없는 층 그림")]
        public Texture2D texture;

        [Tooltip("땅(1.0) 대비 흐르는 속도. 0이면 멈춘 하늘")]
        public float speedRatio;
    }

    [SerializeField, Tooltip("뒤 → 앞 순서")]
    private Layer[] layers = Array.Empty<Layer>();

    [SerializeField, Tooltip("층 그림의 높이 (아트 픽셀)")]
    private int stageHeight;

    [SerializeField, Tooltip("바닥에서 캐릭터·대상의 발이 서는 높이 (아트 픽셀)")]
    private int groundHeight;

    public Layer[] Layers       => layers;
    public int     StageHeight  => stageHeight;
    public int     GroundHeight => groundHeight;
}

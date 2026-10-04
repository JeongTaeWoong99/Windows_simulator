using System;
using UnityEngine;

// 캐릭터 한 명의 연출 그림 — 대기·달리기·공격 프레임과 인벤토리(상반신)·위젯(머리) 크롭.
//
// ■ 손으로 만들지 않는다
//   'Assets/Art/characters/<키>/'의 레시피를 에디터 도구('Window/DesktopWindowControl/아트/전부 다시 굽기')가
//   가공해 채운다. 값을 직접 고치면 다음 굽기에서 덮인다 — 레시피를 고친다.
//
// ■ 공격은 연속 공격 순서대로 들어 있다
//   대상에 닿으면 1타부터 순서대로 치고, 판정 순간에 닿는 것은 언제나 **마지막 공격**(마무리)이다.
//   칸이 남으면 마무리 전 타들을 1타부터 다시 돈다(1·2·3·1·4). 공격이 하나뿐인 캐릭터는 그것을 반복한다.
//
// ■ 모든 캐릭터가 같은 규격이다 ('Art 규칙.md')
//   왼쪽을 본다 · 칸 크기 같음 · 발이 칸 아래 가운데. 그래서 재생 쪽은 캐릭터를 가리지 않고 프레임만 바꾼다.
[CreateAssetMenu(menuName = "DesktopWindowControl/Visual/Character Visual", fileName = "CharacterVisual")]
public class CharacterVisual : ScriptableObject
{
    [Serializable]
    public struct AttackMotion
    {
        public Sprite[] frames;

        [Tooltip("타격이 닿는 프레임 번호 — 이 프레임이 판정·번쩍임 순간에 맞춰진다")]
        public int hitFrame;

        [Tooltip("공격 이펙트 — 캐릭터와 같은 칸 규격, 캐릭터 위에 겹쳐 같은 진행도로 재생한다. 모션과 이펙트가 따로 있는 팩용. 비우면 없음")]
        public Sprite[] effectFrames;

        [Tooltip("타격 이펙트 — 타격 순간 대상 가운데에서 한 번 터진다(전체 세팅의 타격 이펙트 시간). 비우면 없음")]
        public Sprite[] hitEffectFrames;
    }

    [SerializeField, Tooltip("대기 자세 — 달리기 ↔ 공격 사이 숨, 멈춘 슬롯에 쓴다. 비어 있으면 달리기 첫 프레임")]
    private Sprite[] idleFrames = Array.Empty<Sprite>();

    [SerializeField, Tooltip("달리기 한 바퀴 (제자리 달리기 루프)")]
    private Sprite[] runFrames = Array.Empty<Sprite>();

    [SerializeField, Tooltip("연속 공격 순서대로. 하나뿐이면 반복한다")]
    private AttackMotion[] attacks = Array.Empty<AttackMotion>();

    [SerializeField, Tooltip("인벤토리용 상반신 크롭")]
    private Sprite? portrait;

    [SerializeField, Tooltip("위젯용 머리 크롭")]
    private Sprite? head;

    // 굽기는 위 칸만 쓴다 — 여기는 다시 구워도 남는다. 전체 세팅('SlotStageSettings')에 더한다.
    [CenterHeader("개인 조정 (굽기가 덮지 않는다)")]
    [SerializeField, Range(-60, 60), Tooltip("멈추는 거리에 더한다(아트 픽셀). 무기가 길면 +, 맨손이면 −")]
    private int stopGapOffset;

    [SerializeField, Range(0f, 4f), Tooltip("이 캐릭터 슬롯의 확대 배율(소수 가능). 0이면 전체 세팅. 키가 커서 머리가 잘리면 낮춘다(무대 전체가 함께 작아진다). 정수가 아니면 점 크기가 조금 들쭉날쭉하다")]
    private float pixelScaleOverride;

    [SerializeField, Range(0.5f, 2f), Tooltip("캐릭터만 키우거나 줄인다(무대·대상은 그대로). 발 위치 기준. 1 = 굽기 규격 그대로. 키우면 머리가 잘리지 않는지 함께 본다")]
    private float characterScale = 1f;

    public Sprite[]       IdleFrames    => idleFrames;
    public Sprite[]       RunFrames     => runFrames;
    public AttackMotion[] Attacks       => attacks;
    public Sprite?        Portrait      => portrait;
    public Sprite?        Head          => head;
    public int            StopGapOffset => stopGapOffset;
    public float          CharacterScale => characterScale;

    // 이 캐릭터 슬롯의 확대 배율 — 개인 값이 있으면 그것, 없으면 전체 세팅
    public float GetPixelScale(SlotStageSettings settings) => pixelScaleOverride > 0f ? pixelScaleOverride : settings.PixelScale;

    // 대기 자세 한 장. 대기 그림이 없으면 달리기 첫 프레임
    public Sprite? IdleFrame => idleFrames.Length > 0 ? idleFrames[0] : runFrames.Length > 0 ? runFrames[0] : null;
}

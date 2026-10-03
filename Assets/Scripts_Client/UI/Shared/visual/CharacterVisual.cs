using UnityEngine;

// 캐릭터 한 명의 연출 그림 — 달리기·공격 프레임과 인벤토리(상반신)·위젯(머리) 크롭.
//
// ■ 손으로 만들지 않는다
//   'Assets/Art/characters/<키>/'의 레시피를 에디터 도구('Window/DesktopWindowControl/아트/전부 다시 굽기')가
//   가공해 채운다. 값을 직접 고치면 다음 굽기에서 덮인다 — 레시피를 고친다.
//
// ■ 모든 캐릭터가 같은 규격이다 ('Art 규칙.md')
//   왼쪽을 본다 · 칸 크기 같음 · 발이 칸 아래 가운데. 그래서 재생 쪽은 캐릭터를 가리지 않고 프레임만 바꾼다.
[CreateAssetMenu(menuName = "DesktopWindowControl/Visual/Character Visual", fileName = "CharacterVisual")]
public class CharacterVisual : ScriptableObject
{
    [SerializeField, Tooltip("달리기 한 바퀴 (제자리 달리기 루프)")]
    private Sprite[] runFrames = System.Array.Empty<Sprite>();

    [SerializeField, Tooltip("공격 한 번")]
    private Sprite[] attackFrames = System.Array.Empty<Sprite>();

    [SerializeField, Tooltip("공격 중 타격이 닿는 프레임 번호 — 이 프레임이 판정 순간에 맞춰진다")]
    private int hitFrame;

    [SerializeField, Tooltip("인벤토리용 상반신 크롭")]
    private Sprite? portrait;

    [SerializeField, Tooltip("위젯용 머리 크롭")]
    private Sprite? head;

    public Sprite[] RunFrames    => runFrames;
    public Sprite[] AttackFrames => attackFrames;
    public int      HitFrame     => hitFrame;
    public Sprite?  Portrait     => portrait;
    public Sprite?  Head         => head;
}

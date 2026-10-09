using GameData;
using UnityEngine;

// 슬롯 무대 연출의 전체 세팅 — 모든 슬롯·모든 캐릭터에 똑같이 걸리는 시간·거리 값.
// 캐릭터마다 다른 값은 'CharacterVisual'의 개인 조정 칸에 있다.
//
// 에셋은 'Resources/SlotStageSettings' 하나다(그림과 달리 git에 올라간다 — 숫자뿐이라).
// 무대('SlotStageView')가 매 프레임 이 값을 읽으므로, 플레이 중에 인스펙터·CLI·MCP로 고쳐도 바로 보인다.
//
// ■ 모션 길이는 그림이 정하지 않는다
//   프레임 수가 달라도 달리기 한 바퀴·공격 한 번을 여기 시간에 맞춰 재생한다 — 게임UI 2.5.
//
// ■ 거리 값은 아트 픽셀이다
//   화면에서는 '확대 배율'을 곱한다. 전체 배율은 정수 — 점 그림이 고르게 커진다.
//   캐릭터 개인 배율은 소수도 받는다(1.5 등) — 대신 점 크기가 한두 칸씩 들쭉날쭉해질 수 있다.
//
// ■ 캐릭터 자리만은 패널 폭의 비율이다 (2026-10-09)
//   픽셀 여백이면 창 폭에 따라 칸이 넓어질수록 캐릭터가 오른쪽 끝으로 쏠린다. 비율이면 어느 폭에서나 같은 자리에 선다.
//   대상이 멈추는 자리는 여전히 캐릭터 발에서 'stopGap'(+ 캐릭터 개인 오프셋)만큼 왼쪽이다.
//
// ■ 연출 한 바퀴 = 판정 1회의 최소 시간 (2026-10-09 · T-102)
//   여운 + 숨 + 달리기(다가오기) + 숨 + 공격 1회 = 'RequiredCycleSeconds'. 지금 값이면 0.3 + 0.1 + 1 + 0.1 + 0.6 = 2.1초.
//   판정 주기가 이보다 짧으면 'SlotStageTimeline'이 여운·숨·달리기를 깎아 공격이 끊긴다 —
//   그래서 엑셀 'Constants.MinCycleMs'가 이 값 이상이어야 하고, 서버·클라가 그 값으로 주기를 막는다('WorkStationProgress').
//   ⚠️ 여기 시간을 늘렸으면 'MinCycleMs'도 함께 올린다. 짧으면 플레이 시작 때 경고가 한 번 뜬다.
[CreateAssetMenu(menuName = "DesktopWindowControl/Visual/Slot Stage Settings", fileName = "SlotStageSettings")]
public class SlotStageSettings : ScriptableObject
{
    // 'Resources.Load' 경로
    public const string ResourcePath = "SlotStageSettings";

    [CenterHeader("시간 (초)")]
    [SerializeField, Range(0.3f, 2f), Tooltip("대상이 화면 왼쪽 밖에서 나와 멈춤 자리에 닿기까지 — 이동 속도가 여기서 나온다")]
    private float approachSeconds = 1f;

    [SerializeField, Range(0.4f, 1.6f), Tooltip("달리기 한 바퀴. 프레임 수와 무관하게 이 시간에 맞춰 재생한다")]
    private float runLoopSeconds = 0.8f;

    [SerializeField, Range(0.3f, 1.2f), Tooltip("공격 한 번의 기준 길이. 프레임 수와 무관하게 이 시간에 맞춰 재생한다 — 처치 타가 판정에 닿도록 슬롯마다 조금씩 늘거나 준다")]
    private float attackSeconds = 0.6f;

    [SerializeField, Range(0f, 0.6f), Tooltip("처치 뒤 마무리 공격의 여운. 이 동안 배경은 멈춰 있다")]
    private float recoverSeconds = 0.3f;

    [SerializeField, Range(0f, 0.3f), Tooltip("달리기 ↔ 공격 사이에 끼우는 대기 자세 한순간. 0이면 바로 맞붙는다")]
    private float idleSeconds = 0.1f;

    [SerializeField, Range(0f, 0.6f), Tooltip("쓰러진 대상이 그 자리에서 사라지는 시간. 0이면 즉시")]
    private float fadeSeconds = 0.3f;

    [SerializeField, Range(0f, 0.3f), Tooltip("타격마다 대상이 하얗게 번쩍이는 시간")]
    private float flashSeconds = 0.08f;

    [SerializeField, Range(0.05f, 1f), Tooltip("타격 이펙트(공격마다 넣었을 때) 한 번 재생 시간")]
    private float hitEffectSeconds = 0.3f;

    [CenterHeader("거리 (아트 픽셀)")]
    [SerializeField, Range(0, 120), Tooltip("캐릭터 발과 대상 오른쪽 끝 사이 — 대상이 멈추는 거리")]
    private int stopGap = 34;

    [CenterHeader("자리")]
    [SerializeField, Range(0.5f, 0.95f), Tooltip("캐릭터 발의 가로 자리 — 패널 왼쪽 끝 0, 오른쪽 끝 1. 0.7이면 열 칸 중 7~8번째. 모든 캐릭터에 똑같이 걸린다")]
    private float characterAnchor = 0.7f;

    [CenterHeader("화면")]
    [SerializeField, Range(1, 4), Tooltip("아트 픽셀 하나를 화면 몇 칸으로 — 정수만")]
    private int pixelScale = 2;

    [SerializeField, Range(0f, 2f), Tooltip("먼 층(땅이 아닌 층)의 속도에 곱한다. 땅은 대상과 같아야 해서 곱하지 않는다")]
    private float farLayerSpeed = 1f;

    public float ApproachSeconds => approachSeconds;
    public float RunLoopSeconds  => runLoopSeconds;
    public float AttackSeconds   => attackSeconds;
    public float RecoverSeconds  => recoverSeconds;
    public float IdleSeconds     => idleSeconds;
    public float FadeSeconds     => fadeSeconds;
    public float FlashSeconds    => flashSeconds;
    public float HitEffectSeconds => hitEffectSeconds;
    public int   StopGap         => stopGap;
    public float CharacterAnchor => characterAnchor;
    public int   PixelScale      => pixelScale;
    public float FarLayerSpeed   => farLayerSpeed;

    // 연출 한 바퀴에 필요한 초 — 여운 + 숨 + 달리기 + 숨 + 공격 1회 (위 머리 주석)
    public float RequiredCycleSeconds => recoverSeconds + 2f * idleSeconds + approachSeconds + attackSeconds;

    private static bool _minCycleChecked;

    private static SlotStageSettings? _current;

    // 지금 쓰는 세팅. 에셋이 없으면 기본값 인스턴스로 버틴다(목업에서 고른 값 그대로다).
    public static SlotStageSettings Current
    {
        get
        {
            if (_current == null)
            {
                _current = Resources.Load<SlotStageSettings>(ResourcePath);
            }

            if (_current == null)
            {
                _current = CreateInstance<SlotStageSettings>();
            }

            WarnIfMinCycleTooShort(_current);

            return _current;
        }
    }

    // 엑셀 최소 주기가 연출 한 바퀴보다 짧으면 한 번 알린다 (Current에서 호출 — 테이블이 적재된 뒤 첫 번만 본다)
    private static void WarnIfMinCycleTooShort(SlotStageSettings settings)
    {
        if (_minCycleChecked || !GameDataLoader.IsLoaded)
        {
            return;
        }

        _minCycleChecked = true;

        float minCycle = Constants.MinCycleMs / 1000f;

        if (minCycle + 0.0005f < settings.RequiredCycleSeconds)
        {
            Debug.LogWarning($"[SlotStageSettings] 엑셀 'MinCycleMs'({Constants.MinCycleMs}ms)가 연출 한 바퀴({settings.RequiredCycleSeconds:0.00}초)보다 짧다 — "
                           + "빠른 슬롯에서 공격이 끊긴다. Constants.xlsx 값을 올린다.");
        }
    }
}

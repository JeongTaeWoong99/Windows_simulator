using MikaProtocol;
using UnityEngine;

// 서버 없이 슬롯 무대를 돌려 보는 미리보기 — 'SlotStageView' 옆에 붙이고 플레이한다.
// 세팅('SlotStageSettings')·캐릭터 개인 조정을 고치며 볼 때 쓴다. 실제 슬롯에는 붙이지 않는다.
//
// 주기·배속만 여기 값이고, 나머지는 실제 슬롯과 같은 길(그림 목록 → 무대)로 그린다.
[RequireComponent(typeof(SlotStageView))]
public class SlotStagePreview : MonoBehaviour
{
    [SerializeField, Tooltip("CharacterTable의 TID. 0이면 빈 슬롯")]
    private int characterTid = 1001;

    [SerializeField]
    private EIndustryType industry = EIndustryType.Mining;

    [SerializeField, Range(1, 5)]
    private int industryLevel = 1;

    [SerializeField, Range(0.2f, 10f), Tooltip("판정 1회의 초")]
    private float cycleSeconds = 4f;

    [SerializeField, Range(0.1f, 4f), Tooltip("시간 배속 — 미리보기 전용")]
    private float timeScale = 1f;

    private SlotStageView _stage = null!;
    private float         _clock;

    // 고른 값이 바뀌었을 때만 그림을 다시 고른다
    private (int, EIndustryType, int) _shown = (-1, EIndustryType.None, -1);

    private void Awake()
    {
        _stage = GetComponent<SlotStageView>();
    }

    // 미리보기는 스스로 시간을 센다 — 실제 슬롯과 달리 주인이 없다 (Unity 메시지)
    private void Update()
    {
        var wanted = (characterTid, industry, industryLevel);

        if (wanted != _shown)
        {
            _shown = wanted;
            _stage.Show(characterTid, industry, industryLevel);
        }

        float delta = Time.deltaTime * timeScale;
        _clock = Mathf.Repeat(_clock + delta, cycleSeconds);

        if (characterTid == 0)
        {
            _stage.DrawIdle();

            return;
        }

        _stage.Tick(_clock, cycleSeconds, delta);
    }
}

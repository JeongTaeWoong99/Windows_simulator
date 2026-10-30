using System;
using UnityEngine;

// 소리 채널 — 설정 화면의 음량 줄과 1:1이다. 저장 키도 이 이름을 쓴다('WindowSettings.AudioKey').
public enum SoundChannel
{
    Master,
    Bgm,
    Sfx,
    Ui,
}

// 소리의 주인 — 배경 음악·효과음·버튼 소리를 틀고, 채널별 음량을 정해 저장한다.
// 무엇을 틀지는 부르는 쪽이 'Sounds.Xxx'로 고른다. 소리 파일은 그림 저장소에 있다('SoundCatalog').
//
// ■ 채널 = AudioSource 하나씩 (자식 오브젝트)
//   Bgm Source — 반복 재생 한 곡 · Sfx Source — 게임 사건(골드·보상) · Ui Source — 버튼.
//   효과음 둘을 가른 것은 음량을 따로 조절하려고다 — 버튼 소리만 끄고 싶은 사람이 있다.
//   실제 음량 = 마스터 × 채널.
//
// ■ 권위 소스 — 에디터·빌드 모두 저장값 ('DisplayManager'와 같다 · 'Settings 규칙.md' 1장)
//   소리는 에디터에서도 실제로 들리는 값이라 마지막 선택이 유지되는 편이 테스트에 맞다. 인스펙터 'setStart*'는 첫 실행 기본값.
//
// ■ 소리가 없어도 돈다 — 그림 저장소를 받지 않은 PC면 'Sounds.Xxx'가 null이고, 그때는 조용히 건너뛴다.
public class SoundManager : MonoService<SoundManager>
{
    public const int MaxVolumePercent = 100;

    [CenterHeader("채널")]
    [SerializeField, Tooltip("배경 음악 — 반복 재생. 자식 'Bgm Source'")]
    private AudioSource bgmSource = null!;

    [SerializeField, Tooltip("게임 사건 효과음 — 골드·보상. 자식 'Sfx Source'")]
    private AudioSource sfxSource = null!;

    [SerializeField, Tooltip("버튼 소리. 자식 'Ui Source'")]
    private AudioSource uiSource = null!;

    // 바탕화면에 늘 떠 있는 앱이다 — 배경 음악은 낮게 시작한다(일하는 중에 거슬리지 않게)
    [CenterHeader("첫 실행 기본값 (저장값이 있으면 그걸 따른다) · %")]
    [SerializeField, Range(0, MaxVolumePercent)] private int setStartMaster = 80;
    [SerializeField, Range(0, MaxVolumePercent)] private int setStartBgm    = 30;
    [SerializeField, Range(0, MaxVolumePercent)] private int setStartSfx    = 70;
    [SerializeField, Range(0, MaxVolumePercent)] private int setStartUi     = 50;

    // 'SoundChannel' 순서의 음량(%)
    private readonly int[] _volumes = new int[Enum.GetValues(typeof(SoundChannel)).Length];

    // 저장값을 읽는다. 다른 서비스를 건드리지 않는 순수 값 로드라 Awake에서 안전하다 (Unity 메시지)
    protected override void Awake()
    {
        base.Awake();

        int[] fallbacks = { setStartMaster, setStartBgm, setStartSfx, setStartUi };

        foreach (SoundChannel channel in Enum.GetValues(typeof(SoundChannel)))
        {
            _volumes[(int)channel] = Mathf.Clamp(WindowSettings.LoadInt(WindowSettings.AudioKey(channel), fallbacks[(int)channel]), 0, MaxVolumePercent);
        }
    }

    // 참조 검증 → 음량 적용 → 배경 음악 (Unity 메시지)
    private void Start()
    {
        this.RequireRef(bgmSource, nameof(bgmSource));
        this.RequireRef(sfxSource, nameof(sfxSource));
        this.RequireRef(uiSource,  nameof(uiSource));

        ApplyVolumes();
        PlayBgm(Sounds.BgmIdle);
    }

    #region 재생

    // 배경 음악을 바꾼다. 같은 곡이면 처음부터 다시 틀지 않는다
    public void PlayBgm(AudioClip? clip)
    {
        if (clip == null || (bgmSource.clip == clip && bgmSource.isPlaying))
        {
            return;
        }

        bgmSource.clip = clip;
        bgmSource.loop = true;
        bgmSource.Play();
    }

    // 게임 사건 효과음 — 겹쳐 울린다
    public void PlaySfx(AudioClip? clip)
    {
        if (clip != null)
        {
            sfxSource.PlayOneShot(clip);
        }
    }

    // 버튼 소리 — 겹쳐 울린다
    public void PlayUi(AudioClip? clip)
    {
        if (clip != null)
        {
            uiSource.PlayOneShot(clip);
        }
    }

    #endregion

    #region 음량

    // 채널 음량(%) — 설정 슬라이더의 시작값
    public int GetVolume(SoundChannel channel) => _volumes[(int)channel];

    // 끄는 동안 매 프레임 불리므로 저장하지 않는다 — 손을 떼면 'SaveVolume'이 한 번 저장한다(투명도 슬라이더와 같다)
    public void SetVolume(SoundChannel channel, int percent)
    {
        _volumes[(int)channel] = Mathf.Clamp(percent, 0, MaxVolumePercent);
        ApplyVolumes();
    }

    // 지금 음량을 저장한다 (설정 슬라이더에서 손을 뗐을 때)
    public void SaveVolume(SoundChannel channel)
    {
        WindowSettings.SaveInt(WindowSettings.AudioKey(channel), _volumes[(int)channel]);
    }

    // 실제 음량 = 마스터 × 채널
    private void ApplyVolumes()
    {
        float master = _volumes[(int)SoundChannel.Master] / 100f;

        bgmSource.volume = master * _volumes[(int)SoundChannel.Bgm] / 100f;
        sfxSource.volume = master * _volumes[(int)SoundChannel.Sfx] / 100f;
        uiSource.volume  = master * _volumes[(int)SoundChannel.Ui]  / 100f;
    }

    #endregion
}

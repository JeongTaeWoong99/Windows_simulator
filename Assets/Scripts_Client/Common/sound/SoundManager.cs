using System;
using UnityEngine;

// 소리의 주인 — 배경 음악·효과음·버튼 소리를 틀고, 채널별 음량을 정해 PlayerPrefs에 저장한다.
// 무엇을 틀지는 부르는 쪽이 고른다(보통 프로젝트의 이름표 클래스 — 'XxxSounds.Click' → 'SoundCatalog.Of("click")').
//
// ■ 채널 = AudioSource 하나씩 (자식 오브젝트)
//   Bgm Source — 반복 재생 한 곡 · Sfx Source — 게임 사건 · Ui Source — 버튼.
//   Master는 소스가 없고 셋에 곱한다.
//
// ■ 음량 저장 — 'Audio.<채널>' (% 0~100). 에디터·빌드 모두 저장값을 읽는다
//   소리는 에디터에서도 실제로 들리는 값이라 마지막 선택이 유지되는 편이 테스트에 맞다. 인스펙터 'setStart*'는 첫 실행 기본값.
//   설정 슬라이더는 끄는 동안 'SetVolume'(적용만), 손을 떼면 'SaveVolume'(한 번 저장)을 부른다 — 드래그마다 디스크에 쓰지 않는다.
//
// ■ 소리가 없어도 돈다 — 'Play*'는 null을 조용히 건너뛴다. 소리 파일이 없는 PC(별도 저장소 등)에서도 게임은 그대로다.
public class SoundManager : MonoService<SoundManager>
{
    public const int    MaxVolumePercent = 100;
    public const string PrefsPrefix      = "Audio.";

    [CenterHeader("채널")]
    [SerializeField, Tooltip("배경 음악 — 반복 재생. 자식 'Bgm Source'")]
    private AudioSource bgmSource = null!;

    [SerializeField, Tooltip("게임 사건 효과음. 자식 'Sfx Source'")]
    private AudioSource sfxSource = null!;

    [SerializeField, Tooltip("버튼 소리. 자식 'Ui Source'")]
    private AudioSource uiSource = null!;

    [CenterHeader("시작 배경 음악")]
    [SerializeField, Tooltip("시작하면 틀 배경 음악의 'SoundCatalog' 키. 비우면 틀지 않는다")]
    private string startBgmKey = "";

    [CenterHeader("첫 실행 기본값 (저장값이 있으면 그걸 따른다) · %")]
    [SerializeField, Range(0, MaxVolumePercent)] private int setStartMaster = 80;
    [SerializeField, Range(0, MaxVolumePercent)] private int setStartBgm    = 30;
    [SerializeField, Range(0, MaxVolumePercent)] private int setStartSfx    = 70;
    [SerializeField, Range(0, MaxVolumePercent)] private int setStartUi     = 50;

    // 'SoundChannel' 순서의 음량(%)
    private readonly int[] _volumes = new int[Enum.GetValues(typeof(SoundChannel)).Length];

    // 채널의 저장 키 — 'Audio.Master' · 'Audio.Bgm' · 'Audio.Sfx' · 'Audio.Ui'
    public static string PrefsKey(SoundChannel channel) => PrefsPrefix + channel;

    // 저장값을 읽는다. 다른 서비스를 건드리지 않는 순수 값 로드라 Awake에서 안전하다 (Unity 메시지)
    protected override void Awake()
    {
        base.Awake();

        int[] fallbacks = { setStartMaster, setStartBgm, setStartSfx, setStartUi };

        foreach (SoundChannel channel in Enum.GetValues(typeof(SoundChannel)))
        {
            _volumes[(int)channel] = Mathf.Clamp(PlayerPrefs.GetInt(PrefsKey(channel), fallbacks[(int)channel]), 0, MaxVolumePercent);
        }
    }

    // 참조 검증 → 음량 적용 → 시작 배경 음악 (Unity 메시지)
    private void Start()
    {
        this.RequireRef(bgmSource, nameof(bgmSource));
        this.RequireRef(sfxSource, nameof(sfxSource));
        this.RequireRef(uiSource,  nameof(uiSource));

        ApplyVolumes();

        if (startBgmKey.Length > 0)
        {
            PlayBgm(SoundCatalog.Of(startBgmKey));
        }
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

    // 배경 음악을 멈춘다
    public void StopBgm()
    {
        bgmSource.Stop();
        bgmSource.clip = null;
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

    // 적용만 한다 — 슬라이더를 끄는 동안 매 프레임 불린다
    public void SetVolume(SoundChannel channel, int percent)
    {
        _volumes[(int)channel] = Mathf.Clamp(percent, 0, MaxVolumePercent);
        ApplyVolumes();
    }

    // 지금 음량을 저장한다 (슬라이더에서 손을 뗐을 때). 값이 그대로면 쓰지 않는다
    public void SaveVolume(SoundChannel channel)
    {
        string key = PrefsKey(channel);

        if (PlayerPrefs.HasKey(key) && PlayerPrefs.GetInt(key) == _volumes[(int)channel])
        {
            return;
        }

        PlayerPrefs.SetInt(key, _volumes[(int)channel]);
        PlayerPrefs.Save();
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

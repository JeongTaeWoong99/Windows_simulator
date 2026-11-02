using UnityEngine;

// 어떤 소리를 어디서 쓰는지 한 곳에 모은 이름표(트는 쪽은 Common 'SoundManager') — 파일은 그림 저장소 'Assets/Art/audio/<채널>/<키>.wav'.
// 소리를 바꾸려면 같은 이름으로 파일을 덮는다(코드는 그대로). 새 소리는 파일을 넣고 여기 한 줄을 더한다.
public static class Sounds
{
    // ── bgm — 배경 음악 (반복) ──

    // 기본 배경 음악 'bgm_idle'은 코드가 틀지 않는다 — 씬 'Sound Manager'의 시작 배경 음악 키('startBgmKey')

    // ── ui — 버튼 ──

    // 버튼 누름 — 모든 버튼 ('ButtonPressFx')
    public static AudioClip? UiClick => SoundCatalog.Of("ui_click");

    // ── sfx — 게임 사건 ──

    // 골드 획득 — 동전이 상단 골드에 닿을 때 ('CoinFlyPresenter')
    public static AudioClip? CoinGain => SoundCatalog.Of("coin_gain");

    // 보상 공개 — 뽑기·감정 결과 · 레벨 업 ('GachaResultPresenter' · 'LevelUpFx')
    public static AudioClip? RewardChime => SoundCatalog.Of("reward_chime");
}

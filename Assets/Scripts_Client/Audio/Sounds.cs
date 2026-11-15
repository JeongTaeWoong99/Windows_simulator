using UnityEngine;

// 어떤 소리를 어디서 쓰는지 한 곳에 모은 이름표(트는 쪽은 Common 'SoundManager') — 파일은 그림 저장소 'Assets/Art/audio/<채널>/<키>.wav'.
// 소리를 바꾸려면 같은 이름으로 파일을 덮는다(코드는 그대로). 새 소리는 파일을 넣고 여기 한 줄을 더한다.
//
// ■ 소리 출처 — 2026-11-15부터 코드 합성(8비트 · Unity AI 크레딧 0): 'Art/_source/audio/claude-sfx/make_sfx.py'.
//   배경 음악 'bgm_idle'만 Unity AI(Lyria)다.
// ■ 여러 벌 중 하나 — 자주 나는 소리(골드)는 '<키>_1' · '_2' · '_3'을 두고 매번 하나를 고른다. 같은 소리만 반복되면 지루하다(2026-11-15 사용자).
public static class Sounds
{
    // ── bgm — 배경 음악 (반복) ──

    // 기본 배경 음악 'bgm_idle'은 코드가 틀지 않는다 — 씬 'Sound Manager'의 시작 배경 음악 키('startBgmKey')

    // ── ui — 버튼 · 창 ──

    // 버튼 누름 — 모든 버튼 ('ButtonPressFx')
    public static AudioClip? UiClick => SoundCatalog.Of("ui_click");

    // 안 됨 — 거절 흔들기와 함께 ('RefusalShake')
    public static AudioClip? Error => SoundCatalog.Of("error");

    // 팝업 열기 · 닫기 — 가운데 팝업(확인 · 수량 · 응축 · 큐브 · 아티팩트) ('PanelTransition.SetVisible' · 각 팝업의 Open/Close)
    public static AudioClip? PopupOpen  => SoundCatalog.Of("popup_open");
    public static AudioClip? PopupClose => SoundCatalog.Of("popup_close");

    // ── sfx — 게임 사건 ──

    // 골드 획득 — 동전이 상단 골드에 닿을 때 ('CoinFlyPresenter'). 세 벌 중 하나
    public static AudioClip? CoinGain => Pick("coin_gain", CoinVariants);

    // 보상 공개 — 뽑기·감정 결과 ('GachaResultPresenter')
    public static AudioClip? RewardChime => SoundCatalog.Of("reward_chime");

    // 레벨 업 — 작업슬롯 캐릭터 ('LevelUpFx')
    public static AudioClip? LevelUp => SoundCatalog.Of("level_up");

    // 응축 성공 (★ 그대로) · ★ 상승 / 큐브 등급 상승 ('CharacterCondensePresenter' · 'EnchantRevealFx')
    public static AudioClip? Condense => SoundCatalog.Of("condense");
    public static AudioClip? RankUp   => SoundCatalog.Of("rank_up");

    // 큐브 굴림 — 새 값 공개가 시작될 때 ('EnchantRevealFx')
    public static AudioClip? CubeRoll => SoundCatalog.Of("cube_roll");

    // 판매 완료 ('SellCartPresenter')
    public static AudioClip? Sell => SoundCatalog.Of("sell");

    private const int CoinVariants = 3;

    // '<키>_1' ~ '<키>_n' 중 하나 — 몇 벌이 빠져 있어도 있는 것으로 돌아간다
    private static AudioClip? Pick(string key, int count)
    {
        int start = Random.Range(0, count);

        for (int i = 0; i < count; i++)
        {
            AudioClip? clip = SoundCatalog.Of($"{key}_{(start + i) % count + 1}");

            if (clip != null)
            {
                return clip;
            }
        }

        return null;
    }
}

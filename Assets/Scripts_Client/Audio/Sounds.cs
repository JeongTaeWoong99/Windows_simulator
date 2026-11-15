using GameData;
using UnityEngine;

// 어떤 소리를 어디서 쓰는지 한 곳에 모은 이름표(트는 쪽은 Common 'SoundManager') — 파일은 그림 저장소 'Assets/Art/audio/<채널>/<키>.wav'.
// 소리를 바꾸려면 같은 이름으로 파일을 덮는다(코드는 그대로). 새 소리는 파일을 넣고 여기 한 줄을 더한다.
//
// ■ 소리 출처 — 2026-11-15부터 코드 합성(Unity AI 크레딧 0): 'Art/_source/audio/claude-sfx/make_sfx.py'.
//   결은 판타지 · 양피지(하프 · 종 · 종이 바스락 · 나무) — 높은 8비트 '뾱뾱'은 쓰지 않는다(2026-11-15 사용자).
//   버튼 'ui_click' · 보상 'reward_chime'은 예전 소리 그대로다(사용자가 예전 것이 낫다고 했다). 배경 음악 'bgm_idle'은 Unity AI(Lyria).
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

    // 팝업 열기 · 닫기는 따로 소리를 두지 않는다 — 여닫는 버튼 소리로 충분하다(두루마리 소리가 버튼과 겹쳐 이상했다 · 2026-11-15 사용자)

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

    // 큐브 굴림 — 새 값 공개가 시작될 때 ('EquipEnchantPresenter'). 수동은 한 번 '띡', 자동은 더 짧고 작은 '톡'
    //   자동은 빠르게 연달아 나서, 같은 소리면 정신없다(2026-11-15 사용자)
    public static AudioClip? CubeRoll     => SoundCatalog.Of("cube_roll");
    public static AudioClip? CubeRollAuto => SoundCatalog.Of("cube_roll_auto");

    // 뽑기 고등급 공개 — 차오름(영웅 0.45초 · 전설↑ 0.7초) · 터짐(영웅 < 전설 < 신화) ('GachaResultPresenter.RevealBigAsync')
    //   차오름 길이는 'RewardRevealSettings'의 Epic/TopChargeDuration에 맞춰 만들었다 — 그 값을 바꾸면 소리도 다시 굽는다
    public static AudioClip? GachaCharge(GlobalRarity rarity)
        => SoundCatalog.Of(rarity >= GlobalRarity.Legendary ? "gacha_charge_top" : "gacha_charge_epic");

    public static AudioClip? GachaBurst(GlobalRarity rarity) => SoundCatalog.Of(rarity switch
    {
        >= GlobalRarity.Mythic    => "gacha_burst_mythic",
        >= GlobalRarity.Legendary => "gacha_burst_legendary",
        _                         => "gacha_burst_epic",
    });

    // 도감 새 등록 — 장착한 아티팩트 칸이 반짝일 때 '띠링' ('ArtifactCodexPresenter')
    public static AudioClip? CodexRegister => SoundCatalog.Of("codex_register");

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

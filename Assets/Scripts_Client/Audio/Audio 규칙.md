# Audio 규칙

> 최종 업데이트: 2026-11-15 (2차 — 판타지 결로 다시 합성(하프·종·양피지 바스락·나무·북) · 버튼·보상은 예전 소리 · 큐브 자동 따로 · 뽑기 고등급 차오름·터짐) · 2026-11-15 (코드 합성 효과음 — 팝업 열기·닫기 · 거절 · 레벨 업 · 응축 · ★/등급 상승 · 큐브 굴림 · 판매 · 골드는 세 벌 중 하나 `<키>_1~3`) · 2026-10-31 (`SoundManager`·`SoundCatalog`를 Common `sound/`로 옮김 — 여기는 이름표 `Sounds`만 · 시작 배경 음악은 씬 `startBgmKey`) · 2026-10-31 (아트/검사가 `Sounds` 이름표를 목록에서 못 찾으면 ❌ · 신설 — 소리 목록 `SoundCatalog` · 찾는 이름 `Sounds`) · 대상: `Assets/Scripts_Client/Audio/`

**어떤 소리를 쓰는지 아는 곳.** 소리를 **트는** 것·음량 저장·소리 목록은 Common [`sound/`](<../Common/sound/sound 규칙.md>)(`SoundManager` · `SoundCatalog`)이고, 여기는 이 게임의 이름표 `Sounds`만 둔다.

## 1. 무엇이 여기 있나

| 파일 | 역할 |
|------|------|
| (Common) `SoundCatalog` 에셋 | 그림 저장소 `Assets/Art/Resources/SoundCatalog.asset` — **손으로 채우지 않는다**(메뉴 **아트/목록만 다시 쓰기**가 `Art/audio/`를 훑어 쓴다) |
| `Sounds.cs` | 코드가 쓰는 소리 이름 — `Sounds.UiClick`처럼 부른다. 키 문자열은 여기에만 |

## 2. 소리 하나 넣기

1. 파일을 `Assets/Art/audio/<bgm|ui|sfx>/<키>.wav`에 넣는다 — 임포트 설정은 [`Art 규칙.md`](<../../Art/Art 규칙.md>) "소리 넣기".
2. **아트/목록만 다시 쓰기**.
3. `Sounds`에 한 줄 — `public static AudioClip? Xxx => SoundCatalog.Of("<키>");`
4. 부르는 곳에서 `Services.Get<SoundManager>().PlaySfx(Sounds.Xxx)` (버튼은 `PlayUi`, 배경은 `PlayBgm`).

**바꾸려면 같은 이름으로 파일만 덮는다** — 코드는 그대로다.

## 3. 채널 — 설정 음량 줄과 1:1

| 채널 | 무엇 | 지금 부르는 곳 |
|------|------|----------------|
| `Bgm` | 반복 배경 음악 | 씬 `Sound Manager`의 `startBgmKey` = `bgm_idle` |
| `Sfx` | 게임 사건 | 골드 획득 `CoinGain`(`CoinFlyPresenter` — 첫 동전이 닿을 때 · `coin_gain_1~3` 중 하나) · 보상 공개 `RewardChime`(`GachaResultPresenter`) · 레벨 업 `LevelUp`(`LevelUpFx`) · 응축 `Condense` · ★/등급 상승 `RankUp`(`CharacterCondensePresenter` · `EnchantRevealFx`) · 큐브 굴림 `CubeRoll`(수동 '띡') · `CubeRollAuto`(자동 '톡' — 연달아 나서 따로)(`EquipEnchantPresenter`) · 뽑기 고등급 `GachaCharge`(영웅 0.45초 · 전설↑ 0.7초 — `RewardRevealSettings` 차오름 길이에 맞춤) · `GachaBurst`(영웅 < 전설 < 신화)(`GachaResultPresenter.RevealBigAsync`) · 판매 `Sell`(`SellCartPresenter`) |
| `Ui` | 버튼 · 창 | `ButtonPressFx` — 누르는 연출이 붙은 버튼 전부 `UiClick` · 거절 `Error`(`RefusalShake`) · 가운데 팝업 열기·닫기 `PopupOpen`·`PopupClose`(`PanelTransition` · 응축 · 큐브 · 아티팩트 창) |
| `Master` | 셋 모두에 곱한다 | — |

## 4. ⚠️ 소리가 없어도 돈다

그림 저장소를 받지 않은 PC면 목록이 없어 `Sounds.Xxx`가 **null**이다. `SoundManager.Play*`는 null을 조용히 건너뛴다 —
부르는 쪽이 null을 따로 막지 않는다.

대신 **파일 이름을 바꾸거나 키를 잘못 적어도 조용하다** — 그래서 **아트/검사**가 `Sounds`의 이름표를 하나씩 불러 보고, 목록에서 못 찾으면 `❌ 소리`로 알린다.

## 5. 넣지 않는 소리

바탕화면에 늘 떠 있는 앱이다. **자주 나는 소리(채취 타격·자원 획득 틱)는 넣지 않는다** — 사람이 누른 것과 드문 사건에만.

## 소리 결 — 판타지 · 양피지 (2026-11-15)

- 합성 소리는 하프·종·종이 바스락·나무·북만 쓴다. 높은 사각파 '뾱뾱'(8비트 · 아케이드)은 쓰지 않는다 — 사용자가 1차 시안을 그렇게 말했다.
- 생성기 `Art/_source/audio/claude-sfx/make_sfx.py`는 소리마다 크기(`level`)를 따로 둔다 — 짧은 '톡'이 평균 세기 맞춤 때문에 커지지 않게.
- 차오름 소리 길이는 `RewardRevealSettings`의 `EpicChargeDuration`·`TopChargeDuration`에 맞춰 구웠다. 값을 바꾸면 소리도 다시 굽는다.

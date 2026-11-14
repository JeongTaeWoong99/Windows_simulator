---
name: codixel
description: 움직임 없는 그림(대상·소품·몬스터 한 장)에 뼈를 넣어 흔들기·맞음·숨쉬기 같은 동작을 만들 때, 보석·불·나무 같은 소재를 코드로 그릴 때, 큰 그림(AI 원본·참고 그림)을 픽셀로 줄이는 방식을 고를 때 연다 — Unity AI 크레딧 0. 타격·불똥 같은 입자 이펙트는 `pixel-fx`, 있는 PNG를 쓰기만 하면 해당 없음.
---

> 최종 업데이트: 2026-11-14 (신설 — mankidev/codixel 이식 · PixelOE 경량판 `pixeloe_lite.py`)

# Codixel — 있는 그림을 뼈로 움직이고, 소재를 코드로 그린다

**원리:** Claude는 상상으로 복잡한 것(용·사람)을 그리면 못생겨진다. **있는 그림에서 시작해** 픽셀로 줄이고,
뼈를 넣어 코드로 움직이면 잘 나온다. 라이브러리 [codixel](https://github.com/mankidev/codixel)(MIT)의 `core/pixel.js`를 쓴다.
Claude 사용량만 쓴다 — Unity AI 크레딧 0.

| 잘 맞는 것 | 맞지 않는 것 |
|---|---|
| 정지 그림 한 장의 흔들림·맞음·숨쉬기·날갯짓(뼈 `P.Rig`) · 줄기·꼬리처럼 휘는 것(`P.bend`) · 보석 회전(`P.Gem`) · 불꽃(`P.Flame`) · 하늘·바위·나무 같은 배경 소재 | 달리기·공격처럼 팔다리 모양이 새로 생기는 동작 — AI 영상(`unity-ai-assets`) · 입자 이펙트 — `pixel-fx` |

## 위치

| 무엇 | 경로 |
|---|---|
| 라이브러리 (그림 저장소) | `Assets/Art/_source/fx/codixel~/core/pixel.js` — 끝 `~`라 Unity가 읽지 않는다. Node만 있으면 된다(설치 없음) |
| 원 스킬 문서 | `codixel~/SKILL.orig.md` — **API 전체 목록과 화풍 지침(색 띠·색상 이동·선)은 여기** |
| 샘플 | `codixel~/samples/make_samples.js` — 나무 흔들림(`P.bend`) · 허수아비 맞음(`P.Rig` 뼈 셋) · 보석 3종 회전(`P.Gem`) · 모닥불(`P.Flame`) |
| 미리보기 출력 | `codixel~/records/` (git 제외) |
| 파이프라인 | `python -I claude_gen.py run codixel --sheet <폴더>` — `_source/`에서. 바뀐 미리보기만 contact.png로 모은다 |

## 절차

1. **출발 그림을 고른다** — 우리 그림(`targets/` · `characters/` · `icons/`)이 있으면 그것. 없으면 사용자에게 참고 그림을 묻는다.
   남의 그림을 그대로 쓰면 라이선스를 한 번 짚는다.
2. **큰 그림이면 픽셀로 줄인다** — `ai_to_pixel.py <원본> <결과> <긴 변> --oe [--outline]`(PixelOE 방식) 또는 기본(최빈색).
   가는 선·무늬·눈이 있는 그림은 `--oe`가 낫다(지도 길·열쇠 무늬가 산다 — 2026-11-14 비교). 평평한 색 덩어리는 기본도 된다.
3. **뼈를 넣는다** — 관절 자리는 짐작하지 않는다: `P.ruler(sprite, 4, { step: 8 })`로 눈금 그림을 띄워 읽는다.
   `rig.ownerMap()` + `rig.legend()`로 **어느 픽셀이 어느 뼈인지 먼저 확인**하고, 틀린 곳은 `bind({ paint })`로 고친다.
   땅에 박힌 부분(기둥·뿌리)은 움직이지 않는 뼈로 따로 둔다 — 안 그러면 기둥까지 꺾인다(허수아비 샘플).
4. **움직인다** — `P.ease`·감쇠 사인으로 빨라졌다 느려지게. 작은 그림(64px 이하)을 크게 돌리면 가장자리가 깨진다 — 각도를 작게(0.2 rad 안팎).
5. **본다** — 번호 붙은 시트(`P.sheet`)를 Read로 보고 고친다. GIF는 사용자에게 준다(Read는 GIF 첫 장만 보인다).
6. **게임에 넣는 것은 사용자가 미리보기를 고른 뒤** — `P.pack`으로 프레임 묶음 PNG를 만들어 `Assets/Art/` 규칙 자리에 넣는다
   ([`Art 규칙.md`](../../../../Assets/Art/Art%20규칙.md)). 미리보기 단계에서 게임 파일을 만들지 않는다.

## 함정

- `P.load`는 평범한 PNG만 읽는다(인터레이스 PNG는 Pillow로 다시 저장).
- `P.Gem`은 크기 40 이상에서 깎은 면이 보인다. 24 이하는 덩어리로 뭉갠다 — 크게 그린 뒤 `P.resize(..., { mode: true })`.
- `P.Smoke`는 40px 안팎 그림에서 흰 기둥처럼 보였다(모닥불 샘플에서 뺐다).
- `P.Flame` 같은 흐름 효과는 처음 10~20프레임이 자라는 시간이다 — 버리고 쓴다. 이음매 없는 반복은 보장하지 않는다.
- 이 PC의 파이썬은 Pillow + numpy만 있다. PixelOE 원본(torch·opencv)은 깔지 않고 `pixeloe_lite.py`(numpy 이식)를 쓴다.

---
name: pixel-fx
description: 픽셀 이펙트(타격·불똥·파편·번쩍임·마법·연기 같은 움직이는 효과)를 새로 만들거나 고칠 때 연다 — Unity AI 크레딧 없이 Claude가 코드(pixelfx)로 그려 PNG 프레임으로 굽는다. 이미 있는 이펙트 PNG를 쓰기만 하거나, 움직임 없는 연출 이펙트 한 장(`claude-fx/make_fx.py` — 번짐·별 조각·자물쇠 등)을 고칠 때는 해당 없음.
---

> 최종 업데이트: 2026-11-11 (`fx/` 전부 점 필터 — 접두 조건 삭제) · 2026-11-08 (신설 — patch-gg/pixelfx 이식 · 산업별 타격 5종)

# 픽셀 이펙트 — Claude가 코드로 그린다

**원리:** 이펙트 하나 = JS 파일 하나. 시간 `t`마다 픽셀을 찍는 코드라, 렌더하면 프레임 PNG가 나온다.
AI 생성(Unity AI 크레딧)이 아니라 **Claude 사용량만** 쓴다 — 요금제의 남는 사용량으로 만들 수 있다.
방법은 [pixelfx](https://github.com/patch-gg/pixelfx)(MIT · Hugo Duprez의 [How to make (good) pixel art effects with Claude](https://www.spritefusion.com/blog/how-to-make-good-pixel-art-effects-with-claude))를 따른다:
**한 번에 잘 나오지 않는다 — 그리고, 렌더하고, 눈으로 보고, 고친다.**

| 잘 맞는 것 | 맞지 않는 것 |
|---|---|
| 타격·불똥·파편·연기·빛·마법·투사체·상태 이상 — 입자와 도형 | 캐릭터 몸의 달리기·공격 동작 — 팔다리를 새로 그려야 해서 AI 영상(`unity-ai-assets`)이 낫다 |

## 위치

| 무엇 | 경로 |
|---|---|
| 라이브러리 (그림 저장소) | `Assets/Art/_source/fx/pixelfx~/` — 끝 `~`라 Unity가 읽지 않는다. `src/core`(엔진) · `src/fx/<분류>/`(효과 244종 — **참고 자료**) · `tools/` |
| **우리 효과** | `src/fx/slot/` — `_lib.js`(공용 번쩍임·고리·규격) · `hit-<산업>.js` |
| Unity로 내보내기 | `tools/export-unity.mjs` — 맨 위 `EXPORTS` 목록에 한 줄 |
| 결과 | `Assets/Art/fx/<폴더>/<이름>_<번호>.png` — 규칙은 [`Art 규칙.md`](../../../../Assets/Art/Art%20규칙.md) "타격 이펙트 `fx/hit/`" |
| 원본 문서 | `pixelfx~/docs/AUTHORING.md`(효과 작성 계약 · 품질 기준) · `skill/SKILL.md`(원 스킬) · `docs/CATALOG.md`(전체 목록) |

Node 18 이상만 있으면 된다(설치 단계 없음). `records/`는 미리보기 출력이라 git에 넣지 않는다(`.gitignore`).

## 순서

모든 명령은 `Assets/Art/_source/fx/pixelfx~/`에서 돌린다.

1. **비슷한 것부터 찾는다** — `node tools/list.mjs <단어>`(예: `spark`, `dust`, `slash`). 가까운 효과 2~3개의 JS를 읽고 기법을 빌린다.
   입자 `Particles`(`P.SPARK`·`GRIT`·`DUST`·`SMOKE` …) · 굴러떨어지는 조각 `Bits`(`B.CHUNK`·`SHARD`) · 빛 `Lights` — `src/core/sim.js`.
2. **한 줄로 정한다** — 무엇이, 어떻게 움직이고, 무슨 색이고, 얼마나 긴지. 예: "곡괭이가 바위를 때림: 흰 번쩍임 + 깨지는 고리, 금빛 불똥 부채꼴, 돌 조각이 굴러 떨어짐, 0.3초".
3. **초안** — `src/fx/slot/<이름>.js`. 규격은 `_lib.js`의 `SIZE`·`ORIGIN`·`DURATION`·`FLOOR`를 쓴다(화면 규격과 같다). 방향은 **오른쪽으로 튀게** 그리고 내보낼 때 `flip`.
4. **보고 고친다 — 최소 세 번.** `node tools/render.mjs slot/<이름> --all --zoom 4 --no-actor --opts '{"flip":true}'` → `records/slot/<이름>/contact.png`를 Read로 본다.
   - 덩어리가 점으로 흩어진다 → `Bits`의 `life`가 짧아 반투명 디더가 걸린 것. `life: 2`로 두고 `draw`에서 끝 시간에 그리기를 끊는다
   - 밋밋하다 → 첫 장에 흰 번쩍임, 빠르게 나가고 천천히 멈춘다, 색 수를 줄인다
   - 캔버스 밖으로 나간다 → 속도를 줄인다(`check`가 알린다)
5. **검사** — `node tools/check.mjs slot` — 캔버스 밖 · 끝 장이 비지 않음 · 무작위 씨앗 누락을 잡는다. 경고는 고쳐서 없앤다.
6. **내보내기** — `tools/export-unity.mjs`의 `EXPORTS`에 `{ id, dir, name, opts }` 한 줄 → `node tools/export-unity.mjs <id>`.
   빈 끝 장은 버리고, 줄어든 번호의 옛 파일은 지운다.
7. **Unity** — 에디터에서 새로 고침(플레이 중이면 미룬다) → 메뉴 **아트/목록만 다시 쓰기**. 움직임은 메뉴 **아트/이펙트 미리보기**에서 PNG 하나를 누르면 이어 재생된다. 타격(`hit_`)은 점 필터가 자동으로 붙는다.
   `fx/` 아래는 전부 점 필터다(2026-11-11) — 접두를 따로 더하지 않는다.

## 규칙

- **PNG는 원본이 아니다** — 모양을 바꾸려면 JS를 고쳐 다시 내보낸다.
- `Math.random` 금지 — `new Rng(o.seed)`만 쓴다(같은 결과가 다시 나와야 한다).
- 캐릭터 시트에 넣지 않는 것(맞는 대상 · 타격 · 파편)은 이렇게 따로 만든다 — [`unity-ai-assets`](../../common/unity-ai-assets/SKILL.md) "캐릭터 시트".
- 다른 게임의 특정 스프라이트·캐릭터·로고를 베끼지 않는다. 일반적인 모티프(불꽃·별·조각)는 괜찮다.
- 라이브러리를 새 버전으로 바꿀 때는 `src/core`·`tools/lib`만 덮고 `src/fx/slot/`·`tools/export-unity.mjs`는 남긴다. 받은 커밋을 `Art 규칙.md` 원본 목록에 고친다.

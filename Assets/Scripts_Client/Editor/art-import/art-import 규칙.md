# art-import 규칙

> 최종 업데이트: 2026-10-04 (연속 공격 · T-097) · 대상: `Assets/Scripts_Client/Editor/art-import/`

`Assets/Art/`의 원본 그림을 같은 규격의 결과물로 굽는 에디터 도구다.
**규격·폴더·새 그림 넣는 절차는 [`Art 규칙.md`](<../../../Art/Art 규칙.md>)에 있다** — 여기는 코드 쪽만 적는다.

| 파일 | 하는 일 |
|---|---|
| `ArtSpec.cs` | 경로 · 메뉴 뿌리 · 규격 상수(칸 크기 · PPU · 불투명 기준) — **규격을 바꾸면 여기와 `Art 규칙.md`를 함께 고치고 전부 다시 굽는다** |
| `ArtImage.cs` | 픽셀 버퍼 — 자르기 · 뒤집기 · 축소(가장 가까운 픽셀 / 알파 가중 평균) · 겹치기 · 테두리 · 발 위치 · 흰 실루엣 · PNG 저장 |
| `CharacterRecipe.cs` · `BackgroundRecipe.cs` · `TargetRecipe.cs` | 레시피 SO — 무엇을 어떻게 가공할지. 우클릭 **이 레시피 굽기** |
| `ArtBaker.cs` | 굽기 — 레시피 → PNG + 결과 SO(`CharacterVisual`·`BackgroundVisual`·`TargetVisual`). 메뉴 **아트/전부 다시 굽기** |
| `ArtImportPostprocessor.cs` | `characters/`·`backgrounds/`·`targets/`의 임포트 설정 고정. `_source/`는 건드리지 않는다 |
| `ArtValidator.cs` | 결과물·목록 검사 — 고치지 않고 알린다(❌ · ⚠️ · ℹ️). 메뉴 **아트/검사** |

결과 SO의 런타임 쪽은 `UI/Shared/visual/`에 있다(빌드에 들어가야 하므로 Editor 밖).

## 함정

- **원본 픽셀은 PNG 파일에서 직접 읽는다** — 텍스처의 `Read/Write`를 켜지 않아도 되고, 원본 임포트 설정(압축·최대 크기)에 휘둘리지 않는다.
  스프라이트 사각형은 파일 너비 ÷ 텍스처 너비로 늘려 맞춘다.
- **발 위치는 기본(idle) 첫 프레임 하나로 정하고 모든 프레임에 같은 오프셋을 쓴다.** 프레임마다 맞추면 달리기가 위아래로 떨린다.
- **띠를 다시 구워도 칸 스프라이트 ID는 이름으로 유지한다**(`ISpriteEditorDataProvider`) — 바꾸면 결과 SO의 참조가 끊긴다.
- **타격 프레임은 공격(타)마다 자동으로 고른다** — 앞쪽(왼쪽) 끝이 가장 크게 뻗는 프레임. 어긋나면 그 타의 `hitFrameOverride`(0 = 자동 — 배열에 새 칸을 넣으면 0이라 자동이 기본이 된다).
- **공격 수가 줄면 남는 띠(`_attack_N`)와 예전 단일 띠(`_attack`)를 굽기가 지운다.**
- **배경 이음매 검사는 경고만 한다** — 원본이 처음부터 이어지지 않는 팩도 있다. 거슬리면 원본을 바꾼다.

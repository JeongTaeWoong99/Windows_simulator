---
date: 2026-09-12
title: 씬에 붙지 않는 UI 부품에 자리를 만들었다 — UI/Shared/ 와 거짓 이름 3건 개명 (T-049)
tags: [client, ui, editor, docs]
---

# UI/Shared/ 신설과 거짓 이름 정리 (T-049)

→ 범위·단계별 결과는 `tasks/T-049-UI공유부품정리.md`, 자격·배제 기준은
`Assets/Scripts_Client/UI/Shared/Shared 규칙.md`.

## 목적 / 배경

**폴더가 하이어라키의 거울인데, 씬에 붙지 않는 파일은 비칠 대상이 없었다.**
`UI/`의 규칙은 `<캔버스>/<Presenter>/`인데 프리팹·씬에 붙지 않는 파일에는 대응 오브젝트가 없어
**갈 곳이 정해져 있지 않았다.** 그래서 *"처음 쓴 화면"* 의 폴더에 놓였고, 그 순간
**위치가 소유권을 거짓으로 주장했다.**

- `InventorySlotView`·`StorageSlotData`는 `Storage/StorageGridPresenter/`에 있었지만
  **`!System Canvas`의 가챠 결과 팝업이 같은 클래스·같은 프리팹을 쓴다** — 창고를 고치면
  팝업이 조용히 따라 바뀐다. **이름의 `Inventory`·`Storage`도 거짓이었다.**
- `RarityPalette`·`ResultMessages`·`WorkStationProgress`·`IndustryLabel`은 `UI/System/`에
  있었는데 `#System Canvas`와 **무관**하다. `System 규칙.md`가 *"어디에도 속하지 않는 것들"* 이라
  **적어 두고도 자리를 만들어 주지 못한 상태**였다.
- `StorageInformationPresenter`는 2026-09-04에 판매 목록이 그 자리를 차지해
  **이름과 역할이 어긋난 채** 문서에 미뤄져 있었다.

여기에 **T-048이 남긴 낡은 서술 7건**(표시가 스트립으로 바뀌었는데 주석·문서가 안 따라왔다)과
**씬 오브젝트 표기 2건**이 함께 나왔다.

## 주요 결정 / 근거

| 갈림길 | 결정 | 근거 |
|--------|------|------|
| 가르는 축 | **캔버스/Presenter 유지 + 예외 폴더 하나** | 역할별 상위 폴더(`UI/Presenters/`·`UI/Views/`)로 가르면 *"폴더를 열면 이 화면이 무엇으로 이뤄졌나 보인다"* 는 성질과 캔버스별 `<폴더명> 규칙.md` 체계가 **동시에** 무너지고, 한 화면을 고치려고 세 폴더를 오간다 |
| 공급자 3개 | **옮기지 않았다** | 창고 격자 전용이라 `Storage` 접두가 **사실**이다. `Sources/` 하위 폴더도 안 만들었다 — 이동만으로 그 폴더가 Presenter 1 + 공급자 3이 된다 |
| 개명 범위 | `SlotView` · `SlotData` (`Inventory`·`Storage` 제거) | 소유자가 여럿인 것의 이름에 한 소유자를 박으면 **다음 사람이 다른 화면을 못 본다** |
| `SellCartPresenter` 접두 | **`Storage`를 붙이지 않는다** | `SellCartModel`과 짝이 맞는 고유한 이름이다. `MenuPresenter`·`GachaPresenter`도 접두가 없다 |
| 씬 표기 | 낱말을 **띄운다** (`Amount Input Presenter`) | 15개 중 둘만 붙여 써서 어긋나 있었다. `WorkStation`은 **한 낱말인 도메인 용어**라 그대로 둔다 |
| 마스터 반영 | **예시 이름 + `Shared/` 규칙만** | 프로젝트 폴더·클래스 이름은 올리지 않는다(전역 CLAUDE.md). 예시도 중립적인 `ItemGridPresenter`·`ItemSlotView`로 바꿨다 — 개명 반영이 아니라 **규칙에 맞춘 것** |
| 대안 (기각) | 이름만 고치고 자리는 둔다 | 자리가 그대로면 **다음에 또 같은 파일이 쌓인다.** 규칙에 자리가 없는 것이 원인이었다 |

## 지뢰 / 주의사항

- 🔴 **`m_EditorClassIdentifier`는 유니티가 다시 써 주지 않는다.** 씬·프리팹에 **옛 클래스 이름이
  문자열로** 남는다. 참조 자체는 GUID라 안 끊기지만 텍스트만 낡는다 —
  `EditorUtility.SetDirty` + `SaveOpenScenes`로도 안 바뀌어 **파일을 직접 고치고 재임포트**했다.
  개명 뒤에는 **`grep`으로 이 필드를 따로 확인한다.**
- ⚠️ **개명은 파일명과 클래스명을 같은 단계에서 바꾼다.** 한쪽만 바꾸면 MonoBehaviour가
  *"script cannot be loaded"* 로 떨어져 **프리팹·씬 배선이 끊긴다.**
- ⚠️ **`[SerializeField]` 필드 이름은 건드리지 않았다**(`slotPrefab`). 타입만 바뀌어서
  인스펙터 배선이 살아남았다 — 필드 이름까지 바꾸면 `[FormerlySerializedAs]`가 필요하다.
- 🔴 **개명이 규칙 문서를 무효화할 수 있다.** `UI 규칙.md`의 이름 규칙이
  *"`StorageInformationPresenter`처럼 캔버스 이름을 앞에 단다"* 였는데 `SellCartPresenter`는 접두가 없다.
  **그 문장을 함께 고치지 않으면 다음 사람이 이 개명을 규칙 위반으로 읽는다** —
  "접두는 규칙이 아니라 수단이다"로 다시 썼다.
- `AssetDatabase.MoveAsset`/`RenameAsset`은 **`.meta`를 함께 움직여 GUID를 유지한다.**
  6개 이동 + 3개 개명 뒤에도 씬·프리팹 배선이 전부 살아 있었다(코드로 필드별 확인).
- 씬 diff는 **4줄뿐**이다(오브젝트 이름 3 + 클래스 문자열 1). T-046 때처럼 `RectTransform`이
  대량으로 다시 쓰이지 않았다 — 이름만 건드리면 그렇다.
- Unity MCP가 도메인 리로드 중이면 `Could not load file or assembly ...RunCommand.Dynamic...`으로
  한 번 실패한다. **같은 명령을 다시 보내면 된다** (코드 문제가 아니다).

## 검증 (2026-09-12)

- 컴파일 — 단계마다 `AssetDatabase.Refresh()` → 콘솔 Error **0건.**
- 프리팹 `SlotView.prefab` — 8필드 + `aptitudeValueTexts` **5/5** 배선 유지.
- 씬 — `StorageGridPresenter.slotPrefab`·`GachaResultPresenter.slotPrefab` 둘 다 `SlotView`,
  `SellCartPresenter`의 참조 6개·`AmountInputPresenter` 5개 전부 유지.
- 잔재 검색 — 옛 이름·옛 경로 10종이 `.claude/Agent/`·`tasks/archive/` 밖에서 **0건**
  (남은 것은 *"예전 `InventoryPresenter`는…"* 처럼 **과거임을 명시한 서술**뿐이다).
- 스킬 — 사본↔마스터 diff가 **기존 공동소유 배너 3줄뿐**(내용 동일).

## 후속 작업 / 주의사항

- ⏳ **플레이 확인이 남았다** — 창고 두 탭 · 가챠 결과 팝업 · **판매 담기 → 일괄 판매** · 수량 팝업.
  5·6단계가 씬을 만졌으니 뒤 둘이 핵심이다.
- **`Shared/`는 한 방향으로만 쓰는 폴더가 아니다.** 파일이 한 캔버스만 쓰게 줄어들면
  **그 폴더로 내린다** — 공용 폴더는 *지금의* 사실을 적는 자리다(`Shared 규칙.md`에 못박았다).
- `IndustryLabel.cs`는 `Shared/`로 옮겼지만 **T-047이 끝나면 삭제될 파일**이다.
  T-047 본문의 경로·호출부 목록도 이 작업에서 함께 고쳤다(호출부가 둘 → 하나로 줄었다).
- 칸의 상세 정보는 **판매 패널로 돌아오지 않는다** — 커서 옆 호버 UI로 간다(T-050 등록).

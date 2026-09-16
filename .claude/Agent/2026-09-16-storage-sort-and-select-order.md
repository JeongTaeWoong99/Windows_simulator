---
date: 2026-09-16
title: 창고 [정렬] 버튼 · 배치 목록 적성순 · 캐릭터 줄 등급 색
tags: [client, ui, storage, workstation]
---

# 창고 [정렬] 버튼 · 배치 목록 적성순 · 캐릭터 줄 등급 색

## 목적 / 배경
- T-015 남은 메인 두 항목(창고 필터·정렬 · 하단 탭 자리)과 T-057(배치 목록 정렬)을 한 번에 마무리했다.
- 대화 중 방향이 바뀌었다: 필터 UI 대신 **[정렬] 버튼 하나**를 두고, 칸 위치는 서버 몫(T-058)으로 넘겼다. → `tasks/T-015` · `tasks/T-058` · `tasks/T-044`

## 변경 내용
- `StorageSlotSource` — `Sort`(규칙으로 줄 세우고 Key별 자리 기억) · `ClearOrder` · `Rebuild`가 기억한 자리를 따른다. 파생은 `CompareForSort` 구현
- `ResourceSlotSource`·`CharacterSlotSource` — 정렬 규칙 (규칙 표는 `Storage 규칙.md`의 "[정렬] 버튼" 절)
- `StorageGridPresenter.SortCurrent` · `OnLoginCompleted`(기억 초기화) / `StorageTabPresenter.sortButton`(선택 참조)
- `WorkStationSelectPresenter.CompareRows` — 적성↓ → 등급↓ → 개체 번호 / `CharacterStateRowView.SetRarity`·`backgroundImage`
- `GameDataLoader.GetItemType` · `PlayerDataModel.GetCharacterTid` 추가
- 프리팹 `CharacterStateRowView.prefab`에 `backgroundImage` 연결(YAML 직접), 씬 `Tab Presenter`에 `Sort Button` 추가(Unity MCP · **씬 저장은 사용자**)
- 기획 `ui/README.md` Q12 해소 — 하단 메뉴 줄 = 특별 이벤트 자리

## 주요 결정 / 근거
- **필터를 버렸다** — 탭이 칸 200개를 함께 쓰는 격자라 일부만 보이면 빈 칸이 "사라진 아이템"처럼 읽힌다(사용자 판단 · 캐주얼 방치형 관례).
- **정렬은 누를 때만, 그 뒤엔 기억한 자리** — 매 `Rebuild`마다 규칙으로 줄 세우면 채취할 때마다 칸이 뒤섞인다. 새 항목은 도착 순서대로 뒤에.
- **기억한 쪽과 새 항목을 한 비교자로 섞지 않았다** — `List.Sort`가 불안정이라 새 항목끼리 순서가 흔들린다. 둘로 갈라 정렬 후 이어 붙인다.
- 비교 규칙은 **끝까지 동점이 없게**(개체 번호·TID까지) — 같은 이유.
- 산업 순은 TID 대역이 아니라 `ItemType` 값 — TID로는 낚시(1xxxx)가 농사(2xxxx) 앞에 와 배치 화면·적성 스트립과 어긋난다.
- 로그인 구독은 `OnDisable`에서 풀지 않고 `OnDestroy`에서 — 창고가 닫힌 채 재로그인해도 기억을 버려야 한다.
- 줄 바탕은 등급 색 **그대로**(알파 조정 없음) — 사용자 선택. 고급(형광 초록)에서 글자 대비가 약할 수 있다는 점은 알렸다.

## 업데이트 (2026-09-16) — 정렬 UI를 도구 줄로 옮기고 일괄 담기를 얹었다

- 탭 줄에 넣었던 [정렬] 버튼을 **걷어내고**, 탭 줄 아래 낮은 줄(`StorageToolPresenter` · 높이 36)로 옮겼다.
  탭 네 개 사이에 성격이 다른 버튼이 끼면 "무엇이 화면을 고르는 것인가"가 흐려진다(사용자 지적).
- 화살표는 **방향 토글**이다 — `StorageSortOrder`로 오름/내림을 오가고, 오름차순은 `CompareForSort` 결과를 통째로 뒤집는다(기준을 하나만 뒤집으면 표를 봐야 안다).
- 같은 줄에 **일괄 담기**(등급 범위 드롭다운 + [판매])를 넣었다 → `tasks/T-056`. 담기까지만 하고 팔지 않는다.
- 캐릭터·장비 탭의 일괄 담기는 **잠그지 않고 알림**을 띄운다(사용자 지시) — 창구가 없어 `ServerWaitManager.RaiseNotice`를 새로 열었다(대기를 열지 않는 알림 전용).
- 도구 줄은 **자기 오브젝트를 끈다.** 대신 탭 구독을 `Start`/`OnDestroy`에 걸었다 —
  `OnDisable`에서 풀면 다시 켤 신호를 못 받아 특성 탭에서 영영 안 돌아온다.
  처음엔 자식만 껐는데, 배경과 줄 높이(40)가 **빈 띠로 남아** 사라진 것으로 안 보였다.
- 화살표·[판매]는 임시로 문자다(▲▼ 글리프는 neodgm_pro에 있다). 🎨 스프라이트가 오면 Image로 바꾼다.

### 폴리싱 (같은 날, 사용자 지적)

- 여백이 규칙에서 벗어나 있었다(`4,4,2,2`). **버튼 줄은 `5 5 5 5` · spacing 5**다 → 고치면서
  줄 높이도 36 → **40**(위젯 30 + 여백 5+5)이 됐다. 격자는 `flexH 1`이라 알아서 390으로 줄었다.
- 배경이 없어 탭 줄과 어긋났다 → `Background`(Sliced) 추가 + 컴포넌트 순서를 탭 줄과 동일하게 맞췄다.
- **일괄 담기를 "더하기"에서 "다시 잡기"로 바꿨다** — 누를 때마다 `SellCartModel.Clear()` 후 담는다.
  덮어쓰기(`Add`)만으로는 **범위를 좁혀도 이전 범위가 남는다**(영웅 이하 → 일반 이하인데 영웅·희귀가 그대로).
  화면의 `○○ 이하`와 팔릴 것이 어긋나는데, 목록을 끝까지 훑기 전에는 안 보인다. 우클릭으로 담은 것도 함께 빠진다.
- 🔴 **등급 이름은 엑셀에서 오지 않는다** — `StorageToolPresenter.RarityNames`가 코드 사본이다.
  `Enum.xlsx`의 `Desc`는 `Enum.cs`에 **주석으로만** 나간다(`// 일반, 흰색/회색 #9D9D9D`).
  `IndustryLabel.cs`와 똑같은 구멍이라 [`T-047`](../../tasks/T-047-산업이름출처.md)에 등급까지 포함해 적어 두었다.

## 후속 작업 / 주의사항
- **T-057은 플레이 확인 통과로 보관했다**(`a39b026`). 창고 도구 줄(T-015 정렬 · T-056 일괄 담기)은 **플레이 확인 전이다.**
- 씬 `Assets/Scenes/Original/DesktopWindow_Control.unity`는 dirty 상태 — 사용자가 저장해야 `Sort Button`이 남는다.
- ⚠️ `Menu Presenter`에 **비활성 `xxx Button (3~5)` 세 개가 이미 보인다** — 기획 2.4 "잠긴 표시를 보여 주지 않는다"와 어긋난다. 끌지 말지는 사용자 결정으로 남겼다.
- T-044 착수 시 `_rank`·`ClearOrder`·`OnLoginCompleted`를 걷어내고 서버 칸 번호로 채운다(빈 칸은 빈 채로).

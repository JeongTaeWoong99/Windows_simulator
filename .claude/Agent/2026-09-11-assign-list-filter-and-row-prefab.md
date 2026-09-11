---
date: 2026-09-11
title: 배치 목록을 걸러 내는 쪽으로 뒤집고 줄을 프리팹 풀로 전환 (T-046)
tags: [client, ui]
---

# 배치 목록 산업 필터 · 줄 프리팹화 · 창고 '배' 마크 (T-046)

## 목적 / 배경

작업슬롯 배치 목록(2단계)이 "지금 고를 수 있는 것"을 못 보여 줬다.
씬에 줄이 **1개뿐**이라 16마리 중 1마리만 보였고(문서는 "21줄"이라 적고 있었다 — 낡은 서술),
산업을 눌러도 적성 0인 줄을 잠그기만 할 뿐 목록에 그대로 남았다.

→ 범위·결정 항목은 `tasks/T-046-배치목록필터.md`, 화면 배치는 `Assets/Scripts_Client/UI/UI 배치 현황.md`.

## 주요 결정 / 근거

- **"숨기지 않고 잠근다" → "건다"로 뒤집었다** (사용자 결정). 성립 조건은 **창고 캐릭터 탭이
  보유 목록의 진실을 대신 답하는 것**이라, 창고 쪽 보강(마크·적성 요약)이 같은 일감에 묶여 있다.
  창고를 손대지 않은 채 목록만 걸러 내면 "내 캐릭터가 어디 갔나"의 답이 **어디에도 없어진다.**
- **줄 풀은 창고 격자가 아니라 판매 줄(`StorageInformationPresenter`)을 베꼈다.**
  일감 원문은 창고(`StorageGridPresenter`)를 가리켰지만, 창고는 **프레임 200개가 씬에 고정**이라
  프레임 개수가 그대로 상한이 된다. 캐릭터 목록은 **보유 수가 곧 줄 수**라 같은 문제가
  형태만 바꿔 돌아온다 → 프레임 없이 `Content`(VerticalLayoutGroup + ContentSizeFitter)에 직접 쌓는다.
- **`CharacterStateRowView`의 적성 0 잠금을 걷어냈다.** 목록이 걸러 내면 그 줄은 생기지 않는다.
  관문을 둘 두면 다음 사람이 어느 쪽이 진짜인지 알 수 없다. `SetAssignable`은 **응답 대기 전용**으로 남았다.
- **3단계 산업 버튼도 적성으로 잠갔다**(`CanSelectIndustry`). 2단계만 걸러 내면 같은 화면이 두 말을 한다.
- **창고 칸 보조 문구를 적성 요약으로 바꿨다.** 배치 여부를 마크가 말하게 되면서 문구와 중복됐고,
  걸러 낸 캐릭터의 적성을 볼 곳이 창고뿐이라 그 자리를 적성에 넘겼다.

## 지뢰 / 주의사항

- 🔴 **줄을 만든 뒤 `LayoutRebuilder.ForceRebuildLayoutImmediate(rowParent)`를 반드시 부른다.**
  uGUI 레이아웃은 프레임 맨 끝에 도는데, 그 사이 `WidgetPositionLayout.VerifyNoOverflow`가
  `LateUpdate`에서 훑어 "자식이 부모보다 넓다 → 해소됐다"가 왕복으로 찍힌다(판매 줄에서 겪은 그대로).
- **안내 문구를 `Content` 안에 두지 않는다.** 처음엔 "숨긴 N마리" 한 줄을 `Content`의 마지막 자식으로
  넣고 매번 `SetAsLastSibling()`으로 내렸는데, **실물에서 마지막 줄과 겹쳐 보였다**(같은 층에 쌓이니까).
  판매 목록처럼 **스크롤 패널의 자식으로 목록 위에 겹쳐** 두고 **빈 목록일 때만** 켠다
  (`StorageInformationPresenter`의 `emptyText`와 같은 형태 — 문구는 씬에 두고 코드는 켜고 끄기만 한다).
- ⚠️ **잠긴 버튼은 `colors.disabledColor`로 칠해진다.** 산업 버튼 3색(노랑/하양/회색)에서
  회색을 `normalColor`에 넣으면 잠그는 순간 무시되고 직전 색이 남는다.
- **배치 판정은 `PlayerDataModel.FindSlotIndexOf` 하나만 쓴다.** 배치 목록과 창고가 같은 것을 보므로
  각자 `WorkStationSlots`를 훑으면 두 화면이 다른 말을 한다.
- 🔴 **한글 산업 이름을 런타임에 주는 곳이 없어 코드에 사본을 만들었다** — `UI/System/IndustryLabel.cs`.
  진짜 출처는 `Enum.xlsx`인데 `EnumGenerator`가 그 이름을 **`// 농사` 주석으로만** 내보낸다.
  표기는 기획 단일 진실을 따라 **채굴**로 넣었고, **엑셀·`Enum.cs` 주석은 "채광"이라 지금 어긋나 있다.**
  → 출처를 엑셀로 되돌리는 일감 **`tasks/T-047`** 을 세웠다. 그게 끝나면 이 파일은 지운다.
  ⚠️ T-047에 적어 둔 함정: `Desc` 칸을 그대로 쓰면 안 된다 — 메모 칸이라
  `GlobalRarity`의 `Desc`는 `"일반, 흰색/회색 #9D9D9D"`다. 표시 이름 전용 컬럼이 필요하다.
- `InventorySlotView`의 마크 둘(`sellMark`·`assignMark`)은 **자리가 같지만 탭이 갈라** 겹치지 않는다.
  자원 탭 = 판매, 캐릭터 탭 = 배치. 가챠 결과 팝업은 둘 다 부르지 않아 늘 꺼져 있다.

## 에디터 작업 — Unity MCP로 처리했다 (같은 날)

프리팹 추출·안내 문구 생성·배선·`Assign Mark` 추가까지 `Unity_RunCommand`로 끝냈다(씬 저장 완료).

- **`rowParent`는 다시 잡지 않았다.** 필드 타입을 `Transform` → `RectTransform`으로 바꿔도
  씬에 저장된 것은 그 `RectTransform` 컴포넌트의 fileID라 **참조가 그대로 살아남는다.**
  (`LayoutRebuilder`가 `RectTransform`을 요구해 바꾼 것이다)
- 🔴 **씬을 저장하면 RectTransform 값이 대량으로 다시 쓰인다** — 이번에 `anchoredPosition`·
  `sizeDelta` 약 270곳이 바뀌었다. **LayoutGroup이 런타임에 계산해 둔 값이 파일에 내려앉는 것**이고
  (씬은 저장 전까지 `isDirty=false`였다) 동작은 같다. **구조 변화는 줄 4개 삭제 + 안내 문구 1개 추가뿐이다** —
  `git diff`에서 GameObject 블록만 세어 확인했다. 다음에 씬을 건드릴 때도 같은 크기의 diff가 나온다.
- 프리팹 루트 이름은 `Character State Row` → **`CharacterStateRowView`** 로 바꿨다(파일명·다른 프리팹 관례).

## 후속 작업

- ⏳ **플레이 재확인이 남았다** — 1차 확인(2026-09-11)에서 UI 둘을 고쳤다(안내 문구 자리 · 버튼 전이색).
- `tasks/T-047` — 산업 이름의 출처를 엑셀로. 끝나면 `IndustryLabel.cs`를 걷어내고
  씬 버튼 라벨도 코드가 채우게 한다(그래야 이름의 출처가 하나가 된다).
- `T-035`의 (c)(3단계에 배치된 캐릭터 정보 표시)는 그대로 남아 있다 — 같은 화면이라 함께 하면 싸다.

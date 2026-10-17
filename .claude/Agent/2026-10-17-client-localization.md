---
date: 2026-10-17
title: 클라 화면 문구 전체를 번역 키로 (KR·EN) — T-136
tags: [client, ui, localization, data]
---

# 클라 화면 문구 전체를 번역 키로 (KR·EN) — T-136

## 목적 / 배경
- 설정에서 언어를 KR·EN으로 고르면 화면 전체가 바뀌게 한다. EN 문구는 Claude 초안(사용자 지시 2026-10-17).
- 사용자 지시: "순차적으로 작업하고 이상없나 확인하고 커밋" — 화면 묶음마다 바꾸고 → EN 캡처로 확인 → 커밋을 되풀이했다. 사용자 실측 뒤 화면별 커밋 12개를 하나로 합쳤다.

## 변경 내용
- 코드 문자열 → `Lang.Get/Format("UI.<화면>.<이름>")`, 씬 고정 TMP → `LocalizedText` 키. 키는 `GameDesign/Excel/Localization/Loc_UI.xlsx`(UI.* 637개).
- 화면마다 `Lang.Changed`로 다시 그린다. `UIManager.MainScreenEntry.title` → `titleKey`(`FormerlySerializedAs`).
- 인벤토리 [제외] 분류·경매장 분류를 **언어 무관 ID**로 바꿨다 — `AuctionText.GetItemCategoryId`/`GetItemGroupName`, `ResourceSlotSource.GroupOf`/`GroupName`. 저장 키 `Inventory.BulkExcludeGroupIds`(새 키).
- 문서: `Data 규칙.md` 5절(다시 그리기 규칙 · 한국어로 둔 것), `Inventory 규칙.md` [제외], `Main 규칙.md` 제목 키.

## 주요 결정 / 근거
- **저장·비교에는 화면 문구를 쓰지 않는다.** [제외] 체크를 이름으로 저장하면 언어를 바꾸는 순간 풀린다 → ID 저장. 예전 키의 체크는 버리고 기본값(큐브·상자·유물)으로 시작한다(옮기는 코드를 두지 않음 — 이름→ID 역변환이 KR 고정 문자열을 남긴다).
- **꺼질 때 구독을 끊는 화면은 켜질 때 언어 처리를 다시 부른다.** 경매 검색 탭이 꺼진 사이 언어를 바꾸자 트리·드롭다운이 옛 언어로 남았다(Refresh는 드롭다운을 안 채운다). Search·History·Register의 `OnEnable`이 `OnLanguageChanged()`를 부른다. 스스로 꺼지는 줄(도구 줄·가챠)은 Start/OnDestroy 구독.
- 드롭다운은 고른 값을 지킨 채 다시 채운다(`SetValueWithoutNotify`) — 선택 이벤트가 요청을 다시 보내지 않게.
- 개발 로그·에디터 도구·오브젝트 이름·`CanSendSlotRequest` 라벨·레이아웃 진단·언어 이름 `한국어`는 일부러 한국어로 둔다.

## 후속 작업 / 주의사항
- **문자열 스캐너 함정:** 정규식으로 `"..."`만 잡으면 `$"{Label("보유")}"`처럼 보간 안쪽 문자열을 놓친다. 마지막에 "주석 아닌 줄에 한글이 있나"로 줄 단위로 다시 훑어 E1까지 놓친 것(경매 "내 매물"·"보유 골드"·"{n}개 거래"·"{star}성")을 잡았다.
- `Lang.Format(key, nullable string)`은 params에 null 배열로 들어갈 수 있다(CS8604) — `?? ""`로 막는다.
- 사용자 실측 완료(2026-10-17) — T-136 보관 · #64 닫음. 이미 떠 있는 알림·경매 `적용 조건` 칩은 다음 표시 때 새 언어가 된다(의도).
- 테스트 사본 씬 전용 `Gacha Send Button.prefab`은 손대지 않았다.

---
date: 2026-10-25
title: 이슈 정리 — DB 잠김 확인 · [합치기]/[창고에 쌓기] 버튼 · 판매/분해 목업 · 사이트 목업 탭
tags: [client, ui, web, docs]
---

# 이슈 정리 — #85 · #82 · #83 · #81

## 목적 / 배경
- 사용자 요청: 공이 돌아온 이슈 85 · 83 · 82 · 81을 처리. 83은 회의 결과를 반영해 목업부터, 사이트에 목업 바로 보기 추가.

## 변경 내용
- #85(T-180) — 코드 변경 없음. 재현 확인 후 보관 · 이슈 닫음.
- #82(T-175) — `InventoryGridPresenter.MergeCurrent` · `StackIntoWarehouse` · `PlayerDataModel.CanMergeItemSlots` · `CanStackIntoWarehouse` · 도구 줄 `Merge Button`(70) · 창고 정렬 줄 `Stack Button`(110) · `Loc_UI.xlsx` 7키. 보관 · 이슈 닫음.
- #83 — `GameDesign/design/ui/판매분해목업.html`(구현 전 사용자 검토 대기).
- #81 — 서버 T-181 커밋 `eb8b40a8`이 원격에 없어 푸시 요청 · 공을 서버로.
- 사이트 — `/mockups`(`src/lib/mockups.ts` · `pages/mockups/*`) · 배포 체크아웃 `fetch-depth: 0` + `filter: blob:none` · v0.7.0.

## 주요 결정 / 근거
- 합치기·쌓기는 **할 것이 없으면 요청하지 않고 토스트** — 서버가 빈 목록 + Ok를 돌려줘 눌러도 무반응처럼 보인다. 판정은 서버 규칙(앞 칸부터 채움 · 창고에 이미 있는 자원만)과 같게.
- 요청은 격자가 보낸다 — 칸 요청 대기(`s_slotWaitHandle`)가 격자끼리 하나라 정렬·끌기와 겹치지 않게.
- 목업 연결 문서는 파일 이름 검색(그대로 + percent-encoding) — `research/`는 언급이 많아 뺐다.

## 확인 방법 (다음에도 쓸 것)
- 로그인 화면은 `LoginPresenter.idInput`에 값을 넣고 `OnLoginButtonClicked`를 리플렉션으로 부른다. 패킷은 `MikaNetwork.NetworkManager.Instance.Send(new MikaProtocol.…)` — eval에는 using이 없어 전체 이름이 필요하다.
- 기본 계정(1234)은 캐릭터·장비 탭이 50/50으로 가득 차 뽑기가 거절된다(응답 15B) — 새 계정(`t180`)을 쓴다.
- `unity cmd eval --code`는 `' '` 같은 인용이 섞이면 인수 파싱이 깨진다 — `eval_file`로 돌린다.
- 투명 창이라 `screenshot`/`capture_game_view`는 검게 나온다 — 배치는 `RectTransform.rect` 수치로 확인했다.

## 후속 작업 / 주의사항
- #83 구현은 목업 승인 뒤: `SellCartModel`을 한 종류만 담게 · 탭 전환 시 비움 · 장비 두 버튼. 장비 "분해만" 결론이면 서버·엑셀 먼저(판매 거절) → 이슈로 요청.
- 도구 줄 1줄은 여유가 3px뿐이다(spacer 최소 20) — 버튼을 더 넣으면 줄이 넘친다.
- 씬 diff에 `Coin Fly Presenter` 블록 위치 이동이 섞였다(내용 같음 — Unity 재직렬화).

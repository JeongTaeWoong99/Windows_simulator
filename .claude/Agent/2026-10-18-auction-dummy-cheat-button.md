---
date: 2026-10-18
title: 치트 창에 경매 더미 매물 칸 (T-168 클라)
tags: [client, editor, cheat, auction]
---

# 치트 창에 경매 더미 매물 칸 (T-168 클라)

## 목적 / 배경
- 서버 `SeedAuctionListings = 16`(T-168 · 이슈 #77)을 치트 창에서 보낸다 — #71 "1,000건 이상"·쪽 넘기기·정렬 실측의 선행.

## 변경 내용
- `Assets/Scripts_Client/Editor/cheat-console/AuctionDummyCheatSection.cs` 새 칸 · `CheatSection.cs` 분류 `Auction`(경매) 추가
- 사용법·화면 → `cheat-console 규칙.md` "더미 매물 칸"

## 주요 결정 / 근거
- **새 분류 '경매'** — 지급(내 계정)·월드(시계·배수)·우편 어디에도 맞지 않는다. 경매 치트가 늘면 여기에 모은다.
- **자원 섞기를 토글로 뺐다** — `TidPicker` 팝업에는 0을 고를 항목이 없어서 `Arg2 = 0`을 체크 하나로 보낸다.
- **`CheatSender`는 고치지 않았다** — 응답 두 번 중 첫 번째가 대기 요청을 지우고, 두 번째는 대기 없이 로그에 한 줄로만 남는다(시간 초과·팝업과 무관).
- 개수는 1~5,000으로 자른다(서버 상한과 같다). 삭제는 따로 버튼(`Arg1 = 0`) — 개수 칸에서 0을 막아 실수 삭제를 피한다.

## 후속 작업 / 주의사항
- 실측 미완 — Play로 1,050개 → 검색 탭 "1,000건 이상"·쪽·정렬 확인 → #71 · #77 처리 → T-155 · T-168 마감.

## 업데이트 (2026-10-18) — 실측 확인 · UI 버그 둘 · 등록 직후 내 매물 누락 원인

- 사용자 실측 확인 → T-168 보관 · #77 닫음.
- **우편 줄 버튼이 쪼그라듦** (`Prefabs/MailRowView.prefab`) — `Info Panel`의 preferred 폭이 글 전체 길이(줄바꿈 전)로 잡혀, 합이 줄 폭을 넘으면 HLG가 **아이콘·버튼까지 min~preferred 사이로 같이 줄였다**(버튼 min이 -1 = 0). `Info Panel` pref 0 · flex 1, 버튼 min = pref 90 · flex 0.
- **경매 정렬 드롭다운 목록 글 겹침** (씬 `Sort Dropdown` 템플릿) — TMP_Dropdown 줄은 템플릿 `Item` 높이(20)로 고정이라 두 줄로 넘어가면 겹친다. `Content`에 VLG + CSF(세로 preferred), `Item`에 VLG(왼쪽 패딩 20 = 체크 자리) + min 높이 20, 배경·체크는 `ignoreLayout` → 줄마다 글 높이만큼. 다른 드롭다운에는 아직 안 넣었다(글이 짧다).
  - ⚠️ 씬은 저장하지 않았다 — 사용자의 미저장 변경이 함께 있었다.
- **등록 직후 판매 목록에 안 뜸 — 서버 쪽 타이밍.** 클라는 등록 성공 응답에서 바로 `RequestMyListings`를 보내는데, 서버 `OnAuctionRegistered`는 원장(DB)에 쓴 뒤 응답을 보내고 경매장 전달은 릴레이(`KickRelay`)로 **나중에** 한다. 내 매물 조회는 경매장(`GetSellerListingsAsync`)에서 읽으므로 아직 없는 목록이 온다. 탭을 오가면 그때는 도착해 있어 보인다. → 서버 수정 요청 대상(진우).

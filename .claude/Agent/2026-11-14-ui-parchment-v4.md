---
date: 2026-11-14
title: UI 양피지 4차 — 제목 아이콘 · 드롭다운 너비/체크 · 신화색 · 칸 정보 덜기
tags: [client, ui, art]
---

# UI 양피지 4차 — 제목 아이콘 · 드롭다운 너비/체크 · 신화색 · 칸 정보 덜기

## 목적 / 배경
- 사용자 지적 9가지: 화면 제목에 버튼 아이콘 · 드롭다운 체크가 안 보임 · 목록이 글자보다 넓음 · 신화 테두리가 UI 색과 비슷 ·
  캐릭터 칸 종족/크기/산업 그림 끄기 · 설정 슬라이더 끝이 테두리에 붙음 · 빈 작업슬롯 문구가 위로 쏠림 · 뽑기 '무기 3…' 잘림 · 글자 간격.
- 팰월드식(칸 정보 최소 + 툴팁) 검토 HTML — 바탕화면 `UI 제안 - 팰월드식 칸 정보/제안.html`.

## 변경
- 코드: `MainCanvasView.SetTitle(string, Sprite?)` + `titleIcon` · `UIManager.MainScreenEntry.titleIcon` ·
  `DropdownListFit`(새 — TMP_Dropdown 목록 너비, 최소 머리 · 최대 가장 긴 줄) · `CheckDropdownView.FitList`도 같은 규칙 ·
  `RarityPalette` 신화 E6CC80 → FF3D6E · `GachaPoolSummary.KindName`(카드엔 '무기'만, 종수는 확률 툴팁 첫 줄).
- 그림(`make_icons.py`): `menu_workslot`(모루+망치) · `ui_check`(굵은 흰 체크 + 외곽선).
- 씬: 열 제목 3 · 덮개 머리 3 · 경매 판매 등록 창 머리에 `Title Icon` · 드롭다운 21개에 `DropdownListFit` + 체크 그림 교체 ·
  설정 슬라이더 트랙·손잡이 길 좌우 8 안쪽 + 줄 오른쪽 여백 14 · 빈/잠긴 작업슬롯 아이콘·문구 가운데 ·
  HUD 계정·골드 간격 3→8 · 위젯 상태 줄 묶음 간격 6→14(아이콘↔숫자 4→7) · '+ 판매 등록' 버튼 140→168.
- 프리팹 `SlotView`: `Size Badge` · `Race Badge` · 적성 칸 `Icon` 5개 삭제(선택 참조라 코드 변경 없음).
- 문서: Main·UI·Shared·Art 규칙 · `GameDesign/design/ui/README.md` 등급색 표.

## 결정
- 신화색은 클라 표시색만 바꿨다 — 서버 `Enum.cs` 주석(E6CC80)은 진우 영역이라 두었다.
- 작업슬롯 칸(Work Slot 0~7)의 아이콘·문구는 프리팹이 아니라 씬에 붙은 것이라 씬에서 고쳤다.

## 확인
- 플레이 캡처: 제목 아이콘(작업슬롯·인벤토리·길드·설정·우편·경매장·아티팩트·거점) · 드롭다운 3종 펼침 · 캐릭터 칸 · 뽑기 카드 · 오디오 슬라이더 · 판매 등록 창.
- 빈(열린) 작업슬롯은 테스트 계정에 없어 잠긴 칸으로만 확인 — 같은 배치를 쓴다.

## 남은 일
- 팰월드식 칸 정보 줄이기 — 사용자 결정 대기(제안: 1단계 캐릭터 칸 장비 4칸·적성 → 툴팁).

---
date: 2026-09-14
title: 해금 시스템 기획 — UnlockTable 한 표 · 해금 행동 · 첫 적용은 작업슬롯
tags: [design, data, unlock, excel]
---

# 해금 시스템 기획 — `UnlockTable` 한 표 · 해금 행동 · 첫 적용은 작업슬롯

## 목적 / 배경

- 해금이 산업 레벨(적성+계정 레벨, 자동)·작업슬롯(레벨×골드, 구매)·특성 액티브(계정 레벨, 자동) 세 곳에
  각각 다른 모양으로 있었다. 사용자가 "해금은 시스템·콘텐츠 전반에서 엄청 많이 쓰인다"며 범용화를 요청,
  grill 형식으로 결정을 받아 `GameDesign/design/unlock/README.md`를 신설했다.
- 결정 내용은 그 문서 1장이 전부다. 여기엔 근거와 지뢰만 남긴다.

## 주요 결정 / 근거

- **한 표·조건은 컬럼(`Gold`·`AccountLevel`·`RequiredUnlockTIDs`)·AND.** 처음엔 "조건 = 행 + enum" 두 표를 권했지만
  사용자가 "최대한 단순"을 원해 컬럼 방식으로 갔다. 종류 추가 = 컬럼 추가. OR·문자열 파싱은 두지 않는다
  (`Ref`·`Min`/`Max`가 문자열 안까지 못 들어간다).
- **콘텐츠가 `UnlockTID`를 참조한다.** 해금 시스템이 콘텐츠를 모른다. 슬롯은 테이블이 없어 `WorkSlotTable(WorkSlotTID=칸 번호 · UnlockTID)`를 신설 —
  행 수가 상한(8), `UnlockTID 0`이 시작 2칸.
- **해금은 유저의 행동(`C_UnlockRequest`), 서버 안에서 원자적.** 사용자가 "차감과 해금을 분리해야 재사용"이라 했는데
  뜻은 코드 재사용이었다 — 기존 `User.TrySpendGold`를 그대로 부른다. 클라 요청은 하나다.
- **산업 레벨 조건에서 적성을 뺐다(계정 레벨만).** 2026-08-01 결정 번복. 해금이 수동이 되면서 "계정 레벨만이면 자동 해금"이라는
  당시 근거가 사라졌다. 잃는 것(가챠 동기)은 unlock 4.2·산업레벨 3.1에 적었다. 이관은 계정 레벨이 서버에 생긴 뒤(T-021).
- **잠긴 것은 전부 보인다.** ui 2.4의 "비어 있을 때는 잠긴 표시를 안 보인다"는 기획 없는 콘텐츠 자리 규칙이라 충돌이 아니다 — ui #10에 그렇게 적었다.
- **계정 레벨 획득식은 보류.** `AccountLevel` 컬럼은 만들어 두고 값은 전부 0. 슬롯은 골드 + 선행만으로 시작한다.
- 골드 값(500 → 60,000)은 테스트값.

## 변경 내용 (문서 밖)

- `GameDesign/Excel/Unlock.xlsx` 신설 → `UnlockTable`(6행) · `WorkSlotTable`(8행), 생성물·Unity 미러까지.
- `Server/ExcelGenerator/TableCodeGenerator.cs` — **배열 컬럼의 `Default(Null) = ""`를 빈 배열로.** 저장소 첫 배열 컬럼이라
  기존 코드가 `new int[] { "" }`를 만들어 컴파일이 깨졌다. `GameDesign/CLAUDE.md`·excel 스킬 `컬럼 레퍼런스.md`에 표기를 적었다.
- 일감: T-038 전면 재작성(범용 해금 첫 구현) · T-021 이관 일감으로 재정의 후 ⏸ · T-012·T-016·T-039 축소.

## 함정

- openpyxl로 **새 워크북에 다른 파일의 `_style`을 복사하면 저장이 `IndexError`로 죽는다** (스타일 ID가 원본 워크북 것).
  기존 xlsx를 열어 시트를 갈아 끼우고 다른 이름으로 저장하는 방식으로 우회했다.
- `check-doc-graph.ps1`은 링크를 지우면 상대 문서 블록에 "남음" 경고를 낸다 — 산업레벨 7장 #6의 UI 링크를 없애자 ui 블록에서 산업레벨을 빼야 했다.
- `.claude/skills/common/` 두 스킬은 다른 세션이 `SKILL.md` + 상세 문서로 갈라 둔 상태다. 요지 파일이 아니라 상세 파일(`컬럼 레퍼런스.md`)을 고쳐야 할 것이 있다.

## 후속 작업 / 주의사항

- Unity `.meta` 없음 — `Assets/Scripts_Server/GameData/Tables/UnlockTable.cs`·`WorkSlotTable.cs`, `StreamingAssets/Data/UnlockTable.bytes`·`WorkSlotTable.bytes`.
- 서버 구현은 T-038. 클라 조건 표시는 T-039가 테이블로 지금 가능.
- **계정 레벨 획득식**이 다음 결정이다 — 산업 레벨 이관(T-021)·슬롯 레벨 요구치(T-012)·특성 액티브가 전부 여기 걸려 있다.

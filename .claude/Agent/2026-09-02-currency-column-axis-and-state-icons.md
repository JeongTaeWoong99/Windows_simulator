---
date: 2026-09-02
title: 재화 패킷 열 축 대응 + 상태바를 아이콘 + 텍스트로
tags: [client, ui, packet, state]
---

# 재화 패킷 열 축 대응 + 상태바를 아이콘 + 텍스트로

## 목적 / 배경

서버가 재화를 **행 축에서 열 축으로** 바꾸면서(`71fdaa4`) **Unity가 컴파일되지 않았다** —
에러 3건, 전부 `res.Currencies`.

| | 전 | 후 |
|---|---|---|
| DB | `t_user_currency (user_id, currency_type, amount)` — 재화 하나 = **행** | `(user_id PK, gold, dia)` — 재화 하나 = **열** |
| 패킷 | `S_CurrencyResponse { List<CurrencyInfo> }` | `{ long Gold; long Dia }` |
| DTO | `CurrencyInfo` | **삭제됨** |

서버가 클라까지 고쳤다가 **담당 폴더가 아니라 되돌렸고**([T-042](../../tasks/archive/T-042-클라재화패킷대응.md) ·
이슈 #20), 유료 재화 `Dia`가 함께 생겼다.

## 변경 내용

### 코드 (3파일)

- `PlayerDataModel.cs` — `Dictionary<byte, long> _currencies`와 `GetCurrency(byte)`를 **걷어내고**
  `Gold`·`Dia` 프로퍼티로. `OnCurrencyReceived`는 두 줄 대입으로 끝난다
- `ServerPacketHandler.cs` — 수신 로그를 `골드 N · 다이아 N`으로
- `StatePresenter.cs` — `diaText` 필드 + `RequireRef` + `Refresh` 한 줄

### 씬 (Unity MCP)

`State Presenter` 아래 `Nick`·`Gold`·`Dia`를 각각 **`Xxx Panel`(정렬 상자)** 로 싸고
그 안에 **`Xxx Image`(아이콘) + 기존 텍스트**를 넣었다.

### 문서 · 일감

- `State 규칙.md` 전면 개정 — 계층 · 비율 · 아이콘 · "다이아는 늘 0"
- `패킷 레퍼런스.md` · `서버 동작 이해.md` · `UI 배치 현황.md` 한 줄씩
- T-042 완료 → `archive/`

## 주요 결정 / 근거

**클라도 사전을 걷어내고 필드로 갔다.** `Dictionary<byte, long>`에 `CurrencyType`을 키로
담는 것은 **없어진 행 축을 클라만 들고 있는 것**이다. 사전만 채우는 식으로 때우면
컴파일은 통과하지만 **같은 어긋남이 다음 재화에서 다시 난다.**

**다이아를 화면에 붙였다 — 값이 늘 0인데도.** 지급·차감 경로가 기획에 없다
(`trade/README.md` — "상점은 골드 상점"). 그래도 **자리를 먼저 잡아 두는 쪽**을 택했다.
대신 "0은 버그가 아니다"를 `State 규칙.md`에 남겼다 — 안 적으면 다음 사람이 고치려 든다.

**숫자만 셋을 나열하지 않았다.** 다이아를 넣으면 상태바에 숫자가 셋이 되는데 라벨이 없다.
아이콘을 붙여 구분하되, **아트가 없으므로 내장 `Knob`에 짝이 되는 글자색을 입혀** 자리만 잡았다
(`RarityPalette`가 등급을 색으로만 표시하는 것과 같은 방식). 아트가 오면 `Sprite`만 갈아 끼운다.

## 작업 방식 — 다음에도 쓸 것

**오브젝트를 옮겨도 인스펙터 배선은 살아남는다.** `Nick Text`·`Gold Text`를 새 패널 안으로
옮겼는데 `StatePresenter`의 참조가 그대로였다 — **오브젝트 참조라 경로가 아니라 대상을 가리킨다.**
재배선이 필요 없었다(새로 만들어 갈아 끼웠다면 필요했을 것이다).

**폭은 픽셀이 아니라 파생이다.** 바깥 비율(`3 : 2 : 2 : 1×5`)은 `FlexibleWidth`를 텍스트에서
패널로 옮기기만 하면 유지되고, 아이콘의 정사각형은 `SquareLayoutElement`가 부모 높이에서 파생시킨다.
⚠️ 패널 안쪽 그룹은 `ChildForceExpandWidth`를 꺼야 한다 — 켜 두면 아이콘까지 늘어난다.
(자세한 경위는 아래 "업데이트" 절)

**MCP `IRunCommand`는 `void Execute(ExecutionResult)`다.** `object Execute()`로 쓰면
`CS0535`로 막힌다 — 반환 대신 `result.Log(...)`로 찍는다.

## 후속 작업 / 주의사항

- ⚠️ **재생 검증이 아직 안 끝났다.** 컴파일 0 · 배선 3/3 · 아이콘 33×33까지 확인했고,
  **로그인 후 실제 표시는 사람이 봐야 한다**(Screen Space 캔버스는 스크립트로 측정되지 않는다).
- ⚠️ **`GameData.CurrencyType`이 쓰이지 않게 됐다.** `Assets/Scripts_Server/GameData/Enum.cs`는
  **서버 미러라 손대지 않았다.** `Dia`도 이 enum에 없다 — 정리 여부는 서버 판단이다.
- ⚠️ **새 분석기 경고 1건** — `MIKA001: S_ItemSellResponse에 [PacketHandler]가 없습니다`.
  이번 작업과 무관하고 **[T-032](../../tasks/T-032-클라판매UI.md)(클라 판매 UI)의 몫**이다.
- **커밋하지 않았다** (요청 없음).

## 업데이트 (2026-09-02) — 아이콘 폭에 적은 `33`을 걷어냈다

처음에는 `Xxx Image`의 `LayoutElement`에 `preferredWidth = 33`을 적었다. **정사각형을 만들려고
높이와 같은 숫자를 손으로 넣은 것**인데, 사용자가 **"가운데 950이나 2 : 1 비율이 바뀌면
정사각형이 깨지지 않느냐"**고 짚었다. 맞는 지적이었다.

```
열 1080 − 가운데 950 = 130  →  위젯 2 : 상태 1  →  상태 43  →  padding 5+5  →  33
                                                                              ↑ 내가 베낀 값
```

**그리고 이 저장소는 이미 그 규칙을 적어 두고 있었다** —
`WidgetPositionLayout.cs:96`의 **"⚠️ 이 값을 상수로 베껴 두지 말 것. …베껴 둔 쪽은 안 움직여
조용히 어긋난다."** 내 `33`이 정확히 그 위반이었다.

### 고정·잠금이 아니라 파생으로 갔다

사용자는 **"950과 2 : 1을 고정하고 못 바꾸게 잠그자"**를 제안했는데, 그러지 않았다.

- `widgetWeight`/`stateWeight`는 **의도적으로 인스펙터에 노출**돼 있다 —
  "양수면 무엇이든 성립하는 조절값이고 `ExecuteAlways`로 돌려 보는 것이 이 값의 결정 방법"
  (`WidgetPositionLayout.cs:77`).
- 그 비율이 `WidgetSlotHeight` → **작업표시줄 맞춤 창 배율**의 근거다. 굳히면 그 기능도 굳는다.
- 무엇보다 **잠금은 베낀 숫자를 남겨 둔 채 원본만 못 움직이게 하는 것**이라 문제를 덮는다.

### 이미 있는 것을 못 찾을 뻔했다

`SquareLayoutElement`를 새로 만들려다가 **`Common/ugui-layout/`에 이미 있는 것을 발견**했다.
`Layout 규칙.md`가 "범용 컴포넌트 `FlexibleGridLayoutGroup`·`SquareLayoutElement`도 함께
`Common/ugui-layout/`으로 내려갔다"고 적어 두고 있었는데 **그 줄을 읽고도 지나쳤다.**
→ **`Common/` 아래에 이미 있는지 먼저 본다.** 폴더 목록만 봐도 3초다.

게다가 기성품이 내 설계보다 나았다 — 자기 높이가 아니라 **부모 높이**를 읽는다.
UGUI는 가로를 세로보다 먼저 계산해서 자기 높이는 한 프레임 늦는데, 부모 높이는 이미 확정돼 있다.

### 실측으로 확인했다

가운데 칸을 임시로 바꿔 보고 되돌렸다.

| 가운데 | 상태 칸 | 아이콘 |
|---|---|---|
| 950 | 43 | 33×33 ✅ |
| 900 | 60 | 50×50 ✅ |
| 800 | 93 | 83×83 ✅ |

> ⚠️ **같은 오브젝트에 `LayoutElement`를 함께 두지 않는다** — 둘 다 가로를 주장해
> 승패가 `layoutPriority`에 좌우된다(컴포넌트 주석). 그래서 붙이지 않고 **교체**했다.

> 곁가지 — `Layout 규칙.md`가 가운데를 **900**으로 적고 있었다(실제 950). 함께 정정하고,
> **"파생값을 숫자로 베끼지 않는다"** 절을 신설했다.

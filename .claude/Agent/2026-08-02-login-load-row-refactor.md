---
date: 2026-08-02
title: 로그인 Load 경로를 Row 기반으로 리팩토링
tags: [server, repository, login, refactor]
---

# 로그인 Load 경로를 Row 기반으로 리팩토링

선행 문서: [아키텍처 리뷰](../../GameDesign/아키텍처리뷰/2026-08-01-서버아키텍처리뷰.md) 후보 2의 **Load 부분만** 실행.
사용자가 namespace 평탄화·`DbConnection` 래퍼·`RepositoryContracts.cs`를 먼저 만들어 두었고, 그 WIP를 이어받아 완성했다.

## 변경 내용

**계약** — `Repository/RepositoryContracts.cs`
- 공개 Row 4종: `InventoryRow` · `CurrencyRow` · `CharacterRow(long CharacterId, int CharacterTid, ...)` · `WorkStationSlotRow(..., long CharacterId)`
- `PlayerLoginData` — 리포지토리가 로직 스레드로 넘기는 Row 묶음. **Row는 User의 partial까지만 들어간다.**

**DB** — `DB/DbConnection.cs` (Dapper 래퍼) · `DB/DBManager.cs`
- 래퍼 메서드 4개: `QueryAsync`(List 반환) / `QueryFirstOrDefaultAsync` / `ExecuteAsync` / `ExecuteScalarAsync`
- `IRepository.ExecuteAsync(DbConnection)` — `IDbConnection`은 DBManager만 안다
- `DBManager.Initialize(Func<SqliteConnection>)` 오버로드 추가 — 테스트가 `:memory:`를 넣는 seam. 파일명 오버로드는 유지(경로 탐색 동일)

**로그인 흐름**
- `LoginRepository.ExecuteAsync` = **Row 수집 + 신규 지급 INSERT까지만** (동작 보존). 도메인 변환·`startedAt` 결정이 DB 스레드에서 빠졌다
- `Apply()` → `User.OnLoginDataLoaded(PlayerLoginData)` 호출만
- `User.OnLoginDataLoaded`(구 `LoadDB` 대체): `startedAt = UtcNow`를 **로직 스레드가** 결정 → `LoadInventory` / `LoadCurrencies` / `LoadCharacters` / `LoadWorkStation` → `RefreshWorkStationSpeed` → `Login()`
- 영역별 Load는 각 partial에 산다: `User.Inventory.cs` `User.Currency.cs` `User.Character.cs`(GameTable 스킵 정책 포함) `User.WorkStation.cs`
- `Inventory.Load`가 `ItemInfo`(Protocol DTO) 대신 `Item`(도메인)을 받는다 — DTO는 `Snapshot()`(패킷 조립)에서만 등장

## 주요 결정 / 근거

- **위치 기반 record는 SQL에서 `AS` 별칭 필수.** Dapper의 `MatchNamesWithUnderscores`는 프로퍼티 매핑에만 적용되고 생성자 매핑에는 적용되지 않는다. `SELECT item_id AS ItemId ...` 형태로 통일.
- **신규 지급(기본 캐릭터·기본 슬롯)은 아직 `LoginRepository.ExecuteAsync`에 남겼다.** 로직 스레드로 옮기면 쓰기 왕복이 1회 늘어나는 구조 변경이라 "Load만" 범위를 벗어난다. 후보 2 후속에서 옮긴다.
- **Row는 순수 코어에 넣지 않는다.** `WorkStation.Load`·`Inventory.Load`는 도메인 객체를 받는다 — 코어가 Repository 타입을 참조하면 의존 방향이 뒤집히고 기존 테스트(342줄)가 오염된다.
- 사용자 WIP의 namespace 평탄화 잔재(`User.User`·`Character.Character`·`Slot` 별칭·`GlobalUsing`)를 함께 정리했다. 서버 스킬 문서의 예시 코드가 옛 namespace를 쓰고 있는지는 확인하지 않았다.

## 검증

- `dotnet build WSGameServer` 오류 0
- `dotnet test` **83/83 통과** (기존 테스트 변경 없음 — 순수 코어 시그니처가 안 바뀌었다는 증거)
- 런타임 로그인 경로는 실서버로 검증하지 않았다 — 더미 클라 로그인 확인 권장

## 후속 작업 / 주의사항

- `AddItemRepository.Key`가 여전히 0 (파티션 붕괴 버그) — 이번 범위에서 제외했다. **한 줄 수정**(`public long Key => User.SessionId;`)이니 다음 작업에서 반드시.
- 밴/삭제 응답 미전송, `Apply()`의 침묵 실패 등 아키텍처 리뷰 부수 발견 목록은 그대로 남아 있다.
- `DbConnection`이라는 이름이 `System.Data.Common.DbConnection`과 겹친다. WSGameServer namespace 안에서는 우리 타입이 이기지만, `System.Data.Common`을 using하는 파일에서는 혼동 여지가 있다.

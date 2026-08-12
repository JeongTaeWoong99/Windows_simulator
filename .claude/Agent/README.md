# Agent 작업 로그 (.claude/Agent)

이 폴더는 **코드 작업의 결정·맥락을 다음 작업(사람/Agent)이 참고**하도록 남기는 로그 저장소다.

- 새 코드 작업을 **시작하기 전** → `agent-log-reader` 스킬에 따라 관련 로그를 먼저 읽는다.
- 코드 작업을 **마친 뒤** → `agent-log-writer` 스킬에 따라 이 폴더에 로그(.md)를 남긴다.

## 파일 규칙

- 파일명: `YYYY-MM-DD-<kebab-slug>.md` (예: `2026-07-25-excelgenerator-bytes.md`)
- 하나의 작업 단위 = 하나의 로그 파일
- 아래 `INDEX`에 한 줄씩 최신순으로 추가한다.

## INDEX

<!-- 최신 작업이 위로. 형식: - [YYYY-MM-DD 제목](파일명.md) — 한 줄 요약 -->

- [2026-08-12 Scripts_Client 문서 구조 재편 — 폴더별 규칙 md 신설과 주석 걷기](2026-08-12-client-doc-restructure.md) — 클라 5,329줄 중 **주석 1,912줄(36%)** 이 구조 문서 역할을 하던 상태를 정리 · `폴더 구조.md`(전체 지도) + 폴더별 `<폴더명> 규칙.md` 6개 신설 · `CLAUDE.md`에서 클라 전용 nullable 규칙 제거(**공용 문서라 한쪽 담당 규칙을 두지 않는다**) · `UI 스크립트 규칙.md`→**`UI 규칙.md`**, `서버동작이해.md`→**`서버 동작 이해.md`** 개명(참조 17곳) · `DesktopWindow/README.MD` 283줄 해체 — **제약은 규칙 md로, 실험 기록은 6월 로그로** · 🔒 **`Common/`만 주석을 걷지 않는다**(툴킷 사본이라 md가 따라가지 않아 새 프로젝트에서 설명이 사라진다) · `WindowSettings` 42%→23% · `GameDataLoader` 30%→23% · ⚠️ **과거 로그·archive의 옛 파일명은 일부러 안 고쳤다**(그 시점의 사실) · ⚠️ 개명은 `.meta`까지 `git mv`(GUID 보존) · 주석 걷기 `DesktopWindow`→`Log`→`Managers`→`UI` 잔여

- [2026-08-12 nullable 참조 형식 규칙을 Arca Unity Toolkit 표준으로 승격](2026-08-12-nullable-rule-to-toolkit.md) — 클라 40파일 점검 → 이탈 2건(`InventorySlotView`·`WorkStationSlotView`가 `= null!`만 있고 `RequireRef` 없음) 수정 · **`?`는 안전장치가 아니라 "비어도 정상" 선언**이라 필수 참조에 쓰면 미연결이 조용해진다 · 종속 View는 `Awake`, Presenter는 `Start`에서 검증 · 🔴 **툴킷에 `csc.rsp` 생성 단계가 없던 것이 실제 결함** — `MonoBehaviourExtensions`가 이미 `Object?`를 써서 새 프로젝트에 심으면 **CS8632**가 뜬다 · 툴킷 `clean-code-style` **9장 신설** + 세팅 절차에 `-nullable:enable` 추가(커밋 `5cc0414`) · ⚠️ **Unity 객체에 `?.`·`??` 금지**(가짜 null 통과) — 현재 사용처 14건은 전부 순수 C#

- [2026-08-10 UI 레이어를 MVP(Legacy) 구조로 재편 + 메인 화면 토글](2026-08-10-ui-mvp-rename.md) — 접미사 원칙을 "붙는 오브젝트" → **"MVP 역할"** 로 교체(`*PanelUI`→`*Presenter` · `*CanvasUI`→`*CanvasView` · `PlayerDataManager`→**`PlayerDataModel`**) · **레퍼런스에 화면별 View 클래스가 없다**(`HealthPresenter`가 Slider를 직접 쥔다)는 걸 확인하고 신설을 기각 — 반복 칸 3종만 View · **화면마다 캔버스를 두지 않는다** → `#Main Canvas (MAIN VIEW)` 하나로 통합 · **전환 층을 하나로 눕혔다** — `WorkStationPresenter`(위젯 하나 없는 껍데기)를 지우고 목록·선택·설정을 형제로, `MainScreen`을 3값으로, 켜고 끄는 곳은 `UIManager.ShowMainScreen` 한 곳 · **`MainCanvasView`에 합병하지 않은 이유 = 캔버스는 `CloseAllExceptWidget`에서 꺼진다** · 참조 방향은 **"살아 있는 쪽이 넘긴다"**(목록→선택 단방향, 선택은 평소 꺼져 있어 구독 불가) · 화면 제목을 `MainCanvasView.SetTitle`로(문구는 인스펙터) — 캔버스 View가 위젯을 쥐는 유일한 예외, 가르는 기준은 **"Model을 구독하느냐"** · 오브젝트 이름에서 **`Presenter`(화면) / `Panel`(정렬 상자)** 분리 · 표기를 세 번 갈아엎어 `(MAIN VIEW)` / `(↓ SUB VIEW)` 로 안착(스크립트 붙은 것에만) · 캔버스에 Presenter를 얹던 규칙 위반 3건 교정 + 껍데기 Presenter 4개 신설(`Widget`·`Menu`·`StorageTab`·`StorageInformation`) · 폴더를 **`<캔버스>/<Presenter>/` 두 단**으로 평탄화 · ⚠️ **enum 값을 중간에 끼우면 씬이 int로 저장돼 배선이 조용히 어긋난다**(`Setting` 1→2) · ⚠️ **`[CenterHeader]`에 `< >`를 넣으면 `< < 참조 > >`가 된다**(30곳 교정) · 시작 화면을 `ResetMainScreen`으로 못박음 — **씬에 무엇이 켜진 채 저장됐든 무시**(옛 "켜진 것 중 첫 번째만 남긴다"는 설정 켜 둔 채 저장한 씬이 설정으로 시작했다). 단 **캔버스는 켜지 않는다** — 로그인 전에 게임 화면이 비친다 · 여백을 계층별 두 값으로 통일(컬럼 위 0/간격 10 · 캔버스 이하 5 5 5 5/5 · **위젯 속살은 제외** — 높이 30 줄에 5는 내용을 눌린다) · 컴포넌트 순서를 종류별로 통일(크기 주장 → 그리기 준비 → 그리는 것 → 입력 → 자식 배치 → 내 코드) · ⚠️ **`Child Control Height` 토글은 그 컬럼 전체의 높이 규약을 바꾼다** — off면 `LayoutElement`가 무시되고 `RectTransform.Height`가 실제 높이, on으로 켜자 숨어 있던 `flexibleHeight = 1`이 드러나 **형제가 꺼질 때 위젯이 화면 전체로 늘어났다**(고정 칸은 `flexH = 0`, 90+900+90 = 1080이라 남는 높이 자체가 없다) · ⚠️ **전부 닫을 때 화면만 끄면 캔버스 `LayoutElement`가 900px를 먹어 위젯이 밀린다** · ⚠️ **`OnEnable`에서 일하는 `WidgetPositionLayout`을 여닫히는 캔버스에 두면 꺼진 동안 죽는다** → `!Horizental Columns`로 이관 · 씬 수술은 Unity MCP + `PasteComponentAsNew`(직렬화 참조 보존), **컴포넌트 순서는 `MoveComponentUp`만 통한다**(`m_Component` 직접 수정은 Unity가 거부, `MoveComponentRelativeToComponent`는 대화상자로 MCP가 끊긴다) · ⛔ 미커밋

- [2026-08-10 클라가 적성 패킷을 쓰기 시작했다 — EIndustryType 대응 + 적성 0 잠금](2026-08-10-client-aptitude-lock.md) — 이슈 #11·#13의 **클라 수신 측**. `Send(byte)` 컴파일 깨짐 복구 + `(GameData.ItemType)` 캐스팅 제거 · `PlayerDataManager.GetAptitude` 신설(**`CharacterTable`을 읽지 않는다** — 오늘은 값이 같아 틀려도 안 잡힌다) · **적성 0은 숨기지 않고 잠근다**(기획 확정 · 숨기면 적성 0→1 승격 때 화면이 조용히 틀린다) · 잠금 판정을 줄이 들어 `SetAssignable`이 대기 잠금과 AND · `ui/README` 6장 미결 #11 해소(낡은 서술이었다) · ✅ **#11 실구동 확인 후 A-2 진단 구역 제거**(재발 시 진단 복구가 아니라 #12를 본다) · ⚠️ 계정이 `1001`(전 산업 적성 1) 하나뿐이라 **잠금이 화면에 안 나타난다**(T-010)

- [2026-08-10 카운트다운이 결과보다 0.9초 빠른 문제 — 위상 오차 3종](2026-08-10-countdown-phase-fix.md) — 이슈 #11. 관측 0.9초 = **①(`ToInfo()` 초 절삭) + ②(푸시 해상도 1초)**, 둘 다 세션 고정이라 누적되지 않는다 · 패킷 필드 `LastTickAtUnix`→**`LastTickAtUnixMs` 개명**(값만 바꾸면 클라 `*1000L`이 조용히 통과한다) · `Interval` 1초→**0.1초** · ③ `LastTickAt`을 **소비한 만큼만** 전진(안 고치면 틱을 줄인 만큼 손실이 커진다 — 시간당 3.6초→36초) · **60FPS+슬롯별 Task 제안은 반대**(단일 로직 스레드에 데이터 레이스 · 스케줄링이 실제 일보다 비싸다) · red-green 확인 후 테스트 196건 통과 · **폐지된 오프라인 정산을 전제하던 주석 8곳 정리**(로그인 수신 세트의 `S_GatherResultResponse`·`RollMany`의 존재 이유 등 — "폐지됐다"고 적은 근거 서술은 남긴다) · ⛔ 실제 구동 검증 미실시

- [2026-08-10 캐릭터 적성을 패킷으로 — 소유자를 서버 런타임으로](2026-08-10-character-aptitude-packet.md) — 이슈 #13. 적성은 지금 TID 고정이고 클라 미러도 완비돼 있었지만 **장비(T-002)가 적성에 얹힐 여지** 때문에 `CharacterInfo.Aptitudes` 신설 · **`AptitudeInfo{Industry, Value}` 구조체 — 순서 규약을 두지 않는다**(어긋나도 컴파일이 통과하는 종류의 사고를 없앤다) · **`EIndustryType` 신설**(1차 산업만 — `ItemType`은 아이템 분류와 산업을 겸해서 코드가 `ItemType industry`로 이름 보충 중이었다) + 프로토콜 산업 필드 `byte`→enum 통일 + 드리프트 가드 2건 · 오늘은 전달값=테이블값이라 **틀려도 테스트에 안 잡힌다** · ⚠️ `GrantCharacterRepository`가 임의 TID를 받는데 콜백은 `DefaultCharacterTid`로 조회(여러 종류 지급 시 선수정) · 변경 푸시는 T-022 · **`Enum.xlsx`에 `IndustryType` 신설 + 서버 전면 교체(T-023 완료 — 기획평가 R7 해소)** — 값은 `ItemType`과 1:1 유지(DropTID·DB가 그 숫자에 묶임)라 **데이터 이관 없음** · `GetAptitude(ItemType.Misc)`가 이제 컴파일 에러다 · 🔴 **Unity 클라 `WorkStationSelectPanelUI.Send(byte)` 컴파일 깨짐**(담당 분리라 미수정, 이슈에 기재) · 테스트 194건 통과

- [2026-08-10 산업 레벨 선택 수단 — DB·패킷](2026-08-10-industry-level-db-packet.md) — `industry_level` 컬럼 + `t_user_industry_level` 신설(ADD COLUMN이 주석을 어긋내 **테이블 재작성**으로 이관) · 배치 패킷에 레벨 필드 + `IndustryLevelLocked` · 저장된 해금 레벨 대조 검증(하한 포함) · 구 클라 `0`은 핸들러가 Lv1로 정규화 · 테스트 186건 통과 · **T-017 완료·보관, 해금 판정은 T-021로 분리**

- [2026-08-10 판정 비용 테이블 전환](2026-08-10-judgecost-table.md) — `IndustryLevelCatalog` 신설, `JudgeCost` 상수 → `RequiredScore × 1000`(초→ms 단위 환산) · 슬롯 인스턴스 `JudgeCostUnits` + `Assign`이 (산업·레벨·비용) 일괄 변경 · 행 누락은 경고+30초 폴백 · 테스트 178건 통과 · 레벨 선택 수단(DB·패킷·해금)은 T-017 잔여

- [2026-08-10 드롭 롤 레벨 분리](2026-08-10-drop-level-filter.md) — `DropTableCatalog` 키를 `(ItemType, Level)`로 확장해 레벨별 테이블 25개 등록 · `WorkStationSlot.IndustryLevel` 추가(기본 Lv1 · DB 미저장) · `Settle`이 슬롯 레벨로 조회 · 테스트 170건 통과 · T-017 드롭 오동작 해소(JudgeCost·해금·DB·패킷은 잔여)

- [2026-08-09 드롭 시트 실데이터 테스트 추가](2026-08-09-drop-sheet-tests.md) — `DropSheetTest.cs` 신규(산업별 Theory 10케이스) · 6등급 산출은 시트+ItemTable 조인, 분포는 `RollMany` 1M회 실측을 이항분포 5σ 경계로 검증 · 실데이터 적재는 `GameTableFixture` 재사용 · 테스트 129건 통과 · T-005 완료 조건 2 충족

- [2026-08-08 Drop 엑셀 산업별 파일 분리 + 시트 병합 규약](2026-08-08-drop-excel-split-sheet-merge.md) — 시트 이름 `<테이블명>.<접미사>` 병합 규약 신설(ExcelGenerator) · `Drop.xlsx`→산업별 5파일(레벨별 시트) · `Industry.xlsx` 산업별 시트 분리 · DropTID `산업×100000+레벨×100+순번` 재채번(산업 자리=`ItemType` enum — ⚠️ ItemTID 대역과 어긋남) · 테스트 119건 통과 · 서버 오동작(T-017)은 범위 밖
- [2026-08-07 싱글턴 제거 · `GameServer` 조립 지점 — 리뷰 후보 4](2026-08-07-di-composition-root.md) — `ILogicExecutor`/`IServer` seam 신설 · `GatheringScheduler`·`SessionWatchdog`·`DBManager`·`NetworkManager` 탈싱글턴(생성자 주입) · `Entity` 폐기 후 `User`로 인라인 · `*.Instance` 21→14곳 · `FakeLogicExecutor`(기록/즉시 두 모드)로 테스트 복구 **119건 통과** · ⚠️ `Destroy()`를 즉시 실행 모드로 돌리면 `UserManager.Instance`가 오염된다 · **`Program.cs`의 `Run()` await 누락은 미해결**
- [2026-08-05 클라이언트 구조 재정비 — 송신을 UI로, 매니저는 수신 전담](2026-08-05-client-structure-refactor.md) — `SessionManager`→`PlayerDataManager`(수신 전담) · 송신 3종을 각 UI로 이관 · `HeartbeatManager`→`PingManager` · `UI/`를 패널별 폴더로 · 창 설정 `PlayerPrefs` 영속화 · ⚠️ **씬 배선 미완 — 로그인·가챠 버튼 3개가 죽어 있다**
- [2026-08-04 하트비트 무응답 세션 정리 · 중복 로그인 kick — 이슈 #10 / T-001](2026-08-04-heartbeat-idle-session.md) — 무응답 세션을 15초에 정리 · 중복 로그인은 기존 세션을 끊는다
- [2026-08-03 클라 로그 체계 정리 · 하트비트 · 가챠 갱신 통일 — 이슈 #8·#10](2026-08-03-client-log-and-heartbeat.md) — `ClientLog` 신설(`[↑송신]`/`[↓수신]` 태그 + 송신 훅) · `HeartbeatManager` 5초/15초 · 가챠를 `ApplyItemChanges` 하나로 통일 · ⚠️ **#10 서버 파트(세션 정리·pid kick)는 미착수** · `characterId` 기본값 1→1001(TID 1 폐기)
- [2026-08-02 기획 문서·엑셀·코드 불일치 전수 정리](2026-08-02-doc-data-code-sync.md) — 아이템 30→156종 · `Hunting` enum · `ItemRarity` 개명 · 낚시의 오프라인/요일 잔존 정정 (문서 12개) · 🔴 **드롭 롤이 레벨을 무시해 Lv1에서 Lv5가 나온다**(T-017 최우선) · 코드는 미수정
- [2026-08-02 기획 문서 의존 그래프 도입 — 재귀 전파 규칙과 검사기](2026-08-02-doc-graph-propagation.md) — `문서관계도.md`(그래프 단일 원본) + `check-doc-graph.ps1`(깨진 링크·**갱신일 역전**) · 문서 17개 헤더에 `바뀌면 갱신` 블록 · ⚠️ 블록은 그래프 계산에서 제외해야 한다(안 하면 간선이 양방향으로 번진다)
- [2026-08-02 가챠 풀 엑셀 이관 + ItemRarity → GlobalRarity 개명 — 이슈 #9](2026-08-02-gacha-excel-migration.md) — `Gacha.xlsx → GachaTable`(Ref 검사) · 가챠 전용 아이템 6종(100001~) · 하드코딩 `GachaTable.cs` 폐기 → `GachaPoolCatalog` · ⚠️ `EGlobalRarity` 와이어 값 재정렬(서버·클라 동시 빌드 필수)
- [2026-08-02 가챠 응답에 인벤토리 변경분(누적 총량) 포함 — 이슈 #8](2026-08-02-gacha-item-change-notify.md) — `S_GachaDrawResponse.ItemChangeInfos` 추가 · `ItemChangeInfo` 주석 정정(값은 원래 총량) · 클라 `AddGachaRewards` 제거는 클라 담당 몫 · Inventory 회귀 테스트 3건
- [2026-08-02 로그인 Load 경로를 Row 기반으로 리팩토링](2026-08-02-login-load-row-refactor.md) — Row 계약(`PlayerLoginData`) + 영역별 `Load*` 분리 · Dapper 래퍼 `DbConnection` · **Row는 프로퍼티 record + 컬럼명 그대로(snake_case), 매핑 옵션 OFF** · 신규 지급은 로직 스레드로 이동(`Login`은 지급 후)
- [2026-08-01 게임 UI 캔버스 골격 확정 — 16:9 배율 창 · 3열 정렬 · 렌더 모드](2026-08-01-ui-canvas-skeleton.md) — 캔버스 4→3개(위젯은 패널) · **LayoutGroup은 root Canvas를 못 움직인다 → nested Canvas** · Overlay→Screen Space-Camera · `WindowManager` 1:2 세로 → 16:9 절대 배율
- [2026-08-01 mattpocock-skills 각색 — 프로젝트 스킬 갱신 2건 + server-tdd 신설](2026-08-01-skill-updates-from-mattpocock.md) — task-writer(일감 쪼개기 절차) · agent-log-writer(압축 기준) · **server-tdd 신설** · CLAUDE.md 서버 스킬 표 정정
- [2026-08-01 산업 레벨 시스템 기획 — 배치가 (산업, 레벨, 캐릭터) 세 칸이 된다](2026-08-01-industry-level.md) — 경험치 없이 조건 해금 · 효과는 드롭 테이블 확장 · **낚시터·사냥터·수종이 `IndustryLevel`로 통합** · 해금 조건은 미정(T-016)
- [2026-07-30 보상 구조에서 특별보상 층(9:1) 폐지](2026-07-30-special-reward-removal.md) — 판정 1회 = 희귀도 롤 한 번 · 문서 9개 갱신 · **보스 티켓 경로와 낚시 차별화가 미정으로 남음**
- [2026-07-30 작업속도 계산을 가산/승산 분류 합성으로 재구성](2026-07-30-workspeed-add-mul.md) — `적성 × (1 + Σ가산) × Π승산` · `WorkSpeed` 누산기 신설 · **EquipSlot 부위 확정(무기1·장신구2·보석1)**
- [2026-07-29 작업슬롯 패킷 4종 Unity 클라 연동](2026-07-29-workstation-packet-client.md) — 배치/해제 송신 + 30초 채취 푸시 수신 확인 · `MonoService` null 등록 방지(CRTP 대신 런타임 검사)
- [2026-07-29 MIKA001 경고가 Unity 콘솔에 뜨지 않던 문제 수정](2026-07-29-mika001-unity-visibility.md) — 진단의 `Location.None`이 원인(Unity는 위치 없는 경고를 파싱 못 함) · 분석기 DLL 자동 동기화 추가
- [2026-07-29 작업슬롯 서버 구현 — 시각 기반 채취 정산과 30초 푸시](2026-07-29-workstation-slot-impl.md) — `t_workstation_slot` · 패킷 4종 · `LastTickAt` 정산 · 스케줄러 · 테스트 18건
- [2026-07-29 작업슬롯 구조 전환 — 산업 택1·요일 로테이션 폐지](2026-07-29-workstation-slot-design.md) — 슬롯당 캐릭터 1명 · 서버 권위 + 30초 푸시 · **30fps 루프 반대 근거** · P3 근거 상실
- [2026-07-29 가중치 추첨기 WeightedPicker 도입](2026-07-29-weighted-picker.md) — 드롭·희귀도·가챠 공용 추첨기(누적합 + 이진 탐색) · 그룹 인덱스 헬퍼 · 경계 검증 테스트 19건
- [2026-07-29 서버 프레임워크 프로젝트를 MikaNetwork.Lib로 묶어 폴더 구조 정리](2026-07-29-server-folder-restructure.md) — 프레임워크 5개 이동 + MikaSourceGen 이중 폴더 해소 · 게임 코드는 위치 유지(경로 하드코딩)
- [2026-07-29 데이터 파이프라인 스크립트를 GameDesign으로 이전하고 서버 테스트 프로젝트 신설](2026-07-29-pipeline-move-and-tests.md) — `generate-tables.ps1` 이동 + 절대경로 제거 · `WSGameServer.Tests`(xUnit·Shouldly·Moq) 신설
- [2026-07-28 낚시 드롭 테이블 시트 생성 및 ItemTID 참조 무결성 검사 도입](2026-07-28-drop-table-ref-check.md) — Drop.xlsx(낚시 Basic/Special) + `Ref` 마커 기반 참조 검사 · 드롭 시트는 `DropTID(ID)` 선두 규칙
- [2026-07-27 ExcelGenerator 생성 코드를 C# 9로 수정](2026-07-27-csharp9-codegen-fix.md) — 이슈 #6 대응 · 블록 네임스페이스 + 파이프라인 C# 9 규약 검사 추가
- [2026-07-27 낚시 기획 확정 및 요일 로테이션 구조 전환](2026-07-27-fishing-design-lock.md) — 낚시 논점 확정 + 요일 효율 보너스 전환 + **채취주기 30초 통일·개체 변량 폐기**(2차 업데이트)
- [2026-07-26 GameDesignCore 도입 및 시스템별 상세 기획안 구축](2026-07-26-game-design-core.md) — 게임 기획 단일 진입점 + 상세안 7종 + 평가 문서 + 강제 참조 스킬

- [2026-06-12 데스크톱 투명 창 검증 — URP 포기하고 Built-in으로 이전](2026-06-12-desktop-window-urp-to-builtin.md) — 프로젝트 착수 **전** 데모 단계 기록(원본은 `DesktopWindow/README.MD`, 2026-08-12에 이관) · 🔴 **URP는 FinalBlit이 백버퍼 알파를 1로 덮어써 투명이 불가능** — 두 번 시도해 모두 실패, Built-in으로 이전(파이프라인을 바꾸면 이 게임의 전제가 깨진다) · 증상이 "UI는 보이는데 배경만 검정"이라 헷갈린다 · 빌드 검은 화면 원인 3종(스플래시 중 창 핸들·스타일 통째 교체·CanvasScaler Match) · `.meta`까지 복사해 GUID 보존으로 씬 참조 유지 · ⚠️ 당시 구성(`WindowPanelUI`·세로 1/3 프리셋·360×720)은 **현재와 다르다** · 앞으로 참고할 제약·함정은 [`DesktopWindow 규칙.md`](../../Assets/Scripts_Client/DesktopWindow/DesktopWindow%20규칙.md)로 이관됨

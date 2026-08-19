# Agent 작업 로그 (.claude/Agent)

이 폴더는 **코드 작업의 결정·맥락을 다음 작업(사람/Agent)이 참고**하도록 남기는 로그 저장소다.

- 코드 작업을 **마친 뒤** → `agent-log-writer` 스킬에 따라 이 폴더에 로그(.md)를 남긴다. **항상.**
- 과거의 **경위·근거가 필요할 때** → `agent-log-reader` 스킬에 따라 아래 INDEX를 태그로 훑고
  해당 로그 **본문만** 읽는다. 일반적인 기능 추가·수정에는 열지 않는다.

## 파일 규칙

- 파일명: `YYYY-MM-DD-<kebab-slug>.md` · 하나의 작업 단위 = 하나의 로그 파일
- 프론트매터에 `date` · `title` · `tags`를 둔다
- 아래 `INDEX`에 한 줄씩 최신순으로 추가한다

> ⚠️ **INDEX는 한 줄이다. 상세는 본문에 둔다.**
> 여기에 ⚠️ 경고·설계 근거를 늘어놓으면 이 파일이 통째로 읽히는 비용이 매번 발생한다.
> INDEX의 역할은 **"어느 로그를 열지 고르는 것"** 뿐이다.

## 태그

`#client` `#server` `#ui` `#design` `#data` `#test` `#editor` `#docs` `#infra`
— 찾을 때는 태그로 grep한다. 전체를 정독하지 않는다.

## INDEX

<!-- 최신 작업이 위로. 형식: - [YYYY-MM-DD 제목](파일명.md) — `#태그` 한 줄 요약 -->

- [2026-08-20 에디터 메모리 사용량 상단 툴바 표시기](2026-08-20-editor-memory-toolbar.md) — `#client` `#editor` 프로세스 메모리를 메인 툴바에 1초마다 표시하고 클릭 시 언로드+GC로 정리
- [2026-08-19 죽은 저장 쓰기·미사용 API 정리 (P3)](2026-08-19-window-settings-deadcode-cleanup.md) — `#client` `#window` 고정 설정 3개의 죽은 SaveBool 제거·키 예약 표기, SystemCanvasView.Show 예약 문서화, SettingPresenter 토글은 의도적 비활성이라 유지

- [2026-08-19 System 오버레이 2단 구조 통일 + CanvasGroup 토글·로딩 지연 표시 (P2)](2026-08-19-system-overlay-flatten-canvasgroup.md) — `#client` `#ui` Presenter→Panel→Box 3단을 (↓ SUB VIEW) 2단으로 평탄화, SetActive→CanvasGroup 토글, 로딩 0.15s grace로 깜빡임 제거

- [2026-08-19 WindowManager 시작 설정 권위 소스 = 에디터=인스펙터/빌드=저장값 (P1)](2026-08-19-windowmanager-setting-sync.md) — `#client` `#window` LoadSettings를 #if UNITY_EDITOR로 분기 + OnValidate로 위젯 미러, WidgetPositionLayout도 같은 규칙 정렬

- [2026-08-18 서버 왕복 대기 로딩·실패 알림·연결 끊김 종료 (A-2 + A-5)](2026-08-18-server-wait-loading-notice.md) — `#client` `#ui` `#network` `ServerWaitManager` 단일 창구 + `UI/System` 오버레이(로딩·알림)로 요청 대기·실패 사유·연결 끊김 종료를 화면에 노출

- [2026-08-17 항상 위가 작업표시줄에 가려지던 문제 — 주기·포커스 재확정](2026-08-17-topmost-above-taskbar.md) — `#client` `#window` topmost는 한 번만 확정하면 작업표시줄(그 자체 topmost)에 밀린다 → 0.5초 주기 + 포커스 상실 시 `HWND_TOPMOST` 재확정(`SWP_NOACTIVATE`)
- [2026-08-17 타이틀바 제거 동반 UI 개편 — 토글 고정·캔버스 드래그·종료·위치 통합 (A-1 (3))](2026-08-17-titlebar-removal-ui-overhaul.md) — `#client` `#ui` `#window` 토글 3개 저장값 무시 고정 · 캔버스 `IBeginDragHandler`로 창 이동(SC_MOVE 위임) · 종료 버튼 · 위치 드롭다운 6칸 1개로 통합(Middle 제거·레거시 마이그레이션)
- [2026-08-17 창 스냅 어긋남 수정 — 크기·위치 원자 적용 (A-1)](2026-08-17-window-snap-atomic-apply.md) — `#client` `#window` 크기·위치를 따로 적용하던 걸 `ApplySizeAndPosition` 단일 SetWindowPos로 통합 (모니터 오판정·DPI 앵커 오차 제거)
- [2026-08-15 빌드에서 3열 폭이 어긋나는 문제 — 해결](2026-08-15-build-ui-layout-mismatch.md) — `#client` `#ui` `#window` 범인은 `FlexibleGridLayoutGroup`의 `min = 자기 폭` 순환(한 번 넓어지면 못 줄어듦) · 창·열 가설은 둘 다 반증 · 넘침 상시 가드 추가 · ✅ 빌드 검증 완료
- [2026-08-14 문서·스킬 토큰 효율화 + 구조 점검 (Phase 1~8)](2026-08-14-doc-token-diet.md) — `#docs` `#client` `#infra` 트리거 조건화 + 대형 문서·스킬 하이브리드 분할 · 서버·기획은 권고안만
- [2026-08-14 에디터 서버 콘솔 — 버튼으로 WSGameServer 토글 실행](2026-08-14-editor-server-console.md) — `#editor` `#client` 툴바 버튼 → 서버 시작/정지 + 로그 실시간 tail · `Editor/`를 기능별 하위 폴더로 재편 · ⛔ Unity 실구동·`.meta` 미검증
- [2026-08-12 Scripts_Client 문서 구조 재편 — 폴더별 규칙 md 신설과 주석 걷기](2026-08-12-client-doc-restructure.md) — `#client` `#docs` 구조 설명 주석 36%를 md로 이관 · `폴더 구조.md` + 폴더별 `<폴더명> 규칙.md` 6개 신설 · `Common/`만 주석 유지
- [2026-08-12 nullable 참조 형식 규칙을 Arca Unity Toolkit 표준으로 승격](2026-08-12-nullable-rule-to-toolkit.md) — `#client` `#docs` `= null!` + `RequireRef`를 `clean-code-style` 9장으로 · Unity 객체에 `?.`·`??` 금지 근거
- [2026-08-10 UI 레이어를 MVP(Legacy) 구조로 재편 + 메인 화면 토글](2026-08-10-ui-mvp-rename.md) — `#ui` `#client` 접미사를 MVP 역할 기준으로 · 캔버스 통합 · 전환 층 단일화 · 씬 배선 함정 다수(레이아웃·enum·컴포넌트 순서)
- [2026-08-10 클라가 적성 패킷을 쓰기 시작했다 — EIndustryType 대응 + 적성 0 잠금](2026-08-10-client-aptitude-lock.md) — `#client` `#ui` 적성 0은 숨기지 않고 잠근다(기획 확정) · `GetAptitude` 신설
- [2026-08-10 카운트다운이 결과보다 0.9초 빠른 문제 — 위상 오차 3종](2026-08-10-countdown-phase-fix.md) — `#server` `#client` 초 절삭 + 푸시 해상도 + 틱 전진 오차 · `LastTickAtUnixMs` 개명
- [2026-08-10 캐릭터 적성을 패킷으로 — 소유자를 서버 런타임으로](2026-08-10-character-aptitude-packet.md) — `#server` `#design` `CharacterInfo.Aptitudes` + `EIndustryType` 신설(산업과 아이템 분류 분리)
- [2026-08-10 산업 레벨 선택 수단 — DB·패킷](2026-08-10-industry-level-db-packet.md) — `#server` `#data` `industry_level` 컬럼 + 배치 패킷 레벨 필드 · 저장된 해금 레벨 대조 검증
- [2026-08-10 판정 비용 테이블 전환](2026-08-10-judgecost-table.md) — `#server` `#data` `JudgeCost` 상수 → `IndustryLevelCatalog` 테이블 조회
- [2026-08-10 드롭 롤 레벨 분리](2026-08-10-drop-level-filter.md) — `#server` `#data` 드롭 테이블 키를 `(ItemType, Level)`로 확장 · T-017 드롭 오동작 해소
- [2026-08-09 드롭 시트 실데이터 테스트 추가](2026-08-09-drop-sheet-tests.md) — `#server` `#test` `#data` 산업별 Theory 10케이스 · 분포를 1M회 실측해 이항분포 5σ로 검증
- [2026-08-08 Drop 엑셀 산업별 파일 분리 + 시트 병합 규약](2026-08-08-drop-excel-split-sheet-merge.md) — `#data` `#server` 시트 `<테이블명>.<접미사>` 병합 규약 신설 · DropTID 재채번
- [2026-08-07 싱글턴 제거 · `GameServer` 조립 지점](2026-08-07-di-composition-root.md) — `#server` `ILogicExecutor`/`IServer` seam + 생성자 주입으로 탈싱글턴 · `Program.cs` `Run()` await 누락 미해결
- [2026-08-05 클라이언트 구조 재정비 — 송신을 UI로, 매니저는 수신 전담](2026-08-05-client-structure-refactor.md) — `#client` `#ui` 송신 3종을 각 UI로 이관 · `HeartbeatManager`→`PingManager` · 창 설정 `PlayerPrefs` 영속화
- [2026-08-04 하트비트 무응답 세션 정리 · 중복 로그인 kick](2026-08-04-heartbeat-idle-session.md) — `#server` 무응답 세션 15초 정리 · 중복 로그인은 기존 세션을 끊는다
- [2026-08-03 `User` 테스트 seam 열기](2026-08-03-user-testability-seam.md) — `#server` `#test` 채널·DB 큐 주입 + 시각은 인자로 · **`IClock` 주입은 검토 후 철회**
- [2026-08-03 클라 로그 체계 정리 · 하트비트 · 가챠 갱신 통일](2026-08-03-client-log-and-heartbeat.md) — `#client` `ClientLog` 신설(`[↑송신]`/`[↓수신]` 태그) · 가챠를 `ApplyItemChanges` 하나로
- [2026-08-02 기획 문서·엑셀·코드 불일치 전수 정리](2026-08-02-doc-data-code-sync.md) — `#design` `#data` `#docs` 아이템 30→156종 · 문서 12개 갱신 · 드롭 레벨 무시 버그 발견(T-017)
- [2026-08-02 기획 문서 의존 그래프 도입 — 재귀 전파 규칙과 검사기](2026-08-02-doc-graph-propagation.md) — `#design` `#docs` `문서관계도.md`(그래프 단일 원본) + `check-doc-graph.ps1`(깨진 링크·갱신일 역전)
- [2026-08-02 가챠 풀 엑셀 이관 + ItemRarity → GlobalRarity 개명](2026-08-02-gacha-excel-migration.md) — `#data` `#server` 하드코딩 폐기 → `GachaPoolCatalog` · 와이어 값 재정렬(서버·클라 동시 빌드 필요)
- [2026-08-02 가챠 응답에 인벤토리 변경분(누적 총량) 포함](2026-08-02-gacha-item-change-notify.md) — `#server` `S_GachaDrawResponse.ItemChangeInfos` 추가 · 값은 증분이 아니라 총량
- [2026-08-02 로그인 Load 경로를 Row 기반으로 리팩토링](2026-08-02-login-load-row-refactor.md) — `#server` Row 계약(`PlayerLoginData`) + 영역별 `Load*` 분리 · Row는 컬럼명 그대로(snake_case)
- [2026-08-02 요청 1:1 응답 패킷에 EResultCode 도입](2026-08-02-response-result-code.md) — `#server` `bool Success` → `EResultCode`(1~99 공통 / 100~ 가챠 / 200~ 작업슬롯) · 요청엔 반드시 응답한다
- [2026-08-02 일반 캐릭터 1001~1006 입력 · 시작 캐릭터를 1001로](2026-08-02-character-table-fill.md) — `#data` `#design` 캐릭터 6종 추가(1001 시작 · 1002~1006 산업 담당) · `Race` 컬럼은 두지 않는다
- [2026-08-01 게임 UI 캔버스 골격 확정 — 16:9 배율 창 · 3열 정렬](2026-08-01-ui-canvas-skeleton.md) — `#ui` `#client` **LayoutGroup은 root Canvas를 못 움직인다 → nested Canvas** · Overlay→Screen Space-Camera
- [2026-08-01 mattpocock-skills 각색 — 스킬 갱신 2건 + server-tdd 신설](2026-08-01-skill-updates-from-mattpocock.md) — `#docs` task-writer(일감 쪼개기) · agent-log-writer(압축 기준) · server-tdd 신설
- [2026-08-01 산업 레벨 시스템 기획 — 배치가 (산업, 레벨, 캐릭터) 세 칸이 된다](2026-08-01-industry-level.md) — `#design` 경험치 없이 조건 해금 · 낚시터·사냥터·수종을 `IndustryLevel`로 통합
- [2026-07-30 보상 구조에서 특별보상 층(9:1) 폐지](2026-07-30-special-reward-removal.md) — `#design` 판정 1회 = 희귀도 롤 한 번 · 문서 9개 갱신
- [2026-07-30 작업속도 계산을 가산/승산 분류 합성으로 재구성](2026-07-30-workspeed-add-mul.md) — `#design` `#server` `적성 × (1 + Σ가산) × Π승산` · EquipSlot 부위 확정
- [2026-07-29 작업슬롯 패킷 4종 Unity 클라 연동](2026-07-29-workstation-packet-client.md) — `#client` 배치/해제 송신 + 30초 푸시 수신 · `MonoService` null 등록 방지
- [2026-07-29 MIKA001 경고가 Unity 콘솔에 뜨지 않던 문제 수정](2026-07-29-mika001-unity-visibility.md) — `#infra` `#server` 원인은 진단의 `Location.None`(Unity는 위치 없는 경고를 못 읽는다) · 분석기 DLL 자동 동기화
- [2026-07-29 작업슬롯 서버 구현 — 시각 기반 채취 정산과 30초 푸시](2026-07-29-workstation-slot-impl.md) — `#server` `t_workstation_slot` · 패킷 4종 · `LastTickAt` 정산 · 스케줄러
- [2026-07-29 작업슬롯 구조 전환 — 산업 택1·요일 로테이션 폐지](2026-07-29-workstation-slot-design.md) — `#design` 슬롯당 캐릭터 1명 · 서버 권위 + 30초 푸시 · **30fps 루프 반대 근거**
- [2026-07-29 가중치 추첨기 WeightedPicker 도입](2026-07-29-weighted-picker.md) — `#server` 드롭·희귀도·가챠 공용 추첨기(누적합 + 이진 탐색)
- [2026-07-29 서버 프레임워크를 MikaNetwork.Lib로 묶어 폴더 구조 정리](2026-07-29-server-folder-restructure.md) — `#server` 프레임워크 5개 이동 · 게임 코드는 경로 하드코딩 때문에 위치 유지
- [2026-07-29 데이터 파이프라인을 GameDesign으로 이전 + 서버 테스트 프로젝트 신설](2026-07-29-pipeline-move-and-tests.md) — `#infra` `#test` `generate-tables.ps1` 이동 + 절대경로 제거 · `WSGameServer.Tests` 신설
- [2026-07-28 낚시 드롭 테이블 시트 생성 및 ItemTID 참조 무결성 검사 도입](2026-07-28-drop-table-ref-check.md) — `#data` `Ref` 마커 기반 참조 검사 · 드롭 시트는 `DropTID(ID)` 선두
- [2026-07-27 ExcelGenerator 생성 코드를 C# 9로 수정](2026-07-27-csharp9-codegen-fix.md) — `#infra` 블록 네임스페이스 + 파이프라인 C# 9 규약 검사(Unity 제약)
- [2026-07-27 낚시 기획 확정 및 요일 로테이션 구조 전환](2026-07-27-fishing-design-lock.md) — `#design` 요일 효율 보너스 전환 · 채취주기 30초 통일 · 개체 변량 폐기
- [2026-07-26 GameDesignCore 도입 및 시스템별 상세 기획안 구축](2026-07-26-game-design-core.md) — `#design` `#docs` 게임 기획 단일 진입점 + 상세안 7종 + 참조 스킬
- [2026-06-12 데스크톱 투명 창 검증 — URP 포기하고 Built-in으로 이전](2026-06-12-desktop-window-urp-to-builtin.md) — `#client` 🔴 **URP는 FinalBlit이 백버퍼 알파를 1로 덮어써 투명이 불가능** → Built-in 고정(파이프라인을 바꾸면 전제가 깨진다) · 제약은 `DesktopWindow 규칙.md`로 이관

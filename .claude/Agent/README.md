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

- [2026-09-26 창고 칸 툴팁](2026-09-26-storage-slot-tooltip.md) — `#client` `#ui` `#docs` 내용은 공급자 `BuildTooltip`이 만들고 트리거는 격자가 코드로 붙인다 · `RarityLabel` 공용화 (T-050)
- [2026-09-26 치트 지급 3종에 창고 한도 검사](2026-09-26-cheat-storage-limit.md) — `#server` `#test` GiveItem·GiveCharacter·GiveEquip이 넘치면 StorageFull (#40)
- [2026-09-25 럭키 상자 표시 · 수량 팝업 문구 · 일감 전수 점검](2026-09-25-lucky-box-display-and-task-audit.md) — `#client` `#ui` `#docs` 상자 개봉 팝업을 "몇 개를 열까?"로 · 레벨 툴팁에 `■ 럭키 상자` 묶음 (T-030 · 이슈 #33)
- [2026-09-25 창고 가득 참 문구 · enum 개수 파생 · 프레임 수 대조](2026-09-25-storage-full-and-enum-counts.md) — `#client` `#ui` `StorageFull(103)` 문구 · 장비 칸·산업 수를 enum에서 파생 (T-064 · T-085 클라 파트)
- [2026-09-25 공용 툴팁 — 산업 레벨 정보를 펼침 패널에서 툴팁으로](2026-09-25-tooltip.md) — `#client` `#ui` `#docs` 호버는 `IPointerEnter`가 아니라 Win32 커서 폴링 레이캐스트의 맨 위 하나 · 대상 오른쪽 옆, 넘치면 왼쪽 (T-088)
- [2026-09-25 배치·장착 중인 개체도 창고 제자리에 남는다](2026-09-25-storage-keep-deployed-in-place.md) — `#client` `#ui` `#docs` 팰월드식 "목록에서 빼기"를 **철회** → 제자리 + 딤 + `배` 마크 + [정렬] 맨 뒤. 칸 수가 안 변해 **칸 풀 동적 관리가 통째로 불필요**해졌다 (T-086·T-087 보관 · 이슈 #36 닫음)
- [2026-09-25 창고 칸에서 빠진 자리를 빈 칸으로 남긴다](2026-09-25-storage-keep-empty-slots.md) — `#client` `#ui` 장착·배치로 개체가 빠져도 **뒤의 것을 당겨 오지 않는다** — 자리(`Key → 칸`)를 공급자가 들고, 새 항목은 첫 빈 칸으로
- [2026-09-24~25 창고에서 배치·장착 개체를 다루는 방식](2026-09-24-storage-sort-assigned-first.md) — `#client` `#ui` 앞으로 몰기·줄 끊기·딤·고정 패널을 **세 번 시도하고 전부 롤백** → **창고 목록에서 빼는** 팰월드 방식으로 전환 (T-086 · 이슈 #36). **표시를 세 번 고쳐도 안 풀리면 판정을 의심한다**
- [2026-09-24 거래소 — 자원 수량 구매 · 시세 (T-091)](2026-09-24-market-quantity.md) — `#server` `#test` `#docs` 로스트아크식으로 자원을 최저가부터 원하는 수량만큼 여러 판매자에 걸쳐 사고, 매물을 일부씩 파는 구조로 바꿨다
- [2026-09-24 경매장 서버 — 즉시구매 (T-094)](2026-09-24-auction-server.md) — `#server` `#data` `#test` `#docs` 별도 프로세스 경매장과 메인 연동(잠금·에스크로·우편 정산·outbox·대사)을 구현하고 실측·독립 리뷰까지 반영했다
- [2026-09-24 캐릭터 레벨 효과 간소화 요청 — 적성 포인트를 자동 가산으로](2026-09-24-character-level-effect-simplification.md) — `#design` `#docs` 서버는 완성돼 있고 클라 화면만 없다 · 상한 150칸이 등급별 한 값이라 배치 퍼즐 방어가 성립하지 않아 이슈 #35로 간소화를 요청했다
- [2026-09-24 작업슬롯에서 캐릭터를 뺄 때 장비도 함께 해제한다](2026-09-24-unassign-releases-equips.md) — `#client` `#ui` 서버의 배치 해제는 착용을 건드리지 않아, 클라가 칸 4개의 해제를 먼저 보내고 배치 해제를 잇는다
- [2026-09-24 공용 값 소유권 전수 조사 — T-085 통합·이슈 #34](2026-09-24-shared-constant-ownership.md) — `#docs` `#server` `#client` 클라·서버 이중 상수와 한도 검사 누락을 한 일감으로 묶고 소유를 서버로 정리했다
- [2026-09-24 산업 레벨 정보 펼침 패널 + 화면의 스크롤을 하나로](2026-09-24-industry-level-info.md) — `#client` `#ui` 레벨 탭이 곧 펼침 토글(`▲`/`▼`) · 3중이던 스크롤을 **머리 고정 + 몸통 하나**로 합쳤다(`flexH`는 스크롤 안에서 뜻을 잃는다 · 껍데기를 지우면 Viewport의 형제도 죽는다)
- [2026-09-24 장비 장착 · 해제 UI — 효율 계산 자리를 갈아 끼운다](2026-09-24-equip-attach-ui.md) — `#client` `#ui` 팝업 없이 같은 자리를 고르기 목록으로 바꿨다 · 칸 넷이 `▼`에 밀려 폭이 깨진 원인은 LayoutElement 부재 · 속도 가산 줄을 만들며 전역 배수 역산도 고쳤다
- [2026-09-23 장비 인챈트 (T-092)](2026-09-23-equip-enchant.md) — `#server` `#data` `#docs` 인챈트 부여·큐브·확장과 속도·경험치 가산을 구현하고 실서버·문서 전파까지 마쳤다
- [2026-09-23 인챈트 데이터 테이블](2026-09-23-enchant-data-tables.md) — `#data` `#design` EquipEnchant.xlsx 3시트와 인챈트 아이템 5종을 넣고 파이프라인을 돌렸다 (서버 코드는 미구현)
- [2026-09-23 치트창 해금 칸을 세 묶음으로 접는다](2026-09-23-cheat-unlock-foldouts.md) — `#client` `#editor` 51줄을 작업슬롯·산업 레벨·산업 속도로 갈랐다 (TID 대역이 아니라 `UserTraitTable`에서 파생)
- [2026-09-23 슬롯 배치 리스트에 산업 레벨 표기](2026-09-23-slot-list-industry-level.md) — `#client` `#ui` 레벨 이름 대신 `Lv{n}`만 붙여 한 줄을 유지했다 (오토사이징이라 길어지면 글자가 조용히 작아진다)
- [2026-09-22 서버 답이 돌아온 이슈 #30·#31 정리](2026-09-22-issue-30-31-close.md) — `#docs` `#client` 가챠 1번 풀은 테스트가 읽고 있어 남기고, 치트 해금은 포인트 미차감이 정의라 치트창을 그대로 뒀다
- [2026-09-22 창고 탭이 꺼진 채 저장된 SUB VIEW를 깨운다](2026-09-22-storage-tab-wake-screens.md) — `#client` `#ui` 초기 활성 상태가 씬 저장값에 의존해 특성 탭이 빈 화면이 되던 것을 탭 줄이 켜고 시작하도록 고쳤다
- [2026-09-20 특성 트리 화면 · 계정 레벨 표시 · 산업 레벨 선택 칸](2026-09-20-trait-tree-account-level.md) — `#client` `#ui` 창고 특성 탭만 격자를 쓰지 않는 예외로 두고, 트리 모양을 TID가 아니라 테이블에서 파생시켰다
- [2026-09-20 unity-handoff → unity-editor-ops 개편](2026-09-20-unity-editor-ops-skill.md) — `#docs` `#infra` `#editor` 에디터를 직접 조작하는 전제로 다시 쓰고, auto 모드에서 작동하는 확인 수단은 `deny`뿐임을 실측
- [2026-09-20 가챠 장비 풀 배선 · 구슬 풀 제거 · 상자 개봉](2026-09-20-gacha-equip-and-box-open.md) — `#client` `#ui` 보상 결과 팝업을 상자와 공유하고, 개봉은 비어 있던 좌클릭 축에 붙였다
- [2026-09-20 github-issue-writer 스킬 신설 · 가챠 구슬 풀 제거 이슈](2026-09-20-github-issue-writer-skill.md) — `#docs` `#infra` 이슈 제목 꼬리표·assignee/label 필수 규칙을 스킬로 굳히고 마스터에 승격
- [2026-09-20 0920 QA 피드백 답변 문서화 · 일감 반영](2026-09-20-qa-feedback-0920.md) — `#docs` `#client` 규빈님 피드백 8건에 답을 달고 일감 4건 신규 + T-058 확장
- [2026-09-19 장비 시트 밸런싱 — 5행 → 69종](2026-09-19-equip-table-balance.md) — `#data` `#design` 무기·보석은 산업 전용, 장신구는 전 산업 (Common~Mythic)
- [2026-09-19 창고 장비 탭 · 치트 장비 지급·정산 횟수](2026-09-19-equip-storage-tab.md) — `#client` `#ui` `#editor` 장비가 화면에 보이는 첫 경로 (T-043 장비 몫 · T-059)
- [2026-09-17 캐릭터 장비 시스템 서버 구현 — EquipTable · 장착/해제 · 속도 가산](2026-09-17-equip-server.md) — `#server` `#data` `#protocol` `#test` T-002 완료 · 종류/칸 분리 · 자동 이동 트랜잭션 · Unity CLI 실서버 검증
- [2026-09-16 서버 긴 설계 근거 주석을 Server/docs/로 분리](2026-09-16-server-comments-to-docs.md) — `#server` `#docs` 채취-정산·세션-감시·데이터-카탈로그 문서 신설, 코드엔 1~2줄 + 링크만
- [2026-09-17 적성 포인트 — 엑셀 상한·DB·패킷·서버 구현 (T-003)](2026-09-17-aptitude-point-server.md) — `#server` `#data` `#protocol` `#db` `#test` 10레벨마다 1포인트로 산업 적성 +1, 캐릭터·산업별 상한, 남은 포인트는 저장하지 않고 계산
- [2026-09-16 DB 작업 실패 경로 — OnFailed로 되돌리고 세션을 끊는다 · 밴/삭제 응답](2026-09-16-db-failure-path.md) — `#server` `#db` `#protocol` `#test` DB 실패·밴·삭제가 무응답으로 멈추던 로그인 경로 · 첫 `:memory:` SQLite 테스트
- [2026-09-16 DB 작업 파티션 키를 SessionId에서 계정(Pid 해시)으로](2026-09-16-db-partition-key-by-account.md) — `#server` `#db` `#test` 재접속 시 마지막 쓰기와 첫 읽기가 다른 채널에서 병렬로 돌아 수확이 사라지던 문제
- [2026-09-18 설정에 프레임 제한 · FPS 텍스트 위치 드롭다운](2026-09-18-frame-rate-fps-text.md) — `#client` `#ui` `DisplayManager`(구 `FrameRateManager`) · `FpsTextPresenter` 신설 (T-062)
- [2026-09-17 작업슬롯 칸 바탕을 캐릭터 등급 색으로](2026-09-17-workslot-rarity-color.md) — `#client` `#ui` 목록·위젯 미니 칸에 `SetRarity` (T-061)
- [2026-09-17 치트 에디터 창 · 안전장치 팝업 · 씬 복사 알림 툴바 토글](2026-09-17-cheat-editor-window.md) — `#client` `#editor` 치트를 한 창에 모아 보내기 (T-059)
- [2026-09-16 잠긴 작업슬롯 표시 · 해금 요청](2026-09-16-workslot-locked-display.md) — `#client` `#ui` 열린 목록 기반 잠김 판정 · `ConfirmPresenter` · `C_UnlockRequest` 연결 (T-039)
- [2026-09-16 해금 시스템 서버 구현 — `UnlockCatalog` · `t_user_unlock` · 해금 패킷, 첫 적용은 작업슬롯](2026-09-16-unlock-server.md) — `#server` `#protocol` `#test` T-038 완료 · Unity CLI eval로 실서버 왕복 검증
- [2026-09-16 창고 캐릭터 칸 — 레벨 배지 · 경험치 세로 게이지 · 칸이 프레임을 채움](2026-09-16-character-level-exp-gauge.md) — `#client` `#ui` `#editor` 아이콘 모서리 레벨 배지 · 왼쪽 벽 Slider 게이지 · 여백 예외 (T-052)
- [2026-09-16 창고 [정렬] 버튼 · 배치 목록 적성순 · 캐릭터 줄 등급 색](2026-09-16-storage-sort-and-select-order.md) — `#client` `#ui` 필터 대신 정렬 버튼(세션 한정 기억) · 배치 목록 적성순 (T-015 · T-057)
- [2026-09-15 memory-meter 플레이 중 갱신 정지 · Material 예외 원인 재조사](2026-09-15-memory-meter-playmode-pause.md) — `#editor` `#client` 플레이 중 정지는 예방 조치 · memory-meter 원인설은 재현 실패로 미확인
- [2026-09-15 서버 콘솔 창 프레임 드랍 — 가상 스크롤로 교체](2026-09-15-server-console-virtual-scroll.md) — `#editor` `#client` 로그 전체 재레이아웃이 원인 · 보이는 줄만 그리고 줄 단위 복사
- [2026-09-14 작업슬롯 선택 화면을 목업 구조로](2026-09-14-workstation-select-mockup-layout.md) — `#client` `#ui` `#editor` 산업 아이콘 · 캐릭터 카드 · 장비 자리 · 효율 계산 스크롤 (T-053)
- [2026-09-14 작업슬롯 세팅 패널에 배치된 캐릭터 이름·적성 표시](2026-09-14-workstation-setting-assigned-info.md) — `#client` `#ui` `#editor` 해제 버튼만 있던 3단계에 두 줄 표시 · 패널 VLG 정석화 (T-035)
- [2026-09-13 문서 토큰 효율화 — 이슈 #18 서버·기획 쪽 적용](2026-09-13-doc-token-diet-server-side.md) — `#docs` `#design` INDEX·공용 스킬 2개·게임기획코어 목차. 공유 트리 동시 작업 지뢰 기록
- [2026-09-16 치트 명령 — admin 전용 지급·정산 패킷](2026-09-16-cheat-system.md) — `#server` `#protocol` enum 명령 + 기존 지급 함수 재사용, 테스트 13건 · 해금 재검토 7건은 09-14 로그 업데이트 절
- [2026-09-14 해금 시스템 기획 — `UnlockTable` 한 표 · 해금 행동 · 첫 적용은 작업슬롯](2026-09-14-unlock-system-design.md) — `#design` `#data` 세 곳의 해금을 한 표로 모으고 산업 레벨 조건에서 적성을 뺐다 · `Unlock.xlsx` 신설
- [2026-09-13 캐릭터 경험치 곡선 — 판정 1회당 `ExpPerJudge` · `CharacterLevelTable` 만렙 100](2026-09-13-character-exp-curve.md) — `#data` `#design` T-003의 획득식·곡선을 엑셀로 확정하고 서버 누적·레벨업·저장·푸시까지 구현
- [2026-09-12 씬에 붙지 않는 UI 부품에 자리를 만들었다 — `UI/Shared/` 와 거짓 이름 3건 개명](2026-09-12-ui-shared-folder.md) — `#client` `#ui` `#editor` `#docs` 폴더가 하이어라키의 거울이라 씬에 안 붙는 파일은 갈 곳이 없었다 · `SlotView`/`SlotData`/`SellCartPresenter`
- [2026-09-12 창고 칸의 적성 표시를 문구에서 5칸 스트립으로](2026-09-12-storage-aptitude-strip.md) — `#client` `#ui` `#editor` 19자가 100px 칸에서 5px로 줄던 것을 20×20 다섯 칸(위치=산업·숫자만)으로 바꿔 14px 확보
- [2026-09-11 배치 목록을 걸러 내는 쪽으로 뒤집고 줄을 프리팹 풀로 전환](2026-09-11-assign-list-filter-and-row-prefab.md) — `#client` `#ui` 산업·배치로 목록을 거르고 줄은 프리팹 풀 · 창고 칸에 '배' 마크와 적성 요약
- [2026-09-10 배치된 작업슬롯의 산업 교체](2026-09-10-workstation-industry-swap.md) — `#client` `#ui` 세팅 단계의 불빛을 슬롯에서 파생시켜 롤백 경로 없이 교체를 붙였다 · 실측 통과
- [2026-09-07 핑 송신을 백그라운드 타이머로 — 드래그 끊김의 클라 몫](2026-09-07-ping-timer-client.md) — `#client` `#network` 넣는 쪽(타이머)과 비우는 쪽(B-1)이 **둘 다** 메인 스레드 밖이어야 한다 · 🔑 오탐 가드가 없으면 서버가 안 끊어도 **클라가 스스로 앱을 내린다** · ✅ 실측 통과(20초 드래그 · 1시간 방치) — 이슈 #19·#21 닫힘
- [2026-09-06 창고 캐릭터 칸에 등급 색 표시](2026-09-06-character-slot-rarity-color.md) — `#client` `#ui` `#data` 공급자가 `CharacterTable` 등급을 칸에 넘긴다(TID로 읽는다)
- [2026-09-04 인벤토리 판매 UI + 가챠 4버튼 · 캐릭터 보상 분기](2026-09-04-sell-ui-and-gacha-buttons.md) — `#client` `#ui` `#packet` 우클릭으로 담고 한 번에 판다 · 결과창이 `ItemId`만 읽던 잠복 결함을 닫았다 · 🔴 팝업 자리를 한 번 틀렸다(캔버스 order)
- [2026-09-02 캐릭터 가챠 — 시트 3분할 · 캐릭터 30종 · 골드 비용](2026-09-02-character-gacha.md) — `#server` `#data` `#design` 보상 종류를 컬럼이 아니라 시트로 갈랐다 (T-026·T-027)
- [2026-09-02 하트비트 — A-1 적용 정정 (클라분은 롤백)](2026-09-02-heartbeat-a1-fix-and-ping-timer.md) — `#server` `#design` 늘린 것이 판정이 아니라 검사 주기였다 · 핑 주기 ≤ 판정 ÷ 3 규칙화 · 클라 타이머는 T-041로 · ✅ 2026-09-08 업데이트: 전부 닫힘
<!-- 최신 작업이 위로. 형식: - [YYYY-MM-DD 제목](파일명.md) — `#태그` 한 줄 요약 -->

- [2026-09-02 재화 패킷 열 축 대응 + 상태바 아이콘](2026-09-02-currency-column-axis-and-state-icons.md) — `#client` `#ui` `#packet` 사전을 걷어내고 필드로 (행 축을 클라만 들고 있었다) · 오브젝트를 옮겨도 인스펙터 배선은 살아남는다
- [2026-09-02 창고 탭 전환 — 격자 하나 + 탭별 공급자](2026-09-02-storage-tab-grid-sources.md) — `#client` `#ui` 캐릭터 탭의 ⏸가 오해였다(데이터는 이미 왔다). GUID 유지 개명으로 씬 배선이 살아남았다
- [2026-08-31 스킬 동기화 오탐](2026-08-31-skill-sync-false-diff.md) — `#infra` `#docs` CRLF와 의도적 중립화를 차이로 세어 1,600줄 오탐. 세었으면 읽어야 한다
- [2026-08-31 플레이 종료 때 뜨던 UITK Material 예외](2026-08-31-uitk-material-missing-reference.md) — `#editor` 유니티 내부 레이스(18회 중 2회). 메모리 미터가 매초 툴바를 갱신해 창을 열어 두고 있었다
- [2026-08-30 로그인 화면에 종료 버튼 연결](2026-08-30-login-exit-button.md) — `#client` `#ui` 로그인 중엔 상태 패널 종료 버튼이 가려져 ESC뿐이었다. 씬 커밋 시 m_IsActive 확인 교훈
- [2026-08-30 창 크기를 유니티가 늦게 덮어쓰던 문제](2026-08-30-window-size-instrumentation.md) — `#client` 우리 적용 0.5초 뒤 유니티가 저장 해상도로 덮어 두 증상이 났다. 계측으로 확정 · 부팅 8초 감시로 차단
- [2026-08-30 보더리스 창이 프레임 두께만큼 부풀던 문제](2026-08-30-borderless-frame-inflation.md) — `#client` `GetWindowLong`이 교체 직후 옛 스타일을 줘 렌더 영역이 47px 커지고, 창 밀림·맞춤 깨짐 두 증상이 함께 났다
- [2026-08-30 재시작 시 창이 작업표시줄 높이만큼 위로 밀리던 문제](2026-08-30-window-position-restore.md) — `#client` 앵커만 저장되고 실제 좌표는 안 저장되던 것을 좌표 저장·복원으로 고침(앵커보다 우선)
- [2026-08-30 창 크기에 작업표시줄 맞춤 항목 추가](2026-08-30-taskbar-fit-scale.md) — `#client` `#ui` 위젯 바가 작업표시줄과 같은 높이가 되는 배율을 실측 역산해 드롭다운 5번째 항목으로 추가
- [2026-08-30 드래그로 화면 밖에 나간 창을 되올린다](2026-08-30-window-drag-clamp.md) — `#client` 드래그 종료 시 `ClampIntoMonitor`가 세로만 모니터 안으로 되돌린다(기준은 작업 영역이 아니라 모니터 전체)
- [2026-08-28 하트비트 끊김 — 원인 규명·시험 적용·철회](2026-08-28-ping-disconnect-cause.md) — `#client` `#server` `#infra` 판정/주기 여유 0과 세션 루프의 메인 스레드 종속을 찾아 고쳤으나, 서버 담당 폴더라 전부 되돌리고 이슈 #19로 판단 요청
- [2026-08-28 씬이 저절로 더티가 되는 원인 제거](2026-08-28-scene-dirty-flag.md) — `#client` `#ui` `#editor` `OnValidate`의 무조건 `SetDirty`와 `[ExecuteAlways]`의 무변경 쓰기를 막고, 진단 도구 `scene-dirty-tracer`를 추가
- [2026-08-27 클라 일감 4건 점검 · T-034 등록](2026-08-27-client-task-audit.md) — `#client` `#docs` 미체크 항목을 ⏸(기획·서버 대기)와 🎨(연출·리소스 후순위)로 가르고, 순서 메모를 서버·기획 요청 목록으로 재작성
- [2026-08-26 Common 자산별 재편 + 툴킷 승격](2026-08-26-toolkit-common-restructure.md) — `#client` `#editor` `#infra` Common을 자산 하나=폴더 하나로 재편하고, 범용 코드 3종·클라 스킬 2종·작업관리 스킬 4종을 Arca Unity Toolkit 마스터로 올렸다
- [2026-08-26 위젯 수확 스트립 + `PlayerDataLogger` 삭제](2026-08-26-widget-harvest-strip.md) — `#client` `#ui` `#editor` `#docs` 위젯이 배치된 슬롯을 게이지로 그리게 하고, 대체 화면이 갖춰진 임시 로그 관찰자를 걷어냄
- [2026-08-25 가챠 결과 팝업 신설](2026-08-25-gacha-result-popup.md) — `#client` `#ui` `#editor` `!System Canvas`에 `GachaResultPresenter` 추가, 칸은 `InventorySlotView` 프리팹 재사용
- [2026-08-25 Scripts_Client 전량 스타일 정리](2026-08-25-comment-brace-spacing-style.md) — `#client` `#docs` XML 문서 주석 제거·제어문 중괄호 강제·수직 간격 규칙 적용
- [2026-08-23 Editor·UI 규칙 문서를 하위 폴더별로 분할](2026-08-23-rule-doc-split.md) — `#docs` `#client` `#ui` `#editor` 하위 폴더 규칙.md 12개 신설하고 참조를 절 제목 방식으로 전환
- [2026-08-23 로딩 오버레이 — 차단은 즉시, 표시만 0.15초 지연](2026-08-23-loading-block-immediate.md) — `#client` `#ui` 대기 중 화면이 꺼져 응답을 놓치던 거짓 무응답 알림을 즉시 차단으로 없앰
- [2026-08-22 메모리 미터 툴팁 조판 정리 + 커밋/워킹셋 설명 정정](2026-08-22-memory-tooltip-layout.md) — `#client` `#editor` `#docs` 툴팁 폭은 못 늘리므로 줄을 24자로 끊고, 긴 설명은 Editor 규칙.md로 이관
- [2026-08-22 열의 남는 높이를 위젯 2 : 상태 1로 나누기](2026-08-22-column-side-height-ratio.md) — `#client` `#ui` WidgetPositionLayout이 사이드 칸 높이를 비율로 계산해 preferredHeight에 못박는다
- [2026-08-20 서버 대기 로딩·실패 알림 이후 낡은 문서·주석 정정](2026-08-20-server-wait-docs-followup.md) — `#docs` `#client` `#ui` 8975b28이 .md를 안 담아 남은 구현 이전 서술을 문서 5·주석 3에서 정정
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

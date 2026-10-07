---
id: T-132
제목: 클라 — 오브젝트 풀링 도입 · 코루틴을 UniTask·DOTween으로
담당: 태웅
상태: Closed
우선순위: 보통
목표: 261024
---

# T-132 클라 — 오브젝트 풀링 도입 · 코루틴을 UniTask·DOTween으로

## 배경

UniTask·DOTween을 들였다(`UNITASK_DOTWEEN_SUPPORT`, 2026-10-07). 새 클라 코드는 코루틴을 쓰지 않고,
자주 생기고 사라지는 오브젝트는 풀로 돌려쓴다. 규칙은 스킬 두 개 —
[`unitask`](../../.claude/skills/client/unitask/SKILL.md) · [`dotween`](../../.claude/skills/client/dotween/SKILL.md) · [`object-pool`](../../.claude/skills/client/object-pool/SKILL.md).

조사 결과(2026-10-07) **지금 코드는 생성·삭제가 잦지 않다** — 목록 줄 대부분은 이미 "늘리기만 하고 남는 줄은 끄는" 방식이고,
`Destroy`는 슬롯 뷰 해제·무대 배경 층 두 곳뿐이었다. 풀링의 실익은 **앞으로 만들 획득 연출**과 복제된 줄 풀 코드 정리다.

## 할 일

- [x] `Common/object-pool/` — `PrefabPool<T>` · `PooledObject`(대여 토큰·이중 반납 방어) · `IPoolable` · `UIRowList<T>` (2026-10-08)
- [x] 스킬 `unitask-dotween` · `object-pool` + `CLAUDE.md`·`폴더 구조.md` 3-6 연결 · 툴킷 마스터 반영 (2026-10-08)
- [x] 코루틴 → UniTask — `ServerWaitManager`(응답 타임아웃) · `LoadingPresenter`(지연 표시) · `WindowManager`(창 핸들 대기·크기 감시). 남은 코루틴 0건
- [x] 슬롯 뷰 — `WorkStationListPresenter`·`WidgetPresenter`는 해제 때 파괴 대신 끄고 다시 쓴다 · `SlotStageView` 배경 층 재사용
- [x] `AuctionRowList`를 `UIRowList` 위에 얹음
- [x] 실측 — 로딩 지연 표시 · 응답 타임아웃 알림 · 슬롯 배치→해제→다른 캐릭터 배치 (이전 그림이 남지 않나) · **빌드에서** 창 초기화(`Player.log`)
- ✂️ 목록 줄 정리 → [T-133](../T-133-목록줄UIRowList전환.md)로 뗐다
- ✂️ 수확 획득 연출 → 🎨 [T-015](T-015-산업아이템클라적용.md)(채취 획득 표시)에 `PrefabPool`로 만든다고 적어 뒀다

## 완료 조건

새 클라 코드에 코루틴이 없고, 연출 반복 중 Profiler에서 `Object.Instantiate` 샘플이 0이다.

## 막고 있는 것 / 선행 일감

- 없음

## 관련 커밋

- `92569b2` 풀·코루틴 → UniTask·슬롯 뷰 재사용·경매 줄 · `e0a773d` 스킬·문서 · 이 커밋 — 사용자 빌드 실측(2026-10-08)

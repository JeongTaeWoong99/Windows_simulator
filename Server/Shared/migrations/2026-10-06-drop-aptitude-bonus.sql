-- T-003 레벨 효과 = 작업속도 자동 가산 (2026-10-06 · 이슈 #35). 한 번만 돌린다. 서버를 끈 상태에서 적용한다.
-- 적성 포인트(수동 찍기)를 철거하면서 찍은 보너스 5컬럼을 지운다. 적성은 CharacterTable 값으로 고정이다.
-- 새 서버는 이 컬럼을 읽지도 쓰지도 않으므로, 돌리기 전에도 동작은 같다 — 남은 값은 버려진다(테스트 단계).
BEGIN;

ALTER TABLE t_character DROP COLUMN farming_bonus;
ALTER TABLE t_character DROP COLUMN fishing_bonus;
ALTER TABLE t_character DROP COLUMN mining_bonus;
ALTER TABLE t_character DROP COLUMN logging_bonus;
ALTER TABLE t_character DROP COLUMN hunting_bonus;

COMMIT;

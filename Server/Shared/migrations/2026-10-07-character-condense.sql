-- T-130 캐릭터 응축 (2026-10-07). 한 번만 돌린다. 서버를 끈 상태에서 적용한다.
-- 개체는 넣은 재료의 누적 수만 기억한다 — ★은 CharacterStarTable의 누적 기준으로 서버가 읽는다.
BEGIN;

-- 응축에 넣은 재료의 누적 수. 최고 ★ 기준(지금 76)에서 멈춘다
ALTER TABLE t_character ADD COLUMN condense_count INTEGER NOT NULL DEFAULT 0;

COMMIT;

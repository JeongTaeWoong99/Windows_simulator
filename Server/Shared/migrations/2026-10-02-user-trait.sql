-- T-108 레벨형 특성 (2026-10-02). 한 번만 돌린다. 서버를 끈 상태에서 적용한다.
-- 특성이 해금 노드(t_user_unlock)이던 것을 특성별 레벨 한 줄로 바꾼다.
BEGIN;

CREATE TABLE t_user_trait (
    user_id        INTEGER NOT NULL,            -- 소유 유저 (t_user.user_id 참조)
    user_trait_tid INTEGER NOT NULL,            -- 특성 (UserTraitTable.UserTraitTID)
    level          INTEGER NOT NULL,            -- 지금 레벨. 기본 레벨보다 올린 특성만 행이 있다
    PRIMARY KEY (user_id, user_trait_tid)       -- (유저, 특성) 유일 = UPSERT 타깃 · 로그인 조회도 user_id 접두로 탄다
) STRICT;

-- 옛 특성 노드 해금(2102~2505 산업 레벨 · 3101~3505 속도). 레벨로 옮기지 않는다 — 테스트 단계라 DB를 비웠다.
DELETE FROM t_user_unlock WHERE unlock_tid BETWEEN 2100 AND 3599;

COMMIT;

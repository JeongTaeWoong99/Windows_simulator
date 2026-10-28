-- 0010 거점 개편 — 건물 5채 · 건물별 특성 트리 (T-183 · 이슈 #75)
-- 계정 특성 포인트를 없애고 건물 레벨이 그 건물의 포인트가 된다. 옛 특성은 작업장 트리 노드로 옮기되 기록은 초기화한다(테스트 단계).
-- 옛 섬 건물(T-166)은 해금 행(4xxx)이었다 — 표에서 지웠으므로 기록도 지운다.

CREATE TABLE t_user_building (
    user_id      INTEGER NOT NULL,          -- 소유 유저 (t_user.user_id 참조)
    building_tid INTEGER NOT NULL,          -- 건물 (BuildingTable.BuildingTID)
    level        INTEGER NOT NULL,          -- 지금 레벨. 행이 없거나 0(치트로 내림)이면 짓지 않은 것 — Lv0
    PRIMARY KEY (user_id, building_tid)     -- (유저, 건물) 유일 = UPSERT 타깃 · 로그인 조회도 user_id 접두로 탄다
) STRICT;

DELETE FROM t_user_trait;

DELETE FROM t_user_unlock WHERE unlock_tid BETWEEN 4000 AND 4999;

ALTER TABLE t_user_account DROP COLUMN trait_point;

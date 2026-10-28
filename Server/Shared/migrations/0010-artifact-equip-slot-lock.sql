-- 0010 아티팩트 칸을 장비 칸과 합치고 번호 단위로 잠근다 (T-188 · 이슈 #87)
-- 아티팩트 칸(t_user_artifact_stack.slot)과 인벤토리 장비 칸(t_user_equip.slot_position)이 한 격자를 함께 쓴다.

-- 아티팩트 잠금 — 개체가 없어 번호 단위다. 잠긴 번호만 행이 있고, 보유가 0개가 돼도 남는다(자원 잠금과 같다).
CREATE TABLE t_user_artifact_lock (
    user_id      INTEGER NOT NULL,          -- 소유 유저 (t_user.user_id 참조)
    artifact_tid INTEGER NOT NULL,          -- 잠긴 아티팩트 번호 (ArtifactTable.ArtifactTID)
    PRIMARY KEY (user_id, artifact_tid)     -- (유저, 번호) 유일 = 두 번 잠가도 한 줄 · 로그인 조회도 user_id 접두로 탄다
) STRICT;

-- 장비 칸과 겹친 아티팩트가 있는 유저만, 아티팩트 칸을 인벤토리 장비 마지막 칸 뒤로 칸 순서대로 다시 놓는다.
-- PK(user_id, slot)가 중간에 부딪히지 않게 먼저 멀리 비켜 둔다. 격자(50칸)를 넘친 칸은 서버가 적재 때 빈 칸으로 옮긴다.
CREATE TEMP TABLE _overlap_user AS
SELECT DISTINCT s.user_id,
       (SELECT MAX(e2.slot_position) + 1 FROM t_user_equip e2
        WHERE e2.user_id = s.user_id AND e2.container = 0 AND e2.auction_trade_id = 0) AS base
FROM t_user_artifact_stack s
JOIN t_user_equip e ON e.user_id = s.user_id AND e.slot_position = s.slot AND e.container = 0 AND e.auction_trade_id = 0;

UPDATE t_user_artifact_stack SET slot = slot + 1000000
WHERE user_id IN (SELECT user_id FROM _overlap_user);

UPDATE t_user_artifact_stack AS s
SET slot = r.new_slot
FROM (SELECT st.user_id, st.slot,
             o.base + ROW_NUMBER() OVER (PARTITION BY st.user_id ORDER BY st.slot) - 1 AS new_slot
      FROM t_user_artifact_stack st
      JOIN _overlap_user o ON o.user_id = st.user_id) AS r
WHERE s.user_id = r.user_id AND s.slot = r.slot;

DROP TABLE _overlap_user;

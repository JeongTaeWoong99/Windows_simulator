-- 0009 아티팩트를 인벤토리로 받아 도감에 장착 (T-181 · 이슈 #81)
-- 감정한 아티팩트는 인벤토리 아티팩트 탭에 번호별로 쌓이고, 도감에는 번호마다 1개를 장착한다. 장착한 번호만 효과가 붙는다.
-- t_user_artifact는 "보관함 수량"에서 "도감 장착"으로 바뀐다. 옛 수량은 1개를 도감에 남기고 나머지를 인벤토리로 옮긴다.

CREATE TABLE t_user_artifact_stack (
    user_id      INTEGER NOT NULL,          -- 소유 유저 (t_user.user_id 참조)
    slot         INTEGER NOT NULL,          -- 인벤토리 아티팩트 탭의 칸 번호(0부터)
    artifact_tid INTEGER NOT NULL,          -- 아티팩트 번호 (ArtifactTable.ArtifactTID). 같은 번호가 여러 칸에 있을 수 있다
    count        INTEGER NOT NULL,          -- 이 칸의 수량. 1 이상 Constants.ArtifactMaxStack 이하
    PRIMARY KEY (user_id, slot)             -- (유저, 칸) 유일 = UPSERT 타깃 · 로그인 조회도 user_id 접두로 탄다
) STRICT;

-- 도감에 1개를 남기고 남는 수량을 칸 0부터 옮긴다. 1000(ArtifactMaxStack)을 넘는 몫은 버린다 — 지금 데이터에는 없다.
INSERT INTO t_user_artifact_stack (user_id, slot, artifact_tid, count)
SELECT user_id,
       ROW_NUMBER() OVER (PARTITION BY user_id ORDER BY artifact_tid) - 1,
       artifact_tid,
       MIN(count - 1, 1000)
FROM t_user_artifact
WHERE count > 1;

-- 다 내보낸 번호(수량 0)는 도감에 장착한 적이 없는 것으로 본다.
DELETE FROM t_user_artifact WHERE count = 0;

ALTER TABLE t_user_artifact ADD COLUMN is_active INTEGER NOT NULL DEFAULT 1;  -- 효과 켜짐 (0/1) — 도감에서 빼는 대신 끈다

ALTER TABLE t_user_artifact DROP COLUMN count;

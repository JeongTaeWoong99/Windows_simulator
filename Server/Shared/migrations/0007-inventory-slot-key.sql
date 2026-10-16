-- 0007 자원 칸별 묶음 (T-160) — 같은 자원이 여러 칸에 나뉠 수 있다. MaxStack은 칸 하나의 상한이 된다.
-- 키를 (유저, 보관함, 아이템)에서 (유저, 보관함, 칸)으로 바꾼다. SQLite는 PK를 고칠 수 없어 표를 새로 만들어 옮긴다.

CREATE TABLE t_user_inventory_new (
    user_id   INTEGER NOT NULL,            -- 소유 유저 (t_user.user_id 참조)
    container INTEGER NOT NULL DEFAULT 0,  -- 보관함. 0=인벤토리, 1=창고
    slot      INTEGER NOT NULL,            -- 보관함 자원 탭의 칸 번호(0부터)
    item_id   INTEGER NOT NULL,            -- 아이템 종류 ID (ItemTable.ItemTID). 같은 아이템이 여러 칸에 있을 수 있다
    count     INTEGER NOT NULL DEFAULT 0,  -- 이 칸의 수량. 1 이상 ItemTable.MaxStack 이하
    PRIMARY KEY (user_id, container, slot) -- (유저, 보관함, 칸) 유일 = UPSERT 타깃 · 로그인 조회도 user_id 접두로 탄다
) STRICT;

-- 예전에는 칸 중복을 서버 메모리만 막았다. 겹친 행은 음수 칸으로 비켜 두면 로그인 때 첫 빈 칸으로 옮겨진다.
INSERT INTO t_user_inventory_new (user_id, container, slot, item_id, count)
SELECT user_id, container,
       CASE WHEN rn = 1 THEN slot ELSE -item_id END,
       item_id, count
FROM (
    SELECT user_id, container, slot, item_id, count,
           ROW_NUMBER() OVER (PARTITION BY user_id, container, slot ORDER BY item_id) AS rn
    FROM t_user_inventory
    WHERE count > 0
);

DROP TABLE t_user_inventory;

ALTER TABLE t_user_inventory_new RENAME TO t_user_inventory;

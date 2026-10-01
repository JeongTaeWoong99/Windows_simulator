-- T-058 인벤토리 칸 위치 (2026-10-01). 한 번만 돌린다. 서버를 끈 상태에서 적용한다.
-- 설계: Server/docs/인벤토리-창고.md 3장
BEGIN;

-- 자원: PK에 container가 들어가 테이블을 다시 만든다. 기존 행은 인벤토리(0), 칸은 item_id 순으로 0부터.
CREATE TABLE t_user_inventory_new (
    user_id   INTEGER NOT NULL,            -- 소유 유저 (t_user.user_id 참조)
    container INTEGER NOT NULL DEFAULT 0,  -- 보관함. 0=인벤토리, 1=창고
    item_id   INTEGER NOT NULL,            -- 아이템 종류 ID (ItemTable.ItemTID)
    count     INTEGER NOT NULL DEFAULT 0,  -- 보유 수량(스택)
    slot      INTEGER NOT NULL DEFAULT 0,  -- 보관함 자원 탭의 칸 번호(0부터). 중복은 서버 메모리가 막는다
    PRIMARY KEY (user_id, container, item_id)  -- (유저, 보관함, 아이템) 유일 = UPSERT 타깃
) STRICT;

INSERT INTO t_user_inventory_new (user_id, container, item_id, count, slot)
SELECT user_id, 0, item_id, count,
       ROW_NUMBER() OVER (PARTITION BY user_id ORDER BY item_id) - 1
FROM t_user_inventory;

DROP TABLE t_user_inventory;
ALTER TABLE t_user_inventory_new RENAME TO t_user_inventory;

-- 캐릭터: 칸은 character_id 순으로 0부터.
ALTER TABLE t_character ADD COLUMN container INTEGER NOT NULL DEFAULT 0 /* 보관함. 0=인벤토리, 1=창고 */;
ALTER TABLE t_character ADD COLUMN slot INTEGER NOT NULL DEFAULT 0 /* 보관함 캐릭터 탭의 칸 번호(0부터). 중복은 서버 메모리가 막는다 */;
UPDATE t_character
SET slot = (SELECT COUNT(*) FROM t_character c2
            WHERE c2.user_id = t_character.user_id AND c2.character_id < t_character.character_id);

-- 장비: 칸은 기존 slot_position을 그대로 쓴다.
ALTER TABLE t_user_equip ADD COLUMN container INTEGER NOT NULL DEFAULT 0 /* 보관함. 0=인벤토리, 1=창고 */;

COMMIT;

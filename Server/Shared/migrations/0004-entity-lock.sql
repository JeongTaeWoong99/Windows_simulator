-- 0004 캐릭터·장비 잠금 (T-137) — 잠긴 개체는 판매·경매 등록·응축 재료에서 빠진다. 장착·배치는 그대로 된다.
-- 자원(t_user_inventory)은 TID 하나 = 칸 하나라 잠그지 않는다.

ALTER TABLE t_character  ADD COLUMN is_locked INTEGER NOT NULL DEFAULT 0;  -- 잠금 여부 (0/1). 경매로 넘어간 개체는 잠글 수 없어 늘 0
ALTER TABLE t_user_equip ADD COLUMN is_locked INTEGER NOT NULL DEFAULT 0;  -- 잠금 여부 (0/1). 경매로 넘어간 개체는 잠글 수 없어 늘 0

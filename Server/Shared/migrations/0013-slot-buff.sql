-- 0013 작업슬롯 소모품 버프 — 영약·나침반 (T-195 · 이슈 #110·#111)
-- 아이템마다 한 줄. 같은 아이템은 남은 횟수가 합산되고, 다른 아이템은 따로 동시에 걸린다. 0이 되면 행을 지운다.

CREATE TABLE t_user_slot_buff (
    user_id    INTEGER NOT NULL,                  -- 소유 유저 (t_user.user_id 참조)
    slot_index INTEGER NOT NULL,                  -- 작업슬롯 번호 (WorkSlotTable.WorkSlotTID) — 캐릭터를 빼도 슬롯에 남는다
    item_tid   INTEGER NOT NULL,                  -- 버프를 건 아이템 (ItemUseTable.ItemTID) — 효과·값은 표에서 읽는다
    remaining  INTEGER NOT NULL,                  -- 남은 판정 횟수 (> 0)
    PRIMARY KEY (user_id, slot_index, item_tid)   -- (유저, 슬롯, 아이템) 유일 = UPSERT 타깃 · 로그인 조회도 user_id 접두로 탄다
) STRICT;

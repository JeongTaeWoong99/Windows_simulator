-- 0014 작업슬롯 자동 사용 칸 — 칸에 넣어 둔 영약 (이슈 #113 · T-195)
-- 아이템마다 한 줄. 같은 아이템의 버프(t_user_slot_buff)가 0이 되면 여기서 1개씩 꺼내 쓴다. 0이 되면 행을 지운다.

CREATE TABLE t_user_slot_auto_use (
    user_id    INTEGER NOT NULL,                  -- 소유 유저 (t_user.user_id 참조)
    slot_index INTEGER NOT NULL,                  -- 작업슬롯 번호 (WorkSlotTable.WorkSlotTID) — 캐릭터를 빼도 칸은 슬롯에 남는다
    item_tid   INTEGER NOT NULL,                  -- 넣어 둔 영약 (ItemUseTable.ItemTID)
    count      INTEGER NOT NULL,                  -- 넣어 둔 개수 (1 ~ ItemTable.MaxStack)
    PRIMARY KEY (user_id, slot_index, item_tid)   -- (유저, 슬롯, 아이템) 유일 = UPSERT 타깃 · 로그인 조회도 user_id 접두로 탄다
) STRICT;

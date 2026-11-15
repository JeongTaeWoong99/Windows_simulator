-- 자동 사용은 ON인 영약만 기록한다. 기존 보관분은 로그인 환급이 끝날 때까지 별도 원장에 보존한다.
ALTER TABLE t_user_slot_auto_use RENAME TO t_user_slot_auto_use_refund;

CREATE TABLE t_user_slot_auto_use (
    user_id    INTEGER NOT NULL,                -- 소유 유저 (t_user.user_id 참조)
    slot_index INTEGER NOT NULL,                -- 작업슬롯 번호 (WorkSlotTable.WorkSlotTID)
    item_tid   INTEGER NOT NULL,                -- 자동 사용이 ON인 영약 (OFF이면 행 없음)
    PRIMARY KEY (user_id, slot_index, item_tid) -- 설정 유일성 · 로그인·환급 조회는 기존 PK의 user_id 접두 사용
) STRICT;

INSERT INTO t_user_slot_auto_use (user_id, slot_index, item_tid)
SELECT user_id, slot_index, item_tid FROM t_user_slot_auto_use_refund WHERE count > 0;

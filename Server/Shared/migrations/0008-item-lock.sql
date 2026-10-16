-- 0008 자원 잠금 (T-159) — 자원은 개체가 없어 종류(TID) 단위로 잠근다. 잠긴 종류는 즉시 판매·경매 등록에서 빠진다.
-- 잠긴 종류만 행이 있다. 보유가 0개가 돼도 행은 남는다 — 다시 얻으면 그대로 잠겨 있어야 한다.

CREATE TABLE t_user_item_lock (
    user_id INTEGER NOT NULL,     -- 소유 유저 (t_user.user_id 참조)
    item_id INTEGER NOT NULL,     -- 잠긴 아이템 종류 ID (ItemTable.ItemTID)
    PRIMARY KEY (user_id, item_id) -- (유저, 종류) 유일 = 두 번 잠가도 한 줄 · 로그인 조회도 user_id 접두로 탄다
) STRICT;

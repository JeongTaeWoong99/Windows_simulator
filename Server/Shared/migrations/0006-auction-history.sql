-- 0006 경매·거래소 거래 기록 (T-156) — 경매장 완료 탭. 판매 완료 · 구매 완료 · 기간 만료 · 취소가 유저별로 한 줄씩 남는다.
-- 정산·반환 트랜잭션 안에서 함께 쓴다 — 우편만 오고 기록이 빠지거나, 기록만 남고 거래가 롤백되는 일이 없다.

CREATE TABLE IF NOT EXISTS t_auction_history (
    history_id    INTEGER PRIMARY KEY,                        -- 기록 ID (rowid 별칭). 커서 · "새 기록" 기준
    user_id       INTEGER NOT NULL,                           -- 기록 주인 (t_user.user_id)
    type          INTEGER NOT NULL,                           -- 1 판매 완료 · 2 구매 완료 · 3 기간 만료 · 4 취소 · 5 반환(경매장 거부·분실)
    kind          INTEGER NOT NULL,                           -- 1=자원 2=장비 3=캐릭터 4=아티팩트 (EAuctionKind)
    tid           INTEGER NOT NULL,                           -- ItemTID · EquipTID · CharacterTID · ArtifactTID
    count         INTEGER NOT NULL,                           -- 수량 (장비·캐릭터는 1)
    unit_price    INTEGER NOT NULL,                           -- 단가. 여러 매물에 걸친 구매는 평균(내림)
    amount        INTEGER NOT NULL,                           -- 판매: 받은 대금(수수료 뺀) · 구매: 낸 금액 · 만료: 돌려받은 등록비 · 취소: 0
    fee           INTEGER NOT NULL DEFAULT 0,                 -- 판매 수수료 (판매 완료만)
    enchant_grade INTEGER NOT NULL DEFAULT 0,                 -- 장비 인챈트 등급 (표시용 · 0=없음)
    trade_id      INTEGER NOT NULL,                           -- 거래 (t_auction_trade.trade_id). 여러 매물 구매면 첫 거래
    created_at    TEXT    NOT NULL DEFAULT (datetime('now'))  -- 일어난 시각 (UTC)
) STRICT;

-- 완료 탭은 유저별 최신순 · 기간으로 읽는다. 보관 기간 정리도 같은 인덱스를 탄다.
CREATE INDEX IF NOT EXISTS idx_auction_history_user ON t_auction_history (user_id, history_id);

CREATE TABLE IF NOT EXISTS t_auction_history_seen (
    user_id      INTEGER PRIMARY KEY,         -- 유저 (t_user.user_id). 유저당 1행 = UPSERT 타깃
    last_seen_id INTEGER NOT NULL DEFAULT 0   -- 완료 탭을 마지막으로 열었을 때의 가장 큰 history_id — 이보다 큰 것이 "새 기록"(탭 배지)
) STRICT;

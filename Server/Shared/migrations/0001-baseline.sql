-- 0001 기준 스키마 — 2026-10-11 game.sqlite3(user_version 0)의 테이블 15 · 인덱스 6을 그대로 옮겼다.
-- IF NOT EXISTS라 이미 있는 DB에 돌려도 바뀌는 것이 없고 판만 1이 된다. 빈 DB에는 전부 만든다.

CREATE TABLE IF NOT EXISTS t_user (
    user_id     INTEGER PRIMARY KEY,                        -- 유저 PK (rowid 별칭)
    provider_id TEXT    NOT NULL UNIQUE,                    -- 외부 제공자 ID
    nickname    TEXT    NOT NULL,                           -- 표시 이름
    admin_level INTEGER NOT NULL DEFAULT 0,                 -- 0=일반, 1+=관리자
    is_deleted  INTEGER NOT NULL DEFAULT 0,                 -- 삭제 여부 (0/1)
    is_banned   INTEGER NOT NULL DEFAULT 0,                 -- 밴 여부 (0/1)
    created_at  TEXT    NOT NULL DEFAULT (datetime('now'))  -- 가입 시각 (UTC)
) STRICT;

CREATE TABLE IF NOT EXISTS t_character (
    character_id  INTEGER PRIMARY KEY,                        -- 캐릭터 개체 PK (rowid 별칭, DB 발급)
    user_id       INTEGER NOT NULL,                           -- 소유 유저 (t_user.user_id 참조)
    character_tid INTEGER NOT NULL,                           -- 캐릭터 종류 (CharacterTable.CharacterTID). 번호 재사용 금지
    level         INTEGER NOT NULL DEFAULT 1,                 -- 레벨. 스탯 계산의 입력
    exp           INTEGER NOT NULL DEFAULT 0,                 -- 현재 레벨에서의 누적 경험치
    created_at    TEXT    NOT NULL DEFAULT (datetime('now'))  -- 획득 시각 (UTC)
, farming_bonus INTEGER NOT NULL DEFAULT 0 /* 적성 포인트로 찍은 농사 보너스. 실효 적성 = CharacterTable 기본값 + 이 값. 남은 포인트는 저장하지 않는다(레벨에서 계산) */, fishing_bonus INTEGER NOT NULL DEFAULT 0 /* 적성 포인트로 찍은 낚시 보너스. 실효 적성 = CharacterTable 기본값 + 이 값. 남은 포인트는 저장하지 않는다(레벨에서 계산) */, mining_bonus INTEGER NOT NULL DEFAULT 0 /* 적성 포인트로 찍은 채굴 보너스. 실효 적성 = CharacterTable 기본값 + 이 값. 남은 포인트는 저장하지 않는다(레벨에서 계산) */, logging_bonus INTEGER NOT NULL DEFAULT 0 /* 적성 포인트로 찍은 벌목 보너스. 실효 적성 = CharacterTable 기본값 + 이 값. 남은 포인트는 저장하지 않는다(레벨에서 계산) */, hunting_bonus INTEGER NOT NULL DEFAULT 0 /* 적성 포인트로 찍은 사냥 보너스. 실효 적성 = CharacterTable 기본값 + 이 값. 남은 포인트는 저장하지 않는다(레벨에서 계산) */, auction_trade_id INTEGER NOT NULL DEFAULT 0 /* 경매 거래 잠금 (t_auction_trade.trade_id). 0=잠금 없음. 0이 아니면 아무의 메모리에도 없다 — 로그인 적재에서 뺀다 */, container INTEGER NOT NULL DEFAULT 0 /* 보관함. 0=인벤토리, 1=창고 */, slot INTEGER NOT NULL DEFAULT 0 /* 보관함 캐릭터 탭의 칸 번호(0부터). 중복은 서버 메모리가 막는다 */, condense_count INTEGER NOT NULL DEFAULT 0) STRICT;

CREATE TABLE IF NOT EXISTS "t_user_workstation_slot" (
    user_id        INTEGER NOT NULL,            -- 소유 유저 (t_user.user_id 참조)
    slot_index     INTEGER NOT NULL,            -- 슬롯 번호 (0부터). 해금된 슬롯만 행으로 존재한다
    industry       INTEGER NOT NULL DEFAULT 0,  -- 지정 산업 = GameData.ItemType (0=미지정)
    industry_level INTEGER NOT NULL DEFAULT 1,  -- 지정 산업 레벨 (1~). 해금 여부는 t_user_industry_level이 갖는다
    character_id   INTEGER NOT NULL DEFAULT 0,  -- 배치된 캐릭터 개체 (t_character.character_id). TID가 아니다. 0=비어 있음
    PRIMARY KEY (user_id, slot_index)           -- (유저, 슬롯) 유일 = UPSERT 타깃
) STRICT;

CREATE TABLE IF NOT EXISTS t_user_unlock (
    user_id     INTEGER NOT NULL,                           -- 소유 유저 (t_user.user_id 참조)
    unlock_tid  INTEGER NOT NULL,                           -- 열린 해금 (UnlockTable.UnlockTID). 열린 것만 행이 있다 — 해금은 영구라 지우지 않는다
    unlocked_at TEXT    NOT NULL DEFAULT (datetime('now')), -- 열린 시각 (UTC)
    PRIMARY KEY (user_id, unlock_tid)                       -- (유저, 해금) 유일 = INSERT OR IGNORE 타깃. 로그인 조회도 이 인덱스를 탄다
) STRICT;

CREATE TABLE IF NOT EXISTS t_user_equip (
    equip_id      INTEGER PRIMARY KEY,                        -- 장비 개체 PK (rowid 별칭, DB 발급)
    user_id       INTEGER NOT NULL,                           -- 소유 유저 (t_user.user_id 참조). 착용과 무관하게 항상 유저 소유
    equip_tid     INTEGER NOT NULL,                           -- 장비 종류 (EquipTable.EquipTID). 번호 재사용 금지
    slot_position INTEGER NOT NULL DEFAULT 0,                 -- 창고 장비 탭 칸 번호 (0부터). 새 장비는 첫 빈 칸
    created_at    TEXT    NOT NULL DEFAULT (datetime('now'))  -- 획득 시각 (UTC)
, enchant_grade INTEGER NOT NULL DEFAULT 0, enchant_1     INTEGER NOT NULL DEFAULT 0, enchant_2     INTEGER NOT NULL DEFAULT 0, enchant_3     INTEGER NOT NULL DEFAULT 0, auction_trade_id INTEGER NOT NULL DEFAULT 0 /* 경매 거래 잠금 (t_auction_trade.trade_id). 0=잠금 없음. 0이 아니면 아무의 메모리에도 없다 — 로그인 적재에서 뺀다 */, container INTEGER NOT NULL DEFAULT 0 /* 보관함. 0=인벤토리, 1=창고 */, pending_grade INTEGER NOT NULL DEFAULT 0 /* 상급 큐브의 고르기 전 인챈트 등급. 0=보류 없음 (T-127) */, pending_1     INTEGER NOT NULL DEFAULT 0 /* 고르기 전 칸 1 (EnchantOptionTID). 0=빈 칸 */, pending_2     INTEGER NOT NULL DEFAULT 0 /* 고르기 전 칸 2 */, pending_3     INTEGER NOT NULL DEFAULT 0 /* 고르기 전 칸 3 */) STRICT;

CREATE TABLE IF NOT EXISTS t_character_equip (
    character_id INTEGER NOT NULL,          -- 착용 캐릭터 개체 (t_character.character_id)
    slot         INTEGER NOT NULL,          -- 칸 = GameData.EquipSlot (무기1 장신구2·3 보석4)
    equip_id     INTEGER NOT NULL UNIQUE,   -- 착용 장비 개체 (t_user_equip.equip_id). 한 장비는 한 곳에만
    PRIMARY KEY (character_id, slot)        -- (캐릭터, 칸) 유일 = 한 칸에 하나. 로그인 조회는 t_user_equip과 JOIN
) STRICT;

CREATE TABLE IF NOT EXISTS t_user_account (
    user_id     INTEGER PRIMARY KEY,         -- 유저 소유 (t_user.user_id 참조). 유저당 1행 = UPSERT 타깃
    level       INTEGER NOT NULL DEFAULT 1,  -- 계정(섬주인) 레벨. AccountLevelTable.AccountLevelTID
    exp         INTEGER NOT NULL DEFAULT 0,  -- 현재 레벨에서 쌓은 경험치. 캐릭터가 얻은 경험치가 그대로 들어온다 (64비트)
    trait_point INTEGER NOT NULL DEFAULT 0   -- 남은(안 쓴) 특성 포인트. 쓴 노드는 t_user_unlock에 남는다
) STRICT;

CREATE TABLE IF NOT EXISTS t_user_mail (
    mail_id        INTEGER PRIMARY KEY,                        -- 우편 PK (rowid 별칭)
    user_id        INTEGER NOT NULL,                           -- 받는 유저 (t_user.user_id 참조)
    template_tid   INTEGER NOT NULL,                           -- 제목·본문·발신자 (MailTemplateTable.MailTemplateTID)
    gold           INTEGER NOT NULL DEFAULT 0,                 -- 첨부 골드
    items          TEXT    NOT NULL DEFAULT '[]',              -- 첨부 아이템 JSON [[ItemTID, 수량], ...] — 보낸 순간 템플릿에서 복사한다
    character_tids TEXT    NOT NULL DEFAULT '[]',              -- 첨부 캐릭터 JSON [CharacterTID, ...] — 한 명 = 한 원소
    equip_tids     TEXT    NOT NULL DEFAULT '[]',              -- 첨부 장비 JSON [EquipTID, ...] — 한 개 = 한 원소
    received_at    TEXT    NOT NULL DEFAULT (datetime('now')), -- 도착 시각 (UTC)
    claimed_at     TEXT                                        -- 받은 시각 (UTC). NULL = 안 받음 — 안 받은 우편은 만료되지 않는다
, equip_ids TEXT NOT NULL DEFAULT '[]' /* 첨부 장비 개체 JSON [{id,tid,grade,options}] — 경매 구매·반환. 장비 행은 잠긴 채 받는 사람 소유이고, 받을 때 잠금을 푼다 */, character_ids TEXT NOT NULL DEFAULT '[]' /* 첨부 캐릭터 개체 JSON [{CharacterId,CharacterTid,Level,Exp,Bonus}] — 경매 구매·반환. 캐릭터 행은 잠긴 채 받는 사람 소유이고, 받을 때 잠금을 푼다 */) STRICT;

CREATE TABLE IF NOT EXISTS t_global_mail (
    global_mail_id INTEGER PRIMARY KEY,                        -- 전체 우편 PK (rowid 별칭)
    template_tid   INTEGER NOT NULL,                           -- 보낼 템플릿 (MailTemplateTable.MailTemplateTID)
    sent_at        TEXT    NOT NULL DEFAULT (datetime('now')), -- 발송 시각 (UTC)
    ends_at        TEXT    NOT NULL                            -- 이 시각 전까지 로그인한 계정이 받는다 (UTC) = 발송 + PeriodDays
) STRICT;

CREATE TABLE IF NOT EXISTS t_user_global_mail (
    user_id        INTEGER NOT NULL,                           -- 받은 유저 (t_user.user_id 참조)
    global_mail_id INTEGER NOT NULL,                           -- 복사받은 전체 우편 (t_global_mail.global_mail_id)
    PRIMARY KEY (user_id, global_mail_id)                      -- 같은 전체 우편을 두 번 받지 않는다 = INSERT OR IGNORE 타깃
) STRICT;

CREATE TABLE IF NOT EXISTS t_auction_trade (
    trade_id    INTEGER PRIMARY KEY,                        -- 거래 ID (rowid 별칭) = 경매장 매물 ID
    seller_id   INTEGER NOT NULL,                           -- 판매자 (t_user.user_id)
    kind        INTEGER NOT NULL,                           -- 1=자원 2=장비
    tid         INTEGER NOT NULL,                           -- ItemTID 또는 EquipTID
    count       INTEGER NOT NULL,                           -- 에스크로 수량 (장비는 1). 자원은 이 행이 곧 보관함이다
    equip_id    INTEGER NOT NULL DEFAULT 0,                 -- 잠근 장비 개체 (t_user_equip.equip_id). 자원은 0
    snapshot    TEXT    NOT NULL DEFAULT '{}',              -- 등록 순간의 아이템 정보 JSON — 우편 첨부·재전송에 쓴다
    unit_price  INTEGER NOT NULL,                           -- 단가. 총액 = 단가 × 수량
    listing_fee INTEGER NOT NULL,                           -- 낸 등록비 — 만료로 돌아오면 환급한다
    state       INTEGER NOT NULL DEFAULT 1,                 -- 1 Listed · 2 Settled · 3 Returned. 전이는 전부 WHERE state = 1 조건부
    buyer_id    INTEGER NOT NULL DEFAULT 0,                 -- 구매자 (t_user.user_id). 정산 전 0
    purchase_id INTEGER NOT NULL DEFAULT 0,                 -- 정산한 구매 시도 ID (경매장 t_purchase와 같은 값)
    sale_fee    INTEGER NOT NULL DEFAULT 0,                 -- 판매 수수료 (sink)
    created_at  TEXT    NOT NULL DEFAULT (datetime('now')), -- 등록 시각 (UTC)
    closed_at   TEXT                                        -- 정산·반환 시각 (UTC)
, listed_count INTEGER NOT NULL DEFAULT 0 /* 등록 수량 — 바뀌지 않는다. count(남은 수량)와 비교해 만료 환급 등록비를 팔리지 않은 비율만큼 계산한다. 0이면 옛 행 = count와 같다 */, character_id INTEGER NOT NULL DEFAULT 0 /* 잠근 캐릭터 개체 (t_character.character_id). 자원·장비는 0 */) STRICT;

CREATE TABLE IF NOT EXISTS t_auction_outbox (
    outbox_id  INTEGER PRIMARY KEY AUTOINCREMENT,           -- 전송 순서. 지운 번호를 다시 쓰지 않는다
    trade_id   INTEGER NOT NULL,                            -- 관련 거래 (t_auction_trade.trade_id)
    kind       INTEGER NOT NULL,                            -- 1 Register · 2 Confirm
    payload    TEXT    NOT NULL,                            -- 보낼 내용 JSON
    created_at TEXT    NOT NULL DEFAULT (datetime('now')),  -- 적은 시각 (UTC)
    sent_at    TEXT                                         -- 경매장이 받은 시각 (UTC). NULL = 아직 안 보냄
, purchase_id INTEGER NOT NULL DEFAULT 0 /* 확정 메시지의 구매 시도 ID (등록은 0). 대사가 아직 경매장에 안 간 확정이 걸린 거래를 거른다 */) STRICT;

CREATE TABLE IF NOT EXISTS "t_user_currency" (
    user_id INTEGER PRIMARY KEY,          -- 소유 유저 (t_user.user_id 참조). 유저당 1행 = UPSERT 타깃
    gold    INTEGER NOT NULL DEFAULT 0    -- 게임 안 유일한 화폐. 보유량 (64비트). 음수 방지는 로직에서 검사한다
) STRICT;

CREATE TABLE IF NOT EXISTS "t_user_inventory" (
    user_id   INTEGER NOT NULL,            -- 소유 유저 (t_user.user_id 참조)
    container INTEGER NOT NULL DEFAULT 0,  -- 보관함. 0=인벤토리, 1=창고
    item_id   INTEGER NOT NULL,            -- 아이템 종류 ID (ItemTable.ItemTID)
    count     INTEGER NOT NULL DEFAULT 0,  -- 보유 수량(스택)
    slot      INTEGER NOT NULL DEFAULT 0,  -- 보관함 자원 탭의 칸 번호(0부터). 중복은 서버 메모리가 막는다
    PRIMARY KEY (user_id, container, item_id)  -- (유저, 보관함, 아이템) 유일 = UPSERT 타깃
) STRICT;

CREATE TABLE IF NOT EXISTS t_user_trait (
    user_id        INTEGER NOT NULL,            -- 소유 유저 (t_user.user_id 참조)
    user_trait_tid INTEGER NOT NULL,            -- 특성 (UserTraitTable.UserTraitTID)
    level          INTEGER NOT NULL,            -- 지금 레벨. 기본 레벨보다 올린 특성만 행이 있다
    PRIMARY KEY (user_id, user_trait_tid)       -- (유저, 특성) 유일 = UPSERT 타깃 · 로그인 조회도 user_id 접두로 탄다
) STRICT;

CREATE INDEX IF NOT EXISTS idx_user_created_at ON t_user (created_at);
CREATE INDEX IF NOT EXISTS idx_character_user_id ON t_character (user_id);
CREATE INDEX IF NOT EXISTS idx_user_mail_user ON t_user_mail (user_id);
CREATE INDEX IF NOT EXISTS idx_auction_trade_seller ON t_auction_trade (seller_id, state);
CREATE INDEX IF NOT EXISTS idx_auction_trade_state  ON t_auction_trade (state, created_at);
CREATE INDEX IF NOT EXISTS idx_auction_outbox_unsent ON t_auction_outbox (trade_id) WHERE sent_at IS NULL;

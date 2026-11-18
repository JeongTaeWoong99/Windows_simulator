CREATE TABLE t_user_raid (
    user_id            INTEGER PRIMARY KEY,                                 -- 소유 유저 (t_user.user_id)
    ticket_count       INTEGER NOT NULL CHECK (ticket_count BETWEEN 0 AND 3), -- 남은 티켓 수
    last_recharge_date TEXT    NOT NULL,                                    -- 마지막 충전 정산일 (한국시간 YYYY-MM-DD)
    party_slot_1       INTEGER NOT NULL DEFAULT 0 CHECK (party_slot_1 >= 0),  -- 선두 캐릭터 (0이면 빈 편성)
    party_slot_2       INTEGER NOT NULL DEFAULT 0 CHECK (party_slot_2 >= 0),  -- 두 번째 캐릭터 (0이면 빈 칸)
    party_slot_3       INTEGER NOT NULL DEFAULT 0 CHECK (party_slot_3 >= 0),  -- 세 번째 캐릭터 (0이면 빈 칸)
    CHECK (party_slot_2 = 0 OR party_slot_1 > 0),
    CHECK (party_slot_3 = 0 OR party_slot_2 > 0),
    CHECK (party_slot_2 = 0 OR party_slot_1 <> party_slot_2),
    CHECK (party_slot_3 = 0 OR (party_slot_1 <> party_slot_3 AND party_slot_2 <> party_slot_3))
) STRICT;

CREATE TABLE t_user_raid_attempt (
    user_id     INTEGER NOT NULL,                          -- 도전한 유저 (t_user.user_id)
    request_id  TEXT    NOT NULL,                          -- 중복 제거용 도전 요청 식별자 (GUID N 형식)
    result_json TEXT    NOT NULL,                         -- 재시도 때 그대로 돌려줄 도전 결과
    PRIMARY KEY (user_id, request_id)                     -- 계정별 요청 조회와 중복 검사를 같이 담당
) STRICT;

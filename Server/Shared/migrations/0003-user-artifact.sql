-- 0003 아티팩트 보관함 (T-150) — 아티팩트는 인벤토리가 아니라 번호(ArtifactTID)별 칸에 수량으로 쌓인다.
-- 행이 있으면 "얻은 적 있음"이다. 수량이 0이 돼도 행을 지우지 않는다 — 도감에서 사라지지 않게.

CREATE TABLE IF NOT EXISTS t_user_artifact (
    user_id           INTEGER NOT NULL,                           -- 소유 유저 (t_user.user_id 참조)
    artifact_tid      INTEGER NOT NULL,                           -- 아티팩트 번호 (ArtifactTable.ArtifactTID)
    count             INTEGER NOT NULL,                           -- 보유 수량. 1 이상이면 효과가 켜진다 · 0도 행을 남긴다
    first_obtained_at TEXT    NOT NULL DEFAULT (datetime('now')), -- 처음 얻은 시각 (UTC) — 덮어쓰지 않는다
    PRIMARY KEY (user_id, artifact_tid)                           -- (유저, 번호) 유일 = UPSERT 타깃 · 로그인 조회도 user_id 접두로 탄다
) STRICT;

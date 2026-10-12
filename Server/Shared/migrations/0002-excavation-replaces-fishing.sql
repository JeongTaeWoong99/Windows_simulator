-- 0002 낚시 → 유적 발굴 (T-142) — enum 2 · 아이템·장비·특성 TID를 그대로 이어 쓰므로 옛 낚시 데이터를 지운다.
-- 남겨 두면 붕어가 "깨진 토기 조각"으로, 낚시 특성 레벨이 발굴 특성 레벨로 조용히 둔갑한다.
-- 대상: 자원 10001~10046 · 장비 1002 2002 1201 1203~1206 3201~3206 · 특성 102 202 302 · 산업 2 슬롯

-- 자원
DELETE FROM t_user_inventory WHERE item_id BETWEEN 10001 AND 10046;

-- 장비 — 착용 줄을 먼저 지운다
DELETE FROM t_character_equip
 WHERE equip_id IN (SELECT equip_id FROM t_user_equip
                     WHERE equip_tid IN (1002, 2002, 1201, 1203, 1204, 1205, 1206, 3201, 3202, 3203, 3204, 3205, 3206));
DELETE FROM t_user_equip
 WHERE equip_tid IN (1002, 2002, 1201, 1203, 1204, 1205, 1206, 3201, 3202, 3203, 3204, 3205, 3206);

-- 특성 — 지우면 기본 레벨로 돌아간다(쓴 포인트는 돌려주지 않는다)
DELETE FROM t_user_trait WHERE user_trait_tid IN (102, 202, 302);

-- 슬롯 — 낚시를 지정한 슬롯은 미지정으로 되돌린다(캐릭터 배치도 푼다)
UPDATE t_user_workstation_slot SET industry = 0, industry_level = 1, character_id = 0 WHERE industry = 2;

-- 경매 — 걸려 있는 낚시 매물은 반환 처리하고 에스크로는 비운다(돌려주지 않는다).
-- 경매장 쪽 매물은 구매 시 메인 정산이 0행이라 실패하고, 만료로 사라진다.
UPDATE t_auction_trade SET state = 3, count = 0, closed_at = datetime('now')
 WHERE state = 1
   AND ((kind = 1 AND tid BETWEEN 10001 AND 10046)
     OR (kind = 2 AND tid IN (1002, 2002, 1201, 1203, 1204, 1205, 1206, 3201, 3202, 3203, 3204, 3205, 3206)));

-- 적성 보너스 컬럼 개명 (로직은 아직 읽지 않는다)
ALTER TABLE t_character RENAME COLUMN fishing_bonus TO excavation_bonus;

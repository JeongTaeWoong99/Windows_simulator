-- 0017 채취 단계 개편 (T-208) — 산업 레벨이 Lv1~5 연속 번호에서 실제 레벨 1 · 20 · 40 · 60 · 80 · 100의 6단계로 바뀐다.
-- 옛 값을 두면 슬롯 레벨 2~5가 표에 없는 레벨로 남고, 개척 특성 Lv k의 뜻(k번째 단계)이 바뀌어 해금이 어긋난다.
-- 결정: 기존 데이터는 초기화 (2026-11-22 진우 · 레벨테마.md 1장)
-- 대상: 슬롯 산업 레벨 · 개척 특성 101~105 · 옛 재질 상자 100019~100033 (인벤토리 · 창고 · 잠금 · 우편 첨부 · 경매 에스크로)

-- 슬롯 — 산업은 그대로 두고 레벨만 Lv1로
UPDATE t_user_workstation_slot SET industry_level = 1 WHERE industry_level <> 1;

-- 개척 특성 — 행을 지우면 기본 레벨(Lv1)이다. 포인트는 저장하지 않고 건물 레벨에서 세므로 지운 만큼 저절로 돌아온다
DELETE FROM t_user_trait WHERE user_trait_tid BETWEEN 101 AND 105;

-- 옛 상자 — 인벤토리(container 0) · 창고(container 1) 칸과 종류 잠금
DELETE FROM t_user_inventory WHERE item_id BETWEEN 100019 AND 100033;
DELETE FROM t_user_item_lock WHERE item_id BETWEEN 100019 AND 100033;

-- 우편 첨부 — 옛 상자 원소만 뺀다. 골드 · 다른 첨부는 그대로
UPDATE t_user_mail
   SET items = (SELECT COALESCE(json_group_array(json(e.value)), '[]')
                  FROM json_each(t_user_mail.items) AS e
                 WHERE json_extract(e.value, '$[0]') NOT BETWEEN 100019 AND 100033)
 WHERE EXISTS (SELECT 1 FROM json_each(t_user_mail.items) AS e
                WHERE json_extract(e.value, '$[0]') BETWEEN 100019 AND 100033);

-- 경매 — 걸려 있는 옛 상자 매물은 반환 처리하고 에스크로는 비운다(돌려주지 않는다 · 0002와 같은 방식).
-- 경매장 쪽 매물은 구매 시 메인 정산이 0행이라 실패하고, 만료로 사라진다.
UPDATE t_auction_trade SET state = 3, count = 0, closed_at = datetime('now')
 WHERE state = 1 AND kind = 1 AND tid BETWEEN 100019 AND 100033;

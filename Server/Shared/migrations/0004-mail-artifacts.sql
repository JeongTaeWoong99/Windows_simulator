-- 0004 우편 첨부 아티팩트 (T-152) — 거래소에서 산 아티팩트·돌아온 매물은 우편으로 오고, 받을 때 보관함에 들어간다.
-- 아티팩트는 인벤토리 아이템이 아니라 items와 섞지 않는다(번호가 ItemTID와 다른 공간이다).

ALTER TABLE t_user_mail ADD COLUMN artifacts TEXT NOT NULL DEFAULT '[]';  -- 첨부 아티팩트 JSON [[ArtifactTID, 수량], ...]

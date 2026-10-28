-- 0011 도감에 장착한 아티팩트는 항상 켜짐 (T-181 · 이슈 #90)
-- 효과 끄기를 없앴다. 꺼 둔 번호도 장착한 것이므로 켜진 것으로 본다 — 켜짐 칸을 통째로 지운다.

ALTER TABLE t_user_artifact DROP COLUMN is_active;

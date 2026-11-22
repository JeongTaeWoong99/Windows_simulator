# 테마 상자 6종의 열기 뽑기표를 만든다 — 레벨테마 7장(T-209)의 계층 확률을 행마다 정수 가중치로 풀어 Gacha.xlsx에 쓴다.
#
# 서버는 한 풀의 아이템·장비·캐릭터·골드·아티팩트 행을 한 번의 가중치 추첨으로 고른다(GachaPoolCatalog).
# 그래서 "칸 → 레벨 → 등급 → 그 안의 하나" 계층 확률을 곱해 각 행의 가중치로 미리 넣는다.
# 풀 하나의 가중치 합 = 10억(백만분율 × 1000) — int 상한(약 21.4억) 안이다.
# 정수로 나눌 때는 계층마다 최대 나머지 방식으로 나눠 칸 · 레벨 · 등급 합이 정확히 맞게 한다.
#
# 사용: python GameDesign/scripts/theme-box-pools.py          (Gacha.xlsx · Item.xlsx에 쓴다 → generate-tables.ps1)
#       python GameDesign/scripts/theme-box-pools.py --check  (DataLog에서 다시 계산해 레벨테마 7장 표와 맞는지 본다)
# 입력: GameDesign/DataLog의 EquipTable · CharacterTable (장비 · 캐릭터 목록). 대상 엑셀은 Excel에서 닫고 실행한다.

import json
import os
import sys
from fractions import Fraction as F

import openpyxl

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
EXCEL = os.path.join(ROOT, "Excel")
DATALOG = os.path.join(ROOT, "DataLog")

TOTAL = 1_000_000_000  # 풀 하나의 가중치 합 (백만분율 × 1000)
GRADES = ["Common", "Uncommon", "Rare", "Epic", "Legendary", "Mythic"]
GRADE_KR = dict(zip(GRADES, ["일반", "고급", "희귀", "영웅", "전설", "신화"]))


def pct(*values):
    return [F(str(v)) / 100 for v in values]


# ── 레벨테마 7장 (확정 2026-11-22 진우) ──
# 7.1 칸 비율(백만분율) — 모든 상자 공통
SLOTS = {"equip": 163_300, "accessory": 163_300, "character": 163_300,
         "material": 200_000, "gold": 200_000, "consumable": 100_000, "artifact": 10_100}

# 단계별 (풀, 상자 이름, 장비 레벨[(레벨, 비율)], 일반 자원 접미, 큐브 개수, 골드 배수, 캐릭터 등급, 장비 등급)
LOW_HIGH = [F(7, 10), F(3, 10)]  # 구간 안 낮은 레벨 70% · 높은 레벨 30%
STAGES = [
    (9,  "개척자의 상자", [(1, F(1))],                    1,  1, 1,   pct(48, 44, 8, 0, 0, 0),          pct(57, 18, 10, 12, 2.5, 0.5)),
    (12, "산골 상자",     list(zip([10, 20], LOW_HIGH)),  11, 2, 3,   pct(26, 43, 25, 6, 0, 0),         pct(62.5, 20, 10, 6, 1.25, 0.25)),
    (15, "광부의 상자",   list(zip([30, 40], LOW_HIGH)),  21, 3, 9,   pct(17, 34, 26.5, 19.5, 3, 0),    pct(64.5, 21, 10, 3.6, 0.75, 0.15)),
    (18, "요새 보급 상자", list(zip([50, 60], LOW_HIGH)), 31, 4, 27,  pct(12, 27, 26, 24, 10.6, 0.4),   pct(66, 21, 10, 2.4, 0.5, 0.1)),
    (21, "성주의 상자",   list(zip([70, 80], LOW_HIGH)),  41, 5, 81,  pct(8, 20, 26, 28, 16, 2),        pct(67, 21.5, 10, 1.2, 0.25, 0.05)),
    (37, "왕실 상자",     list(zip([90, 100], LOW_HIGH)), 51, 6, 243, pct(5, 15, 24, 30, 22, 4),        pct(67.5, 21.6, 10, 0.75, 0.125, 0.025)),
]
BOX_ITEM_OF_POOL = {9: 100081, 12: 100082, 15: 100083, 18: 100084, 21: 100085, 37: 100086}
NEW_POOL = 37  # 왕실 상자 — 성주 풀 21을 임시로 쓰던 것을 새 풀로 (GachaInfoTable 36 다음)

INDUSTRY_PREFIX = [1, 2, 3, 4, 5]  # 유적 발굴 · 농사 · 벌목 · 채굴 · 사냥 — 자원 TID 앞자리
MATERIAL_COUNT = 27

# 7.1 소모품 — 옛 상자 풀의 상대 비율 그대로 10%로 줄인다 (TID 꼬리, 아이템, 개수, 옛 가중치)
CONSUMABLES = [(6, 100010, 1, 13_020), (7, 100015, None, 62_000), (8, 100041, 1, 248_000),
               (9, 100044, 20, 24_180), (10, 100045, 20, 24_180), (20, 100036, 1, 13_020)]

ARTIFACTS = [24, 25]  # 장인의 나무 자 · 은빛 수확패 — 반씩
GOLD_BASE = [(135, 271, F(3, 4)), (271, 677, F(1, 4))]  # 개척자 기준 (작은 몫 75% / 큰 몫 25%)


def split(total, parts):
    """total(정수)을 parts[(키, 비율)] 비율로 나눈다. 최대 나머지 방식 — 합이 정확히 total이다."""
    parts = [(k, f) for k, f in parts if f > 0]
    s = sum(f for _, f in parts)
    exact = [(k, total * f / s) for k, f in parts]
    out = {k: int(v) for k, v in exact}
    rest = total - sum(out.values())
    for k, v in sorted(exact, key=lambda kv: kv[1] - int(kv[1]), reverse=True)[:rest]:
        out[k] += 1
    return out


def load(name):
    with open(os.path.join(DATALOG, name + ".json"), encoding="utf-8") as f:
        return json.load(f)


def build(stage, equips, characters):
    pool, _, levels, res_tail, cube, gold_mul, char_grades, equip_grades = stage
    slot = {k: v * (TOTAL // 1_000_000) for k, v in SLOTS.items()}
    base = pool * 1000
    rows = {"item": [], "equip": [], "character": [], "gold": [], "artifact": []}

    # 재료 — 산업 5종 4%씩
    for i, (p, w) in enumerate(split(slot["material"], [(p, F(1)) for p in INDUSTRY_PREFIX]).items()):
        tid = p * 10000 + res_tail
        rows["item"].append((base + 1 + i, pool, tid, MATERIAL_COUNT, w, None))

    # 소모품
    cw = split(slot["consumable"], [(c[0], F(c[3])) for c in CONSUMABLES])
    for tail, tid, count, _ in CONSUMABLES:
        rows["item"].append((base + tail, pool, tid, count or cube, cw[tail], None))

    # 골드 — 단계마다 ×3
    gw = split(slot["gold"], [(i, g[2]) for i, g in enumerate(GOLD_BASE)])
    for i, (lo, hi, _) in enumerate(GOLD_BASE):
        rows["gold"].append((base + 201 + i, pool, lo * gold_mul, hi * gold_mul, gw[i]))

    # 아티팩트 — 반씩
    aw = split(slot["artifact"], [(a, F(1)) for a in ARTIFACTS])
    for a in ARTIFACTS:
        rows["artifact"].append((base + 300 + a, pool, a, 1, aw[a]))

    # 캐릭터 — 등급 → 그 등급 전부 같은 가중치
    by_grade = {g: sorted(c["CharacterTID"] for c in characters if c["GlobalRarity"] == g) for g in GRADES}
    gw = split(slot["character"], [(g, f) for g, f in zip(GRADES, char_grades) if by_grade[g]])
    missing = [g for g, f in zip(GRADES, char_grades) if f > 0 and not by_grade[g]]
    n = 401
    for g in GRADES:
        if g not in gw:
            continue
        for tid, w in split(gw[g], [(t, F(1)) for t in by_grade[g]]).items():
            rows["character"].append((base + n, pool, tid, 1, w, g))
            n += 1

    # 장비 · 장신구 — 레벨 → 등급 → 그 (레벨, 등급) 전부 같은 가중치. 빈 조합은 같은 칸의 다른 조합에 비례 재분배
    n = 501
    for kind_slot, kinds in (("equip", ("Weapon", "Gem")), ("accessory", ("Accessory",))):
        cell = {(lv, g): sorted(e["EquipTID"] for e in equips
                                 if e["ItemLevel"] == lv and e["GlobalRarity"] == g and e["EquipKind"] in kinds)
                for lv, _ in levels for g in GRADES}
        parts = [((lv, g), lf * gf) for lv, lf in levels for g, gf in zip(GRADES, equip_grades)]
        missing += [(kind_slot, k) for k, f in parts if f > 0 and not cell[k]]
        # 레벨 → 등급 순으로 나눠 레벨 합 · (레벨, 등급) 합이 정확히 맞게 한다
        lw = split(slot[kind_slot], [(lv, sum(f for (l2, g), f in parts if l2 == lv and cell[(l2, g)])) for lv, _ in levels])
        for lv, _ in levels:
            if lv not in lw:
                continue
            cw2 = split(lw[lv], [(g, gf) for g, gf in zip(GRADES, equip_grades) if cell[(lv, g)]])
            for g in GRADES:
                if g not in cw2:
                    continue
                for tid, w in split(cw2[g], [(t, F(1)) for t in cell[(lv, g)]]).items():
                    rows["equip"].append((base + n, pool, tid, 1, w, (lv, g)))
                    n += 1
    return rows, missing


def main_write():
    equips = load("EquipTable")
    characters = load("CharacterTable")
    names = {e["EquipTID"]: e["Name"] for e in equips}
    cnames = {c["CharacterTID"]: c["Name"] for c in characters}
    items = {r["ItemTID"]: r["Name"] for r in load("ItemTable")}
    pools = {s[0] for s in STAGES}

    built = {}
    for stage in STAGES:
        rows, missing = build(stage, equips, characters)
        built[stage[0]] = rows
        if missing:
            print(f"[재분배] 풀 {stage[0]} {stage[1]}: 빈 조합 {missing}")

    wb = openpyxl.load_workbook(os.path.join(EXCEL, "Gacha.xlsx"))

    def rewrite(sheet, new_rows):
        ws = wb[sheet]
        header = next(r for r in range(1, ws.max_row + 1) if ws.cell(r, 1).value is None and ws.cell(r, 2).value and str(ws.cell(r, 2).value).endswith("TID"))
        width = ws.max_column
        data = [[ws.cell(r, c).value for c in range(2, width + 1)] for r in range(header + 1, ws.max_row + 1)]
        data = [d for d in data if d[0] is not None]
        first = next((i for i, d in enumerate(data) if d[1] in pools), len(data))
        before = [d for i, d in enumerate(data) if i < first and d[1] not in pools]
        after = [d for i, d in enumerate(data) if i >= first and d[1] not in pools]
        out = before + new_rows + after
        ws.delete_rows(header + 1, ws.max_row - header)
        for i, d in enumerate(out):
            for c, v in enumerate(d, start=2):
                ws.cell(header + 1 + i, c, v)

    def all_of(key):
        return [r for p in sorted(built) for r in built[p][key]]

    rewrite("GachaItemTable", [[t, p, tid, cnt, w, f"자원 {items[tid]} x{cnt}" if tid < 100000 else f"소모품 {items[tid]} x{cnt}"]
                               for t, p, tid, cnt, w, _ in all_of("item")])
    rewrite("GachaGoldTable", [[t, p, lo, hi, w, f"골드 {lo}~{hi}"] for t, p, lo, hi, w in all_of("gold")])
    rewrite("GachaArtifactTable", [[t, p, a, c, w, f"테마 상자 · {'장인의 나무 자' if a == 24 else '은빛 수확패'}"] for t, p, a, c, w in all_of("artifact")])
    rewrite("GachaCharacterTable", [[t, p, tid, c, w, f"{g} {cnames[tid]}"] for t, p, tid, c, w, g in all_of("character")])
    rewrite("GachaEquipTable", [[t, p, tid, c, w, f"Lv{lv} {g} {names[tid]}"] for t, p, tid, c, w, (lv, g) in all_of("equip")])

    # 풀 메타 — 테마 상자 6종. 왕실 상자는 새 풀
    ws = wb["GachaInfoTable"]
    header = next(r for r in range(1, ws.max_row + 1) if ws.cell(r, 2).value == "GachaInfoTID")
    desc = "상자 전용 — 골드로 뽑을 수 없다 · 테마 상자 열기 보상(레벨테마 7장 · T-209)"
    found = set()
    for r in range(header + 1, ws.max_row + 1):
        tid = ws.cell(r, 2).value
        if tid in pools:
            ws.cell(r, 7, desc)
            found.add(tid)
    if NEW_POOL not in found:
        r = ws.max_row + 1
        for c, v in enumerate([NEW_POOL, "왕실 상자", "None", 0, 0, desc], start=2):
            ws.cell(r, c, v)
    wb.save(os.path.join(EXCEL, "Gacha.xlsx"))

    # 상자 아이템 — 왕실 상자가 새 풀을 연다 · 메모의 "임시" 문구를 걷는다
    wb = openpyxl.load_workbook(os.path.join(EXCEL, "Item.xlsx"))
    for ws in wb.worksheets:
        hdr = next((r for r in range(1, min(ws.max_row, 20) + 1) if ws.cell(r, 2).value == "ItemTID"), None)
        if hdr is None:
            continue
        col = {ws.cell(hdr, c).value: c for c in range(1, ws.max_column + 1)}
        if "OpenGachaId" not in col:
            continue
        for r in range(hdr + 1, ws.max_row + 1):
            tid = ws.cell(r, 2).value
            for pool, box in BOX_ITEM_OF_POOL.items():
                if tid == box:
                    ws.cell(r, col["OpenGachaId"], pool)
                    d = ws.cell(r, col["Description"]).value or ""
                    ws.cell(r, col["Description"], d.split(" · 열기")[0] + f" · 열기 풀 {pool}(레벨테마 7장 · T-209)")
    wb.save(os.path.join(EXCEL, "Item.xlsx"))
    print("Gacha.xlsx · Item.xlsx 갱신 완료 — format-excel.py → generate-tables.ps1 을 돌린다")


def main_check():
    """DataLog에서 상자별 칸 비율 · 등급 분포를 다시 계산해 레벨테마 7장 표와 견준다."""
    equips = {e["EquipTID"]: e for e in load("EquipTable")}
    chars = {c["CharacterTID"]: c for c in load("CharacterTable")}
    items = {i["ItemTID"]: i for i in load("ItemTable")}
    tables = {k: load(k) for k in ("GachaItemTable", "GachaEquipTable", "GachaCharacterTable", "GachaGoldTable", "GachaArtifactTable")}
    ok = True

    def near(actual, expected, label, tol=F(1, 10**7)):
        nonlocal ok
        if abs(actual - expected) > tol:
            ok = False
            print(f"  ✗ {label}: 실제 {float(actual) * 100:.6f}% · 표 {float(expected) * 100:.6f}%")

    for stage in STAGES:
        pool, name, levels, res_tail, cube, gold_mul, char_grades, equip_grades = stage
        if items[BOX_ITEM_OF_POOL[pool]]["OpenGachaId"] != pool:
            ok = False
            print(f"  ✗ {name} 아이템이 풀 {pool}을 열지 않는다")
        rows = {k: [r for r in v if r["GachaId"] == pool] for k, v in tables.items()}
        total = sum(r["Weight"] for v in rows.values() for r in v)
        slot = {k: 0 for k in SLOTS}
        for r in rows["GachaItemTable"]:
            slot["material" if r["ItemTID"] < 100000 else "consumable"] += r["Weight"]
        for r in rows["GachaEquipTable"]:
            slot["accessory" if equips[r["EquipTID"]]["EquipKind"] == "Accessory" else "equip"] += r["Weight"]
        slot["character"] = sum(r["Weight"] for r in rows["GachaCharacterTable"])
        slot["gold"] = sum(r["Weight"] for r in rows["GachaGoldTable"])
        slot["artifact"] = sum(r["Weight"] for r in rows["GachaArtifactTable"])

        print(f"== 풀 {pool} {name} — 가중치 합 {total:,}")
        print("  칸: " + " · ".join(f"{k} {slot[k] / total * 100:.4f}%" for k in SLOTS))
        for k, v in SLOTS.items():
            near(F(slot[k], total), F(v, 1_000_000), f"칸 {k}")

        cg = {g: sum(r["Weight"] for r in rows["GachaCharacterTable"] if chars[r["CharacterTID"]]["GlobalRarity"] == g) for g in GRADES}
        print("  캐릭터 등급: " + " · ".join(f"{GRADE_KR[g]} {cg[g] / slot['character'] * 100:.4f}%" for g in GRADES))
        for g, f in zip(GRADES, char_grades):
            near(F(cg[g], slot["character"]), f, f"캐릭터 {g}")

        for kslot, label in (("equip", "장비"), ("accessory", "장신구")):
            er = [r for r in rows["GachaEquipTable"] if (equips[r["EquipTID"]]["EquipKind"] == "Accessory") == (kslot == "accessory")]
            lv = {l: sum(r["Weight"] for r in er if equips[r["EquipTID"]]["ItemLevel"] == l) for l, _ in levels}
            eg = {g: sum(r["Weight"] for r in er if equips[r["EquipTID"]]["GlobalRarity"] == g) for g in GRADES}
            print(f"  {label} 레벨: " + " · ".join(f"Lv{l} {lv[l] / slot[kslot] * 100:.4f}%" for l in lv)
                  + " | 등급: " + " · ".join(f"{GRADE_KR[g]} {eg[g] / slot[kslot] * 100:.4f}%" for g in GRADES))
            for (l, lf) in levels:
                near(F(lv[l], slot[kslot]), lf, f"{label} Lv{l}")
            for g, f in zip(GRADES, equip_grades):
                near(F(eg[g], slot[kslot]), f, f"{label} {g}", tol=F(1, 10**5))
            # 같은 (레벨, 등급) 안은 같은 가중치 (정수 나눗셈 차이 1까지)
            for l, _ in levels:
                for g in GRADES:
                    ws = [r["Weight"] for r in er if equips[r["EquipTID"]]["ItemLevel"] == l and equips[r["EquipTID"]]["GlobalRarity"] == g]
                    if ws and max(ws) - min(ws) > 1:
                        ok = False
                        print(f"  ✗ {label} Lv{l} {g} 가중치가 고르지 않다 {ws}")

        gold = sorted((r["MinAmount"], r["MaxAmount"], r["Weight"]) for r in rows["GachaGoldTable"])
        print("  골드: " + " / ".join(f"{lo:,}~{hi:,} ({w / slot['gold'] * 100:.0f}%)" for lo, hi, w in gold))
        if [(lo, hi) for lo, hi, _ in gold] != [(lo * gold_mul, hi * gold_mul) for lo, hi, _ in GOLD_BASE]:
            ok = False
            print("  ✗ 골드 구간이 ×3 기울기가 아니다")

    print("검산 통과" if ok else "검산 실패")
    return 0 if ok else 1


if __name__ == "__main__":
    if "--check" in sys.argv:
        sys.exit(main_check())
    main_write()

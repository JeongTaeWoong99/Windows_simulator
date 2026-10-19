# 여러 게임 특성 시스템 비교 — T-113 특성 다양화

> 작성일: 2026-10-19 · 03시 회차 (KST) · 특별 회차(여러 게임 비교)
> 관련 일감: [T-113 특성 다양화](../../../../tasks/T-113-특성다양화.md) · 상세 기획안 [특성](../../trait/README.md)
> 비교한 게임: 패스 오브 엑자일 · 스타듀 밸리 · 쿠키 클리커 · 뱀파이어 서바이버즈 · 올드스쿨 룬스케이프 · 팩토리오
> 추천 MVP 묶음: **섬 전역 개량 · 발굴 특기 · 상자 감각 · 응축 숙련** (목표 회차 261024 · 패킷·DB 변경 0 · 새 EffectType 2개)
> 이 문서는 레퍼런스 분석이자 **제안 원본**이다. 기획을 확정하지 않는다 — 확정은 [게임기획코어](../../게임기획코어.md)와 [특성](../../trait/README.md)에서 한다. 수치는 전부 테스트값 초안이다(T-113: 수치는 진우가 시뮬레이터로 맞춘다).

## 한눈에

- **무엇:** 장르가 다른 인기 게임 6개의 특성(패시브·퍽·영구 업그레이드) 시스템을 비교했다. 그 결과로 우리 섬주인 특성 후보 14개를 T-113의 세 갈래에 맞춰 뽑았다.
  - 세 갈래: ① 콘텐츠 특화 6개 · ② 특별한 특성 4개 · ③ 다른 종류 4개
- **배운 것:** 체감이 큰 특성은 수치 가산보다 다음 셋이었다(해석).
  - **콘텐츠 하나를 키우는 노드** — PoE 아틀라스
  - **귀찮은 단계를 지우는 렐릭** — OSRS
  - **드롭 테이블을 편집하는 봉인** — 뱀파이어 서바이버즈
- **비교 메모:** PoE 캐릭터 패시브는 "레벨로 99점"이다. 우리 "계정 Lv100 = 99점"과 수치까지 같아 좋은 비교 기준이 된다(사실).
- **우리에게:** MVP는 넷이다.
  - **섬 전역 개량**: 데이터만으로 들어간다. 계정 Lv50부터 열리는 전 산업 속도 가산이다.
  - **발굴 특기**: 이미 있는 아티팩트용 효과 4·5를 다시 쓴다.
  - **상자 감각**: 새 효과 1개.
  - **응축 숙련**: 새 효과 1개.
- **③ 갈래(기능·편의):** 골라 담기 · 섬주인의 계획서 · 섬 운영안이 후보다. T-169 결정과 "편의를 특성 포인트 뒤에 둘지" 결정을 기다린다.
- **넣지 않은 것:** 판매가·골드 +%(faucet 직접 증가) · 꺼진 동안 생산(오프라인 진행) · 액티브 횟수 +1(이월)은 원칙과 충돌해 뺐다.
- **이미 다룬 관련 회차(새로 분석하지 않음):** [폴아웃 4 퍼크 차트](2026-10-18-21-fallout-4-perk-chart.md) · [하데스 형벌의 서약](2026-10-18-00-hades-pact-of-punishment-bounty.md) · [발헤임 포세이큰 파워](2026-10-18-03-valheim-forsaken-altar-power.md) · [문명 VI 기술 트리](2026-10-17-09-civilization-vi-tech-tree-eureka.md)

## 1. 게임별 분석

표기: 사실에는 출처를 달았다. "(해석)"은 분석 의견이다.

### 패스 오브 엑자일 (Path of Exile) — 아틀라스 패시브 트리

> ARPG(F2P) · PC·콘솔 · 2013-10-23 · Grinding Gear Games · Steam 동시접속 최고 229.3k(2024-07, SteamDB 인용 [wnhub](https://wnhub.io/news/marketing/item-46458))

![포인트 — 아틀라스 맵의 보너스 포인트](https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/238960/4b4df511d6212f5e0a63a8c771dd737185eba2c5/ss_4b4df511d6212f5e0a63a8c771dd737185eba2c5.jpg)
*이 그림에서 볼 것: 현행 아틀라스 맵 화면이다. 중앙 원형에 "100/100"이 보인다. 맵을 처음 완료할 때마다 받는 보너스 포인트다. 좌상단의 나침반 버튼 3개는 트리 슬롯으로 보인다(해석).* — 출처: [Steam](https://store.steampowered.com/app/238960)

![특성 화면 — 지역 아틀라스 트리(구버전 대용)](https://static.wikia.nocookie.net/pathofexile_gamepedia/images/6/62/Haewark_Hamlet_skill_tree_1.png/revision/latest?cb=20210122100932)
*이 그림에서 볼 것: 3.13~3.16 시절의 "Haewark Hamlet" 지역 원형 트리다. 현행 트리 화면을 구하지 못해 대용으로 넣었다.*
*- 상단: "2 Points Left / 0 Refund Points Left"*
*- 하단: APPLY·CANCEL 버튼*
*- 노드: 콘텐츠별 아이콘 갈래로 나뉘어 있다.*
— 출처: [PoE Wiki (Fandom) — Atlas passive skill](https://pathofexile.fandom.com/wiki/Atlas_passive_skill)

![효과 — 노드 하나가 콘텐츠 하나를 키운다](https://static.wikia.nocookie.net/pathofexile_gamepedia/images/9/91/Uncharted_Realms_Skill_Tree.png/revision/latest?cb=20210122193230)
*이 그림에서 볼 것: "The Uncharted Realms" 트리의 노드마다 효과를 화살표로 단 위키 주석 이미지다. "Zana Missions…", "Synthesis Maps…", "Shaper/Elder…"처럼 노드가 각각 특정 콘텐츠를 강화한다. 화살표는 위키가 그린 것이다.* — 출처: [같은 위키 문서](https://pathofexile.fandom.com/wiki/Atlas_passive_skill)

**시스템 요약** (사실, [maxroll](https://maxroll.gg/poe/getting-started/atlas-passive-tree) · 공식 export `points.totalPoints=138`)
- **포인트:** 맵 첫 완료 보너스 100 + 유니크 맵 10 + 나이트메어 맵 5 + 퀘스트 23 = 최대 138점.
- **효과:** 캐릭터 전투력이 아니라 **"내 맵에 무엇이 얼마나 나오는가"**를 바꾼다. 일반 노드 → 노터블 → 키스톤 구조다.
- **되돌리기:** Orb of Unmaking으로 환불 포인트를 얻는다. Kirac 상인에게 Orb of Regret로 살 수도 있다.
- **참고:** 캐릭터 패시브 트리는 레벨로 99점 + 퀘스트 23~24점이다([Fandom](https://pathofexile.fandom.com/wiki/Passive_skill)).

**왜 재미있나 (해석)**
- 같은 맵을 돌아도 트리 하나로 "내가 키우는 콘텐츠"가 정해진다.
- 특정 콘텐츠를 **끄는** 노드가 있어서, 특화가 곧 선택이 된다.

**왜 잘 먹히나 (해석)**
- 리그 메카닉이 수십 종이라 특화할 대상이 많다.
- 떼어 오면 안 되는 이유: 우리 산업은 5종뿐이다. "끄기"형은 선택지를 줄이는 처벌로 느껴질 수 있다.
- 약점: 노드가 많아 신규 이용자가 길을 잃고, 외부 플래너에 기댄다.

| T-113 갈래 | 특성(원문) | 효과 | 조건 |
|---|---|---|---|
| ① | Heart of the Grove | Harvest 작물이 Tier 4일 확률 +60%, 고르지 않은 작물이 시들지 않을 확률 10% | 노터블 |
| ① | Backup Cache | 스트롱박스에서 나온 화폐 복제 | 노터블(상자 특화) |
| ① | Prospector's Luck | 맵에 광맥(Ore Deposits)이 나올 확률 +20% | 노터블(채굴 특화) |
| ② | Crop Rotation | Harvest가 Tier 1 작물만 나옴 + 수확 시 다른 색 작물 등급 상승 확률 | 키스톤(대가 + 보상) |
| ② | Immutable Dogma | Ritual 보상 재굴림 불가, 대신 Tribute +100% | 키스톤 |
| ③ | "끄기" 계열(Black Thumb 등) | 해당 콘텐츠가 나오지 않음 + 다른 추가 콘텐츠 +2% | 노터블 |
| ③ | Unwavering Vision | 스카라브 사용 불가 + **아틀라스 포인트 +20** | 키스톤(포인트를 주는 특성) |

효과 원문 출처: [공식 atlastree-export](https://raw.githubusercontent.com/grindinggear/atlastree-export/master/data.json)

### 스타듀 밸리 (Stardew Valley) — 전문직(Professions)과 숙련(Mastery)

> 농장 생활 시뮬 · 멀티 플랫폼 · 2016-02-26 · ConcernedApe · 4,100만 장 이상([PocketGamer.biz](https://www.pocketgamer.biz/stardew-valley-sells-over-41-million-copies))

![특성 화면 — 기술 탭](https://blogger.googleusercontent.com/img/b/R29vZ2xl/AVvXsEh-xykg5weQh4aeMGlagIOhD3xVo1lUR3efpCFar_zNaRzokh3U1-wKDL2k9YMa9rOYW75xy3JXnA8DFv9QDxYN4MCVatmtLSzZQsySCwANGh3kAxUmqGsGpbdW2M8NVhNEJVlxJL8woMw/s1600/034.png)
*이 그림에서 볼 것: 인게임 기술 탭이다. 5개 기술(Farming·Mining·Foraging·Fishing·Combat)의 10칸 막대에서 **5번째·10번째 칸이 넓다.** 이 두 칸이 전문직을 고르는 레벨이다.* — 출처: [arrpeegeez 공략](https://www.arrpeegeez.com/2016/02/stardew-valley-walkthrough-guide-skills.html)

![선택 — Lv5 전문직 2지선다](https://alittlebithuman.com/wp-content/uploads/2023/05/RancherorTillerStardewValley.png)
*이 그림에서 볼 것: "Level Up / Level 5 Farming / Choose a profession:" 창이다. 왼쪽 Rancher(동물 생산품 +10%)와 오른쪽 Tiller(작물 +10%) 중 하나를 고른다.* — 출처: [A Little Bit Human](https://alittlebithuman.com/rancher-or-tiller-stardew-valley-farming-skill-guide/)

![선택 — Lv10 전문직 2지선다](https://alittlebithuman.com/wp-content/uploads/2023/05/ArtisanorAgriculturistStardewValley.png)
*이 그림에서 볼 것: 같은 창의 Lv10 단계다. Artisan(가공품 +40%)과 Agriculturist(작물 성장 10% 빠름) 중에 고른다. **판매가냐 속도냐**의 갈림이 보인다.* — 출처: [같은 글](https://alittlebithuman.com/rancher-or-tiller-stardew-valley-farming-skill-guide/)

**시스템 요약** (사실, [Skills](https://stardewvalleywiki.com/Skills) · [The Sewers](https://stardewvalleywiki.com/The_Sewers) · [Mastery Cave](https://stardewvalleywiki.com/Mastery_Cave))
- 기술 5종이 행동 경험치로 오른다. 산업 5종인 우리와 같은 구조다.
- **Lv5에 2개 중 1개, Lv10에 그 갈래의 2개 중 1개**를 고른다.
- 되돌리기: 하수도의 Statue of Uncertainty에 10,000g을 내면 그날 밤 다시 고른다.
- 1.6 숙련(Mastery): 5개 기술이 모두 Lv10이 된 뒤 초과 경험치로 숙련을 연다. 비용은 1만~3만, 합계 10만이다.

**왜 재미있나 (해석)**
- 선택지가 "+%"가 아니라 **팔기·가공·편의 중 어느 쪽으로 갈지**를 묻는다.

**왜 잘 먹히나 (해석)**
- 같은 자원을 팔지 가공할지 고르는 경제가 있어서다.
- 떼어 오면 안 되는 이유: 2지선다는 배타적 선택이다. 포인트를 누적하는 우리 레벨형과 섞으면 규칙이 두 가지가 된다.
- 약점: 최적해가 알려져 있어 선택이 사실상 하나로 수렴한다.

| T-113 갈래 | 특성(원문) | 효과 | 조건 |
|---|---|---|---|
| ① | Artisan | 가공품 판매가 +40% | 농사 Lv10 |
| ① | Gemologist | 보석 판매가 +30% | 채광 Lv10 |
| ② | Gatherer | 채집물 2배 획득 확률 20% | 채집 Lv5 |
| ② | Botanist | 채집물이 항상 최고 품질 | 채집 Lv10(Gatherer 갈래) |
| ③ | Tracker | 채집물 위치 표시 | 채집 Lv10(정보 편의) |
| ③ | Luremaster / Mariner | 통발에 미끼 불필요 / 쓰레기 안 나옴 | 낚시 Lv10(유지비·잡동사니 제거) |
| ③ | Mining Mastery | 매일 채광 능력 2개 중 1개를 고르는 석상 제작법 | 전 기술 Lv10 이후 |

### 쿠키 클리커 (Cookie Clicker) — 천국 업그레이드(Heavenly Upgrades) · 방치형

> 방치·클리커 · 웹·PC·모바일 · Steam 2021-09-01 · Orteil, DashNet · Steam 리뷰 88,718건(Steam appdetails 1454400)

![포인트 — 첫 승천 화면](https://static.wikia.nocookie.net/cookieclicker/images/4/41/FirstAscend.jpg/revision/latest?cb=20151226201338)
*이 그림에서 볼 것: 웹 v1.9(2015)의 첫 승천 화면이다.*
*- 상단: Prestige level 126 / Heavenly chips 126 / Reincarnate*
*- 중앙 "Legacy" 툴팁: 칩으로 영구 업그레이드를 사고, prestige 레벨당 CpS +1%*
*- 주변 빈 원: 아직 잠긴 노드*
— 출처: [Cookie Clicker Wiki — Ascension](https://cookieclicker.fandom.com/wiki/Ascension)

![특성 화면 — Steam판 승천 트리](https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/1454400/ss_5ba0fdb8fcf5b02cd4b8ec14869662b406c886a1.1920x1080.jpg)
*이 그림에서 볼 것: Steam판 승천 트리다.*
*- 산 노드는 점선으로 이어져 있다.*
*- 툴팁 "How to bake your dragon [Purchased]"는 **기능 해금**이다.*
*- 하단 알림: "You gain 1,003 prestige levels!"*
— 출처: [Steam](https://store.steampowered.com/app/1454400)

![효과 — 편의 해금 "Buy all"](https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/1454400/ss_39c9c68ddc4fb90b24eb9611fde0c517c260af86.1920x1080.jpg)
*이 그림에서 볼 것: 우측 Store 상단 업그레이드 줄 아래 "BUY ALL UPGRADES" 바가 Inspired checklist의 효과다. 중앙에는 보유 업그레이드 목록이 있다.* — 출처: [Steam](https://store.steampowered.com/app/1454400)

**시스템 요약** (사실, [Ascension](https://cookieclicker.fandom.com/wiki/Ascension))
- 승천(리셋)하면 누적 생산에 비례해 Heavenly Chips와 Prestige Level을 1:1로 받는다.
- 칩으로 선행 조건이 있는 트리를 산다. 산 업그레이드는 영구다.

**왜 재미있나 (해석)**
- 수치 업그레이드 사이에 **기능을 여는 것**(Buy all·영구 슬롯)과 **장난스러운 조건**(레벨 숫자에 7이 들어 있어야 열림)이 섞여 있다.

**왜 잘 먹히나 (해석)**
- 리셋이 반복되는 구조라 "다음 회차 편의"의 가치가 크다.
- 떼어 오면 안 되는 이유: Twin Gates of Transcendence(꺼진 동안 생산)는 **우리 P2의 오프라인 진행 없음과 충돌**한다.
- 약점: 후반 가격이 천문학적이고, 되돌리기가 없다.

| T-113 갈래 | 특성(원문) | 효과 | 조건 |
|---|---|---|---|
| ① | Divine discount / Divine sales | 건물 / 업그레이드 가격 1% 할인 | 선행 업그레이드 |
| ① | Kitten wages | 고양이 업그레이드 10% 할인 | 선행 업그레이드 |
| ② | Lucky Digit | Prestige 효과 +1% 외 | **현재 Prestige Level에 7이 들어 있을 때만** 나타남 |
| ② | Five-finger discount | 커서 100개마다 업그레이드 1% 할인 | 보유량 연동 |
| ② | Sugar baking | 생산 +5%, Lv10 이상 건물마다 +1% | 건물 레벨 연동 |
| ③ | Golden switch | 켜면 생산 +50%, 대신 골든 쿠키 없음(토글) | 기능 해금 |
| ③ | Inspired checklist | "Buy all" + 자동 구매 제외 보관함 | 편의 해금 |
| ③ | Permanent upgrade slot I~V | 넣은 업그레이드 하나가 리셋 후에도 유지 | 장착형 슬롯 |

### 뱀파이어 서바이버즈 (Vampire Survivors) — 파워업(PowerUps) · 로그라이트 영구 강화

> 로그라이트 · PC·콘솔·모바일 · 2022-10-20 · poncle · BAFTA 2023 Best Game([VGC](https://videogameschronicle.com/news/bafta-game-awards-winners)) · Steam 리뷰 252,367건

![특성 화면 — PowerUp Selection](https://vampire.survivors.wiki/images/PowerUps_menu.jpg?4b7cf)
*이 그림에서 볼 것: "PowerUp Selection" 화면이다.*
*- 상단: 보유 골드 291343, 그 아래 파란 **"Refund PowerUps"** 버튼(전액 환불)*
*- 4열 격자: 칸마다 랭크 체크(✓)*
*- 하단 설명칸: Might "피해 5%/랭크(최대 +25%)", 오른쪽에 **Active 체크박스**(개별 끄기)*
— 출처: [Vampire Survivors Wiki — PowerUps](https://vampire.survivors.wiki/w/PowerUps)

![효과 — 판 안에서 쓰는 선택권](https://vampire.survivors.wiki/images/Level_up_screen.jpg?6a157)
*이 그림에서 볼 것: 판 안의 "Level Up!" 3지선다 창이다. 오른쪽 **REROLL / SKIP / BANISH (각 10 left)** 버튼은 파워업으로 산 횟수다. 왼쪽 스탯 목록 끝에도 Reroll·Skip·Banish +10이 보인다.* — 출처: [같은 위키](https://vampire.survivors.wiki/w/PowerUps)

**시스템 요약** (사실, [PowerUps](https://vampire.survivors.wiki/w/PowerUps))
- 판에서 번 골드로 27종을 산다.
- 가격 = 초기가 × (해당 구매 수 + 1) + 20 × 1.1^(전체 구매 수). **아무거나 살 때마다 나머지가 전부 비싸진다.**
- Refund로 거의 전액을 돌려받고, 개별로 끌 수도 있다.

**왜 재미있나 (해석)**
- 공통 수수료 때문에 사는 순서가 전략이 된다.
- 판 안의 **선택권을 사는 파워업**(Reroll·Skip·Banish·Seal)이 순수 수치보다 체감이 크다.

**왜 잘 먹히나 (해석)**
- 한 판 30분 안의 선택지가 곧 재미다.
- 떼어 오면 안 되는 이유: 우리 특성은 계정 레벨당 1점이라 가격 곡선 장치가 맞지 않는다.
- 약점: Curse처럼 함정 항목을 실수로 산다.

| T-113 갈래 | 특성(원문) | 효과 | 조건 |
|---|---|---|---|
| ① | Greed | 골드 획득 +10%/랭크(최대 +50%) | 5랭크 |
| ① | Growth | 경험치 +3%/랭크 | 5랭크 |
| ② | Curse | 적 속도·체력·수 +10%/랭크(대가형) | 끄기 가능 |
| ② | Fever | Gold Fever 지속 +5%/랭크 | 초기가 50,000(후반 전용) |
| ③ | Reroll / Skip / Banish | 랭크당 2회: 레벨업 선택지 다시 굴림 / 건너뛰고 경험치 / 그 판에서 제외 | 횟수형 |
| ③ | Seal I~III | 컬렉션에서 아이템 영구 제외(드롭 테이블 편집) | 초기가 10,000 |
| ③ | Preserve | Reroll 등을 써도 회수가 줄지 않을 확률 10%/랭크 | 5랭크 |

### 올드스쿨 룬스케이프 (Old School RuneScape) — 리그 렐릭(Leagues V: Raging Echoes Relics) · MMO 시즌제

> MMORPG · PC·모바일 · Jagex · 리그 V 2024-11-27~2025-01-22 · 구독자 100만+, 프랜차이즈 동시접속 263,094명(2024-12, [everythingedinburgh](https://everythingedinburgh.com/games/news/old-school-runescape-subscriber-records-jagex/))

![특성 목록 — 리그 V 렐릭 티어](https://oldschool.runescape.wiki/images/RelicsImageTiers2.png?83374)
*이 그림에서 볼 것: 열 하나가 티어 하나다. 1~5티어와 7~8티어는 3개씩, 6티어는 2개다. 아래 표의 Power Miner·Golden God·Friendly Forager·Equilibrium·Pocket Kingdom·Overgrown이 모두 보인다. 인게임 UI가 아니라 공식 아트다.* — 출처: [OSRS Wiki — Raging Echoes League/Relics](https://oldschool.runescape.wiki/w/Raging_Echoes_League/Relics)

![특성 화면 — 인게임 렐릭 창(리그 VI 대용)](https://oldschool.runescape.wiki/images/Demonic_Pacts_League_relic_interface_-_overview.png?735b1)
*이 그림에서 볼 것: 다음 리그(VI, Demonic Pacts)의 인게임 "Relics" 창이다. 같은 렐릭 시스템이라 대용으로 넣었다.*
*- 상단: "11100 points earned" 진행 막대가 문턱마다 끊겨 있다.*
*- 그 아래: "You have a new Relic available for selection!"*
*- 티어별 세로 열: 고른 렐릭은 색이 있고, 고르지 않은 것은 회색이다.*
— 출처: [OSRS Wiki — Demonic Pacts League](https://oldschool.runescape.wiki/w/Demonic_Pacts_League)

![선택·효과 — 렐릭 상세(리그 VI 대용)](https://oldschool.runescape.wiki/images/Demonic_Pacts_League_relic_interface.png?76b2b)
*이 그림에서 볼 것: 렐릭 "Flask of Fervour" 상세 창이다. Active Effects / Passive Effects 칸이 나뉘어 있고, 우측 하단에 Locked·Back 버튼이 있다.* — 출처: [같은 위키](https://oldschool.runescape.wiki/w/Demonic_Pacts_League)

**시스템 요약** (사실, [Relics](https://oldschool.runescape.wiki/w/Raging_Echoes_League/Relics))
- 과제를 깨면 리그 포인트가 쌓인다. 포인트 문턱(0·750·1500·2500·5000·8000·16000·25000)마다 티어가 열린다.
- 티어마다 **3개 중 1개**를 고르고, 바꿀 수 없다.
- 티어가 오르면 공통 패시브(경험치 배율 5→8→12배 등)도 붙는다.

**왜 재미있나 (해석)**
- 렐릭 하나가 수치가 아니라 **"이 행동의 귀찮은 단계를 없앤다"**. 예: 채굴물 자동 은행 보관·자동 제련.
- 채집 게임에서 가장 체감되는 유형이다.

**왜 잘 먹히나 (해석)**
- 본편이 반복 클릭 노동이라, 그 노동을 지우는 것이 큰 보상이 된다.
- 떼어 오면 안 되는 이유: 8주 시즌 한정이라 과격한 효과가 가능했다. 상설 특성에 그대로 넣으면 인플레이션이 생긴다.
- 약점: 되돌릴 수 없어 첫 선택의 실패 비용이 크고, 메타가 쏠린다.

| T-113 갈래 | 특성(원문) | 효과 | 조건 |
|---|---|---|---|
| ① | Power Miner | 채굴 실패 시 50% 추가 성공, 자동 은행 보관, 자동 제련(토글) | 티어 1 |
| ① | Golden God | 알케미 룬 소모 없음, 골드 +15%, 자동 반복 | 티어 3 |
| ① | Treasure Arbiter | 클루 상자가 항상 최대 횟수로 보상을 굴림 | 티어 5 |
| ② | Friendly Forager | 채집하면서 약초를 함께 줍는 확률 | 티어 2(교차 산업) |
| ② | Equilibrium | 경험치를 얻을 때 총레벨의 10% 추가, **가장 낮은 기술은 20%** | 티어 4(보정형) |
| ③ | Pocket Kingdom | 방치형 미니게임 원격 관리, 비용 −50%, 생산 2배, 하루 → 매시간 정산 | 티어 7 |
| ③ | Banker's Note | 은행권 변환 도구(인벤토리 압축) | 티어 6 |
| ③ | Overgrown | 작물이 죽지 않음, 자동 재수확·재파종 | 티어 8 |

### 팩토리오 (Factorio) — 연구: 조건 해금(Trigger)과 무한 연구 · 자동화 전략

> 자동화·전략 · PC·스위치 · 1.0 2020-08-14 · Wube Software · 350만 장(2022-12, [PC Gamer](https://pcgamer.com/factorio-has-sold-35-million-copies)) · Space Age 40만 장([GamingOnLinux](https://gamingonlinux.com/2024/10/factorio-space-age-sold-over-400000-copies-and-sets-a-new-player-count-record))

![특성 화면 — 연구 화면](https://wiki.factorio.com/images/Technology_Screen.png)
*이 그림에서 볼 것: 연구 화면(1.x 계열)이다.*
*- 좌측: Research queue와 "Logistics 2 (Available)"의 Cost·Effects*
*- 우측 Technology tree: 완료는 초록, 가능은 노랑, 잠김은 빨강*
— 출처: [Factorio Wiki — Research](https://wiki.factorio.com/Research)

![조건 해금 — 행동으로 열리는 연구](https://cdn.factorio.com/assets/blog-sync/fff-376-early-trigger-technologies.png)
*이 그림에서 볼 것: "Steam power (Available)" 칸에 비용 대신 **"Researched by: Craft 50 Iron plate"**가 적혀 있다. "Electronics"는 "Craft 10 Copper plate"다. 오른쪽 아래에 Start research 버튼이 있다.* — 출처: [Friday Facts #376](https://factorio.com/blog/post/fff-376)

![무한 연구 — 1–∞ 레벨](https://cdn.factorio.com/assets/blog-sync/fff-376-rocket-control-unit-productivity.png)
*이 그림에서 볼 것: "Rocket control unit productivity 2"다. 아이콘 우하단의 **"1–∞"** 레벨 표시와 툴팁 "productivity: +10%"를 보면 된다. 개발 블로그(2024) 시점 화면이다.* — 출처: [Friday Facts #376](https://factorio.com/blog/post/fff-376)

**시스템 요약** (사실, [Technologies](https://wiki.factorio.com/Technologies) · [Mining productivity](https://wiki.factorio.com/Mining_productivity_(research)))
- 공장에서 만든 과학팩을 연구소가 소비해 기술을 연다. 대기열이 있다.
- **Trigger technology:** 연구소 없이 행동 조건만으로 열린다. 예: 철판 50개 제작, 원유 첫 채굴.
- **무한 연구:** Mining productivity는 레벨당 +10%이고 상한이 없다.

**왜 재미있나 (해석)**
- 연구 재화가 곧 내 생산물이다.
- 조건 해금은 "해 봤으니 알게 됨"이라는 납득감을 준다.

**왜 잘 먹히나 (해석)**
- 결국 다 연다. 선택이 아니라 순서의 문제다.
- 떼어 오면 안 되는 이유: 우리는 포인트가 부족해 다 못 찍는다. 선택의 무게가 다르다.
- 약점: 고민이 약하다. 구조는 [문명 VI 기술 트리](2026-10-17-09-civilization-vi-tech-tree-eureka.md) 회차와 겹친다.

| T-113 갈래 | 특성(원문) | 효과 | 조건 |
|---|---|---|---|
| ① | Mining productivity | 채굴 생산성 +10%/레벨 | 무한 |
| ② | Oil processing / Uranium processing | 해당 자원을 처음 채굴하면 열림 | Trigger(행동 조건) |
| ② | Agricultural science pack | bioflux 100개 제작 시 열림 | Trigger(누적량 조건) |
| ③ | Toolbelt | 인벤토리 +10칸 | 1회성 편의 |
| ③ | Research queue | 연구 대기열(새 게임에서 기본 켜짐) | 편의 |

## 2. 비교 표


| 게임 | 특성 구조 | 포인트 획득 | 선택의 무게 | 되돌리기 | 상한 |
|---|---|---|---|---|---|
| 패스 오브 엑자일 (아틀라스) | 연결형 트리(일반 → 노터블 → 키스톤) | 맵 첫 완료 100 + 유니크 10 + 나이트메어 5 + 퀘스트 23 | 무거움 — 키스톤은 대가 + 보상 | Orb of Unmaking·Regret로 부분 환불 | 138점 |
| 스타듀 밸리 | 기술 5종 × 2단 갈래(Lv5·Lv10에서 2지선다) + 숙련 | 행동 경험치로 기술 레벨 | 무거움 — 배타 선택 | 10,000g 석상으로 그날 밤 재선택 | 기술당 2개 + 숙련 5 |
| 쿠키 클리커 | 선행 조건 트리 | 승천할 때 누적 생산에 비례한 칩 | 가벼움 — 결국 다 산다 | 없음 | 사실상 무한 |
| 뱀파이어 서바이버즈 | 독립 목록 27종 | 판에서 번 골드(살 때마다 전체 가격 상승) | 가벼움 — 사는 순서가 전략 | 전액 환불 + 개별 끄기 | 항목별 랭크 |
| OSRS 리그 렐릭 | 티어별 3지선다(8티어) | 과제 포인트 문턱 | 매우 무거움 | 변경 불가 | 8개 |
| 팩토리오 | 선행 트리 + 무한 연구 + Trigger | 생산한 과학팩 소비 | 가벼움 — 순서만 고른다 | 없음 | 무한 |
| **우리(지금)** | 레벨형 16칸 표(공통·산업 5 × 개척·속도·산출량) | 계정 레벨마다 1점 | 중간 — 130칸 중 99점, 개척 20은 사실상 필수 | 미정(묘약은 데이터에만 있음) | 99점(Lv100) |

---


## 3. 우리 특성 후보

> Design Architect 결과 전부다. 이름은 모두 가칭이고, 수치는 전부 테스트값 초안이다. "(사실)"은 우리 문서·코드·출처, "(제안)"은 이 문서의 안이다.

### 3.1 후보 표 (제안 · 이름은 전부 가칭)

| # | 이름 | 갈래 | 효과 | 참고한 게임 | 새 EffectType (가칭) | 최대 Lv | 레벨당 값 초안 (테스트값) · 계정 Lv 조건 | 연계 시스템 | 난이도 서버/클라/데이터 | P1 P2 P3 P4 |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 상자 감각 | ① 상자 | 상자(나무·은·황금)의 판정당 확률에 곱한다. 발굴은 제외한다(#2가 맡는다) | PoE Prospector's Luck · (폴아웃 4 아이디어 2 '보물 감각' 참고) | 필요: `BoxChanceAdd` | 5 | ×(1+3%/Lv) · Lv L = (L−1)×5 | [아이템](../../item/README.md) 4.1 · [자원채취](../../gathering/README.md) 2.4 | 하 / 하* / 하 | ✅ ✅ ✅ ⚠️ |
| 2 | 발굴 특기 (감정사의 눈 · 유물 탐지, 2행) | ① 발굴·감정 | 감정 비용 할인(아티팩트와 합쳐 500‰까지) · 발굴 유물 확률에 곱 | PoE 노터블(노드 하나가 콘텐츠 하나를 키움: Heart of the Grove) · (폴아웃 4 아이디어 3 참고) | **불필요**: `AppraiseCostDiscount`(4) · `RelicChanceAdd`(5) 재사용 | 5 + 5 | −2%/Lv · ×(1+3%/Lv) · Lv L = (L−1)×5 | [유물 감정](../../gathering/excavation/유물감정.md) 3.3·4장 | 하 / 하* / 하 | ✅ ✅ ✅ ⚠️ |
| 3 | 장인의 손 | ① 장비/인챈트 | 인챈트 큐브(A·B)를 써도 확률로 소모되지 않는다 | VS Preserve(써도 회수가 줄지 않을 확률 10%/랭크) · (폴아웃 4 아이디어 2 참고) | 필요: `CubeSaveChance` | 5 | 2%/Lv · Lv L = (L−1)×5 | [캐릭터](../../character/README.md) 1.4 · [아이템](../../item/README.md) 4.2 | 하~중 / 하* / 하 | ✅ ✅ ✅ ✅ |
| 4 | 분해 숙련 | ① 장비 | 장비를 분해할 때 마력의 파편 +%(소수부는 확률) | Stardew Artisan(가공품 +40%) · Factorio Mining productivity | 필요: `DisassembleYieldAdd` | 5 | +10%/Lv · Lv L = (L−1)×5 | [캐릭터](../../character/README.md) 5.6 · [아이템](../../item/README.md) 4.3 · [T-131](../../../../tasks/T-131-장비분해.md) | 하 / 하* / 하 | ✅ ✅ ✅ ✅ |
| 5 | 응축 숙련 | ① 캐릭터 육성 | 응축 재료 1개를 확률로 2개로 센다. ★ 도달이 빨라진다(★ 효과는 기존 속도 가산 자리) | Stardew Gatherer(2배 획득 확률 20%) · PoE Backup Cache(복제) | 필요: `CondenseBonusChance` | 5 | 5%/Lv · Lv L = (L−1)×5 | [캐릭터](../../character/README.md) 5.5 · [T-169](../../../../tasks/T-169-중복개체수용재검토.md) | 하 / 하* / 하 | ✅ ✅ ✅ ✅ |
| 6 | 장기 위탁 | ① 거래소 | 거래소·경매장 등록 기간을 늘린다(48 → 최대 96시간) | 직접 대응 사례 없음. OSRS 렐릭의 "귀찮은 단계 제거" 방향만 빌림 | 필요: `ListingHoursAdd` | 4 | +12시간/Lv · Lv L = 10+(L−1)×5 (거래소 개방 Lv10에 맞춤) | [거래](../../trade/README.md) 4.3 | 중 / 하* / 하 | ✅ ✅ ✅ ✅ |
| 7 | 균형 개발 | ② 조건부 | 개척 레벨이 가장 낮은 산업(동률 포함)의 슬롯 속도에 가산 | OSRS Equilibrium(가장 낮은 기술 20%) | 필요: `SpeedAddLowestFrontier` | 5 | +2%/Lv · Lv L = 10+(L−1)×5 | [특성](../../trait/README.md) 2.2 · [산업 레벨](../../gathering/산업레벨.md) 3장 · [진행 및 성장](../../progression/README.md) 2.5 | 하~중 / 하* / 하 | ✅ ✅ ✅ ⚠️ |
| 8 | 다각 경영 | ② 산업 조합 | 가동 중인 슬롯에 5개 산업이 모두 있으면 전 산업 속도에 가산 | OSRS Friendly Forager(교차 산업) · Cookie Sugar baking(보유 상태 연동) | 필요: `SpeedAddAllIndustries` | 5 | +1%/Lv · Lv L = 15+(L−1)×5 | [작업슬롯](../../workslot/README.md) 3.1·3.4 · [산업 레벨](../../gathering/산업레벨.md) 2.2 | 중 / 하* / 하 | ✅ ✅ ✅ ✅ |
| 9 | 섬 전역 개량 | ② 특정 레벨 이상 | 전 산업 속도에 가산. **계정 Lv50부터만** 열린다(후반 전용) | VS Fever(후반 전용 고가 파워업) · Factorio 무한 연구 · PoE 레벨 99점 | **불필요**: `SpeedAdd` + `Industry=None` | 10 | +1%/Lv · Lv L = 45+5L (Lv1 50 → Lv10 95) · TID 200(가칭, 300 공통 산출량과 같은 규칙) | [특성](../../trait/README.md) 2.1·2.2 · [진행 및 성장](../../progression/README.md) 2.5·2.6 | **없음** / 하 / 하 | ✅ ✅ ✅ ⚠️ |
| 10 | 개척 일지 | ② 조건부 해금 | 행동 누적(예: 감정 50회)을 채워야 대상 특성의 Lv1을 찍을 수 있다. 포인트는 그대로 든다 | Factorio Trigger technology · (문명 VI 유레카 문서 참고) | 효과가 아니라 장치다: 레벨 표에 조건 컬럼(가칭 `RequiredActionType` · `RequiredActionCount`) + 누적 기록 | — | 예: 발굴 특기 Lv1 ← 감정 50회 | [특성](../../trait/README.md) 2.1 · [퀘스트](../../quest/README.md) 3장(도전) | 상 / 중 / 중 | ✅ ✅ ⚠️ ✅ |
| 11 | 골라 담기 | ③ 기능 | 상자 풀의 범주를 끈다(토글, 언제든). Lv1 장비 · Lv2 비약·영약. 끈 가중치는 같은 상자의 **자원** 몫으로 옮긴다(골드 몫에는 보태지 않는다) | VS Seal(드롭 테이블 편집) · PoE "끄기" 노드 · Stardew Mariner | 필요: `BoxSeal` | 2 | 범주 1개/Lv · Lv1 = 5 · Lv2 = 20 | [아이템](../../item/README.md) 4.1 · [우편](../../mail/README.md) 2.4 · [T-169](../../../../tasks/T-169-중복개체수용재검토.md) | 중 / 중 / 하 | ✅ ✅ ✅ ✅ |
| 12 | 섬주인의 계획서 | ③ 편의 | 찍을 순서를 정해 두면, 포인트와 계정 레벨 조건이 맞을 때 서버가 1레벨씩 자동으로 찍는다 | Factorio Research queue(새 게임에서 기본 켜짐) · (폴아웃 4 아이디어 5 '수첩'과 겹침. 그쪽은 클라 표시만) | 필요: `TraitQueue` | 1 | 기능 해금 · Lv1 = 5 | [특성](../../trait/README.md) 2.1·3장 · [진행 및 성장](../../progression/README.md) 2.1 | 중 / 중 / 하 | ✅ ✅ ✅ ✅ |
| 13 | 여운 | ③ 액티브 강화 | 액티브 1회의 지속시간을 늘린다. 하루 2회·이월 없음은 그대로 | VS Fever(지속 +5%/랭크) · (발헤임 문서 아이디어 A '가호' 참고) | 필요: `ActiveDurationAdd`(분) | 3 | +10분/Lv(60 → 90분) · 계정 조건 = 액티브 해금 뒤(미정) | [특성](../../trait/README.md) 2.4·2.6 · [퀘스트](../../quest/README.md) 2.1 | 하(액티브가 선행) / 하 / 하 | ✅ ✅ ⚠️ ⚠️ |
| 14 | 섬 운영안 | ③ 편의 | 슬롯 배치 `(산업, 레벨, 캐릭터)` 묶음을 저장해 두었다가 한 번에 바꾼다. 기본 1안 + Lv마다 1안 | PoE 아틀라스 화면의 트리 슬롯 버튼 3개(Collector 해석이라 확인 필요) · Cookie Permanent upgrade slot | 필요: `PresetSlotAdd` | 2 | +1안/Lv · Lv1 = 10 · Lv2 = 30 | [작업슬롯](../../workslot/README.md) 2장·3.1 · [게임 UI](../../ui/README.md) 2.0.1 | 중 / 중 / 하 | ✅ ✅ ✅ ✅ |

- **하\***: 지금 특성 화면(T-116 A안: 공통·산업 5 × 개척·속도·산출량)에는 산업과 무관한 콘텐츠 특성이 들어갈 자리가 없습니다. 첫 후보를 넣을 때 새 구역을 한 번 만들어야 하고(클라 중), 그 뒤 줄 추가는 하입니다.
- **보류: 토벌 대장 (T-114).** 레이드가 정해지면 "파견대 판정 보정"이나 "토벌령 제작 자원 절감" 같은 자리가 생깁니다. 지금은 보스 형태·보상·티켓([사냥](../../gathering/hunting/README.md) 4.3)이 전부 미정이라 형태만 남겨 둡니다.
- 폴아웃 4 회차의 추천안 **특산지(끝 레벨의 변신)**는 ② 갈래의 기존 후보로 그대로 유효합니다. 표에서는 반복하지 않았습니다.

#### 3.1.1 후보별 근거
각 항목은 앞에 출처 게임, 뒤에 그 후보가 얹히는 우리 규칙입니다.

1. **상자 감각**
   - 출처(사실): PoE Prospector's Luck는 "맵에 광맥이 나올 확률 +20%"처럼 콘텐츠의 출현 확률을 올립니다.
   - 우리 규칙(사실): 상자는 자원과 따로 굴립니다(`CommonRewardTable`, 판정당 2%·0.3%·0.03%). 서버 `CommonRewardCatalog.Roll`은 이미 `chanceAddPermille`(모든 행 확률에 곱하는 값)를 받습니다. 지금은 발굴 유물에만 0이 아닌 값이 들어갑니다.
2. **발굴 특기**
   - 출처: PoE 노터블은 노드 하나가 콘텐츠 하나를 키웁니다.
   - 우리 규칙(사실):
     - `UserTraitEffect` 4·5는 아티팩트 전용으로 이미 있습니다(`Server/GameData/Enum.cs`).
     - 적용 지점도 이미 있습니다. 할인은 `User.GetAppraiseCost`(500‰ 상한), 유물 확률은 `SettleWorkStation`의 `chanceAdd`입니다.
     - 발굴은 고유 출구가 없는 산업입니다([게임기획코어](../../게임기획코어.md) 5장).
3. **장인의 손**
   - 출처: VS Preserve.
   - 우리 규칙(사실): 큐브는 성공·실패와 관계없이 1개 소모됩니다. 상급 큐브는 고르기 전에 이미 소모됩니다([캐릭터](../../character/README.md) 1.4). 그래서 보존 판정은 소모 시점 한 곳에만 넣으면 됩니다.
4. **분해 숙련**
   - 출처: Stardew Artisan · Factorio 생산성.
   - 우리 규칙(사실):
     - 파편은 0G이고 판매할 수 없습니다([아이템](../../item/README.md) 4.3).
     - 분해는 서버 ✅이고, 칸이 모자라면 거절합니다(T-131). 늘어난 수량으로 칸을 검사해야 합니다.
     - 파편 교환처는 아직 구현되지 않았습니다.
5. **응축 숙련**
   - 출처: Stardew Gatherer.
   - 우리 규칙(사실): ★은 누적 재료 수(8·22·44·76)로 오르고, ★ 효과는 속도 가산 자리에 들어갑니다(캐릭터 5.5). T-169에서 문제가 된 중복 캐릭터에 출구가 하나 더 생깁니다.
6. **장기 위탁**
   - 우리 규칙(사실): 등록 기간은 `AuctionListingHours` 48입니다. 만료되면 등록비를 돌려줍니다([거래](../../trade/README.md) 1장 #10).
   - 그래서 기간을 늘려도 골드 흐름은 같고, 다시 올리는 손만 줄어듭니다.
7. **균형 개발**
   - 출처: OSRS Equilibrium.
   - 우리 규칙(사실): 개척 레벨 = 열린 산업 레벨이고(특성 2.2), 10주 목표는 5개 산업 모두 Lv5입니다(진행 2.5). 효과는 `TraitAddPermille`(가산)에 들어갑니다.
   - 제안: "개척 20점은 사실상 필수"(폴아웃 문서의 빈 곳 3)였던 것을 "어느 산업부터 찍을까"라는 순서 고민으로 바꿉니다.
8. **다각 경영**
   - 출처: OSRS Friendly Forager · Cookie Sugar baking.
   - 우리 규칙(사실): 슬롯마다 산업은 1개이고, 슬롯 수만큼 동시에 돕니다. 배치를 바꾸기 전에는 정산합니다(작업슬롯 1장·3.1).
9. **섬 전역 개량**
   - 출처: VS Fever(초기가 50,000, 후반 전용) · PoE 레벨로 99점(우리와 같은 수).
   - 우리 규칙(사실): `SumTraitEffect`는 `Industry=None`이면 전 산업에 더합니다. 그래서 서버 수정이 없습니다.
   - A안 표의 **공통 × 속도 칸은 지금 비어 있어서** 그 자리에 들어갑니다.
10. **개척 일지**
    - 출처: Factorio Trigger(철판 50개 제작 → Steam power).
    - 우리 규칙(사실): 레벨 조건은 지금 `AccountLevel` 하나뿐입니다. 퀘스트 시스템이 미구현이라([진행 및 성장](../../progression/README.md) 2.7) 누적을 기록할 자리가 없습니다. 그래서 서버 난이도가 상입니다.
11. **골라 담기**
    - 출처: VS Seal · Stardew Mariner.
    - 우리 규칙(사실): 상자 풀은 장비 9% · 비약 10~20% · 영약 3.9~9.9%입니다(아이템 4.1). 넘침 우편은 장비 칸이 찬 상태에서 상자가 장비를 줄 때 생깁니다(우편 2.4). T-169의 "칸이 금방 찬다"를 섬주인이 직접 고르는 방식으로 완화합니다.
12. **섬주인의 계획서**
    - 출처: Factorio Research queue.
    - 우리 규칙(사실): 배우기 순서에서 포인트 차감은 맨 뒤입니다(특성 2.1). 포인트는 계정 레벨업에서 생깁니다(`User.Account.cs`).
    - 제안: 레벨업 지점에서 대기열 첫 칸을 `TryLearnTrait`로 시도합니다. P2(띄워만 두면 자란다)를 특성 성장까지 넓힙니다.
13. **여운**
    - 출처: VS Fever.
    - 우리 규칙(사실): 액티브는 하루 2회 · 1시간 · 이월 없음이고, 발동·만료 때 정산합니다(특성 2.4·4장). 액티브가 아직 미착수라서 그 뒤에 들어갑니다.
14. **섬 운영안**
    - 우리 규칙(사실): DB에 남는 것은 배치 설정뿐이고(작업슬롯 2장), 바꾸기 전에 정산합니다(3.1).
    - 제안: 일괄 배치는 정산 1회 + 칸마다 기존 검증(적성 0 · 미해금 레벨)으로 처리합니다.


### 3.2 갈래별 추천 상위 2

| 갈래 | 1위 | 2위 | 이유 |
|---|---|---|---|
| ① 콘텐츠 특화 | **상자 감각** | **발굴 특기** | 상자는 첫 세션 15분부터 모두가 체감합니다. 서버 적용 지점(`Roll`의 인자)도 이미 있고, 1점 가치(근사 +0.58%)가 산업 산출량(+0.76%)과 비슷해 맞추기 쉽습니다. 발굴 특기는 새 EffectType이 0개이고, 고유 출구가 없는 발굴에 정체성을 줍니다. 할인 상한 500‰도 아티팩트와 같이 써서 규칙이 늘지 않습니다. (3위는 응축 숙련: faucet과 무관하고 T-169의 중복에 출구를 줍니다.) |
| ② 특별한 특성 | **섬 전역 개량** | **균형 개발** | 섬 전역 개량은 데이터만으로 되고, A안의 빈 칸에 들어갑니다. 계정 Lv50 이후 포인트의 목적이 되어 구간 5(10주 이후)에 할 일을 만듭니다. 균형 개발은 필수였던 개척을 "순서의 선택"으로 바꾸고, 산업 속도·산출량 특화와 긴장을 만듭니다. 10주 목표(5개 산업 모두 Lv5)와도 방향이 같습니다. |
| ③ 다른 종류 | **골라 담기** | **섬주인의 계획서** | 골라 담기는 수치가 아니라 드롭 테이블을 편집하는 것(VS Seal)이라 ③의 정의에 가장 맞습니다. 칸 문제(T-169)와 우편 넘침에 직접 닿습니다. 단 T-169 결정 뒤에 넣습니다. 계획서는 레벨업 때 클릭을 없애 P2를 특성 성장까지 넓힙니다. 다만 Factorio는 대기열을 결국 기본 기능으로 돌렸습니다(→ 3.5 질문 2). |


### 3.3 MVP 묶음 (목표 회차 261024)

**MVP: ⑨ 섬 전역 개량 · ② 발굴 특기(2행) · ① 상자 감각 · ⑤ 응축 숙련**

**고른 이유**
- **서버 구현 상태(사실)**
  - 레벨형 특성 서버(T-108)와 클라 표(T-116, Closed)가 이미 있습니다.
  - 배우기 패킷 `C_UserTraitLearnRequest`를 그대로 씁니다. 그래서 패킷·DB 변경이 0입니다.
- **새 EffectType 수**: 섬 전역 개량 0 · 발굴 특기 0(4·5 재사용) · 상자 감각 1 · 응축 숙련 1입니다. enum 뒤에 2개만 붙이면 됩니다("뒤에만 추가" 규칙).

**서버가 할 일(제안)**
1. `UserTraitCatalog`에 효과 4·5와 신규 효과의 목록을 추가합니다. 알 수 없는 EffectType은 로드할 때 거부하게 합니다(3.6 참고).
2. 특성 합을 세 곳에 더합니다. `GetAppraiseCost`의 할인, 발굴 `chanceAdd`, 발굴 외 산업의 `Roll` `chanceAddPermille`입니다.
3. `TryLearnTrait`의 "바꾸기 전에 정산" 조건에 판정 결과를 바꾸는 효과(유물·상자 확률)를 넣습니다.
4. `TryCondenseCharacter`의 개수 계산에 한 줄을 넣습니다.

**나머지 작업**
- 클라: 공통 × 속도 칸 하나 + 콘텐츠 특화 구역 하나(4줄)입니다.
- 데이터: `UserTraitTable` 5행 · `UserTraitLevelTable` 30행 · `Enum.xlsx` 2개입니다. 이름은 excel-table-creator 절차대로 먼저 확인받습니다.
- 포인트: 130칸 → 160칸이 됩니다(99점 = 62%).

**빠진 것**
- ③ 갈래는 이번 MVP에 없습니다.
  - 골라 담기: T-169 결정을 기다립니다.
  - 계획서: 질문 2(특성으로 둘지)를 기다립니다.
  - 여운: 액티브가 미착수입니다.
- **상자 감각과 유물 탐지는 질문 3이 (b) 이상일 때만 들어갑니다.** (a)로 정해지면 MVP를 섬 전역 개량 · 감정사의 눈 · 응축 숙련 · 장인의 손으로 바꿉니다.


### 3.4 지켜야 할 것 점검 표

| 후보 | 속도는 가산 | 일일 장치(P3) | 골드 faucet (거래와 함께) | 99점 균형 | 어긋날 때 회피안 |
|---|---|---|---|---|---|
| 1 상자 감각 | — | — | ⚠️ 간접. 상자 내용의 30%가 골드입니다. Lv5면 수입 약 +2.9%, 그중 골드 약 +0.9%(근사) | ✅ 5칸 · 1점 ≈ +0.58% | 산출량 특성과 같은 "1점 예산"으로 맞추고 거래 시뮬레이션에 함께 넣습니다 · 발굴은 제외합니다 |
| 2 발굴 특기 | — | — | ⚠️ 감정 비용(골드 sink)을 깎습니다. 유물이 늘면 감정 풀의 골드가 간접으로 늡니다 | ✅ 10칸. 발굴을 안 돌리는 섬은 고를 이유가 없습니다(선택) | 할인 상한 500‰을 아티팩트와 공유합니다 · 유물 탐지는 상자 감각과 같은 예산으로 둡니다 |
| 3 장인의 손 | — | — | ✅ 경미. 남는 큐브를 즉시 판매(500G)할 때만 간접으로 늡니다 | ✅ | — |
| 4 분해 숙련 | — | — | ✅ 파편은 0G이고 판매할 수 없습니다 | ⚠️ 교환처가 없으면 효과가 비어 죽은 점이 됩니다 | 교환 일감 뒤에 엽니다 |
| 5 응축 숙련 | ✅ ★ 가산 자리 | — | ✅ | ✅ | — |
| 6 장기 위탁 | — | — | ✅ 등록비는 총액 기준이고 만료면 환급됩니다 | ✅ 4칸 | — |
| 7 균형 개발 | ✅ `TraitAddPermille` | — | ⚠️ 속도가 오르면 채취 총량이 늡니다 | ⚠️ 5개가 동률이면(시작·10주 완성) 전 산업에 붙어 1점 ≈ +2%가 됩니다. 산업 속도의 2배입니다 | 레벨당 값을 낮추거나 "동률이면 절반"으로 둡니다 · 시뮬레이터로 확인합니다 |
| 8 다각 경영 | ✅ | — | ✅ 기존 속도 특성과 같은 결 | ✅ 조건을 채우면 1점 ≈ +1% | 슬롯이 5칸이 되기 전엔 죽은 칸이라 계정 조건을 Lv15부터 둡니다 |
| 9 섬 전역 개량 | ✅ `SpeedAdd`·`None` | — | ⚠️ 후반에 전 산업 +10%라 10주 이후 faucet이 늡니다 | ⚠️ 칸이 +10 늡니다. 대신 Lv50 이후 포인트의 출구가 됩니다 | 레벨당 +1%를 유지하고 10주 이후 경제와 함께 봅니다 |
| 10 개척 일지 | — | ⚠️ 조건이 "기간 안에 N회"면 ❌입니다 | 대상 특성을 따릅니다 | ✅ 포인트 비용은 그대로 | 누적·영구 조건만 둡니다 · 달성해도 팝업 없이 특성 탭에만 표시합니다(P1) |
| 11 골라 담기 | — | — | ⚠️ 끈 몫을 **골드로** 옮기면 faucet이 직접 늘어 ❌입니다 | ✅ 2칸 | 끈 몫은 자원 몫으로만 옮깁니다 |
| 12 계획서 | — | — | ✅ | ⚠️ 1점이 "세금"처럼 읽힙니다 | 기본 기능으로 두는 안을 검토합니다(질문 2) |
| 13 여운 | ✅ 액티브 효과가 속도면 가산 자리 | ⚠️ 하루 가속 상한이 2시간 → 3시간이 됩니다. 이월·횟수는 그대로라 "가속"은 유지됩니다 | ⚠️ 액티브 효과가 "골드 획득 증가"로 정해지면 직접 증가라 ❌입니다 | ✅ 3칸 | 액티브 효과·배수가 확정된 뒤(특성 5장 Q2) 값을 정합니다 |
| 14 섬 운영안 | — | — | ✅ | ✅ 2칸 | — |

**포인트 총량(사실 + 계산)**

| 상황 | 칸 수 | 99점이 차지하는 비율 |
|---|---|---|
| 지금 | 130칸 | 76% |
| 개척 20칸 제외 | 110칸 | 79점 / 110칸 = 72% |
| MVP 추가 후 | 160칸 | 62% |
| 후보 전부 추가 후(장치 제외 +62) | 192칸 | 개척 제외 79점 / 172칸 = 46% |

**1점 가치 근사(해석)**
- 가정: 5개 산업에 균등하게 배치했고, 상자가 수입의 24%를 차지합니다(아이템 4.1의 "+32%"에서 나온 값).
- 기존 칸:
  - 산업 속도 +5% → 수입 +1.0%
  - 산업 산출량 +5% → +0.76%
  - 공통 산출량 +2% → +1.52%
- 후보 칸:
  - 상자 감각 → +0.58%
  - 섬 전역 개량 · 다각 경영 → +1.0%
  - 균형 개발 → +0.4%(최저 산업 1개) ~ +2.0%(5개 동률)
- 속도 계열은 경험치도 늘려서 계정 레벨 → 포인트로 조금 되먹임됩니다.

**넣지 않은 것**

| 게임 사례 | 판정 | 이유 |
|---|---|---|
| 판매가·골드 획득 +% (VS Greed · OSRS Golden God · Stardew Artisan/Gemologist) | ❌ | faucet을 직접 늘립니다. 즉시 판매가는 `BasePrice` 100% 상수입니다([거래](../../trade/README.md) 3.1) |
| 꺼진 동안 생산 (Cookie Twin Gates) | ❌ | P2 오프라인 진행 없음과 충돌합니다 |
| 액티브 횟수 +1 · 미사용분 보관 | ❌ | P3 이월 금지에 걸리고, 누를 일이 늘어 P1과도 부딪힙니다 |
| 가챠·슬롯 해금·수수료 할인 (Cookie Divine discount) | ⚠️ | sink가 줄어듭니다. 5장 미확정인 수수료율·가챠 비용이 정해진 뒤에 봅니다 |
| 인벤토리 칸 +N (Factorio Toolbelt · OSRS Banker's Note) | ⚠️ | 기존 기능이 세금이 됩니다 · T-169 결정을 기다립니다 |
| 포인트 자체를 주는 특성 (PoE Unwavering Vision) | ⚠️ | 99점 축이 흔들립니다 |
| 가챠 제외·천장 (VS Banish) | ⚠️ | 가챠 확정 지급이 5장 미확정이라 선택지로만 둡니다 |


### 3.5 결정 필요 질문 (선택지로만 제시)

1. **특성 초기화를 어떻게 두는가?** 새 특성이 늘수록 잘못 찍은 대가가 커집니다. 특성 3장은 "리셋 미정"인데 아이템 4.4는 망각의 묘약을 확정으로 적었고, 서버에는 사용 처리가 없습니다. 폴아웃 회차 Q3과 같은 질문으로, 아직 열려 있습니다.
   - (a) 초기화 없음 — 묘약을 걷어낸다
   - (b) 망각의 묘약으로만 (지금 데이터: 상점 10,000G · 황금 상자 0.5% · 파편 200)
   - (c) VS식 무료 전액 환불, 또는 Stardew식 골드 비용(새 sink)
2. **기능·편의(③)를 특성 포인트 뒤에 두는가?** 대상은 계획서 · 섬 운영안 · 골라 담기입니다.
   - (a) 특성으로만 연다
   - (b) 기본 기능으로 두고, 특성은 확장(프리셋 칸 · 봉인 범주)만 맡는다
   - (c) ③ 갈래는 액티브 강화에만 쓴다
3. **특성이 판정 '덤'의 확률에 개입하는가?** [특성](../../trait/README.md) 5장 Q5와 [자원채취](../../gathering/README.md) 2.4 "특성이 확률에 개입하는지 미정"에 해당합니다. MVP의 상자 감각·유물 탐지가 이 답에 달려 있습니다.
   - (a) 개입하지 않는다 — 확률은 아티팩트·건물·소모품만 바꾼다
   - (b) 상자·유물의 **발견 확률(곱)**까지만, 희귀도 가중치는 건드리지 않는다
   - (c) 희귀도 가중치까지 — 건물 희귀도 노드와 같이 정한다



### 3.6 구현 메모 (사실 — 서버 코드 확인)


- **`UserTraitCatalog.Load`**: `SpeedAdd`·`YieldAdd`·`IndustryUnlock`만 목록에 넣습니다. 다른 `EffectType` 행은 로드는 되지만 어디에도 붙지 않고, 검증도 없습니다. 새 효과를 데이터에 먼저 넣으면 조용히 먹통이 됩니다. 로드할 때 거부하는 검사를 권합니다.
- **`TryLearnTrait`**: `SpeedAdd`·`YieldAdd`일 때만 정산·재계산합니다. 그래서 두 가지가 더 필요합니다.
  - 상자·유물 확률 효과도 정산 대상에 넣어야 합니다. 아티팩트 코드는 이미 `RelicChanceAdd`를 넣습니다.
  - 균형 개발은 개척(`IndustryUnlock`)을 배울 때도 속도를 다시 계산해야 합니다.
- **`AssignWorkStation`**: 그 칸의 속도만 다시 매깁니다. 다각 경영은 전 슬롯 재계산이 필요합니다.
- **`CommonRewardCatalog.Roll`**: `chanceAddPermille`가 이미 있습니다.
- **`UserTraitEffect`**: 0~5까지 있고, 4·5는 "아티팩트 전용" 주석이 달려 있습니다. 새 값은 6부터 뒤에 붙입니다.
- **경매 등록 기간**: `AuctionRules.ListingDuration`이 상수 하나에서 나오는 정적 값입니다. 유저마다 다르게 하려면 등록 경로에 값을 실어야 합니다.
- **소모품 처리 없음**: 망각의 묘약 `100046` · 신속/풍요의 영약 · 은빛 나침반 · 보물 지도는 서버 코드 검색 결과 사용 처리가 0건입니다.



## 4. MisterMind 검토 메모

- **이번 회차의 성격:** 수동 실행으로 들어온 특별 회차 지시(T-113 · 여러 게임 비교)에 따라 절차를 바꿨다. 바뀐 점은 셋이다.
  - 게임 1개 대신 6개를 골랐다.
  - 이미지는 게임마다 2장 이상이다.
  - Architect는 후보 표를 만들었다.
- **재시도:** 없음. ① Picker · ② Collector · ③ Architect 모두 첫 결과가 기준을 통과했다.
- **검토 확인**
  - 이미지 17장 중 스타듀 Lv5 선택 창 · 뱀파이어 서바이버즈 파워업 화면 · 팩토리오 조건 해금 3장을 직접 열어 캡션과 대조했다.
  - OSRS 위키 이미지는 단순 UA(`Mozilla/5.0`)로 요청하면 403을 준다. 실제 브라우저 UA로는 200(image/png·webp)인 것을 확인했다.
  - Fandom 이미지는 확장자와 관계없이 image/webp로 내려온다.
- **대용 이미지**
  - PoE: 현행(3.17~) 아틀라스 트리 화면은 위키·공식 사이트가 막혀 구하지 못했다. 같은 시스템의 구버전 지역 트리로 대신했다.
  - OSRS: 리그 V의 인게임 렐릭 창은 위키에 없다. 리그 VI의 같은 창으로 대신했다.
  - 그래서 그림의 노드·렐릭 이름이 표의 특성과 다르다.
- **탈락 후보 (①)**
  - 알비온 온라인 Destiny Board: 채집·거래 경제라 가장 가깝지만, 위키가 Cloudflare로 막혀 원문을 확인하지 못했다.
  - 패스 오브 엑자일 2 패시브 트리: 같은 이유로 막혔다. 콘텐츠 특화 예시도 PoE1 아틀라스가 더 풍부하다.
- **문서 간 어긋남 (③이 발견했다. 이 회차는 고치지 않았다)**
  1. [특성](../../trait/README.md) 2.1은 `UserTraitEffect`를 3종으로 적었다. 실제 enum에는 4 `AppraiseCostDiscount` · 5 `RelicChanceAdd`(아티팩트 전용)가 더 있다. [유물 감정](../../gathering/excavation/유물감정.md) 4장에만 적혀 있다.
  2. 특성 문서 상단·4장과 게임기획코어 3·5장의 "클라 ❌ 미구현"은 T-116 Closed(2026-10-03)로 이미 낡았다.
  3. 망각의 묘약: 특성 3장은 "리셋 미정", 아이템 4.4는 "확정 · 상점 10,000G"이고, 서버 사용 처리는 없다.
  4. 아이템 4.3·4.4가 적은 P4·P3의 이름과 뜻이 게임기획코어의 원칙과 다르다.
  5. 상자 상한: 진행 및 성장·게임기획코어는 "50개", 아이템 1.1은 `MaxStack` 1000이다.
  6. 분해: 캐릭터 5.6·게임기획코어 5장은 "미구현"인데, T-131 서버는 Resolve이고 아이템 4.3은 재료 쓰임을 확정했다.
  7. [자원채취](../../gathering/README.md) 2.1의 "산업 속도 +10% × 5단"과 "해금 = UnlockTable" 서술이 T-108 이후 구조와 다르다.
  8. 큐브 B: 게임기획코어 5장은 "미구현", 캐릭터 1.4는 "완료(2026-10-11)"다.
  9. 인벤토리 칸: 우편 2.4·유물감정 3장은 "200", 지금 데이터는 `StorageCapacity` 탭마다 50이다.
- **남은 부족함**
  - PoE 현행 키스톤(Crop Rotation 등)과 OSRS Power Miner의 실제 동작 장면은 이미지로 보여 주지 못했다.
  - 쿠키 클리커 조건부 업그레이드(Lucky Digit 등) 화면도 없다.

## 출처

**인기 근거**
- Steam appdetails(appid 238960 · 413150 · 1454400 · 1794680 · 427520)
- [wnhub — PoE 동시접속](https://wnhub.io/news/marketing/item-46458)
- [PocketGamer.biz — 스타듀 4,100만 장](https://www.pocketgamer.biz/stardew-valley-sells-over-41-million-copies)
- [VGC — BAFTA 2023](https://videogameschronicle.com/news/bafta-game-awards-winners)
- [everythingedinburgh — OSRS 기록](https://everythingedinburgh.com/games/news/old-school-runescape-subscriber-records-jagex/)
- [Jagex — Leagues V 공지](https://www.jagex.com/news/old-school-runescape-s-action-packed-competitive-mode-returns-with-leagues-v-raging-echoes)
- [PC Gamer — 팩토리오 350만 장](https://pcgamer.com/factorio-has-sold-35-million-copies)
- [GamingOnLinux — Space Age 40만 장](https://gamingonlinux.com/2024/10/factorio-space-age-sold-over-400000-copies-and-sets-a-new-player-count-record)

**시스템 원문**
- PoE: [atlastree-export](https://raw.githubusercontent.com/grindinggear/atlastree-export/master/data.json) · [maxroll — Atlas passive tree](https://maxroll.gg/poe/getting-started/atlas-passive-tree) · [Fandom — Passive skill](https://pathofexile.fandom.com/wiki/Passive_skill) · [Fandom — Atlas passive skill](https://pathofexile.fandom.com/wiki/Atlas_passive_skill)
- 스타듀 밸리: [Skills](https://stardewvalleywiki.com/Skills) · [The Sewers](https://stardewvalleywiki.com/The_Sewers) · [Mastery Cave](https://stardewvalleywiki.com/Mastery_Cave)
- 쿠키 클리커: [Ascension](https://cookieclicker.fandom.com/wiki/Ascension)
- 뱀파이어 서바이버즈: [PowerUps](https://vampire.survivors.wiki/w/PowerUps)
- OSRS: [Raging Echoes League/Relics](https://oldschool.runescape.wiki/w/Raging_Echoes_League/Relics) · [Demonic Pacts League](https://oldschool.runescape.wiki/w/Demonic_Pacts_League)
- 팩토리오: [Research](https://wiki.factorio.com/Research) · [Technologies](https://wiki.factorio.com/Technologies) · [Mining productivity](https://wiki.factorio.com/Mining_productivity_(research)) · [Friday Facts #376](https://factorio.com/blog/post/fff-376)

**이미지 출처 페이지**
- [arrpeegeez — 스타듀 기술](https://www.arrpeegeez.com/2016/02/stardew-valley-walkthrough-guide-skills.html)
- [A Little Bit Human — Rancher or Tiller](https://alittlebithuman.com/rancher-or-tiller-stardew-valley-farming-skill-guide/)
- Steam 상점(238960 · 1454400)

**우리 문서**
- [T-113](../../../../tasks/T-113-특성다양화.md) · [T-114](../../../../tasks/T-114-레이드기획.md) · [T-169](../../../../tasks/T-169-중복개체수용재검토.md) · [T-131](../../../../tasks/T-131-장비분해.md)
- [특성](../../trait/README.md) · [캐릭터](../../character/README.md) · [아이템](../../item/README.md) · [거래](../../trade/README.md) · [우편](../../mail/README.md) · [건물](../../building/README.md) · [퀘스트](../../quest/README.md) · [작업슬롯](../../workslot/README.md) · [자원채취](../../gathering/README.md) · [산업 레벨](../../gathering/산업레벨.md) · [유물 감정](../../gathering/excavation/유물감정.md) · [진행 및 성장](../../progression/README.md) · [게임 UI](../../ui/README.md) · [게임기획코어](../../게임기획코어.md)

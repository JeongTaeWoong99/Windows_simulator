---
date: 2026-10-25
title: 이슈 정리 — DB 잠김 확인 · [합치기]/[창고에 쌓기] 버튼 · 판매/분해 목업 · 사이트 목업 탭
tags: [client, ui, web, docs]
---

# 이슈 정리 — #85 · #82 · #83 · #81

## 목적 / 배경
- 사용자 요청: 공이 돌아온 이슈 85 · 83 · 82 · 81을 처리. 83은 회의 결과를 반영해 목업부터, 사이트에 목업 바로 보기 추가.

## 변경 내용
- #85(T-180) — 코드 변경 없음. 재현 확인 후 보관 · 이슈 닫음.
- #82(T-175) — `InventoryGridPresenter.MergeCurrent` · `StackIntoWarehouse` · `PlayerDataModel.CanMergeItemSlots` · `CanStackIntoWarehouse` · 도구 줄 `Merge Button`(70) · 창고 정렬 줄 `Stack Button`(110) · `Loc_UI.xlsx` 7키. 보관 · 이슈 닫음.
- #83 — `GameDesign/design/ui/판매분해목업.html`(구현 전 사용자 검토 대기).
- #81 — 서버 T-181 커밋 `eb8b40a8`이 원격에 없어 푸시 요청 · 공을 서버로.
- 사이트 — `/mockups`(`src/lib/mockups.ts` · `pages/mockups/*`) · 배포 체크아웃 `fetch-depth: 0` + `filter: blob:none` · v0.7.0.

## 주요 결정 / 근거
- 합치기·쌓기는 **할 것이 없으면 요청하지 않고 토스트** — 서버가 빈 목록 + Ok를 돌려줘 눌러도 무반응처럼 보인다. 판정은 서버 규칙(앞 칸부터 채움 · 창고에 이미 있는 자원만)과 같게.
- 요청은 격자가 보낸다 — 칸 요청 대기(`s_slotWaitHandle`)가 격자끼리 하나라 정렬·끌기와 겹치지 않게.
- 목업 연결 문서는 파일 이름 검색(그대로 + percent-encoding) — `research/`는 언급이 많아 뺐다.

## 확인 방법 (다음에도 쓸 것)
- 로그인 화면은 `LoginPresenter.idInput`에 값을 넣고 `OnLoginButtonClicked`를 리플렉션으로 부른다. 패킷은 `MikaNetwork.NetworkManager.Instance.Send(new MikaProtocol.…)` — eval에는 using이 없어 전체 이름이 필요하다.
- 기본 계정(1234)은 캐릭터·장비 탭이 50/50으로 가득 차 뽑기가 거절된다(응답 15B) — 새 계정(`t180`)을 쓴다.
- `unity cmd eval --code`는 `' '` 같은 인용이 섞이면 인수 파싱이 깨진다 — `eval_file`로 돌린다.
- 투명 창이라 `screenshot`/`capture_game_view`는 검게 나온다 — 배치는 `RectTransform.rect` 수치로 확인했다.

## 후속 작업 / 주의사항
- #83 구현은 목업 승인 뒤: `SellCartModel`을 한 종류만 담게 · 탭 전환 시 비움 · 장비 두 버튼. 장비 "분해만" 결론이면 서버·엑셀 먼저(판매 거절) → 이슈로 요청.
- 도구 줄 1줄은 여유가 3px뿐이다(spacer 최소 20) — 버튼을 더 넣으면 줄이 넘친다.
- 씬 diff에 `Coin Fly Presenter` 블록 위치 이동이 섞였다(내용 같음 — Unity 재직렬화).

## 추가 (같은 날)
- 자원 툴팁 '보유' 줄에 칸 상한 — `60 / 9,999 개`(`UI.Inventory.StackOfMax`). 상한은 아이템마다 다르다(1,000 · 9,999). Unity가 플레이 중이라 컴파일·화면 확인은 플레이 종료 뒤로 미뤘다.
- #83 목업 — 자원·캐릭터도 장비와 같은 버튼 모양([골드로 판매 · 총 N 골드 획득]), 장비 줄은 `360 G OR 파편 5`.

## 추가 2 — #83 구현 (`02560dc5` · `e1275e11`)
- 판매 목록 한 종류만(`SellCartModel.ClearOtherKinds` · `Kind`) · 인벤토리 탭을 다른 종류로 바꾸면 비우고 토스트(특성 탭은 유지).
- 씬: `Summary Panel` → `Head Panel`을 맨 위로(종류 · 경고 · [비우기]) · `Total Text` → `Kind Text`(`FormerlySerializedAs("totalText")`) · `Button Row`에 `Sell Button` + 복제 `Disassemble Button`(테마 `Button`, OnClick 영속 호출 비움) · 버튼 글자의 `LocalizedText` 제거(코드가 씀).
- 줄 프리팹 `Price Text` 130 → 200 — `100 G OR 파편 1`이 130에서 19pt로 줄었다.
- 분해 응답: 서버는 장비 삭제를 따로 보내지 않는다 → `PlayerDataModel.BeginDisassemble`에 보낸 목록을 적고 Ok면 지운다(개체 판매와 같은 모양).
- 확인: `editor_play`(eval로 `isPlaying = true`는 메인 스레드 5초 초과로 실패) → `ServerRunner.Start` 리플렉션 → 담기·탭 전환·분해(50→47 · 파편 +3)·판매(+10 G).
- T-173은 이 안으로 대체해 닫음. #83은 진우에게 — 두 방식이면 닫기 / 분해만이면 엑셀·패킷 후 요청.

## 추가 3 — 마석 · 토스트 가운데 · T-182 (`13f6a4cd` · `a822f799` · `ddf75e9f` · Art `6a27715`)
- 마석: 아이템이 없던 게 아니라 분해 재료 100050 '마력의 파편'이 자원 탭에 이미 있었다(1234 계정 30번 칸 · 첫 화면 아래). 아이콘이 없어 대체 그림(빈 자루)이라 못 알아봤다. 엑셀 누락·클라 필터 문제 아님 → 이름을 '마석'으로(Item.xlsx · Loc_Item · `UI.Sell.ShardShort`) + 아이콘 Crystal.png.
- 아이콘: `icon_recipe.asset`에 tid 100050 추가 → 굽기 → **`ArtCatalogSync.Sync()`까지 해야** `VisualCatalog`에 들어간다(레시피 하나만 `Bake`하면 목록이 안 바뀐다). eval 직접 `BakeAll`은 메인 스레드 5초 초과로 실패 — `delayCall`로 돌렸다.
- 토스트: 공용 하나(`ToastPresenter` · 씬 `!Overlay Canvas/Toast Presenter/Panel`). 앵커·피벗 (0.5,0)→(0.5,0.5), 위치 (0,90)→(0,0). `_restY`가 씬 값을 읽어 코드 변경 없음. 에디터 저장은 레이아웃 값 1,378줄을 재직렬화해 HEAD 사본에 4줄만 고쳐 넣었다.
- 겹침: 토스트(Overlay 형제 8)는 뽑기 결과 창·수량 입력·확인 창(2~4) 위에 그려져 그 가운데를 가린다 · 알림 팝업(9)·툴팁(10)에는 가려진다 · 로딩 문구(7)와 자리가 같다. 클릭은 막지 않는다.
- 확인(플레이 · t180): 자원 탭 칸 `item_100050` · '마석' · '3 개' · 툴팁 `3 / 9,999 개` · 토스트 중심 (960, 540) / 1920×1080.
- T-182 등록(열 '창구' · 화면 '마을' 추천 · 이름 확정 전 문서·코드 손대지 않음). 서버 T-181이 머지로 들어와 #81 → 클라(태웅), T-151 ⏸ 해제.

## 추가 4 — 아티팩트 탭 철회 · T-182 이름 보류 (`8039113f` · 이슈 #86)
- 인벤토리 = 자원·캐릭터·장비(서버·기획 합의). 문서의 "인벤토리 아티팩트 탭" → **"아티팩트 칸"**(서버 저장 `EStorageTab.Artifact`·`t_user_artifact_stack`은 그대로). 화면 자리는 T-182.
- 서버 코드 주석의 같은 표기는 손대지 않고 #86에 위치만 적었다(서버 영역).
- T-182: 사용자가 '창구'·'마을'도 애매하다 → 추천을 거두고 "왜 어려운가" 표(기능이 섞인 열 · 콘셉트 · 건물과 겹침 · 배경 미정 · 폭 · 열 확장)와 후보 기록만. 본문은 가칭 '새 열'·'지도 화면'.

## 추가 5 — 토스트를 난 열의 가운데에
- `ToastPresenter.columns`(인벤토리·메인·거점 열) — 출처: 흔들 대상 → `ButtonPressFx.RecentlyPressed`(0.5초) → `WindowManager.UIHitsUnderCursor[0]`. 출처를 품은 열의 가운데 · 폭은 열 안. 없으면 씬 자리(화면 가운데).
- 열과 토스트는 다른 캔버스 — 열 가운데를 화면 좌표로 바꿔 토스트 부모 좌표로(`RectTransformUtility`).
- 확인(플레이): 열마다 토스트 중심 = 열 중심(960 · 317 · 1603, 폭 633 안) · 출처 없음 → (0,0).
- 씬 저장은 레이아웃 값 1,378줄 재직렬화 → HEAD 사본에 `columns` 4줄만 넣었다.

## 추가 6 — T-151 화면 위치와 무관한 부분
- 유물은 이미 `IsBox`(OpenGachaId)라 좌클릭 = 감정이 돌고 있었다. 빠진 것: 비용 표시·골드 막기·결과 창 아티팩트·패킷 핸들러.
- 핸들러 5개 + `PlayerDataModel.ArtifactCodex/ArtifactStacks/ArtifactsChanged/GetAppraiseCost`(서버 식 사본, 상한 500‰는 서버 상수라 사본).
- `ItemUseCompleted`에 아이템 TID를 실었다 — 결과 창 제목 '감정 결과'.
- Special = 흰색(목업 `c: '#FFFFFF'`) · 이름 '스페셜'(Loc 신규).
- 확인(플레이 · t180 · 치트로 유물 지급): 흙 묻은 유물 20개 감정 → 골드 −4,000 +보상 · 아티팩트 3종 칸에 · 결과 창 이름 · 무지개 유물 감정 '감정 결과' · 골드 1,000에서 1,800짜리 → 거절 토스트. 시험으로 바꾼 골드는 되돌렸다(+15만 차이).
- 남은 것: 아티팩트 칸·도감 화면(T-182 자리) · 장착/켜기/판매 UI · 경매 등록 · 감정 연출 · 🎨 아이콘·Special 칸 애니메이션.

## 추가 7 — 아티팩트 화면 1차 (T-151 · #81)
- 사용자: 상세의 [즉시 판매]는 별로 — 좋은 건 경매로 비싸게. → 판매는 보유 칸(우클릭 판매 목록 · Ctrl+우클릭 경매 등록), 상세는 [도감에 장착]·[효과 끄기·켜기]만. 점 알림 없음.
- `#Artifact Canvas`(오버레이 [1] · 경매장 복제) · `ArtifactModel`(고름·판매 목록) · Presenter 5개(머리·도감·보유·상세·판매) · 메인 `Artifact Button (4)`.
- 경매 등록 창에 `RegisterKind.Artifact` · 시세 [팔러 가기] 잠금 해제 · `AuctionModel.RegisterArtifact`. `AskAmount(string subject, …)` 오버로드.
- 확인(플레이 · t180): 장착 · 끄기 · 판매(NEW 경고) · Ctrl+우클릭 → 등록 Ok. t180 상태가 바뀌었다(2번 장착·꺼짐 · 11번 판매 · 12번 매물).
- ⚠️ 씬 저장 때 원본 블록 5개(색·앵커·폰트 크기)가 덩달아 바뀌어 HEAD 블록으로 되돌렸다. 디스크 씬을 고친 뒤 Refresh+OpenScene은 에디터를 "다시 불러올까" 모달로 멈춘다 — 사람이 [Reload].
- 남은 것: 사용자 확인 후 조작·위치 다듬기 · 감정 연출 · 🎨 아이콘.

## 추가 8 — 아티팩트를 장비 탭으로 · 버그 2건 (T-151 · #81)
- 사용자 흐름(서버 기획 동의): 감정 → 장비 탭 · 우클릭/Ctrl+우는 장비와 같고 좌클릭만 아티팩트 창 → 장착하면 도감이 열려 그 칸이 반짝임 · 이미 장착한 번호는 불가 · 결과 창에 바로가기 없음. 서버는 1안(클라 먼저).
- 클라: `EquipSlotSource.FillArtifacts`(장비 뒤 · 키 = -번호) · `Inventory/ArtifactEquipPresenter`(응축 창 복제 · 판매 목록 자리) · `SellCartModel`에 아티팩트(장비 종류로 셈 · 섞이면 분해 잠금) · `ArtifactModel`은 고름 + `Highlight`만 · 도감 반짝임(`HighlightAsync`) · 아티팩트 화면의 보유·판매 Presenter 삭제 · 장비 탭 [제외 ▾] '아티팩트'(기본 체크 · NEW는 늘 뺀다). 잠금·끌기는 거절 토스트.
- 버그: 메인 [아티팩트] 버튼 `interactable` false였다 → 씬 true. 분해 토스트 '?#0' — 보낼 때 마석 TID를 저장하지 않았다 → `_shardItemId`.
- Loc_UI 10키(`UI.Artifact.Window` 등 · `UI.Tool.UncollectedN`).
- 확인(플레이 · 새 계정 t190 — t180은 창고가 가득 차 감정 결과가 우편으로 가고 수령도 `StorageFull`): 장비 탭 NEW 표시 · 좌클릭 창 · 장착 → 도감 반짝 · 우클릭 수량 팝업 · 판매 Ok · 분해 토스트 '마석 +1' · 일괄 담기 제외 켬/끔 알림 · Ctrl+우 등록 창.
- ⚠️ 응축 창에서 복제한 이름 글자(22)가 칸 높이 26에서 말줄임으로 통째로 숨었다(글자 0개) → 20. 씬 저장의 무관 블록은 HEAD로 되돌렸고, 저장 직후 `OpenScene`으로 다시 불러 모달을 피했다(씬이 dirty가 아니면 묻지 않는다).
- 남은 것: 서버 이슈 #87(칸 합치기 · 잠금 · 마석 분해) · 감정 연출 · 🎨 아이콘.

## 추가 9 — 유물 감정 연출 (T-151)
- 목업 타임라인대로 `Overlay/GachaResultPresenter/RelicAppraiseFx`(코드로 만든 층) — 흔들림 → 금 3갈래(Filled) → 번쩍 → 조각 12 → 광선 · 대표 보상 등장 → 아티팩트/Special 유물이면 띠 → 0.8초 머문 뒤 창이 나타나 평소 칸 공개. 감정(`OpenCostGold>0`)만 · 상자·가챠·우편은 그대로.
- Presenter: 무대 동안 Panel `CanvasGroup` 투명·레이캐스트 끔 · 닫기 잠금 · 클릭 = 건너뛰기(토큰 취소 · `_isAppraising`은 남겨 칸 공개로 넘어간다) · 닫기/새 결과/OnDisable은 `CancelAppraise`.
- 그림 `Art/fx/appraise/` 4장(`make_fx.py appraise` · Art 저장소) · Loc_UI 2키(`UI.Gacha.ArtifactFound` · `SpecialRelicFound`).
- ⚠️ `GetComponent<CanvasGroup>() ?? AddComponent` — Unity 가짜 null을 `??`가 못 걸러 CanvasGroup이 안 붙고 결과 창이 아예 안 떴다 → `TryGetComponent`.
- 확인(플레이 · t190): 화면 안 녹화 스크립트(`UniTask.Delay(220)`마다 `CaptureScreenshot`)로 단계별 확인 · 아티팩트 띠 · 클릭 건너뛰기. eval 한 번이 ~2.7초라 바깥에서 찍으면 무대가 이미 끝나 있다.
- 남은 것: 창고 가득 → 우편 알림이 무대 위에 같이 뜬다(그대로 둠) · 🎨 아티팩트 아이콘(대표 보상 칸이 비어 보인다).

## 추가 10 — 아티팩트 탭 겹침 · 1개씩 감정 이슈 #88 (T-151)
- 아티팩트 화면 [전체]·[장착] 버튼 LayoutElement 높이 54가 Filter Row(40)를 넘어 아래 등급 줄을 덮었다 → 32 · 줄 정렬 MiddleLeft. 씬 저장 노이즈 5블록은 HEAD로 되돌렸다.
- 사용자 제안: 유물은 1개씩 감정 → 연출 → 나온 1개만(목록 없음). 기획서는 1~50개 + 목록이라 기획·서버 확인 이슈 #88(아티팩트 확률 약 5% · 지급 치트 선택 포함).
- "아티팩트가 안 나온다" — 도감의 '획득 n/12'는 장착 기준이라 감정으로 받은 것은 장비 탭에만 있다. t190은 9종 보유.

## 추가 11 — 아티팩트 다듬기 · 이슈 #89 · #90 (T-151)
- 장비 탭 아티팩트 칸의 'NEW' 글자(`EquipSlotSource.GetBadge`) 삭제 — 새로 들어온 표시는 장비와 같은 획득 반짝임(`AmountOf`)뿐. 판매 목록 줄의 NEW 꼬리는 '도감에 없는 번호' 경고라 남겼다.
- 판매 목록: 아티팩트 줄도 'N G OR 마석 0' · [마석으로 분해]를 장비 탭이면 보인다(아티팩트가 섞이면 잠김 · 안내 줄). 아티팩트만 담으면 재료 TID가 비어 일반 등급 분해 행에서 이름만 빌린다(`SellCartModel`).
- 도감 상세 [효과 끄기] 뺌 — 꺼 둔 번호만 [효과 켜기]가 남는다(되살릴 길).
- 이슈: #89 분해 마석 엑셀·서버(#87의 마석 항목을 옮김 — #87 본문 취소선) · #90 끄기 패킷 제거·꺼진 번호 마이그레이션. 플레이로 판매 목록 · 상세 · 칸 표시 확인.
- ⚠️ 플레이 직후 eval이 'Services에 없다' · `Loc.Get` NRE로 실패한 적 — 데이터 로드 전에 들어간 플레이였다. 멈추고 다시 플레이하니 정상.

## 추가 12 — 마무리 점검 (T-151)
- 사용자 확인 끝. #88 본문에 '의도' 표(상자 = 50개 한꺼번에 자원 보너스 · 유물 = 1개씩 도감 채우기 · 더 큰 기대) 보강.
- T-151: 막고 있는 것에 ⏸ #87~#90 · 정한 것 4줄(NEW 글자 없음 · 끄기 없음 · 분해 마석 · 상자/유물 역할) · 관련 커밋 · 옛 '아티팩트 탭 없음' 줄 정정. INDEX 행 갱신. 남은 것: 🎨 아이콘 · Special 칸 · 거래소 아티팩트 탭 · 서버 답.
- 정합성 검사 9건은 전부 연구 문서의 T-145 옛 경로 — 이번 작업과 무관.

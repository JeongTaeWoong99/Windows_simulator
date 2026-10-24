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

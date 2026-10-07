---
id: T-133
제목: 클라 — 목록 줄 풀을 UIRowList로
담당: 태웅
상태: New
우선순위: 낮음
목표: 261024
---

# T-133 클라 — 목록 줄 풀을 UIRowList로

## 배경

화면마다 "늘리기만 하고 남는 줄은 끄는" 줄 풀 코드가 복제돼 있다. [T-132](archive/T-132-풀링과UniTask전환.md)에서
`Common/object-pool/UIRowList<T>`를 만들고 경매 줄 목록만 옮겼다. 나머지도 같은 것으로 모아 복제를 없앤다.
동작은 그대로인 리팩토링이다 — 치트 해당 없음.

## 할 일

- [ ] `SellCartPresenter` · `MailPresenter` · `TraitPresenter` · `TooltipPresenter` · `GachaResultPresenter`의 줄 풀을 `UIRowList`로
- [ ] `WorkStationSelectPresenter` — 캐릭터·효율·장비 줄
- [ ] 실측 — 목록이 늘고 줄 때 옛 내용이 비치지 않나 · 줄 클릭이 중복으로 불리지 않나

## 완료 조건

위 화면에 직접 쓴 줄 풀(`List<RowView>` + `Instantiate` 루프)이 없다.

## 막고 있는 것 / 선행 일감

- T-103 실측 — 같은 RowView를 만진다

## 관련 커밋

- 없음

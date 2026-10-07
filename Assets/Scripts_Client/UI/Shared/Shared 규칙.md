# Shared 폴더 규칙

> 최종 업데이트: 2026-10-08 (`ItemGainEffectView` 따라가기 — 땅이 흐르면 대상과 함께) · 2026-10-08 (`ItemGainEffectView` 추가 — 아이템 획득 연출 · `EquipPipsView.SetTooltip`) · 2026-10-07 (`EquipPipsView` 추가 — 캐릭터가 낀 장비 4칸을 네모로 · 인벤토리 캐릭터 칸·작업슬롯 칸 · `EquipLabel`에 장착 읽기 · `RarityPalette.EmptySocket` · T-104) · 2026-10-05 (`VisualCatalog.Current` — 인벤토리 상반신·위젯 머리·자원·장비 아이콘을 목록에서 찾는다 · T-097) · 2026-10-04 (`visual/` 추가 — 슬롯 연출 그림의 런타임 SO · 슬롯 무대 `SlotStageView`·전체 세팅 `SlotStageSettings` · T-097) · 2026-10-03 (`EntityBlockText` 추가 — 팔거나 올릴 수 없는 개체의 사유를 판매·경매 등록이 함께 쓴다 · T-075 · T-096) · 2026-10-01 (`ItemIconView`·`ItemIconContent` 추가 — 목록 줄 아이콘 칸 · `UIRichText` · `EquipLabel`에 능력치 칸 읽기·장비 툴팁 — T-103) · 2026-09-26 (`theme/` 추가 — 아트 전 임시 색을 팔레트 한 곳에서 맞춘다) · 2026-09-26 (`RarityLabel` 추가 — 등급 이름을 도구 줄과 칸 툴팁이 함께 쓴다 · T-050) · 2026-09-25 (`TooltipTrigger`·`TooltipContent` 추가 — 어느 캔버스의 버튼이든 툴팁을 단다 · T-088) · 대상: `Assets/Scripts_Client/UI/Shared/`

**여기는 캔버스가 아니다.** `UI/` 아래의 다른 폴더는 전부 하이어라키의 캔버스 하나를 비추지만
(`#Inventory Canvas` → `Inventory/`), 이 폴더에는 대응하는 오브젝트가 **없다.**

## 왜 이 폴더가 있나

`UI/`의 폴더 규칙은 **`<캔버스>/<Presenter>/`** — 폴더가 하이어라키의 거울이다
([`UI 규칙.md`](<../UI 규칙.md>) §5). 그런데 **씬·프리팹에 붙지 않는 파일에는 비칠 대상이 없다.**
자리가 정해져 있지 않으면 결국 *"처음 쓴 화면"* 의 폴더에 놓이고, 그 순간 **위치가 소유권을
거짓으로 주장한다** — 다른 화면이 그걸 참조하는데도.

| 예전 | 무엇이 거짓이었나 |
|------|------------------|
| `Inventory/InventoryGridPresenter/InventorySlotView.cs` | 인벤토리 소유처럼 보이지만 **가챠 결과 팝업이 같은 클래스·같은 프리팹을 쓴다.** 인벤토리를 고치다 팝업을 조용히 바꾸게 된다. **이름의 `Inventory`도 거짓이었다** → `SlotView`로 개명 |
| `System/RarityPalette.cs` 등 4개 | `#System Canvas`와 **무관**한데 그 폴더에 있었다. `System 규칙.md`가 *"어디에도 속하지 않는 것들"* 이라 적어 두고도 자리를 못 만들어 준 상태였다 |

**소유자가 여럿이면 자리도 공용이어야 한다.** 그게 이 폴더다.

## 여기 있는 것

| 파일 | 하는 일 | 누가 쓰나 |
|------|---------|-----------|
| `SlotView.cs` | **칸 하나** — 완성된 값을 받아 그린다 (종속 View) | `#Inventory Canvas`의 격자 · `!System Canvas`의 가챠 결과 |
| `SlotData.cs` | 칸에 넘기는 **완성값 struct** | 위 칸을 채우는 모두 |
| `ItemIconView.cs` + `Assets/Prefabs/ItemIconView.prefab` | **목록 줄 왼쪽의 작은 아이콘 칸** — 등급 바탕 · 이름 첫 글자(🎨 아이콘 전 임시) · 모서리 수량 · 능력치 칸. 줄에서는 `SquareLayoutElement`로 줄 높이만큼 정사각형 | Market(경매장 줄) · Main(우편 · 장비 고르기) · Inventory(판매 목록) |
| `EquipPipsView.cs` + 프리팹 둘 | **캐릭터가 낀 장비 4칸** — 빈 칸은 어두운 네모 · 낀 칸은 장비 등급색. **4자리 고정**(무기·장신구1·장신구2·보석). `EquipPipsView.prefab` = 색 네모만(능력치 칸과 같은 11px · 14 간격) · `EquipIconsView.prefab` = 등급 바탕 + 장비 아이콘(줄 높이 정사각형). 딤·흑백은 `SlotView.TintColor` 하나로 | Inventory(캐릭터 칸 — 색 네모) · Main(작업슬롯 칸 — 아이콘) — 슬롯 설정 캐릭터 카드에는 두지 않는다(장비 칸이 바로 보인다) |
| `ItemIconContent.cs` | 아이콘 칸의 완성값 + 팩토리(`ForItem`·`ForEquip`·`ForCharacter`·`ForGold`) — 이름·등급 조회를 한 곳에 | 위 아이콘을 채우는 모두 |
| `ResultMessages.cs` | 결과 코드 → 사용자 문구 | Login · Main · Market · Inventory + `PlayerDataModel` |
| `RarityPalette.cs` | 등급 → 표시 색 · 빈 네모 색(`EmptySocket` — 능력치 칸·장착 칸이 함께 쓴다) | Inventory · System |
| `RarityLabel.cs` | 등급 → 한글 이름. ⚠️ **출처가 아니라 사본이다** — `IndustryLabel`과 함께 [`T-085`](../../../../tasks/T-085-공용상수소유권.md)가 끝나면 지운다 | Inventory(도구 줄 드롭다운 · 칸 툴팁) |
| `WorkStationProgress.cs` | 슬롯 스냅샷 → 진행도·남은 초·실효 주기. **복사하면 서버 판정식이 두 벌이 된다** | Main(큰 창) · Widget(상주 위젯) |
| `AptitudeLabel.cs` | 적성 값 → 표기(`0`은 `X`)·색 | Inventory(칸 스트립) · Main(캐릭터 줄·카드) |
| `IndustryLabel.cs` | 산업 → 한글 이름. ⚠️ **출처가 아니라 사본이다** — [`T-085`](../../../../tasks/T-085-공용상수소유권.md)이 끝나면 **이 파일은 지운다** | Main |
| `EquipLabel.cs` | 장비 → 효과 한 줄(`낚시 +30%`·`전산업 +5%`)·칸 이름·칸에 낄 수 있는 종류 · **능력치 칸 읽기**(`ReadStatOptions` — ⏸ 옛 인챈트 필드, 새 패킷이 오면 여기만) · **장비 개체 툴팁**(`BuildTooltip`) · **캐릭터의 장착 읽기**(`WornSlots` 순서 · `ReadWornGrades` · 툴팁 `AddWornRows` — T-104). ⚠️ **거르기는 표시용이고 거절은 서버가 한다** | Inventory(장비 탭) · Main(작업슬롯 장비 칸 · 장비 고르기) · Market(경매장) |
| `EntityBlockText.cs` | 캐릭터·장비 개체 → **팔거나 올릴 수 없는 사유** 한 줄(끼고 있음 · 배치 중 · 장비 낌 · 마지막 캐릭터), 없으면 null. ⚠️ **표시용이고 거절은 서버가 한다** | Inventory(즉시 판매 담기) · Market(경매 등록 흐린 줄) |
| `ItemGainEffectView.cs` | **아이템 획득 연출** — 넘겨받은 월드 위치에서 아이콘이 나타나 떠오르며 사라진다. 여러 개면 좌우 대칭(간격 = 크기 + 빈틈). DOTween `Sequence` + UniTask(`KillAndCancelAwait`) · 아이콘은 `PrefabPool`. `follow`를 넘기면 매 프레임 가로 위치를 그 자리에 맞춘다 — 큰 창 칸은 `SlotStageView.FollowGround`로 땅과 함께 뒤로 흐른다. 꺼지거나 파괴되면 끊긴다. 채취 푸시 → 아이콘 읽기(`ReadGainIcons`)도 여기. ⚠️ 칸 **루트**에 붙인다 — 무대는 `RectMask2D`라 잘린다 | Main(작업슬롯 칸 — 쓰러지는 대상 자리) · Widget(미니 칸 — 머리 자리, 작게) |
| `TooltipTrigger.cs` | 툴팁을 띄울 대상에 붙는다 — 고정 문구(인스펙터) 또는 Presenter가 넘긴 내용 함수. 띄우는 일은 `!System Canvas`의 `TooltipPresenter` | State · Inventory · Main (버튼마다) · Inventory 격자(칸마다 — 코드가 붙인다, T-050) |
| `TooltipContent.cs` | 툴팁 한 장의 내용 — 제목 + 줄(라벨 · 값 · 보조 값 · 바탕색) | 위 트리거에 내용을 넘기는 모두 |
| `theme/UIRichText.cs` | 한 줄 안에서 **라벨은 흐린 색 · 가격은 강조색**으로 가르는 TMP 리치 텍스트 조각. 색은 팔레트 역할에서 읽는다 | Market · Main(우편) |
| `visual/` (`VisualCatalog`·`CharacterVisual`·`BackgroundVisual`·`TargetVisual`) | **연출 그림** — 캐릭터 프레임·크롭 · 패럴랙스 층 · 대상과 레벨 색. 빠진 그림은 대체로 버틴다(캐릭터 = 대체 그림, 아이콘 = 0번). 화면은 `VisualCatalog.Current`(Resources)로 찾고, 목록이 없는 PC면 자리 표시를 그린다. 굽는 쪽은 [`Art 규칙.md`](<../../../Art/Art 규칙.md>) | Main(큰 창 슬롯) · Widget(머리) · Inventory·가챠·목록 줄(상반신·아이콘) |
| `visual/` (`SlotStageView`·`SlotStageTimeline`·`SlotStageSettings`·`SlotStagePreview`) | **슬롯 무대** — 판정 주기 안의 순간을 그린다(배경만 누적). 시간·거리는 전체 세팅 `Resources/SlotStageSettings`, 캐릭터별 값은 `CharacterVisual`의 개인 조정 칸 — 플레이 중 고쳐도 바로 보인다. 미리보기는 서버 없이 돌려 볼 때 | Main(큰 창 슬롯 `Visible Panel`) |
| `theme/` (`UIThemeRole`·`UIThemePalette`·`UIThemeColor`) | 🎨 **아트 전 임시 색** — 역할 → 색 표 하나. 아래 "임시 테마" 절 | 모든 캔버스·프리팹 |

> ⚠️ **툴팁 두 파일은 게임을 모른다** — 원래 자리는 `Common/`이다(아래 "들어올 수 없는 것").
> 쓰는 곳이 늘며 모양이 굳을 때까지 여기서 다듬고, 굳으면 툴킷으로 올린다(마스터 반영은 확인을 받는다).

## 임시 테마 — `theme/`

아트가 없는 동안 색을 **역할**(`PanelBg`·`Button`·`TextMain`…)로만 적고, 색 값은
`Assets/Resources/UIThemePalette.asset` 하나가 정한다. 톤은 Palworld식 다크(짙은 남색 패널 · 흰 글씨 · 하늘 강조 · 주황 선택).

| 색이 | 누가 칠하나 |
|---|---|
| 고정이다 (패널·글씨·일반 버튼) | Image·TMP에 붙은 **`UIThemeColor` 태그** — 에디터에서 씬·프리팹에 **구워 둔다**. 실행 중엔 아무것도 안 한다 |
| 상태로 바뀐다 (고른 탭·찍은 노드·흐린 글씨) | 그 Presenter가 **`UIThemePalette.Of(역할)`** 로 칠한다. 색 필드 대신 `UIThemeRole` 필드를 둔다 |

- 팔레트 값을 고치면 열린 씬은 바로 따라온다. **프리팹은 메뉴 `Window/DesktopWindowControl/UI 테마/적용`** 을 눌러야 한다.
- ⚠️ **버튼 바탕은 흰색이고 색은 `ColorBlock`에 있다** — 틴트가 `Image.color`에 곱해지므로 바탕에 색을 넣으면 탁해진다.
- 새 화면은 Image·TMP에 태그를 붙이고 역할만 고르면 된다. 자동 태그 메뉴는 처음 한 번 쓴 도구다(색을 보고 역할을 짐작).
- 등급색(`RarityPalette`)은 게임 규칙의 의미라 팔레트에 넣지 않는다.
- 🎨 **아트가 들어오면 태그를 걷고 이 폴더를 지운다.**

## 들어올 자격

**둘을 다 만족해야 한다.**

1. **캔버스 하나에 속하지 않는다** — 두 캔버스 이상이 쓰거나, 애초에 화면과 무관한 변환이다.
2. **화면 표현에 쓰인다** — 그리는 값·색·문구·칸. 상태를 들고 있지 않는다.

## 들어올 수 없는 것

| 무엇 | 어디로 | 왜 |
|------|--------|-----|
| **상태를 들고 있는 서비스** (모델·매니저) | `Managers/` | 여기는 표현만이다. 데이터의 주인이 UI 폴더에 살면 화면을 지울 때 주인이 함께 사라진다 |
| **게임을 모르는 범용 코드** (확장 메서드·툴) | `Common/` | 거기는 [Arca Unity Toolkit](https://github.com/JeongTaeWoong99/Arca_Unity_Toolkit)의 사본이라 **프로젝트 이름을 담지 않는다**. 여기 있는 것은 전부 이 게임의 개념(등급·산업·슬롯)을 안다 |
| **한 캔버스만 쓰는 종속 View** (`SellCartRowView`·`WorkStationSlotView`) | 그 Presenter 폴더 | 주인이 하나면 그 옆이 맞다. 여기로 올리면 **찾을 때 화면 폴더를 열었는데 없다** |
| **캔버스 껍데기·Presenter** | `<캔버스>/` | 씬에 붙는 것은 비칠 대상이 있다 |

> ⚠️ **"두 곳에서 쓰니까"만으로는 부족하다.** 한 캔버스의 두 Presenter가 함께 쓰는 것은
> 그 **캔버스 폴더**에 둔다 — 캔버스를 넘지 않으면 소유자가 아직 하나다.

## 고칠 때

- 🔴 **여기 있는 파일을 고치면 여러 화면이 동시에 바뀐다.** 인벤토리를 보며 고쳐도 가챠 결과 팝업이
  함께 움직인다 — **양쪽을 다 확인하고 커밋한다.**
- **화면마다 달라야 하는 것은 분기하지 않고 스위치로 뺀다** — 부품이 화면을 알아보면
  화면이 늘 때마다 부품을 고치게 된다. `SlotView.SetSubVisible`이 그 형태다
  (인벤토리는 보조 문구를 켜고, 가챠 결과는 끈다). 상세는 [`Inventory 규칙.md`](<../Inventory/Inventory 규칙.md>).
- **파일이 하나의 캔버스만 쓰게 줄어들면 그 폴더로 내린다.** 공용 폴더는 **지금의 사실**을
  적는 자리다 — 예전에 공유했다는 이유로 남겨 두면 다음 사람이 못 건드린다.

이름·부착·작성 규약은 [`UI 규칙.md`](<../UI 규칙.md>), 캔버스·레이아웃 함정은
[`Layout 규칙.md`](<../Layout/Layout 규칙.md>)에 있다.

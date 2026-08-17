# Data 규칙

> 최종 업데이트: 2026-08-12 · 대상: `Assets/Scripts_Client/Data/`

**엑셀에서 나온 고정 테이블을 읽는 곳.** 아이템명·캐릭터명·수치처럼 **누구에게나 같은 값**이 여기 있다.
내 계정 상태(인벤토리·슬롯)는 여기가 아니라 [`Managers/PlayerDataModel`](<../Managers/Managers 규칙.md>)이다.

---

## 1. 데이터가 오는 길

사람이 편집하는 원본은 **엑셀 하나뿐**이고, 서버·클라 양쪽 생성물이 거기서 나온다.

```
GameDesign/Excel/*.xlsx              ← 사람이 편집하는 유일한 원본
   │  GameDesign/generate-tables.ps1
   ├─ 정의(.cs)    → Server/GameData/      → Assets/Scripts_Server/GameData/   (미러)
   └─ 데이터(.bytes) → Server/Shared/Data/  → Assets/StreamingAssets/Data/     (미러)
                                                        ↑
                                              GameDataLoader 가 읽는 곳
```

- 서버와 클라가 **같은 `.bytes`** 를 읽는다. MemoryPack 와이어 포맷이 동일하다.
- 패킷은 **Id만** 보낸다. 이름을 보내지 않으므로 테이블이 없으면 화면에 숫자만 뜬다.
- ⚠️ **생성물을 직접 고치지 않는다.** 엑셀을 고치고 `generate-tables.ps1`을 돌린다.

---

## 2. 왜 씬 오브젝트가 아니라 `RuntimeInitializeOnLoadMethod`인가

테이블은 **UI가 이름을 찍는 순간 이미 있어야 한다.** 씬에 매니저로 두면 다른 컴포넌트의
`Awake`·`OnEnable`과 순서 경쟁이 생기고, 그 순서는 Unity가 보장해 주지 않는다.

`BeforeSceneLoad`는 **씬의 어떤 `Awake`보다도 먼저** 실행되므로 순서 문제 자체가 없어진다.
그래서 `GameDataLoader`는 `MonoBehaviour`가 아니라 `static class`이고, `Managers/`에도 있지 않다.

> 같은 부류의 함정이 서비스 조회다 — [`Managers 규칙.md`](<../Managers/Managers 규칙.md>) 3절.

### 왜 `UnityWebRequest`를 쓰지 않는가

StreamingAssets를 파일로 직접 읽을 수 없는 플랫폼은 Android·WebGL이다.
이 게임은 **Windows 데스크톱 전용**이라 `File`로 충분하고, 동기 로드라 순서가 단순해진다.

---

## 3. 실패를 다루는 두 가지 방식

같은 클래스 안에서 **적재**와 **조회**의 실패 처리가 다르다. 의도된 것이다.

| | 적재 (`Load`) | 조회 (`GetItemName` 등) |
|---|---|---|
| 실패하면 | **예외를 그대로 올린다** (fail-fast) | `?#Id`를 돌려주고 계속 간다 |
| 이유 | 이름이 빈 채로 게임이 도는 것보다 그 자리에서 멈추는 편이 원인을 찾기 쉽다 | 표시용이라 화면 하나 때문에 게임을 멈출 이유가 없다 |
| 대신 | 어느 폴더를 봐야 하는지·무엇이 그 폴더를 채우는지 로그로 먼저 알린다 | **처음 한 번은 경고를 남긴다** |

> 조회 경고를 1회로 제한하는 이유 — 매 프레임 갱신되는 UI에서 같은 경고가 쏟아지면
> 콘솔이 덮인다. 하지만 아예 조용하면 "이름이 안 나온다"로만 보이고
> 원인(테이블에 없는 Id가 오고 있다)이 드러나지 않는다.

---

## 4. ⚠️ TID와 개체 번호를 바꿔 넣지 않는다

**가장 자주 틀리는 지점이다.**

| 값 | 무엇인가 | 조회 함수 |
|---|---|---|
| **TID** | 캐릭터 **종류** 번호 (`CharacterTable`의 키) | `GameDataLoader.GetCharacterName(TID)` |
| **개체 번호** | 내가 가진 **그 한 장**의 PK (`t_character.character_id`) | `PlayerDataModel.GetCharacterName(개체번호)` |

같은 캐릭터를 여러 장 가질 수 있어서 TID로는 유일하게 못 찾는다.
슬롯·패킷에 실려 오는 `CharacterId`는 **개체 번호**다 — `GameDataLoader`에 그대로 넣으면 `?#2`가 나온다.

> 적성도 마찬가지다. `CharacterTable`에 적성 열이 있지만 **값의 주인은 서버**다 —
> 패킷의 `CharacterInfo.Aptitudes`를 쓴다. 자세한 건 [`패킷 레퍼런스.md`](<../패킷 레퍼런스.md>)의 「적성은 패킷에 실려 온다」.

# 2026-08-10 캐릭터 적성을 패킷으로 — 소유자를 테이블에서 서버 런타임으로 (이슈 #13)

## 배경

클라가 "이 캐릭터가 이 산업을 할 수 있는가"를 몰라서 배치 화면·창고 캐릭터 탭을 못 만들고 있었다
(GitHub 이슈 #13). 이슈는 두 갈래를 제시했다 — **적성이 TID 고정이면 클라가 테이블을 읽고 이슈를 닫고,
개체마다 다르면 패킷에 실어 달라**.

조사 결과 **적성은 지금 TID 고정이 맞다**(`Character.cs`가 `CharacterTableRow`에서 읽고,
개체는 레벨·경험치만 갖는다). 클라 쪽 미러도 이미 완비돼 있었다 —
`Assets/Scripts_Server/GameData/Tables/CharacterTable.cs`에 적성 5열이 다 있고
`StreamingAssets/Data/CharacterTable.bytes`도 있다. **즉 패킷 없이도 당장은 동작했다.**

## 그런데 테이블 읽기를 택하지 않았다

**장비 4칸(T-002)이 붙으면 적성에 보정이 올라올 여지가 있다.** 그때 클라가 테이블을 직접 읽고 있으면
배치 UI·필터·창고 탭이 한꺼번에 틀어진다. 특히 **적성 0 → 1 승격**이 생기면
클라가 *실제로는 배치 가능한 캐릭터를 숨긴다* — 화면에 안 뜨니 원인도 안 보인다.

되돌리기 비용이 비대칭이라 **지금 패킷에 실었다.** 캐릭터당 8바이트고 서버는 `GetAptitude`를 이미 갖고 있다.
사용자가 *"적성이 서버 런타임이야 확실해"* 로 소유자를 확정했다.

> 같은 원칙의 선례가 이미 있다 — `CurrentWorkSpeed`도 보정이 붙는 값이라
> 클라가 계산하지 않고 서버가 결과만 내려준다(캐릭터 기획 7장).

## 한 일

| 대상 | 내용 |
|------|------|
| `MikaProtocol/PacketEnum.cs` | **`EIndustryType` 신설** — 1차 산업 5종만. `GameData.ItemType`의 산업 구간 미러(`EGlobalRarity`와 같은 패턴) |
| `MikaProtocol/PacketInfo.cs` | `AptitudeInfo{Industry, Value}` 구조체 + `CharacterInfo.Aptitudes`(`List`). 기존 주석 *"적성 같은 고정값은 내려보내지 않는다"* 를 정정 |
| `MikaProtocol` 산업 필드 통일 | `WorkStationSlotInfo.Industry`·`C_WorkStationAssignRequest.Industry`를 `byte`→`EIndustryType`. **와이어는 그대로 1바이트** |
| `User/Character/Character.cs` | `Industries` — 적성이 정의되는 1차 산업 5종. 흩어져 있던 개념에 이름을 줬다 |
| `User/User.Character.cs` | `ToAptitudeInfos()` + `SendCharacters()`가 채워 보낸다 |
| `MikaDummyClient/.../ServerPacketHandler.cs` | 수신 로그에 `Farming=3 Fishing=0 …` 출력(수동 확인용) |
| `GameDesign/design/character/README.md` | **7.1 신설** — 적성 전달값의 소유자는 서버. 1장 #4·6장·7장 표 갱신 |
| `tasks/T-022-적성변경푸시.md` | 세션 도중 변경 푸시(선행 T-002) |
| `tasks/T-023-ItemType산업분리.md` | **`ItemType`이 아이템 분류와 산업을 겸하는 문제** — GameData 쪽 분리 |
| 테스트 | `UserCharacterTest` 신설(패킷 조립 3건) · `CharacterTest` 산업 목록 · `PacketEnumTest` **드리프트 가드 2건** |

테스트 195건 통과, 빌드 경고 0.

## 타입 이름이 `ItemType`이면 안 된다 — 두 번째 되돌림

처음엔 `EItemType`(= `GameData.ItemType` 전체 미러)을 썼다가 **사용자 지적으로 `EIndustryType`으로 바꿨다.**
적성은 아이템 분류가 아니라 산업에 대한 값인데 타입이 그렇게 말하지 않았다.

**증상은 코드 전체에 이미 나와 있었다** — 서버가 타입 이름을 매개변수 이름으로 보충하고 있다.

```csharp
public DropTable Get(ItemType industry, int level)   // DropTableCatalog
public int GetAptitude(ItemType industry)            // Character
private readonly Dictionary<ItemType, int> _industryUnlocks;
```

`EIndustryType`은 **1차 산업 5종 + None만** 담는다. `Misc`·`Special`이 타입에 아예 없어서
적성·배치에 섞일 수 없다. 프로토콜의 다른 산업 필드(`byte Industry` 2곳)도 같이 바꿨다 —
한 프로토콜에 산업 표현이 두 개면 방금 없앤 문제를 다시 만드는 셈이다.

### 뿌리는 `Enum.xlsx`였다 — `IndustryType` 신설 (T-023 착수)

`ItemType` 하나가 *아이템 분류*와 *산업*을 겸하는 게 원인이라, **`Enum.xlsx`에 `IndustryType` 시트를 넣었다.**
손으로 쓴 enum으로는 못 푼다 — 엑셀 컬럼 마커(`eIndustryType`)가 원본을 못 찾고,
DB(`t_workstation_slot.industry`·`t_user_industry_level.industry`)에 남는 숫자가 영구 계약이기 때문이다.

- 값은 **`ItemType` 시트에서 그대로 읽어 넣었다**(손으로 다시 적으면 어긋난다). `Farming=1`~`Hunting=5`.
- `None`·`Max` 센티널은 `EnumGenerator`가 붙이므로 시트에 적지 않는다.
- `generate-tables.ps1` 실행 후 **`.bytes`·`DataLog`는 무변동** — `Enum.cs`만 늘었다.
- `MikaProtocol.EIndustryType`의 미러 기준을 `ItemType` 산업 구간 → **`GameData.IndustryType`** 으로 옮겼다.

⚠️ **`DropTID = 산업×100000 + …`와 DB 저장값이 `ItemType` 숫자에 묶여 있다.**
두 enum이 갈라져도 **값은 1:1이어야 한다** — `PacketEnumTest`에 그 제약을 테스트로 박았다.

**서버 호출부도 전부 옮겼다 (T-023 완료·보관).** `IndustryLevelTable.IndustryType` 컬럼을
`eIndustryType`으로 바꾸고(산업별 시트 5개), 산업을 뜻하던 `ItemType` 자리를 전부 교체했다.
**데이터 이관은 없었다** — 값이 1:1이라 `.bytes`·`DataLog`·DB가 그대로다.

마이그레이션 중 뜬 **컴파일 에러 2건이 정확히 이 작업의 성과**다 —
`CharacterTest`의 `[InlineData(ItemType.Misc/Special)]`. 예전엔 `_ => 0`이 런타임에 삼키던 호출이
이제 컴파일 단계에서 막힌다. 이 리스크는 `기획평가.md`에 **R7**로 등록돼 있던 것이고 함께 해소 처리했다.

### 🔴 Unity 클라이언트 컴파일이 깨진다 — 담당 분리라 손대지 않았다

`Industry` 필드가 `byte` → `EIndustryType`이 됐다. **와이어는 1바이트 그대로**(통신은 안 깨진다).

| 파일 | 증상 |
| --- | --- |
| `UI/WorkStation/SelectPanel/WorkStationSelectPanelUI.cs` | `Send(byte industry, …)`의 `Industry = industry`가 **컴파일 에러**. `Send(0, 0)`(해제)도 같다 |
| `Log/PlayerDataLogger.cs` · `UI/.../WorkStationSlotView.cs` | `(GameData.ItemType)slot.Industry` — enum→enum 캐스팅이라 **컴파일은 되지만** 이제 불필요하고 의미도 어긋난다 |

`Scripts_Client`는 상대 담당이라(CLAUDE.md 협업 규칙) 고치지 않고 **수정 코드까지 이슈에 적어 넘겼다**
([#13 코멘트](https://github.com/JeongTaeWoong99/Windows_simulator/issues/13#issuecomment-5232846538)).

## 순서 규약을 두지 않았다 — 이슈 원문과 다르다

이슈는 `byte[] Aptitudes // 산업 순서(농사·낚시·채굴·벌목·사냥)대로`를 제안했다.
**처음엔 그걸 `ItemType` 인덱싱 배열로 구현했다가, 사용자 지적으로 구조체 목록으로 바꿨다.**

```csharp
public partial struct AptitudeInfo
{
    public EItemType Industry { get; set; }   // 어느 산업인지 스스로 밝힌다
    public byte      Value    { get; set; }
}
```

**순서·인덱스 규약은 어긋나도 컴파일이 통과한다.** 배열이었으면 이슈 원문대로 `[0]`을 농사로 읽는
순간 한 칸씩 밀려서 농사가 항상 0으로 보였을 것이다 — 값이 자기 산업을 들고 다니면 그 사고가 없다.

- **1차 산업 5종이 값 0까지 전부 실린다.** 0을 빼면 "다루지 못함(잠금)"과 "정보 없음"이 구분되지 않는다.
- `Misc`·`Special`·`None`은 배치 대상이 아니라 빠진다(배열일 땐 의미 없는 0 칸이었다).
- `EIndustryType`은 손으로 쓴 미러라 `Enum.xlsx`와 **드리프트할 수 있다** →
  `PacketEnumTest`에 이름·값 1:1 가드 + "1차 산업만 담는다" 가드를 붙였다.

## 남은 것 · 주의

- **오늘은 전달값 = 테이블값이다.** 적성을 바꾸는 주체가 없어서 그렇다.
  클라가 테이블을 읽어도 당장은 똑같이 동작한다 — 그래서 **틀렸는지 테스트로 안 잡힌다.**
- **`GrantCharacterRepository`에 잠재 버그가 있다.** 생성자로 임의 TID를 받는데
  콜백 `User.OnDefaultCharacterGranted`(`User.DB.cs`)는 `DefaultCharacterTid` 상수로 테이블을 조회한다.
  1003을 지급하면 **DB엔 1003, 메모리엔 1001 행**이 붙는다(다음 로그인에 `LoadCharacters`가 자가 복구).
  호출부가 하나뿐이라 아직 안 드러난다. **여러 종류 지급을 붙일 때 여기부터 고친다.**
  덧붙여 `Apply()`가 `FinishLogin()`을 부르므로 **로그인 흐름에 묶여 있어** 세션 도중 지급엔 못 쓴다.
- **캐릭터 획득 경로는 기획 미정**(캐릭터 5장 #4 · T-010). 가챠는 아이템 전용이고
  시작 지급은 1001 하나 고정이다. 엑셀엔 1001~1006 여섯 종이 이미 있다.
- **장비가 적성을 올리는지는 확정이 아니다.** 현재 확정은 "장비 = **속도** 가산"(작업슬롯 3.4).
  이번 변경은 **전달 경로만** 정한 것이지 장비의 작용점을 바꾸지 않았다.
  속도만 올리는 쪽으로 굳으면 **T-022는 닫아도 된다.**
- `check-doc-graph.ps1 -Changed` — 그래프 정합성 OK. `character` 날짜 상승으로 역전 경고 7건이 새로 뜨지만
  **게임 규칙이 바뀐 게 아니라 전달 경로만 바뀌어서** 상위 문서로 전파하지 않았다.
  `workslot`의 "적성 0 = 잠금 표시"도 규칙 자체는 그대로고 이미 `character`를 참조한다.
- 이슈 답변을 [#13에 올렸다](https://github.com/JeongTaeWoong99/Windows_simulator/issues/13#issuecomment-5232846538) —
  클라 컴파일 수정 코드 포함. **이슈는 열어 둔다**(클라 적용 확인 후 닫는다).

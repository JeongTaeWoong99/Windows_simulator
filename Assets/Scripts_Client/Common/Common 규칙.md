# Common 규칙

> 최종 업데이트: 2026-08-12 · 대상: `Assets/Scripts_Client/Common/`

**이 폴더는 이 프로젝트의 것이 아니다.** [Arca Unity Toolkit](https://github.com/JeongTaeWoong99/Arca_Unity_Toolkit)이라는
별도 저장소에서 관리하는 범용 코드의 **사본**이고, 스킬 두 개가 심고 되돌린다.

| 항목 | 값 |
|------|-----|
| 마스터 원본 | `~/.claude/skills/unity-project-setup/templates/code/Common/` |
| 마스터 저장소 | `C:\Users\ASUS\.claude\skills` (이 폴더 자체가 git 저장소다) |
| 심는 스킬 | `/unity-project-setup` — 새 프로젝트에 복사 |
| 되돌리는 스킬 | `/unity-skill-sync` — 여기서 개선한 것을 마스터로 |

---

## 1. ⚠️ 여기에 함부로 파일을 만들지 않는다

새 스크립트를 만들기 전에 **반드시** 아래를 통과해야 한다.

```
1) 이 코드가 이 게임을 아는가?
   패킷(MikaProtocol)·테이블(GameData)·화면 이름·기획 용어를 참조하는가?
   → 하나라도 예 = Common에 둘 수 없다. 해당 기능 폴더로.

2) 어느 유니티 프로젝트에 그대로 옮겨도 쓸모가 있는가?
   → 아니오 = Common에 둘 수 없다.

3) 둘 다 통과했는가?
   → 그래도 바로 만들지 않는다. 사용자에게 승인을 받는다.
```

**왜 승인이 필요한가** — 여기 파일을 하나 만들면 그건 이 프로젝트의 결정이 아니라
**앞으로 만들 모든 유니티 프로젝트의 결정**이 된다. 마스터로 올라가면 다음 프로젝트에 자동으로 딸려간다.
되돌리기가 비싸므로 들어가는 문턱을 높인다.

### 경계의 실례 — `ClientLogger`는 왜 `Log/`에 있나

로거는 어느 프로젝트에나 있을 법한 코드다. 그런데 `ClientLogger`는 `PacketId`(이 게임의 패킷 열거형)와
`[↑송신]`·`[↓수신]` 같은 **이 게임의 태그 약속**을 안다. 1번에서 걸린다.

`Services`·`MonoService`는 반대다. 무엇을 등록하든 상관하지 않는다 — 그래서 여기 있다.

---

## 2. 자산 목록

| 자산 | 런타임 | 에디터 | 내용 |
|------|--------|--------|------|
| **ServiceLocator** | `Service/Services.cs`<br>`Service/MonoService.cs` | — | 역할↔구현 등록·조회. 하드 싱글톤(`X.Inst`) 대체. ⚠️ **조회는 반드시 `Start`** |
| **MonoBehaviourExtensions** | `Extensions/MonoBehaviourExtensions.cs` | — | `RequireRef` — 필수 인스펙터 참조 검증(fail-fast) |
| **CenterHeader** | `Attribute/CenterHeaderAttribute.cs` | `Editor/CenterHeaderDrawer.cs` | 인스펙터 섹션을 가운데 정렬 헤더로 구분. 문구에 `< >`를 넣지 않는다 |
| **HierarchyStyler** | — | `Editor/HierarchyPalette.cs`<br>`Editor/HierarchyStyler.cs` | 하이어라키에서 이름 앞 접두 문자(`!`·`@`·`#`)로 줄을 색칠 |

### 폴더 분류

```
Common/
├── Attribute/    런타임 어트리뷰트 (인스펙터 표식 등)
├── Editor/       에디터 전용 — 드로어·툴 (빌드에서 제외된다)
├── Extensions/   확장 메서드
└── Service/      서비스 로케이터
```

⚠️ **런타임 코드를 `Editor/`에 넣으면 빌드가 깨진다.** Unity는 `Editor`라는 이름의 폴더를
빌드에서 통째로 제외한다. 어트리뷰트와 짝꿍 드로어의 폴더가 갈라져 있는 이유다.

---

## 3. 여기 코드를 고쳤다면

1. **먼저 범용인지 본다.** 이 게임 사정 때문에 고친 거라면 애초에 이 폴더에 있으면 안 되는 코드다
2. 범용 개선이면 `/unity-skill-sync`로 **마스터에 되돌린다.** 안 하면 다음 프로젝트는 낡은 사본을 받는다
3. 주석에 **이 프로젝트의 클래스 이름을 남기지 않는다** — `XxxManager` 같은 자리 표시자를 쓴다
   (실제로 `SessionManager`가 박혀 있다가 그 클래스가 사라진 적이 있다)

> 마스터와 사본은 **양방향으로 어긋날 수 있다.** 한쪽을 일방적으로 덮지 말고 파일별로 본다.

---

## 4. ⚠️ 이 문서는 코드 주석을 대신하지 않는다

다른 폴더는 긴 구조 설명을 코드에서 md로 옮겼지만, **`Common/`은 코드 주석을 그대로 둔다.**

이 파일들은 다른 프로젝트로 복사되는데 **이 md는 따라가지 않는다.** 주석을 걷어내면
새 프로젝트에서는 `MonoService`의 설계 근거(왜 제네릭 제약이 아니라 런타임 검사인가)나
`RequireRef`가 `UnityEngine.Object`를 받는 이유가 통째로 사라진다.

그래서 여기 코드의 주석 비중이 높은 것은 **의도된 것**이다. 줄이지 않는다.
설명이 더 필요하면 마스터의 `templates/code/README.md`에 적는다 — 그건 함께 관리된다.

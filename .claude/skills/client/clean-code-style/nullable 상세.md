# nullable 참조 형식 상세 (9장 부록)

> 최종 업데이트: 2026-08-14 (`SKILL.md` 9장에서 분리)

`SKILL.md` **9장의 요지 표**에 적힌 규칙들의 **예시·근거·함정**이다.
규칙 자체는 `SKILL.md`에 있다 — 여기는 **"왜 그런지"가 궁금할 때만** 연다.

| 무엇이 궁금한가 | 절 |
|---|---|
| `?` 와 `null!` 중 무엇을 쓰나 · 값 타입은? | 1 |
| ⚠️ 왜 Unity 객체에 `?.` 를 쓰면 안 되나 | 2 |
| `RequireRef` 를 `Awake` 에 두나 `Start` 에 두나 | 3 |
| 이벤트 발행이 터진다 | 4 |

> **전제 — `Assets/csc.rsp` 에 `-nullable:enable` 이 있어야 한다.**
> Unity 는 기본으로 꺼져 있다. 이 스위치가 없으면 이 문서 전체가 무의미하고,
> `Common/Extensions/MonoBehaviourExtensions.cs` 의 `Object?` 가 **CS8632 경고**를 낸다.
> (`/unity-project-setup` 3단계가 이 파일을 만든다)

`?` 와 `!` 는 **C# 8.0 의 정식 언어 기능**이다. Unity 전용 문법이 아니다.

| 기호 | 이름 | 런타임 동작 |
|------|------|------|
| `Type?` | nullable 참조 형식 — "비어 있을 수 있다"는 **선언** | 없음 (컴파일러용 표시) |
| `?.` | null 조건부 연산자 — 앞이 null 이면 전체가 null | 있음 |
| `??` | null 병합 연산자 — 왼쪽이 null 이면 오른쪽 | 있음 |
| `x!` · `= null!` | null 용서 연산자 — **경고만 끈다. 검사가 아니다** | **없음** (컴파일하면 사라진다) |

---

## 1. 필수 참조 vs 선택 참조

**`?` 는 안전장치가 아니라 "비어도 정상"이라는 팻말이다.** 전부 `?` 로 두면 미연결이
조용히 무시돼 "왜 안 되지?"가 된다. 실제 안전은 `RequireRef` 가 만든다.

| | 필수 참조 (없으면 성립 불가) | 선택 참조 (없어도 정상) |
|---|---|---|
| 선언 | `private Button okButton = null!;` | `private Button? hintButton;` |
| 검증 | `RequireRef` → **즉시 예외** (fail-fast) | `if (x != null)` → 조용히 건너뜀 |
| 의도 | 미연결을 실행 즉시 드러낸다 | 없는 게 정상인 상황 |

```csharp
// 필수 — 인스펙터가 나중에 채우므로 컴파일러는 CS8618 을 낸다.
//        = null! 로 경고를 끄고, 진짜 검사는 RequireRef 가 한다
[SerializeField] private Button okButton = null!;
this.RequireRef(okButton, nameof(okButton));
// → MissingReferenceException: [XxxPresenter] 'okButton' 참조가 인스펙터에 연결되지 않았습니다.

// 선택 — 비어 있어도 넘어간다
[SerializeField] private Button? hintButton;
if (hintButton != null)
{
    hintButton.onClick.AddListener(OnHintClicked);
}
```

**값 타입(`float`·`int`·`bool`·enum)은 대상이 아니다.** `= null!` 을 붙이지 않는다.

## 2. ⚠️ Unity 객체에 `?.` · `??` 를 쓰지 않는다

Unity 는 `Destroy` 된 오브젝트를 **"가짜 null"** 로 만든다 — C# 메모리상으로는 살아 있는데
`UnityEngine.Object` 의 `==` 오버로드가 `true` 를 돌려주는 상태다.
**`?.` 와 `??` 는 그 오버로드를 무시하고 참조 동등성만 본다.**

```csharp
Destroy(target);

if (target == null) { }   // true — Unity 의 == 오버로드가 동작한다
target?.DoSomething();    // 통과해 버린다! → 파괴된 오브젝트를 건드려 터진다
```

| 대상 | 검사 방법 |
|------|------|
| Unity 객체 (`GameObject`·`Component`·`ScriptableObject`) | **반드시 `== null` / `!= null`** |
| 순수 C# (이벤트·컬렉션·문자열·DTO) | `?.` · `??` 자유롭게 |

같은 이유로 `RequireRef` 의 파라미터가 제네릭 `T` 가 아니라 `UnityEngine.Object` 다
(`MonoBehaviourExtensions` 주석 참조).

## 3. 검증 위치 — Presenter 는 `Start`, 종속 View 는 `Awake`

| 대상 | 위치 | 이유 |
|------|------|------|
| Presenter · Manager | `Start()` | `Services.Get<T>()` 가 `Start` 여야 하므로 함께 둔다 |
| 종속 View (프리팹 한 칸) | `Awake()` | 상위가 `Start` 에서 `Bind` 를 부르기 전에 검증이 끝나 있어야 한다. 서비스를 조회하지 않으므로 `Awake` 로 충분 |

**서비스 필드는 `RequireRef` 가 필요 없다.** `Services.Get<T>()` 가 미등록 시 예외를 던지므로
그 자체로 fail-fast 다.

```csharp
private XxxModel _model = null!;   // Start 의 Services.Get 이 채운다 — RequireRef 불필요
```

## 4. 이벤트는 `?` 선언 + `?.Invoke`

구독자가 0명이면 이벤트 필드는 **진짜 null** 이다. `?.` 없이 `Invoke` 하면 터진다.

```csharp
public event Action<int>? PointsScored;   // 선언에 ?
PointsScored?.Invoke(score);              // 호출에 ?.
```

`?.` 는 검사를 **생략하는 게 아니라 하는 것**이라 성능 이득이 아니다 —
`if (x != null) x.Invoke(...)` 와 같고, 덤으로 스레드 안전하다(임시 복사).

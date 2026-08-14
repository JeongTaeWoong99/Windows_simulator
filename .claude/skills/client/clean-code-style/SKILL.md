---
name: clean-code-style
description: Unity/C# 클린 코드 스타일 규칙. 코드 작성 및 리뷰 시 이 규칙을 따른다.
---

> 최종 업데이트: 2026-08-15 (8·9장을 요지 표로 축약 · 상세를 같은 폴더의 별도 md로 분리)

# Unity/C# 클린 코드 스타일

---

## 1. C# 명명 규칙

| 대상 | 형식 | 예시 |
|------|------|------|
| 클래스, 메서드, Public 필드 | PascalCase | `PlayerController`, `CalculateTrajectory()` |
| 변수, 매개변수 | camelCase | `healthPoints`, `targetPosition` |
| Private 멤버 필드 | `_` + camelCase | `_maxHealth`, `_isDead` |
| Boolean | `is/has/can` 접두사 | `isDead`, `hasHealthPotion`, `canJump` |
| Interface | `I` 접두사 | `IDamageable`, `IInteractable` |
| Enum | 단수형 PascalCase | `WeaponType { Knife, Gun }` |
| 이벤트 | 과거형 동사 | `DoorOpened`, `PointsScored` |
| 이벤트 핸들러 | `On` + 이벤트명 | `OnDoorOpened`, `OnPointsScored` |

**금지:**

```csharp
// Bad — 약어, 한 글자, 불명확한 이름
float hp;
Vector3 pos;
GameObject obj;
string temp;

// Good
float healthPoints;
Vector3 targetPosition;
GameObject enemyObject;
string playerName;
```

---

## 2. MonoBehaviour 클래스 구성 순서

```csharp
public class PlayerController : MonoBehaviour
{
    // 1. Public Fields
    public float DamageMultiplier = 1.5f;

    // 2. [SerializeField] Private Fields
    [SerializeField] private float _maxHealth;

    // 3. Private Fields
    private bool _isDead;

    // 4. Properties
    public float MaxHealth => _maxHealth;
    public bool IsDead => _isDead;

    // 5. Events / Delegates (구독자가 없으면 null 이므로 ? — 9-4 절)
    public event Action<int>? PointsScored;
    public event Action?      OnDied;

    // 6. MonoBehaviour 생명주기 (Awake → OnEnable → Start → Update → OnDisable → OnDestroy)
    private void Awake() { }
    private void Start() { }
    private void Update() { }

    // 7. 나머지 메서드는 사용 단계별 #region 으로 그룹화 (아래 규칙 참조)
    #region 초기화
    public void Setup() { }
    #endregion

    #region 게임 진행
    public void InflictDamage(float damage) { }
    private void Die() { }
    #endregion
}
```

### 메서드 그룹화 — 사용 단계별 #region

필드 → 프로퍼티 → 이벤트 → **생명주기**까지는 상단에 그대로 두고(영역 없음),
그 아래 나머지 메서드는 **사용 단계별 `#region`** 으로 묶는다. Rider 접기로 긴 클래스 탐색이 쉬워진다.

- 표준 묶음: `초기화` / `게임 진행` / `종료 · 결과` / `보조`. 클래스 성격에 맞으면 `입력` / `손패 · 배치` 같은 **관심사 기준**도 가능.
- region **내부 순서는 호출 흐름 우선**(public/private 엄격 분리보다 읽는 순서 우선).
- **소형 클래스**(메서드 3~5개 이하)는 region 없이 주석만 — 오버엔지니어링 금지.

---

## 3. 포맷팅

### Allman 스타일 — 중괄호 항상 별도 줄

```csharp
// Good
if (!showMouse)
{
    Cursor.lockState = CursorLockMode.Locked;
}

// Bad
if (!showMouse) Cursor.lockState = CursorLockMode.Locked;
if (!showMouse) { Cursor.lockState = CursorLockMode.Locked; }
```

### 수평 간격

```csharp
// Good — 연산자 전후 공백, 콤마 뒤 공백
float result = a + b * c;
Vector3 position = new Vector3(0f, 1f, 0f);

// Bad
float result=a+b*c;
Vector3 position=new Vector3(0f,1f,0f);
```

### 수직 간격

```csharp
// Good — 관련 있는 코드는 묶고, 논리 단위 사이에 빈 줄 1개
private void Update()
{
    HandleInput();

    UpdateMovement();

    CheckGroundState();
}
```

---

## 4. Properties & Serialization

### 단일 표현식 프로퍼티

```csharp
// Good — 읽기 전용 프로퍼티는 => 사용
public float MaxHealth => _maxHealth;
public bool IsAlive => _currentHealth > 0f;
```

### Inspector 어트리뷰트

```csharp
[Header("이동 설정")]
[SerializeField, Tooltip("좌우 이동 속도 (m/s)")]
[Range(1f, 20f)]
private float _moveSpeed = 5f;

[Header("점프 설정")]
[SerializeField, Tooltip("점프 힘")]
private float _jumpForce = 10f;
```

**규칙:**
- Inspector 설명은 코드 주석 대신 `[Tooltip]` 사용
- 수치 범위가 있으면 `[Range]` 필수
- 섹션 구분은 `[Header]` 사용

---

## 5. 메서드 설계

### 파라미터 개수 제한

```csharp
// Bad — 파라미터 3개 이상
public void SetupEnemy(float health, float speed, float damage, bool isBoss) { }

// Good — 구조체/클래스로 묶기
public void SetupEnemy(EnemyData data) { }
```

### Flag 파라미터 금지

```csharp
// Bad — true/false가 무슨 의미인지 호출부에서 모름
public float GetAngle(bool inDegrees) { }
GetAngle(true);

// Good — 의도가 명확한 별도 메서드
public float GetAngleInDegrees() { }
public float GetAngleInRadians() { }
```

---

## 6. 주석 규칙

### WHY를 설명, WHAT은 코드로

```csharp
// Bad — 코드가 이미 말하는 내용을 반복
int count = 0; // 카운트를 0으로 초기화

// Good — 코드만으로 알 수 없는 이유를 설명
// 물리 엔진이 FixedUpdate 이후에 적용되므로, 한 프레임 지연 후 체크
private IEnumerator CheckGroundNextFrame() { }
```

### Public API — XML 문서 주석

```csharp
/// <summary>
/// 플레이어에게 데미지를 적용하고 사망 여부를 반환합니다.
/// </summary>
/// <param name="damage">적용할 데미지 양 (0 이상)</param>
/// <returns>이 데미지로 사망했으면 true</returns>
public bool ApplyDamage(float damage) { }
```

### [Tooltip] vs 주석 선택 기준

| 상황 | 사용 |
|------|------|
| Inspector에 보이는 필드 설명 | `[Tooltip]` |
| 코드 실행 이유, 알고리즘 설명 | `//` 주석 |
| Public 메서드/클래스 API 문서 | XML `///` |

### 메서드 1줄 요약 + 바인딩 표기

**모든 메서드 위에 1줄 요약 주석**을 단다(자명한 한 줄 getter 제외). 특히 **호출 경로가 코드만으론 안 보이는** 메서드는
어디서 불리는지를 괄호로 명시한다 — 구독/버튼/엔진 메시지 구분이 핵심.

| 종류 | 표기 예시 |
|------|-----------|
| Unity 메시지 | `// 카드 누름 — 드래그 시작 (Unity 마우스 메시지)` / `// 턴 이벤트 구독 (Unity 메시지)` |
| 이벤트 구독 핸들러 | `// 내 턴 시작 시 배치 카운트 초기화 (OnTurnStarted 구독)` |
| UI 버튼 OnClick | `// 게임 시작 (GameStartBtn OnClick에 할당)` |
| 다른 클래스가 호출 | `// 공격 실행 (EntityManager·EnemyAI가 호출)` |

라인 단위로도 의미가 갈리는 곳엔 인라인 주석을 붙인다(열 맞춰 정렬):
```csharp
_notificationPanel.ScaleZero();     // 알림 패널 숨김
_resultPanel.ScaleZero();           // 결과 패널 숨김
_titlePanel.Active(true);           // 타이틀 패널 켜기
```

---

## 7. 피해야 할 코드 스멜

| 스멜 | 설명 | 해결 |
|------|------|------|
| 불명확한 이름 | `data`, `info`, `temp`, `manager2` | 역할을 명확히 표현하는 이름 |
| 과도한 주석 | 나쁜 코드를 주석으로 설명 | 코드 개선이 우선 |
| Flag 파라미터 | `DoSomething(true, false)` | 별도 메서드로 분리 |
| 매직 넘버 | `if (health < 30f)` | `const float LowHealthThreshold = 30f;` |
| 중첩 조건문 | 3단계 이상 if 중첩 | 조기 반환(guard clause)으로 평탄화 |

---

## 8. 코드 스타일 (선택 규약)

> 위 규칙과 함께 적용한다. 가독성을 높이기 위한 선택으로, 널리 쓰이는 스타일이다.

| 규칙 | 요지 |
|------|------|
| **필드 열 정렬** | 같은 modifier 그룹 안에서 타입명 뒤 공백으로 **변수명 열을 맞춘다.** 그룹(`[SerializeField] private` / `private` / `private readonly` / `const`)이 바뀌면 빈 줄로 끊고 정렬 기준도 리셋 |
| **상수는 최상단** | `const` · `static readonly` 는 멤버 필드보다 위, 클래스 선언 직후에 모은다 |
| **열이 어긋나면 그룹을 나눈다** | `readonly` 유무로 타입 열이 깨지면 **변수를 하나 더 만들어서라도** 같은 그룹으로 묶고, 가변 1개만 분리 |
| **event vs Action** | 외부에서도 `Invoke` 해야 하면 `Action`, 내부 전용이면 `event Action`. **왜 그렇게 골랐는지 주석으로 남긴다** |
| **`[CenterHeader]`** | 직렬화 필드 3개 이상이면 역할별 헤더를 붙인다. ⚠️ **문구에 `< >` 를 넣지 않는다**(Drawer 가 붙인다). ⚠️ **배열 위에는 `[NonReorderable]` 을 같이** 달지 않으면 헤더가 안 보인다 |
| **폴더 구성** | 역할별로 나눈다. 범용 코드는 `Common/`, 계약은 **그것을 정의한 기능 폴더 안 `Contracts/`**, 에디터 전용은 **반드시 `Editor/`**. 프로젝트가 다른 구조를 쓰면 그 프로젝트 `CLAUDE.md` 의 폴더 구조 표를 따른다 |

> 📖 예시·근거·함정은 **[`스타일 상세.md`](<스타일 상세.md>)** 에 있다 — 위 표로 판단이 서면 열지 않아도 된다.

---

## 9. nullable 참조 형식 (`?` · `null!`)

> **전제 — `Assets/csc.rsp` 에 `-nullable:enable` 이 있어야 한다.** 없으면 이 절은 무의미하다.

**`?` 는 안전장치가 아니라 "비어도 정상"이라는 팻말이다.** 실제 안전은 `RequireRef` 가 만든다.

| | 필수 참조 (없으면 성립 불가) | 선택 참조 (없어도 정상) |
|---|---|---|
| 선언 | `private Button okButton = null!;` | `private Button? hintButton;` |
| 검증 | `RequireRef` → **즉시 예외** (fail-fast) | `if (x != null)` → 조용히 건너뜀 |

- ⚠️ **Unity 객체에 `?.` · `??` 를 쓰지 않는다.** `Destroy` 된 오브젝트의 **"가짜 null"** 을
  그대로 통과시킨다. `== null` 로 검사한다. `?.` 는 순수 C#(이벤트·컬렉션)에만.
- **검증 위치** — Presenter · Manager 는 `Start()`, 종속 View 는 `Awake()`.
- **서비스 필드는 `RequireRef` 가 필요 없다** — `Services.Get<T>()` 가 미등록 시 예외를 던진다.
- **값 타입(`float`·`int`·`bool`·enum)은 대상이 아니다.** `= null!` 을 붙이지 않는다.
- **이벤트는 `?` 선언 + `?.Invoke`** — 구독자가 0명이면 진짜 null 이다.

> 📖 `x!` 가 왜 검사가 아닌지, 가짜 null 의 동작, 각 규칙의 예시는
> **[`nullable 상세.md`](<nullable 상세.md>)** 에 있다 — 위 규칙만으로 판단이 서면 열지 않아도 된다.

---
name: feature-design
description: 기능 설계 규칙. 새 기능/클래스/시스템 구현 요청 시 이 규칙을 따른다.
---

# 기능 설계 가이드

---

## 0. 적용 원칙 (먼저 판단)

> **규모와 상황을 먼저 판단한다.**
> 
> 작은 기능, 일회성 스크립트, 프로토타입에는 아래 원칙을 모두 적용할 필요가 없다.
> 
> 오버엔지니어링이 단순한 코드보다 나쁘다 — 필요할 때만 도입한다.

| 규모 | 가이드 |
|------|--------|
| 소규모 (단일 스크립트, 간단한 기능) | 명명·포맷만 지키고 나머지 생략 가능 |
| 중규모 (여러 클래스 협력, 재사용 예정) | SOLID·OOP 체크, 패턴 적용 검토 |
| 대규모 (시스템 전체, 팀 협업) | 모든 원칙 적극 적용, 테스트 필수 |

---

## 1. 구현 전 체크리스트

```
□ 기존 코드베이스에 재사용 가능한 것이 있는가?
□ 클래스가 하나의 책임만 갖는가? (SRP)
□ 추상화/인터페이스가 필요한가? (OCP, DIP)
□ 적용할 수 있는 디자인 패턴이 있는가?
□ 향후 확장성을 고려했는가? (OCP)
□ 테스트 가능한 구조로 설계했는가? (의존성 주입, 인터페이스 분리)
```

---

## 2. SOLID 원칙 빠른 참조

> 상세 구현 및 예시 코드:
> https://github.com/JeongTaeWoong99/Mini_Project/blob/main/Assets/Project/2_Tutorials/LevelUpYourCode/README.md


| 원칙 | 한 줄 요약 | 위반 신호 |
|------|-----------|----------|
| **SRP** | 클래스는 변경 이유가 하나여야 함 | 클래스가 500줄 이상, 여러 관심사 혼합 |
| **OCP** | 확장엔 열려있고, 수정엔 닫혀있음 | 새 기능마다 기존 if/switch 추가 |
| **LSP** | 자식은 부모를 완전 대체 가능 | override 후 동작이 달라지거나 예외 발생 |
| **ISP** | 불필요한 메서드 구현 강요 금지 | 인터페이스에 빈 메서드 구현 |
| **DIP** | 구체 클래스가 아닌 추상에 의존 | `new ConcreteClass()` 직접 생성이 많음 |

```csharp
// DIP 예시 — 구체 클래스 대신 인터페이스에 의존
// Bad
public class EnemyAI
{
    private PathfinderAStar _pathfinder = new PathfinderAStar(); // 구체 클래스에 의존
}

// Good
public class EnemyAI
{
    private readonly IPathfinder _pathfinder;
    public EnemyAI(IPathfinder pathfinder) { _pathfinder = pathfinder; } // 추상에 의존
}
```

---

## 3. 디자인 패턴 빠른 참조

> 상세 구현 및 예시 코드:
> https://github.com/JeongTaeWoong99/Mini_Project/blob/main/Assets/Project/2_Tutorials/LevelUpYourCode/README.md

### 생성 패턴

| 패턴 | 언제 사용 | 신호 |
|------|----------|------|
| **Factory** | 객체 생성 로직 분리 | `if (type == "A") new A(); else new B();` 반복 |
| **Object Pool** | 잦은 생성/파괴 오브젝트 | 총알, 이펙트, 파티클 |
| **Singleton** | 전역 매니저 (남용 주의) | GameManager, AudioManager |

### 행동 패턴

| 패턴 | 언제 사용 | 신호 |
|------|----------|------|
| **Command** | Undo/Redo, 입력 처리 분리 | 되돌리기 필요, 입력을 큐에 저장 |
| **State** | 복잡한 상태 분기 | if-else/enum 상태 분기가 길어짐 |
| **Observer** | 이벤트 발행-구독 | 느슨한 결합, Unity Event 대체 |
| **Strategy** | 알고리즘 교체 가능 | 이동 방식, AI 행동, 무기 공격 방식 |
| **Dirty Flag** | 변경 시에만 연산 실행 | UI 업데이트, 경로 재계산 |

### 구조 패턴

| 패턴 | 언제 사용 | 신호 |
|------|----------|------|
| **Flyweight** | 동일 데이터 공유 | 수백 개 적 스탯, 타일맵 데이터 |

### 아키텍처 패턴

| 패턴 | 언제 사용 |
|------|----------|
| **MVP** | UI 로직 분리 (uGUI 기반) |
| **MVVM** | 데이터 바인딩 자동화 (UI Toolkit 기반) |

---

## 4. OOP 설계 체크

```csharp
// 캡슐화 — public 필드 최소화, [SerializeField] 활용
[SerializeField] private float _maxHealth; // Good
public float maxHealth;                    // Bad (외부에서 직접 수정 가능)

// 다형성 — Interface 활용
public interface IDamageable { void TakeDamage(float amount); }
public interface IInteractable { void Interact(); }

// 상속 vs 컴포지션 — 깊은 상속보다 컴포넌트 분리 우선
// Bad: Enemy → FlyingEnemy → FlyingBossEnemy (3단계 상속)
// Good: Enemy + FlyComponent + BossComponent (컴포지션)
```
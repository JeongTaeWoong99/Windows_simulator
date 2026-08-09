---
name: server-code-style
description: 서버(C#/.NET) 코드 작성 스타일 규칙. 서버 코드를 새로 쓰거나 수정할 때 적용한다.
---

> 최종 업데이트: 2026-08-09

# server-code-style — 서버 코드 작성 스타일

`Server/` 아래 C# 코드를 작성·수정할 때 지키는 규칙이다.

## 주석 — 꼭 필요한 곳에만 단다

주석은 **코드가 스스로 보여줄 수 없는 것**을 적을 때만 단다.

### 달아야 하는 것

| 종류 | 예 |
|------|-----|
| 불변식·순서 제약 | "호출 전에 반드시 정산해야 한다 — 안 그러면 이전 구간에 소급된다" |
| 이 방식을 고른 이유 (버린 대안) | "델타가 아니라 확정 잔액을 쓴다 — 재시도가 곧 재화 복제가 된다" |
| 값의 단위·범위·함정 | "천분율(1000 = 1.0배)", "Amount는 반드시 long — int 상한을 넘는다" |
| 스레드 경계 | "=== DB 스레드에서 실행 ===", "로직 스레드로 넘긴다" |

### 달지 않는 것

- **코드를 그대로 읽어주는 주석** — `LoadInventory()` 위의 `// 인벤토리를 로드한다`.
  이름이 이미 말하고 있다. 주석이 필요하면 이름을 먼저 고친다.
- **변경 이력·작업자 메모** — "~에서 옮겨옴", "리뷰 반영". git과 `.claude/Agent/` 로그가 기록한다.
- **주석 처리된 죽은 코드** — 지운다. 되살릴 일이 있으면 git에 있다.
- **빈 주석 자리표시** — `// Log`, `// TODO` 만 덜렁 남기지 않는다. 지금 처리하지 않을 거면
  무엇을 왜 미루는지까지 적거나, 일감(`tasks/`)으로 올린다.

### 길이 — 1~2줄로 끝낸다

주석은 **1줄, 길어도 2줄**이다. 형태는 길이에 따라 갈린다.

| 길이 | 형태 |
|------|------|
| 1줄 | 한 줄짜리 XML 문서 주석 — `/// <summary>티켓을 소모하고 잔량을 반환한다</summary>` |
| 2줄 | `//` 두 줄 (여러 줄 `/// <summary>` 블록으로 늘리지 않는다) |

```csharp
/// <summary>만료된 티켓은 소모하지 않고 false를 돌려준다</summary>
public bool TryConsume(Ticket ticket)

// 델타가 아니라 확정 잔액을 쓴다 — 재시도가 곧 재화 복제가 된다.
// 호출 전에 반드시 정산해야 한다.
public void ApplyBalance(long amount)
```

3줄이 필요해 보이면 주석을 늘릴 게 아니라 **이름·구조를 고칠 신호**다.

### XML 문서 주석 (`///`)

- public API에 **호출자가 알아야 할 계약**(불변식·순서·오류 모드·단위)이 있을 때만 단다.
- 시그니처가 전부 말해 주면 생략한다 — `public int Count => _items.Count;`에 summary를 달지 않는다.
- 여러 줄로 쓸 일이 생기면 XML 블록 대신 위의 `//` 2줄 형태로 간다.

## 중괄호 — `if`·`foreach`는 항상 붙인다

본문이 한 줄이어도 **중괄호를 생략하지 않는다.** `if`·`else`·`for`·`foreach`·`while` 전부.

```csharp
// 이렇게
if (session == null)
{
    return;
}

foreach (var item in items)
{
    Apply(item);
}

// 이렇게 쓰지 않는다
if (session == null) return;
foreach (var item in items) Apply(item);
```

한 줄 형태는 나중에 줄을 하나 더 넣을 때 조용히 범위를 벗어난다.
diff도 한 줄 추가가 아니라 블록 전체 재작성으로 번진다.

## 함수 형태 — early-return 블록이 기본이다

함수는 **early-return 형태의 블록 본문**으로 쓴다. 실패·예외 조건을 위에서 걸러내고
정상 경로를 아래에 평평하게 둔다. 조건과 결과를 한 식에 욱여넣지 않는다.

```csharp
// 이렇게
public DropTable Get(ItemType industry)
{
    if (!_byIndustry.TryGetValue(industry, out var table))
    {
        throw new KeyNotFoundException($"[{industry}] 드롭 테이블이 없습니다.");
    }

    return table;
}

// 이렇게 쓰지 않는다 — 삼항 + throw를 한 식으로 접은 expression-bodied
public DropTable Get(ItemType industry)
    => _byIndustry.TryGetValue(industry, out var table)
        ? table
        : throw new KeyNotFoundException(...);
```

### expression-bodied(`=>`)는 정말 짧을 때만

**한 줄로 끝나는 단순 위임·프로퍼티 getter**에만 허용한다. 그 외에는 블록 본문으로 쓴다.

```csharp
// 허용 — 한 줄짜리 getter·위임
public int Count => _items.Count;
public bool TryGet(ItemType industry, out DropTable table)
    => _byIndustry.TryGetValue(industry, out table!);

// 금지 — 분기(삼항·throw 식·switch 식)가 들어가는 순간 블록으로 푼다
```

분기가 있는데 `=>`로 접으면 조건 하나만 늘어도 식 전체를 다시 짜야 하고, 중간 값을
로그로 찍을 자리도 없다.

## 언어

- 주석은 **한글**로 쓴다 (CLAUDE.md 협업 규칙). 코드 식별자·기술 용어는 영문 그대로.

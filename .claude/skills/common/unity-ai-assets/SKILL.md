---
name: unity-ai-assets
description: Unity AI로 이미지·스프라이트·사운드·음악 등 리소스를 만들거나 고칠 때 연다 — 실행 전 동의, 구독 없음·크레딧 소진 구분과 안내, 리소스별 사용 크레딧과 전후 잔액 보고. 이미 있는 그림을 잘라 쓰거나 기존 파일만 고치는 작업은 해당 없음.
---

> 최종 업데이트: 2026-11-02 (`unity-editor-ops`에서 분리 · 구독·크레딧 상태 구분 · 크레딧 보고)

# Unity AI 리소스 생성

생성은 **git으로 되돌려도 쓴 크레딧이 돌아오지 않는다.** 그래서 먼저 상태를 보고, 묻고, 쓴 만큼 보고한다.

이 문서는 생성 절차만 다룬다. 만든 파일을 어디에 어떻게 넣는지는 프로젝트의 아트 규칙이 맡는다 (이 프로젝트는 [`Art 규칙.md`](../../../../Assets/Art/Art%20규칙.md)의 "AI로 만든 그림 넣기").

---

## 1. 상태 확인 — 생성을 제안하기 전에

아래를 `Unity_RunCommand`로 **두 번** 실행한다. 첫 호출이 새로 고침을 걸고, 잠시 뒤 다음 호출에서 최신값이 나온다.
`Account`가 internal이라 리플렉션으로 읽는다. `using System.Reflection`은 막혀 있으니 쓰지 않는다.

```csharp
var acc = System.Type.GetType("Unity.AI.Toolkit.Accounts.Services.Account, Unity.AI.Toolkit.Accounts");
var st  = acc.GetField("settings").GetValue(null);
var pb  = acc.GetField("pointsBalance").GetValue(null);
st.GetType().GetMethod("Refresh").Invoke(st, null);
pb.GetType().GetMethod("Refresh").Invoke(pb, null);
foreach (var n in new[] { "HasSubscription", "CanSpendPoints", "AiGeneratorsEnabled" })
    result.Log(n + " = " + st.GetType().GetProperty(n).GetValue(st));
result.Log("Settings = " + st.GetType().GetProperty("Value").GetValue(st));
result.Log("Balance = " + pb.GetType().GetProperty("Value").GetValue(pb)); // PointsAvailable = 잔액 · PointsAllocated = 할당
```

**위에서부터 처음 맞는 줄이 답이다.** 패키지의 상태 배너(`SessionStatusBanner`)와 같은 순서다.

| 상태 | 뜻 | 사용자에게 할 안내 |
|------|-----|-------------------|
| `HasSubscription = False` | **구독 없음** | Unity AI를 쓸 수 있는 플랜이 없습니다. Unity Pro·Enterprise·Industry 라이선스나 Monthly Credit 번들 시트를 구독하면 크레딧이 채워집니다 → <https://unity.com/features/ai> |
| `Settings = null` | 계정 정보를 아직 못 읽음 | 판정하지 않는다. 잠시 뒤 다시 읽고, 그래도 비면 로그인·네트워크·클라우드 프로젝트 연결을 확인해 달라고 한다 |
| `AiGeneratorsEnabled = False` | 조직에서 생성 기능이 꺼짐 | 조직 관리자가 AI 설정에서 켜야 합니다 → <https://cloud.unity.com/home/organizations> |
| `CanSpendPoints = False` | 크레딧을 쓸 수 없는 계정 | 14일 체험을 시작하거나 플랜을 구독해 주세요 → <https://on.unity.com/unityaitrial> |
| `Balance = null` | 잔액을 못 읽음 | "확인 불가"라고 알린다. **0으로 보지 않는다** |
| `PointsAvailable <= 0` | **크레딧 소진** | 크레딧을 다 썼습니다. 매달 초에 자동으로 충전되고, 바로 쓰려면 추가 구매하면 됩니다 → <https://on.unity.com/unityaicredits> |
| 그 밖 | 생성 가능 | 2절로 간다. 잔액이 할당의 10% 이하면 한 줄로 알린다 |

- **구독 없음과 소진을 잔액만으로 가르지 않는다.** 구독이 없으면 잔액을 읽지 못하므로 `Balance`가 비어 있을 수 있다.
- `HasSubscription`은 **서버가 `NoSubscription`을 돌려줘야** False가 된다. 새로 고침을 시작할 때 True로 돌아가므로, 첫 호출의 값으로 판정하지 않는다.
- ⚠️ 이 값들은 패키지 내부 API다. 패키지가 올라가 읽기가 실패하면 "확인 불가"로 알리고 3절 콘솔 진단으로 간다.

---

## 2. 실행 전 — 보여 주고 기다린다

아래를 보여 주고 답을 기다린다. **답을 받기 전에는 생성 도구를 부르지 않는다.**

- 만들 것 · 개수 · 저장 경로 · 프롬프트
- 지금 잔액과 예상 크레딧
- 생성 중에는 응답이 막히고, 수십 초~수 분 걸린다는 점

> 도구 설명 자체가 대화당 첫 호출 전 동의를 요구한다. 같은 대화에서 이미 동의받은 범위를 넘으면 다시 묻는다.

### 크레딧 보고 — 생성했으면 항상

**쓴 크레딧은 추정이 아니라 잔액 차이로 잰다.**

1. 첫 생성 직전 잔액을 읽는다.
2. **리소스를 하나씩 생성하고**, 끝날 때마다 잔액을 읽는다.
   병렬로 돌리면 리소스별 비용을 가를 수 없다 — 사용자가 병렬을 원하면 묶음 합계로 보고하고 그렇게 적는다.
3. 값이 그대로면 몇 초 뒤 다시 읽는다. 끝내 안 바뀌면 "반영 전"이라고 적는다 — 0으로 적지 않는다.
4. 실패한 생성도 표에 적는다. 수정·배경 제거·업스케일도 생성과 똑같이 잰다.
5. 답변에 아래 표를 넣는다. 작업이 여러 턴에 걸치면 턴마다 그 턴의 몫을 보고한다.

```markdown
| 리소스 | 작업 | 사용 | 잔액 |
|--------|------|------|------|
| hero_idle.png | GenerateSprite | 3 | 833 → 830 |
| ui_click.wav | GenerateSound | 5 | 830 → 825 |

**합계 8 크레딧 사용 · 잔액 833 → 825** (할당 1000)
```

Unity AI Assistant 창에서 대화해도 같은 크레딧이 빠진다. 차이가 생성 값보다 크면 그대로 적고 그 가능성을 한 줄로 알린다.

---

## 3. 생성이 막혔을 때 — 패키지부터 의심하지 않는다

**패키지가 전부 제대로 깔려 있어도, 구독이 없으면 모델 목록은 빈 채로 온다.**
`{"Models": []}`는 설치 상태를 말해 주지 않는다. 1절 상태를 다시 읽고, 에디터 콘솔의 에러를 함께 본다.

| 콘솔에 나온 것 | 뜻 | 할 일 |
|----------------|-----|-------|
| `NoSubscription` | **구독 문제다. 패키지는 멀쩡하다** | 1절의 구독 없음 안내를 한다 |
| 아무 것도 없음 | 그때 비로소 패키지·로그인을 본다 | 순서가 이쪽이다 |

> 📌 **"패키지가 없어서 안 된다"는 설치 목록으로 확인하기 전에는 말하지 않는다.**
> 2026-09-20에 확인 없이 `com.unity.ai.generators`가 없다고 단정했다가 틀렸다 —
> 실제로는 다 깔려 있었고 진짜 원인은 `NoSubscription`이었다.

> ⛔ **`com.unity.ai.generators`는 다시 깔지 않는다.** deprecated이고 기능은 `com.unity.ai.assistant`에 전부 통합됐다.
> 둘을 함께 두면 어셈블리 이름과 GUID가 충돌해 **프로젝트 전체 컴파일이 깨진다**(2026-09-20 실제 발생).

구독·충전 전까지 생성할 수 없으면 사람이 다른 수단으로 만들어야 한다 — `unity-editor-ops`의 핸드오프 프롬프트를 쓴다.

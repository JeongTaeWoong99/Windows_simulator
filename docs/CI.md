# CI (GitHub Actions)

> 최종 업데이트: 2026-08-10
> 관련: [`Server/docs/테스트커버리지.md`](../Server/docs/테스트커버리지.md) · [`CLAUDE.md`](../CLAUDE.md)

`.github/workflows/` 아래 워크플로 4개가 있다.

| 파일 | 언제 도나 | 하는 일 |
| --- | --- | --- |
| `server-ci.yml` | `Server/**` 변경 시 push·PR | 빌드 → 테스트 → 커버리지 요약 |
| `client-ci.yml` | `Assets/**`·`Packages/**`·`ProjectSettings/**` 변경 시 push·PR | Unity 빌드 |
| `docs-deploy.yml` | `GameDesign/web/**`·`GameDesign/design/**`·`tasks/**` 변경 시 push | 문서 사이트 빌드 → Cloudflare Pages 배포 |
| `unity-activation.yml` | **수동만** | Unity 라이선스 활성화 파일(.alf) 요청 — 1회용 |

**경로 필터로 갈라 뒀다.** 서버만 고쳤는데 Unity 빌드가 20분 도는 일은 없다.

---

## 1. 서버 CI — 바로 동작한다

시크릿도 설정도 필요 없다. push하면 그때부터 돈다.

```
.NET 9+10 설치 → NuGet 캐시 → 복원 → 빌드(Release) → 테스트 + 커버리지
  → Job Summary에 커버리지 표
  → PR이면 코멘트로도
  → trx·cobertura 아티팩트 14일 보관
```

- **테스트가 실패하면 CI 실패.**
- **커버리지 수치로는 실패시키지 않는다.** 이유는 [테스트커버리지 4장](../Server/docs/테스트커버리지.md).
- `.NET 9`도 설치한다 — `MikaProtocol`·`GameData`가 `net9.0`을 함께 타깃한다.

### Unity 미러 동기화는 CI에서 꺼진다

`MikaProtocol`은 빌드 후 패킷 정의를 Unity로 복사하고, `MikaSourceGen`은 분석기 DLL을 복사한다.
러너에는 갱신할 Unity 프로젝트도 `powershell.exe`도 없으므로 두 타깃에 조건을 달았다.

```xml
Condition="... AND '$(ContinuousIntegrationBuild)' != 'true'"
```

CI가 `-p:ContinuousIntegrationBuild=true`를 넘긴다. **로컬 빌드는 그대로 돈다.**

---

## 2. 클라이언트 CI — 시크릿 3개를 먼저 넣어야 한다

Unity는 라이선스 없이 CI에서 돌지 않는다. **아래는 사람이 한 번 해야 하는 절차다.**

### 절차

1. **Actions 탭 → `Unity 라이선스 활성화 요청` → Run workflow**
2. 실행이 끝나면 아티팩트 **`Unity_v6000.x.alf`** 를 내려받는다
3. <https://license.unity3d.com/manual> 에 `.alf` 를 올리고 **Unity Personal Edition**을 골라 `.ulf` 를 받는다
4. 저장소 **Settings → Secrets and variables → Actions** 에 등록한다

   | 시크릿 | 필수 | 값 |
   | --- | --- | --- |
   | `UNITY_LICENSE` | **필수** | 받은 `.ulf` 파일의 **내용 전체** (XML) |
   | `UNITY_EMAIL` | 선택 | Unity 계정 이메일 |
   | `UNITY_PASSWORD` | 선택 | Unity 계정 비밀번호 |

5. 끝. `unity-activation.yml`은 그 뒤로 쓸 일이 없다(라이선스 만료 시 다시).

> ⚠️ `.alf`·`.ulf` 파일을 **저장소에 커밋하지 않는다.** 계정 자격이다.

### Google 계정으로 가입해 비밀번호가 없다면

**Personal 라이선스는 `.ulf` 파일 기반이라 `UNITY_LICENSE` 하나로 동작하도록 설계돼 있다.**
`UNITY_EMAIL`·`UNITY_PASSWORD`는 방어적으로 넣어 둔 것이고(Professional은 계정+시리얼이 필요하다),
활성화 절차 자체는 브라우저에서 하므로 Google 로그인으로 끝난다.

**먼저 `UNITY_LICENSE`만 넣고 돌려 본다.** 그래도 계정 인증을 요구하면,
<https://id.unity.com> 로그인 화면의 **Forgot password**에 Google 이메일을 넣어
비밀번호를 새로 만든다. Google OAuth 계정도 비밀번호를 **추가**할 수 있고,
그 뒤로는 두 방식 다 쓸 수 있다.

### 시크릿이 없으면 조용히 건너뛴다

`UNITY_LICENSE`가 비어 있으면 경고만 남기고 나머지 스텝을 전부 건너뛴다.
시크릿을 넣기 전까지 PR마다 빨간 X가 뜨면 **CI 신호 자체가 무의미해지기** 때문이다.

### 왜 ubuntu에서 Windows 빌드가 되나

스크립팅 백엔드가 **Mono**다(`ProjectSettings.asset`에 Standalone 항목이 없어 기본값).
IL2CPP였다면 Windows 러너가 필수였다 — **백엔드를 IL2CPP로 바꾸면
`runs-on`을 `windows-latest`로 옮겨야 한다.**

### Unity 테스트는 돌리지 않는다

저장소에 **`.asmdef`가 하나도 없어** 테스트 어셈블리를 만들 수 없다.
Unity Test Framework는 어셈블리 경계를 요구한다 — 클라 테스트가 0건인 근본 원인이
팀 역량이 아니라 이것이다(아키텍처 리뷰 후보 4). 경계가 생기면 그때 붙인다.

---

## 3. 아직 확인되지 않은 것

로컬에서는 **서버 CI와 같은 플래그(`Release` + `ContinuousIntegrationBuild=true`)로
빌드·테스트를 돌려 119건 통과와 Unity 동기화 생략을 확인**했다.
아래는 **러너에서 처음 돌려 봐야 아는 것**이다.

| 항목 | 걸리면 |
| --- | --- |
| ubuntu에서 `SQLite 3.13.0` 패키지(win7 RID) 복원 | 테스트가 실제 DB를 안 건드리므로 괜찮을 것. 실패하면 `runs-on: windows-latest` |
| game-ci에 **Unity 6000.3.10f1** 이미지가 있는지 | 없으면 가장 가까운 6000.3.x로 내린다 |
| 러너 디스크 용량 | `free-disk-space`로 미리 비우지만 모자라면 `large-packages: true` |
| Unity 빌드 시간 | 첫 실행은 Library 캐시가 없어 오래 걸린다(20~40분). 이후 캐시가 붙는다 |

---

## 4. Discord 알림 — 깨졌을 때만

**CI 3종이 각자 마지막에 알림 스텝을 갖는다.** 통과하거나 취소되면 울리지 않는다.

```yaml
      - name: 실패 알림
        if: failure()
        uses: ./.github/actions/discord-notify
        with:
          webhook: ${{ secrets.DISCORD_WEBHOOK }}
```

메시지 만드는 로직은 [`.github/actions/discord-notify/action.yml`](../.github/actions/discord-notify/action.yml)
한 곳에 있다. 워크플로 3개에 같은 코드를 복제하지 않으려는 것이고, 형식을 바꿀 일이 생기면
그 파일만 고친다.

| 항목 | 값 |
| --- | --- |
| 필요한 시크릿 | `DISCORD_WEBHOOK` (Discord 채널 → 설정 → 연동 → 웹후크) |
| 시크릿이 없으면 | 경고만 남기고 건너뛴다. CI를 두 번 실패시키지 않는다 |
| 울리는 조건 | `failure()` — 실패한 실행마다 1건 |
| 안 울리는 경우 | 성공 · 취소 · 시크릿이 없어 빌드를 건너뛴 경우 |

> ⚠️ **`uses: ./` 는 체크아웃된 파일을 쓴다.** 그래서 client-ci는 체크아웃을 조건 없이
> 맨 앞에서 돌린다 — 뒤 스텝이 어디서 깨지든 액션을 찾을 수 있어야 하기 때문이다.

> ⚠️ **연속 실패는 매번 알린다.** 깨진 채로 계속 push하면 그때마다 울린다.
> 소음이 심해지면 상태 변화(실패 전환 · 복구)만 알리는 방식으로 되돌린다 — 아래 참조.

### 이전 방식 (2026-08-10 교체)

`discord-notify.yml` 하나가 `workflow_run`으로 CI 3종을 뒤에서 듣고, **상태가 바뀔 때만**
(실패로 전환 · 복구) 보냈다. CI 파일에 알림 코드가 안 섞이고 연속 실패 소음도 없었지만,
`workflow_run`은 **기본 브랜치에 있는 파일만 실행돼** 브랜치에서 고치면 머지 전까지
동작을 확인할 수 없었다. 알림을 각 CI 안으로 옮기면서 걷어냈다.

---

## 5. 배포(CD)는 없다

지금은 **CI만** 있다. 서버 배포처가 정해지지 않았고, 대상 없이 배포 파이프라인을 만들면
쓰지 않는 설정만 남는다. 클라 산출물도 3일만 보관하고 버린다.

# object-pool 규칙

> 최종 업데이트: 2026-10-08 (신설) · 대상: `Common/object-pool/`

**런타임에 자주 생기고 사라지는 오브젝트를 파괴하지 않고 돌려쓴다.** 게임을 전혀 모르므로
[Arca Unity Toolkit](https://github.com/JeongTaeWoong99/Arca_Unity_Toolkit) 사본인 `Common/` 아래에 있다.
Common 전체에 걸리는 규칙은 상위 폴더의 `Common 규칙.md`에 있다(프로젝트에만 있는 문서다).
언제 무엇을 쓰는지·함정은 [`object-pool` 스킬](.claude/skills/client/object-pool/SKILL.md)에 있다.

| 파일 | 하는 일 |
|------|---------|
| `PrefabPool.cs` | 프리팹 하나의 풀 — `UnityEngine.Pool.ObjectPool<T>`를 감싼다. 짧게 사는 연출·이펙트용 |
| `PooledObject.cs` | 풀에서 나온 오브젝트의 표식 — 스스로 반납 · **대여 토큰** · 이중 반납 방어 |
| `IPoolable.cs` | 꺼낼 때(`OnRent`)·되돌릴 때(`OnReturn`) 알림을 받는 컴포넌트 |
| `UIRowList.cs` | 레이아웃 그룹 아래 목록 줄의 **제자리 풀** — 부모를 옮기지 않고 끄고 켠다 |

---

## 두 풀 중 무엇을 쓰나

| | `PrefabPool<T>` | `UIRowList<T>` |
|---|---|---|
| 대상 | 날아가는 아이콘 · 떠오르는 글자 · 이펙트 | 목록 줄 · 결과 칸 · 툴팁 줄 |
| 쉬는 자리 | `inactiveRoot`로 옮긴다 | 원래 부모 아래에서 꺼 둔다 |
| 순서 | 상관없다 (스택) | i번째 줄 = i번째 형제 |
| 반납 | 오브젝트가 스스로(`PooledObject.Release`) | 목록 주인이 `HideFrom(n)` |
| 넘치면 | `maxSize`를 넘은 것은 파괴 | 줄지 않는다 (최대 길이만큼만 자란다) |

---

## 의존성

- **`UnityEngine.Pool`(Unity 2021.1+)과 `destroyCancellationToken`(Unity 2022.2+)만 쓴다.**
  UniTask·DOTween을 참조하지 않는다 — 없는 프로젝트에서도 컴파일된다.
- 트윈 Kill은 각 `IPoolable` 구현이 맡는다(풀이 DOTween을 모르게).

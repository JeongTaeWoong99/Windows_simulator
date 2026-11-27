using System;
using Steamworks;
using UnityEngine;

// 스팀 업적의 단일 창구 — 게임 사건이 업적을 걸 때는 여기 'Unlock(키)'만 부른다 (T-214).
//
// ■ 왜 서비스인가
//   업적 저장(StoreStats) 결과는 콜백('UserStatsStored_t')으로 온다. 콜백은 'SteamAPI.RunCallbacks'를 불러야 오는데,
//   로그인 티켓('SteamAuth')은 기다리는 동안만 돌린다 — 그래서 상주하며 매 프레임 돌릴 곳이 필요하다('Steam 규칙.md').
//
// ■ 스팀이 없으면 조용히 지나간다
//   에디터 Id 로그인 · 스팀 없이 켠 빌드는 'SteamAuth.IsInitialized'가 false다. 그때 'Unlock'은 아무 일도 하지 않는다 —
//   게임 쪽이 스팀 상태를 따지지 않고 부르게 한다.
//
// ■ 아직 정하지 않은 것 (T-214)
//   업적 목록(기획) · 판정 위치(클라 이벤트 vs 서버 확정 값). 정해지면 'SteamAchievementKeys'에 키를 적고 사건 쪽에서 'Unlock'을 부른다.
//   지금 키는 앱 480(Spacewar) 시험용이다 — 우리 AppID(T-210)가 나오면 바꾼다.
public class SteamAchievementManager : MonoService<SteamAchievementManager>
{
    private Callback<UserStatsStored_t>? _storedCallback;

    private bool _isDirty; // 바꾼 업적이 있어 저장할 것이 남았다 — 한 프레임에 여러 개를 걸어도 저장은 한 번

    // 저장이 끝났다 (성공 여부) — 시험 메뉴 · 로그가 본다
    public event Action<bool>? Stored;

    // 스팀이 켜져 있어 업적을 걸 수 있다
    public bool IsAvailable => SteamAuth.IsInitialized;

    // 업적을 건다 — 이미 걸렸으면 스팀이 무시한다. 저장은 이 프레임 끝에 한 번 (게임 사건 · 시험 메뉴)
    public void Unlock(string key)
    {
        if (!IsAvailable)
        {
            return;
        }

        if (SteamUserStats.SetAchievement(key))
        {
            _isDirty = true;
        }
        else
        {
            Debug.LogWarning($"[업적] 걸기 실패 — 키 '{key}'가 이 앱(steam_appid.txt)에 없다");
        }
    }

    // 걸렸나 — 스팀이 없으면 false
    public bool IsUnlocked(string key)
    {
        return IsAvailable && SteamUserStats.GetAchievement(key, out bool achieved) && achieved;
    }

    // 업적을 되돌린다 — **시험 전용**(480 시험 메뉴). 출시 뒤 게임에서 부르지 않는다
    public void Clear(string key)
    {
        if (IsAvailable && SteamUserStats.ClearAchievement(key))
        {
            _isDirty = true;
        }
    }

    // 콜백을 돌리고, 바뀐 업적이 있으면 저장한다 (Unity 메시지)
    private void Update()
    {
        if (!IsAvailable)
        {
            return;
        }

        _storedCallback ??= Callback<UserStatsStored_t>.Create(OnStored);

        SteamAPI.RunCallbacks();

        if (_isDirty)
        {
            _isDirty = false;
            SteamUserStats.StoreStats();
        }
    }

    // 저장 결과 (스팀 콜백)
    private void OnStored(UserStatsStored_t result)
    {
        bool ok = result.m_eResult == EResult.k_EResultOK;

        if (!ok)
        {
            Debug.LogWarning($"[업적] 저장 실패 — {result.m_eResult}");
        }

        Stored?.Invoke(ok);
    }

    // 콜백 해제 (Unity 메시지)
    private void OnDestroy()
    {
        _storedCallback?.Dispose();
        _storedCallback = null;
    }
}

// 업적 키 — 스팀 개발자 사이트에 등록한 'API 이름'과 같아야 한다.
// ⚠️ 지금은 앱 480(Spacewar)의 시험 업적이다. 우리 목록이 정해지고 AppID가 나오면(T-210 · T-214) 통째로 바꾼다.
public static class SteamAchievementKeys
{
    public const string TestWinOneGame = "ACH_WIN_ONE_GAME";
}

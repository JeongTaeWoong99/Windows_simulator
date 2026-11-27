using UnityEditor;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
	// 에디터에서 스팀 업적을 시험하는 메뉴 (T-214 · 앱 480 Spacewar).
	// 플레이 중 아무 때나 — 스팀 초기화 → 시험 업적 걸기 / 되돌리기 → 저장 콜백을 콘솔에 남긴다.
	// 준비: 스팀 클라이언트 실행·로그인 · 프로젝트 루트 'steam_appid.txt'(480).
	// 확인: 스팀 클라이언트의 Spacewar 업적 목록 · 오버레이 알림(에디터에서는 오버레이가 안 뜰 수 있다).
	internal static class SteamAchievementTestMenu
	{
		private const string Root = "Window/DesktopWindowControl/스팀 업적 시험/";

		[MenuItem(Root + "시험 업적 걸기")]
		private static void Unlock() => Run(true);

		[MenuItem(Root + "시험 업적 되돌리기")]
		private static void Clear() => Run(false);

		[MenuItem(Root + "시험 업적 걸기", true)]
		[MenuItem(Root + "시험 업적 되돌리기", true)]
		private static bool CanRun() => EditorApplication.isPlaying;

		private static void Run(bool unlock)
		{
			if (!SteamAuth.TryInit(out string error))
			{
				Debug.LogWarning($"[업적 시험] 스팀 초기화 실패 — {error}");

				return;
			}

			var manager = Services.Get<SteamAchievementManager>();
			string key  = SteamAchievementKeys.TestWinOneGame;

			manager.Stored -= Log;
			manager.Stored += Log;

			if (unlock)
			{
				manager.Unlock(key);
			}
			else
			{
				manager.Clear(key);
			}

			Debug.Log($"[업적 시험] {(unlock ? "걸기" : "되돌리기")} 요청 — {key} · 지금 상태 {manager.IsUnlocked(key)}");
		}

		private static void Log(bool ok)
		{
			Debug.Log($"[업적 시험] 저장 {(ok ? "성공" : "실패")} — {SteamAchievementKeys.TestWinOneGame} = {Services.Get<SteamAchievementManager>().IsUnlocked(SteamAchievementKeys.TestWinOneGame)}");
		}
	}
}

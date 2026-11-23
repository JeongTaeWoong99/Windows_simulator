using UnityEditor;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
	// 에디터에서 스팀 로그인을 시험하는 메뉴 (T-211 시험 환경 · 이슈 #144 · #152).
	// 플레이 중 로그인 화면이 떠 있을 때 누르면 Id 대신 스팀 티켓으로 로그인한다.
	//
	// 준비: 스팀 클라이언트 실행·로그인 · 프로젝트 루트 'steam_appid.txt'(480) ·
	//       로컬 서버에 사용자 환경 변수 'Game__Steam__WebApiKey'(T-211 일감 — 서버 시작 로그 '키 있음').
	// 결과: 서버에 계정 'steam:<SteamID>'가 생기고 그대로 게임에 들어간다.
	internal static class SteamLoginTestMenu
	{
		private const string MenuPath = "Window/DesktopWindowControl/스팀 로그인 시험";

		[MenuItem(MenuPath)]
		private static void Run()
		{
			var login = Object.FindAnyObjectByType<LoginPresenter>();

			if (login == null || !login.isActiveAndEnabled)
			{
				EditorUtility.DisplayDialog("스팀 로그인 시험", "로그인 화면이 떠 있을 때만 쓸 수 있습니다.", "확인");

				return;
			}

			login.LoginWithSteam();
		}

		[MenuItem(MenuPath, true)]
		private static bool CanRun() => EditorApplication.isPlaying;
	}
}

using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using MikaProtocol;
using UnityEditor;
using Object = UnityEngine.Object;

namespace DesktopWindowControl.EditorTools
{
	// 치트를 보내기 전에 '지금 보내도 되는가'를 따지고, 안 되면 팝업으로 알린다. 보내는 일은 'CheatSender'가 한다.
	// ★ 버튼을 비활성화하지 않고 누른 순간 검사한다 — 회색 버튼은 '왜 안 되는지'를 말해 주지 않는다.
	// 준비 단계(서버 → 클라 → 로그인)와 판정 근거는 'cheat-console 규칙.md'의 "안전장치" 절.
	internal static class CheatGuard
	{
		private const string Title = "치트";

		// 준비 단계 — 작업 순서 그대로다. 앞 단계가 안 되면 뒤 단계도 될 수 없다.
		[Flags]
		public enum Step
		{
			None   = 0,
			Server = 1 << 0,   // 서버가 포트에서 대기 중
			Client = 1 << 1,   // Play 중이고 서버와 연결됨
			Login  = 1 << 2,   // 로그인 완료
		}

		public const Step AllSteps = Step.Server | Step.Client | Step.Login;

		// 창의 상태 줄용 — 서버·클라 단계(OS 연결 목록 조회)는 백그라운드에서 'OsProbeInterval'마다 묻고, 마지막 답을 쓴다.
		// ★ 2026-11-22 프로파일러 — 0.5초마다 메인 스레드에서 OS 연결 목록을 읽어 한 번에 4~7ms(가끔 GC가 겹쳐 20ms+)
		//   프레임을 막고 있었다. 144프레임(6.9ms)에서는 그 프레임이 통째로 밀린다. 로그인 단계는 씬을 봐야 해서 여기서 바로 본다.
		//   보내기 직전 검사('CanSend')는 정확해야 하므로 그대로 'GetCompletedSteps'를 쓴다.
		private const double OsProbeInterval = 2.0;

		private static Task<Step>? _osProbe;
		private static Step        _osSteps;
		private static double      _nextOsProbe;

		public static Step GetCompletedStepsPolled()
		{
			if (_osProbe is { IsCompleted: true })
			{
				_osSteps = _osProbe.Status == TaskStatus.RanToCompletion ? _osProbe.Result : Step.None;
				_osProbe = null;
			}

			if (_osProbe == null && EditorApplication.timeSinceStartup >= _nextOsProbe)
			{
				_nextOsProbe = EditorApplication.timeSinceStartup + OsProbeInterval;

				bool isPlaying = EditorApplication.isPlaying; // 메인 스레드에서만 읽힌다

				_osProbe = Task.Run(() => ProbeOs(isPlaying));
			}

			// 플레이를 막 끈 뒤 아직 옛 답이면 '클라'를 지운다
			var steps = EditorApplication.isPlaying ? _osSteps : _osSteps & ~Step.Client;

			return FindLoggedInModel() != null ? steps | Step.Login : steps;
		}

		// 서버·클라 단계만 — 다른 스레드에서 돈다(유니티 API를 부르지 않는다)
		private static Step ProbeOs(bool isPlaying)
		{
			var steps = Step.None;

			if (IsServerListening())
			{
				steps |= Step.Server;
			}

			if (isPlaying && IsClientConnected())
			{
				steps |= Step.Client;
			}

			return steps;
		}

		// 지금 완료된 단계 (CanSend — 보내기 직전이라 바로 묻는다)
		public static Step GetCompletedSteps()
		{
			var steps = Step.None;

			if (IsServerListening())
			{
				steps |= Step.Server;
			}

			if (EditorApplication.isPlaying && IsClientConnected())
			{
				steps |= Step.Client;
			}

			if (FindLoggedInModel() != null)
			{
				steps |= Step.Login;
			}

			return steps;
		}

		// 세 단계가 모두 끝났으면 true. 아니면 빠진 단계를 모아 한 팝업으로 알리고 false (CheatSender.Send가 부른다)
		public static bool CanSend()
		{
			var missing = AllSteps & ~GetCompletedSteps();

			if (missing == Step.None)
			{
				return true;
			}

			AlertMissing(missing);

			return false;
		}

		// 단계들을 작업 순서대로 "서버 · 클라 · 로그인" 꼴로 잇는다 (팝업 · 창의 상태 줄)
		public static string NamesOf(Step steps)
		{
			var names = new List<string>();

			if (steps.HasFlag(Step.Server))
			{
				names.Add("서버");
			}

			if (steps.HasFlag(Step.Client))
			{
				names.Add("클라");
			}

			if (steps.HasFlag(Step.Login))
			{
				names.Add("로그인");
			}

			return string.Join(" · ", names);
		}

		// 빠진 단계를 "서버 · 클라 · 로그인이 완료되지 않았다." 한 줄로 묶고, 가장 앞 단계를 풀 버튼을 붙인다.
		private static void AlertMissing(Step missing)
		{
			// 조사는 마지막 이름을 따른다 — 로그인(받침 있음)이 빠졌으면 늘 마지막이다.
			var particle = missing.HasFlag(Step.Login) ? "이" : "가";

			var message =
				$"{NamesOf(missing)}{particle} 완료되지 않았다.\n\n" +
				$"{Mark(Step.Server)} 서버 — 포트 {ServerRunner.ServerPort}에서 대기 중\n" +
				$"{Mark(Step.Client)} 클라 — Play 중이고 서버에 연결됨\n" +
				$"{Mark(Step.Login)} 로그인 — 게임 화면에서 로그인";

			// 클라는 Play 시작 때 한 번만 접속한다 — 서버보다 먼저 Play를 눌렀다면 Play를 다시 시작해야 한다.
			if (missing.HasFlag(Step.Client) && EditorApplication.isPlaying)
			{
				message += "\n\n서버를 켜기 전에 Play를 눌렀다면 Play를 멈췄다가 다시 시작한다.";
			}

			if (missing.HasFlag(Step.Server))
			{
				if (EditorUtility.DisplayDialog(Title, message, "서버 콘솔 열기", "닫기"))
				{
					ServerConsoleWindow.Open();
				}

				return;
			}

			if (missing.HasFlag(Step.Client) && !EditorApplication.isPlaying)
			{
				if (EditorUtility.DisplayDialog(Title, message, "Play 시작", "닫기"))
				{
					EditorApplication.isPlaying = true;
				}

				return;
			}

			EditorUtility.DisplayDialog(Title, message, "닫기");

			string Mark(Step step) => missing.HasFlag(step) ? "✗" : "✓";
		}

		// 서버가 포트에서 대기 중인지 — 판정은 'ServerRunner.IsPortListening'.
		// ※ 'ServerRunner.IsRunning'(PID 추적)은 서버 콘솔로 켠 것만 안다 — 그래서 포트를 본다.
		private static bool IsServerListening() => ServerRunner.IsPortListening(ServerRunner.ServerPort);

		// 서버 포트로 맺어진 TCP 연결이 있는지. Play 중일 때만 묻는다(창의 상태 줄 · CanSend).
		// ★ 'NetworkManager'가 연결 상태를 드러내지 않고 그 파일은 서버 담당 폴더라, OS의 연결 목록으로 판정한다.
		// ★ 상태값은 보지 않는다 — 이 PC는 맺어진 연결도 FinWait1 등으로 보고한다('ServerRunner.IsPortListening').
		//   그래서 방금 끊긴 연결이 남아 있거나 더미 클라이언트(MikaDummyClient)가 붙어 있으면 에디터가 끊겨도 '연결됨'으로 보인다 —
		//   그 경우는 보낸 뒤 응답 시간 초과('CheatSender')가 잡는다.
		private static bool IsClientConnected()
		{
			try
			{
				foreach (var connection in IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections())
				{
					if (connection.RemoteEndPoint.Port == ServerRunner.ServerPort)
					{
						return true;
					}
				}
			}
			catch (NetworkInformationException)
			{
				// 조회 실패는 '끊김'으로 본다 — 0.5초마다 도는 자리라 로그를 남기지 않는다.
			}

			return false;
		}

		// 로그인한 계정의 상태 캐시. 플레이 중이 아니거나 로그인 전이면 null (창이 보유 캐릭터·해금 표시에 쓴다)
		// ★ 'Services.Get'이 아니라 씬에서 직접 찾는다 — 'Get'은 미등록이면 예외를 던지고,
		//   에디터 창은 등록 순서와 무관한 시점(매 OnGUI)에 묻기 때문이다.
		public static PlayerDataModel? FindLoggedInModel()
		{
			if (!EditorApplication.isPlaying)
			{
				return null;
			}

			var model = Object.FindFirstObjectByType<PlayerDataModel>();

			if (model == null || !model.IsLoggedIn)
			{
				return null;
			}

			return model;
		}

		// 보낸 치트에 응답이 오지 않았음을 알린다 (CheatSender 시간 초과)
		public static void AlertTimeout(ECheatCommand command, double timeoutSeconds)
		{
			var open = EditorUtility.DisplayDialog
				(Title,
				 $"{command} — {timeoutSeconds:0}초 안에 서버 응답이 없다.\n서버가 꺼졌거나 연결이 끊겼을 수 있다.",
				 "서버 콘솔 열기", "닫기");

			if (open)
			{
				ServerConsoleWindow.Open();
			}
		}

		// 서버가 권한 없음으로 거절했음을 알린다 (S_CheatResponse 수신)
		public static void AlertNoPermission()
		{
			EditorUtility.DisplayDialog
				(Title,
				 "admin 계정이 아니다.\n\n치트는 't_user.admin_level ≥ 1'인 계정만 쓸 수 있다.\n" +
				 "로컬 DB에서 테스트 계정의 admin_level을 1로 올린 뒤 다시 로그인한다.",
				 "닫기");
		}
	}
}

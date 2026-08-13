using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DesktopWindowControl.EditorTools
{
	/// <summary>
	/// WSGameServer를 에디터에서 백그라운드로 켜고/끄는 프로세스 제어기. UI는 없다(창은 'ServerConsoleWindow').
	/// ★ stdout/stderr를 cmd의 '>' 리다이렉트로 로그 파일에 직접 흘려 담는다 —
	///    에디터가 스크립트를 재컴파일(도메인 리로드)해 static·콜백이 소멸해도 로그가 계속 쌓이고, 서버도 안 끊긴다.
	/// ★ 실행 중 프로세스 PID는 'SessionState'에 둔다 — 도메인 리로드를 넘어 살아남고 Unity 재시작 때 비워지므로 재부착에 맞다.
	/// ★ 'dotnet run'은 실제 서버를 자식으로 띄우므로 종료는 트리 전체를 'taskkill /T'로 내린다.
	/// </summary>
	internal static class ServerRunner
	{
		// 서버 프로젝트(.csproj) — 프로젝트 루트 기준 상대 경로
		private const string ServerProjectRelPath = "Server/WSGameServer/WSGameServer.csproj";
		// 서버가 요구하는 .NET SDK 최소 메이저 버전 (= WSGameServer.csproj의 net10.0). TFM을 올리면 같이 올린다.
		private const int RequiredSdkMajor = 10;
		// 서버가 여는 포트(WSGameServer 하드코딩). 크래시로 추적 PID를 잃은 orphan 서버 탐지에 쓴다.
		private const int ServerPort = 10050;
		// 서버 stdout/stderr를 담는 로그 파일 — 창이 tail 한다. 'Temp/'는 git 무시라 커밋되지 않는다.
		private const string LogFileRelPath = "Temp/WSGameServer.log";
		// 실행 중 프로세스(cmd) PID를 담는 세션 키
		private const string PidSessionKey = "DWC.ServerConsole.Pid";

		static ServerRunner()
		{
			// Unity를 닫을 때 서버가 떠돌지 않게 정리한다(SessionState가 지워져 PID를 잃으면 orphan이 된다).
			EditorApplication.quitting += StopIfRunning;
		}

		/// <summary>서버 로그 파일의 절대 경로. 창이 이 파일을 읽어 표시한다.</summary>
		public static string LogFilePath => Path.Combine(ProjectRoot, LogFileRelPath);

		private static string ProjectRoot => Directory.GetParent(Application.dataPath)!.FullName;

		/// <summary>저장된 PID의 프로세스가 살아있으면 true. 죽었으면 PID를 청소한다.</summary>
		public static bool IsRunning
		{
			get
			{
				var pid = SessionState.GetInt(PidSessionKey, 0);
				if (pid == 0)
					return false;

				try
				{
					var proc = Process.GetProcessById(pid);
					if (proc.HasExited)
					{
						SessionState.EraseInt(PidSessionKey);
						return false;
					}
					return true;
				}
				catch (ArgumentException)
				{
					// 그 PID의 프로세스가 이미 없다(서버가 스스로 종료됐거나 Unity 재시작 등)
					SessionState.EraseInt(PidSessionKey);
					return false;
				}
			}
		}

		/// <summary>서버를 백그라운드로 켠다. 이미 켜져 있으면 아무 것도 하지 않는다.</summary>
		public static void Start()
		{
			if (IsRunning)
			{
				Debug.LogWarning("[서버 콘솔] 이미 서버가 실행 중이다.");
				return;
			}

			// 추적하는 서버는 없는데 포트가 열려 있다 = 이전 세션(에디터 크래시 등)의 orphan 서버가 남아 있다.
			// SessionState는 Unity 재시작 때 비워져 PID를 잃으므로, 크래시 후엔 이 툴이 orphan을 추적하지 못한다.
			// 그대로 새로 띄우면 포트 바인딩에 실패하니, 정리할지 물어보고 진행한다.
			if (IsServerPortInUse())
			{
				var cleanup = EditorUtility.DisplayDialog(
					"서버 콘솔",
					$"포트 {ServerPort}이(가) 이미 사용 중이다.\n" +
					"이전 세션(에디터 비정상 종료 등)에서 남은 서버일 수 있다.\n\n" +
					"그 프로세스를 종료하고 새로 시작할까?",
					"종료 후 시작", "취소");

				if (!cleanup)
				{
					Debug.LogWarning($"[서버 콘솔] 포트 {ServerPort} 사용 중 — 시작을 취소했다.");
					return;
				}

				KillByPort(ServerPort);
			}

			var root   = ProjectRoot;
			var csproj = Path.Combine(root, ServerProjectRelPath);
			var log    = Path.Combine(root, LogFileRelPath);

			if (!File.Exists(csproj))
			{
				Debug.LogError($"[서버 콘솔] 서버 프로젝트를 찾을 수 없다: {csproj}");
				return;
			}

			var dotnet = ResolveDotnetExe();

			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(log)!);

				// '/S /C "..."'는 맨 앞·뒤 따옴표만 벗겨 안쪽 경로 따옴표를 보존한다(경로에 공백이 있어도 안전).
				// 'chcp 65001'로 콘솔 코드 페이지를 UTF-8로 맞춰 한글 로그가 깨지지 않게 한다(창은 UTF-8로 읽는다).
				// '>'는 dotnet의 stdout을, '2>&1'은 stderr까지 로그 파일로 보낸다.
				//
				// ★ UseShellExecute=true + WindowStyle=Hidden 으로 '숨겨진 콘솔'을 준다.
				//   서버(Program.cs)는 기동 후 'Console.ReadLine()'으로 대기하는데, 콘솔(stdin)이 없으면
				//   즉시 EOF(null)를 받아 곧바로 종료해 버린다(→ 서버가 바로 꺼지고 접속이 거부된다).
				//   숨겨진 콘솔이 있으면 ReadLine이 정상적으로 블록해 서버가 계속 살아 있고, 이 프로세스는
				//   부모(에디터)와 독립이라 도메인 리로드도 견딘다. (CreateNoWindow는 ShellExecute에선 무시되므로 WindowStyle을 쓴다.)
				var psi = new ProcessStartInfo("cmd.exe")
				{
					Arguments        = $"/S /C \"chcp 65001>nul && \"{dotnet}\" run --project \"{csproj}\" > \"{log}\" 2>&1\"",
					WorkingDirectory = root,
					UseShellExecute  = true,
					WindowStyle      = ProcessWindowStyle.Hidden,
				};

				var proc = Process.Start(psi);
				if (proc == null)
				{
					Debug.LogError("[서버 콘솔] 서버 프로세스를 시작하지 못했다.");
					return;
				}

				SessionState.SetInt(PidSessionKey, proc.Id);
				Debug.Log($"[서버 콘솔] 서버 시작 (pid {proc.Id}, dotnet: {dotnet}).");
			}
			catch (Exception e)
			{
				Debug.LogError($"[서버 콘솔] 서버 시작 실패: {e.Message}");
			}
		}

		/// <summary>실행 중인 서버 프로세스 트리를 종료한다.</summary>
		public static void Stop()
		{
			var pid = SessionState.GetInt(PidSessionKey, 0);
			if (pid == 0)
			{
				Debug.LogWarning("[서버 콘솔] 종료할 서버가 없다.");
				return;
			}

			KillTree(pid);
			SessionState.EraseInt(PidSessionKey);
			Debug.Log("[서버 콘솔] 서버 종료 요청 완료.");
		}

		// dotnet run이 띄운 자식(dotnet→WSGameServer)까지 함께 내리려고 트리 종료('/T')로 죽인다.
		private static void KillTree(int pid)
		{
			try
			{
				var psi = new ProcessStartInfo("taskkill")
				{
					Arguments       = $"/PID {pid} /T /F",
					UseShellExecute = false,
					CreateNoWindow  = true,
				};
				using var kill = Process.Start(psi);
				kill?.WaitForExit(3000);
			}
			catch (Exception e)
			{
				Debug.LogError($"[서버 콘솔] 서버 종료 실패: {e.Message}");
			}
		}

		// 서버는 net10.0라 .NET 10 SDK가 필요하다. 그런데 .NET 10 SDK가 사용자 로컬(~/.dotnet)에만 깔려 있고
		// PATH의 dotnet(Program Files)은 구 SDK인 환경이 있다(Rider 터미널은 ~/.dotnet을 써서 되지만,
		// Unity가 띄운 cmd는 PATH의 구 SDK를 잡아 NETSDK1045로 실패한다). 그래서 요구 SDK를 가진
		// ~/.dotnet\dotnet.exe를 먼저 쓰고, 조건이 안 맞으면 PATH의 dotnet에 맡긴다.
		private static string ResolveDotnetExe()
		{
			var userRoot = Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet");
			var userExe = Path.Combine(userRoot, "dotnet.exe");

			if (File.Exists(userExe) && HasRequiredSdk(Path.Combine(userRoot, "sdk")))
				return userExe;

			return "dotnet"; // PATH에 맡긴다(Program Files에 최신 SDK가 있는 환경)
		}

		// 지정한 sdk 폴더에 메이저 버전이 RequiredSdkMajor 이상인 SDK가 하나라도 있으면 true.
		private static bool HasRequiredSdk(string sdkDir)
		{
			if (!Directory.Exists(sdkDir))
				return false;

			foreach (var dir in Directory.GetDirectories(sdkDir))
			{
				var name = Path.GetFileName(dir);
				var dot  = name.IndexOf('.');
				var majorStr = dot > 0 ? name.Substring(0, dot) : name;
				if (int.TryParse(majorStr, out var major) && major >= RequiredSdkMajor)
					return true;
			}
			return false;
		}

		// 서버 포트가 이미 LISTENING 상태인지 검사한다(외부 라이브러리·프로세스 없이 판별).
		private static bool IsServerPortInUse()
		{
			try
			{
				var listeners = System.Net.NetworkInformation.IPGlobalProperties
					.GetIPGlobalProperties().GetActiveTcpListeners();
				foreach (var ep in listeners)
					if (ep.Port == ServerPort)
						return true;
			}
			catch (Exception e)
			{
				Debug.LogWarning($"[서버 콘솔] 포트 사용 여부 확인 실패(무시): {e.Message}");
			}
			return false;
		}

		// 포트를 LISTENING으로 점유한 프로세스를 netstat로 찾아 트리 종료한다(추적 PID를 잃은 orphan 정리용).
		private static void KillByPort(int port)
		{
			foreach (var pid in FindListeningPids(port))
				KillTree(pid);
		}

		// 'netstat -ano'를 파싱해 해당 포트를 LISTENING 중인 PID들을 모은다.
		private static IEnumerable<int> FindListeningPids(int port)
		{
			var pids = new HashSet<int>();
			try
			{
				var psi = new ProcessStartInfo("netstat")
				{
					Arguments              = "-ano -p tcp",
					UseShellExecute        = false,
					RedirectStandardOutput = true,
					CreateNoWindow         = true,
				};

				using var p = Process.Start(psi);
				if (p == null)
					return pids;

				var output = p.StandardOutput.ReadToEnd();
				p.WaitForExit(3000);

				// 예: "  TCP    0.0.0.0:10050    0.0.0.0:0    LISTENING    12345"
				//   LISTENING 줄의 로컬 주소에만 ':<port>'가 나타난다(원격 주소는 :0).
				foreach (var line in output.Split('\n'))
				{
					if (line.IndexOf("LISTENING", StringComparison.OrdinalIgnoreCase) < 0)
						continue;
					if (!line.Contains($":{port}"))
						continue;

					var parts = line.Split(new[] { ' ', '\t', '\r' }, StringSplitOptions.RemoveEmptyEntries);
					if (parts.Length > 0 && int.TryParse(parts[^1], out var pid) && pid > 0)
						pids.Add(pid);
				}
			}
			catch (Exception e)
			{
				Debug.LogError($"[서버 콘솔] 포트 점유 프로세스 조회 실패: {e.Message}");
			}
			return pids;
		}

		private static void StopIfRunning()
		{
			if (IsRunning)
				Stop();
		}
	}
}

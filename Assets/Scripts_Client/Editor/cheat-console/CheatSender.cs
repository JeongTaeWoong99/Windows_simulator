using System;
using System.Collections.Generic;
using MikaNetwork;
using MikaProtocol;
using UnityEditor;

namespace DesktopWindowControl.EditorTools
{
	// 치트 패킷을 보내고, 응답을 기다리고, 결과를 로그로 쌓는다. UI는 없다(창은 'CheatWindow').
	// ★ 창이 아니라 여기가 상태를 갖는다 — 창을 닫았다 열어도 로그가 남고, 창이 닫혀 있어도 시간 초과를 잡는다.
	// ★ 응답이 안 오는 것으로 연결 끊김을 잡는다. 'NetworkManager'가 연결 상태를 드러내지 않고
	//   그 파일은 서버 담당 폴더라 여기서 고치지 않는다(→ cheat-console 규칙.md).
	[InitializeOnLoad]
	internal static class CheatSender
	{
		private const double ResponseTimeout = 3.0;   // 초 — 로컬 서버라 정상이면 한 프레임 안에 온다
		private const int    MaxLogEntries   = 50;

		public readonly struct LogEntry
		{
			public readonly string Time;
			public readonly string Text;
			public readonly bool   IsError;

			public LogEntry(string text, bool isError)
			{
				Time    = DateTime.Now.ToString("HH:mm:ss");
				Text    = text;
				IsError = isError;
			}
		}

		// 보냈지만 아직 응답이 오지 않은 요청. 같은 명령을 연달아 보낼 수 있어 순서대로 쌓는다.
		private readonly struct Pending
		{
			public readonly ECheatCommand Command;
			public readonly double        SentAt;

			public Pending(ECheatCommand command, double sentAt)
			{
				Command = command;
				SentAt  = sentAt;
			}
		}

		private static readonly List<Pending>  PendingRequests = new();
		private static readonly List<LogEntry> Entries         = new();

		public static IReadOnlyList<LogEntry> Log => Entries;

		// 로그가 바뀌었다 (창이 다시 그린다)
		public static event Action? LogChanged;

		static CheatSender()
		{
			// 도메인 리로드를 끈 프로젝트에서는 정적 생성자가 다시 불리지 않아도 구독이 살아 있다 — 빼고 다시 걸어 중복을 막는다.
			ServerPacketHandler.CheatResponded -= OnCheatResponded;
			ServerPacketHandler.CheatResponded += OnCheatResponded;

			EditorApplication.update -= CheckTimeout;
			EditorApplication.update += CheckTimeout;

			EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
		}

		// 안전장치를 통과하면 치트를 보낸다 (창의 [지급]·[열기] 버튼)
		public static void Send(ECheatCommand command, long arg1 = 0, long arg2 = 0)
		{
			if (!CheatGuard.CanSend())
			{
				return;
			}

			NetworkManager.Instance.Send(new C_CheatRequest { Command = command, Arg1 = arg1, Arg2 = arg2 });
			PendingRequests.Add(new Pending(command, EditorApplication.timeSinceStartup));

			Append($"→ {command}({arg1}, {arg2})", isError: false);
		}

		public static void ClearLog()
		{
			Entries.Clear();
			LogChanged?.Invoke();
		}

		// 치트 응답 도착 (ServerPacketHandler.CheatResponded 구독)
		private static void OnCheatResponded(S_CheatResponse res)
		{
			var index = PendingRequests.FindIndex(pending => pending.Command == res.Command);

			if (index >= 0)
			{
				PendingRequests.RemoveAt(index);
			}

			var isError = res.Result != EResultCode.Ok;
			Append($"← {res.Command} {res.Result}  {res.Message}", isError);

			if (res.Result == EResultCode.NoPermission)
			{
				CheatGuard.AlertNoPermission();
			}
		}

		// 오래 기다린 요청이 있으면 한 번만 알리고 전부 버린다 (EditorApplication.update 구독)
		// 연결이 끊기면 대기 중인 요청이 한꺼번에 초과되므로 팝업을 하나로 합친다.
		private static void CheckTimeout()
		{
			if (PendingRequests.Count == 0)
			{
				return;
			}

			// 일시정지 중에는 'NetworkManager.Update'가 돌지 않아 도착한 응답도 큐에 묶여 있다 — 시계를 멈춘다.
			if (EditorApplication.isPaused)
			{
				for (var i = 0; i < PendingRequests.Count; i++)
				{
					PendingRequests[i] = new Pending(PendingRequests[i].Command, EditorApplication.timeSinceStartup);
				}

				return;
			}

			var oldest = PendingRequests[0];

			if (EditorApplication.timeSinceStartup - oldest.SentAt < ResponseTimeout)
			{
				return;
			}

			PendingRequests.Clear();
			Append($"✕ {oldest.Command} 응답 없음 ({ResponseTimeout:0}초)", isError: true);

			CheatGuard.AlertTimeout(oldest.Command, ResponseTimeout);
		}

		// 플레이를 나가면 기다리던 요청을 버린다 (playModeStateChanged 구독)
		// 정지 뒤에 '응답 없음' 팝업이 뜨면 안 된다 — 끊긴 게 아니라 끈 것이다.
		private static void OnPlayModeStateChanged(PlayModeStateChange state)
		{
			if (state != PlayModeStateChange.ExitingPlayMode)
			{
				return;
			}

			PendingRequests.Clear();
		}

		private static void Append(string text, bool isError)
		{
			Entries.Add(new LogEntry(text, isError));

			if (Entries.Count > MaxLogEntries)
			{
				Entries.RemoveAt(0);
			}

			LogChanged?.Invoke();
		}
	}
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
	/// <summary>
	/// 'Test Copy' 복사본이 오리지널 씬의 최신 커밋보다 낡았는지 검사해 팝업으로 알린다.
	/// 검사 시점은 '유니티로 포커스가 돌아올 때'다 — 터미널에서 'git pull' 하고 창을 눌러 돌아오는
	/// 순간을 자동으로 잡는다(이슈 #14의 '무조건 버튼을 눌러야 하는' 불편 해소).
	/// 낡음을 감지하면 '[지금 복사]'로 그 자리에서 'OriginalSceneCopier.Copy()'를 부를 수 있다.
	/// </summary>
	[InitializeOnLoad]
	internal static class SceneCopyFreshnessChecker
	{
		private const string StatePath = OriginalSceneCopier.DestDir + "/" + OriginalSceneCopier.StateName;

		// 같은 상태로 반복해서 잔소리하지 않도록, 마지막으로 경고한 시그니처를 세션에 저장한다.
		private const string WarnedKey       = "DWC.SceneCopyFreshness.LastWarnedSignature";
		private const double ThrottleSeconds = 2.0; // 포커스 이벤트 중복 방지

		private static double _lastCheck;

		static SceneCopyFreshnessChecker()
		{
			EditorApplication.focusChanged += OnFocusChanged;
		}

		// 유니티로 포커스가 돌아올 때 검사 (EditorApplication.focusChanged 구독)
		private static void OnFocusChanged(bool focused)
		{
			if (!focused)
			{
				return;
			}

			// 개인 설정으로 꺼 뒀으면 여기서 끝낸다 — git 프로세스조차 돌지 않게 가장 이른 자리에서 막는다.
			// (구독 자체는 유지한다. 설정을 다시 켤 때 에디터 재시작이 필요 없도록.)
			if (!SceneCopySettings.AutoCheckEnabled)
			{
				return;
			}

			var now = EditorApplication.timeSinceStartup;

			if (now - _lastCheck < ThrottleSeconds)
			{
				return;
			}

			_lastCheck = now;
			CheckAndMaybeWarn();
		}

		// 낡음 검사 진입점 — 검사 불가한 상황을 먼저 걸러내고, 나머지는 예외로부터 감싼다.
		private static void CheckAndMaybeWarn()
		{
			// 아직 한 번도 복사하지 않았으면 비교 기준이 없다 — 조용히 넘어간다(첫 복사 전 잔소리 금지).
			if (!File.Exists(StatePath))
			{
				return;
			}

			// 오리지널 폴더가 통째로 사라졌으면 비교할 대상이 없다 — 조용히 넘어간다.
			// (없는 폴더에 Directory.GetFiles를 부르면 예외가 나 포커스마다 터진다. 그걸 막는다.)
			if (!Directory.Exists(OriginalSceneCopier.SourceDir))
			{
				return;
			}

			// 포커스가 돌아올 때마다 도는 자리라, 예기치 못한 예외로 콘솔이 도배되지 않게 통째로 감싼다.
			try
			{
				CompareAndPopup();
			}
			catch (Exception e)
			{
				Debug.LogWarning($"[복사본 최신성 검사] 검사 중 예외 — 이번 검사는 건너뛴다: {e.Message}");
			}
		}

		// 기록된 해시와 오리지널의 현재 해시를 비교해, 낡았으면 '[지금 복사]' 팝업을 띄운다.
		private static void CompareAndPopup()
		{
			var recorded = ReadState();

			// 오리지널 씬들의 '현재' 최신 커밋 해시. 커밋 이력이 있는(git이 해시를 준) 씬만 담는다.
			// git이 아예 안 돌면 dict가 비어 gitOk가 false로 남고, 그때는 판단을 포기한다(오탐 방지).
			var current = new Dictionary<string, string>();
			var gitOk   = false;

			foreach (var path in Directory.GetFiles(OriginalSceneCopier.SourceDir, "*.unity"))
			{
				var name = Path.GetFileName(path);
				var hash = EditorGit.LatestCommitOf($"{OriginalSceneCopier.SourceDir}/{name}");

				if (hash == null)
				{
					continue; // 미커밋 씬 또는 git 실패 — 비교에서 제외
				}

				current[name] = hash;
				gitOk = true;
			}

			if (!gitOk)
			{
				return; // git 사용 불가 — 비교할 수 없으므로 알리지 않는다
			}

			var stale = new List<string>();

			// 추가·변경: 현재 커밋된 씬이 기록에 없거나 해시가 달라졌으면 낡음.
			foreach (var kv in current)
			{
				if (!recorded.TryGetValue(kv.Key, out var old) || old != kv.Value)
				{
					stale.Add(kv.Key);
				}
			}

			// 삭제: 기록에 있으나 원본 파일이 사라졌으면 낡음(파일 존재로만 판단 — 미커밋과 구분).
			foreach (var name in recorded.Keys)
			{
				if (!File.Exists($"{OriginalSceneCopier.SourceDir}/{name}"))
				{
					stale.Add(name);
				}
			}

			if (stale.Count == 0)
			{
				return;
			}

			// 같은 상태로 이미 경고했으면 다시 띄우지 않는다.
			var signature = Signature(current);

			if (SessionState.GetString(WarnedKey, "") == signature)
			{
				return;
			}

			SessionState.SetString(WarnedKey, signature);

			var msg = new StringBuilder();

			msg.AppendLine("오리지널 씬이 복사본보다 최신 커밋을 갖고 있다.");
			msg.AppendLine("복사본이 낡았으니 다시 복사하는 것을 권장함.\n");
			msg.AppendLine("바뀐 씬:");

			foreach (var name in stale)
			{
				msg.AppendLine($"  · {name}");
			}

			// DisplayDialogComplex의 인자 순서는 'ok / cancel / alt'이고 반환값은 0 / 1 / 2다.
			// 버튼이 화면에 놓이는 순서는 플랫폼마다 다르지만, 반환값으로 분기하므로 동작은 같다.
			switch (EditorUtility.DisplayDialogComplex
			        ("복사본이 낡음.", msg.ToString(), "지금 복사", "나중에", "다시 알리지 않기"))
			{
				case 0:
					OriginalSceneCopier.Copy();
					break;

				case 2:
					SceneCopySettings.AutoCheckEnabled = false;

					EditorUtility.DisplayDialog
						("자동 알림 끔",
						 $"복사본 최신성 자동 알림을 껐다.\n{SceneCopySettings.ReEnableHint}\n\n" +
						 "상단 툴바의 '오리지널 씬 복사' 버튼은 그대로 쓸 수 있다.",
						 "확인");
					break;
			}
		}

		/// <summary>상태 파일을 "씬파일명 → 커밋해시" 사전으로 읽는다. 한 줄 = "이름\t해시".</summary>
		private static Dictionary<string, string> ReadState()
		{
			var map = new Dictionary<string, string>();

			foreach (var line in File.ReadAllLines(StatePath))
			{
				var tab = line.IndexOf('\t');

				if (tab <= 0)
				{
					continue;
				}

				map[line.Substring(0, tab)] = line.Substring(tab + 1).Trim();
			}

			return map;
		}

		/// <summary>현재 커밋 상태를 한 문자열로. 동일 상태 재경고를 막는 데만 쓴다.</summary>
		private static string Signature(Dictionary<string, string> current)
		{
			var keys = new List<string>(current.Keys);

			keys.Sort();

			var sb = new StringBuilder();

			foreach (var k in keys)
			{
				sb.Append(k).Append('=').Append(current[k]).Append(';');
			}

			return sb.ToString();
		}
	}
}

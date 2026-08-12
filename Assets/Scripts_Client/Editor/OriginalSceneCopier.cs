using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DesktopWindowControl.EditorTools
{
	/// <summary>
	/// 'Scenes/Original'의 씬을 'Scenes/Test Copy'로 복사하는 에디터 툴.
	/// 원본을 직접 열지 않고 각자 로컬 사본에서 테스트하기 위한 것이다 (이슈 #14).
	/// 실제 버튼은 'OriginalSceneCopyToolbarButton'이 상단 툴바에 얹고, 여기는 동작만 갖는다.
	/// </summary>
	public static class OriginalSceneCopier
	{
		// 프로젝트 루트 기준 경로. 자리를 옮기면 함께 고친다.
		private const string SourceDir  = "Assets/Scenes/Original";
		private const string DestDir    = "Assets/Scenes/Test Copy";
		private const string ReadmeName = "README.md";

		[MenuItem("Tools/오리지널 씬 복사 %#c")]
		public static void CopyFromMenu() => Copy();

		/// <summary>
		/// 'Original'의 모든 '.unity'를 'Test Copy'로 덮어써 최신화하고 README에 이력을 남긴다.
		/// 사본은 '.gitignore'로 커밋에서 제외되므로 충돌이 나지 않는다.
		/// </summary>
		public static void Copy()
		{
			if (!AssetDatabase.IsValidFolder(SourceDir))
			{
				EditorUtility.DisplayDialog("오리지널 씬 복사", $"원본 폴더가 없다:\n{SourceDir}", "확인");
				return;
			}

			EnsureFolder(DestDir);

			// 최신화 — 목적지의 기존 씬을 먼저 비운다. 원본에서 지워지거나 이름이 바뀐 씬이 남지 않도록.
			foreach (var stale in Directory.GetFiles(DestDir, "*.unity"))
				AssetDatabase.DeleteAsset(ToAssetPath(stale));

			var copied = new List<string>();
			foreach (var srcAbs in Directory.GetFiles(SourceDir, "*.unity"))
			{
				var src  = ToAssetPath(srcAbs);
				var dst  = $"{DestDir}/{Path.GetFileName(srcAbs)}";
				// CopyAsset은 사본에 새 GUID를 부여한다 — '.meta'를 그대로 복사하면 원본과 GUID가 겹쳐 충돌한다.
				if (AssetDatabase.CopyAsset(src, dst))
					copied.Add(Path.GetFileName(srcAbs));
				else
					Debug.LogError($"[오리지널 씬 복사] 복사 실패: {src} → {dst}");
			}

			WriteReadme(copied);
			AssetDatabase.Refresh();

			if (copied.Count == 0)
			{
				EditorUtility.DisplayDialog("오리지널 씬 복사", $"복사할 '.unity'가 없다:\n{SourceDir}", "확인");
				return;
			}

			var msg = $"{copied.Count}개 씬을 최신본으로 복사했다.\n\n대상: {DestDir}\n시각: {Now()}";
			EditorUtility.DisplayDialog("오리지널 씬 복사 완료", msg, "확인");
			Debug.Log($"[오리지널 씬 복사] {copied.Count}개 복사 완료 → {DestDir} ({Now()})");
		}

		private static void WriteReadme(List<string> copied)
		{
			var branch = RunGit("rev-parse", "--abbrev-ref", "HEAD");
			var head   = RunGit("log", "-1", "--format=%h  %cd  %s", "--date=format:%Y-%m-%d %H:%M");

			var sb = new StringBuilder();
			sb.AppendLine("# Test Copy — 오리지널 씬 복사본 (로컬 전용)");
			sb.AppendLine();
			sb.AppendLine("> 이 폴더는 상단 툴바 '오리지널 씬 복사' 버튼이 자동으로 채운다. 손으로 고치지 않는다.");
			sb.AppendLine("> `.gitignore`에 걸려 **커밋되지 않는다** — 각자 로컬에서만 테스트한다 (이슈 #14).");
			sb.AppendLine("> 원본이 갱신되면 버튼을 다시 눌러 최신화한다.");
			sb.AppendLine();
			sb.AppendLine("## 마지막 복사 정보");
			sb.AppendLine();
			sb.AppendLine($"- 복사 시각: {Now()}");
			sb.AppendLine($"- 원본 위치: {SourceDir}");
			sb.AppendLine($"- 프로젝트 버전(git 브랜치): {branch ?? "정보 없음"}");
			sb.AppendLine($"- 프로젝트 버전(git HEAD): {head ?? "정보 없음"}");
			sb.AppendLine("- 복사한 씬:");
			foreach (var name in copied)
			{
				var mtime = File.GetLastWriteTime(Path.Combine(SourceDir, name)).ToString("yyyy-MM-dd HH:mm:ss");
				sb.AppendLine($"  - {name}  (원본 최종 수정: {mtime})");
			}

			File.WriteAllText(Path.Combine(DestDir, ReadmeName), sb.ToString(), new UTF8Encoding(false));
		}

		// ── 유틸 ──

		private static string Now() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

		// 절대/OS 경로를 Unity의 "Assets/..." 경로로. AssetDatabase API는 이 형식만 받는다.
		private static string ToAssetPath(string osPath) => osPath.Replace('\\', '/');

		private static void EnsureFolder(string assetFolder)
		{
			if (AssetDatabase.IsValidFolder(assetFolder))
				return;
			var parent = Path.GetDirectoryName(assetFolder)!.Replace('\\', '/');
			AssetDatabase.CreateFolder(parent, Path.GetFileName(assetFolder));
		}

		/// <summary>git을 실행해 표준출력을 돌려준다. git이 없거나 저장소가 아니면 'null'.</summary>
		private static string? RunGit(params string[] args)
		{
			try
			{
				var projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
				var psi = new ProcessStartInfo("git")
				{
					WorkingDirectory       = projectRoot,
					RedirectStandardOutput = true,
					RedirectStandardError  = true,
					UseShellExecute        = false,
					CreateNoWindow         = true,
				};
				foreach (var a in args) // ArgumentList로 넘겨 공백 있는 인자('--date=format:...')가 쪼개지지 않게 한다
					psi.ArgumentList.Add(a);
				using var proc = Process.Start(psi);
				if (proc == null)
					return null;
				var output = proc.StandardOutput.ReadToEnd().Trim();
				proc.WaitForExit(3000);
				return proc.ExitCode == 0 && output.Length > 0 ? output : null;
			}
			catch
			{
				return null; // git 미설치 등 — 버전 정보 없이도 복사는 진행한다
			}
		}
	}
}

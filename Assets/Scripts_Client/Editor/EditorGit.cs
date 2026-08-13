using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
	/// <summary>
	/// 에디터 툴이 공용으로 쓰는 git 실행 헬퍼.
	/// 씬 복사기(이력 기록)와 최신성 검사기(해시 비교)가 함께 쓴다.
	/// </summary>
	internal static class EditorGit
	{
		/// <summary>git을 실행해 표준출력을 돌려준다. git이 없거나 저장소가 아니면 'null'.</summary>
		public static string? Run(params string[] args)
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
				
				// ArgumentList로 넘겨 공백 있는 인자('--date=format:...')가 쪼개지지 않게 한다
				foreach (var a in args)
					psi.ArgumentList.Add(a);
				
				using var proc = Process.Start(psi);

				if (proc == null)
				{
					return null;
				}
				
				var output = proc.StandardOutput.ReadToEnd().Trim();
				proc.WaitForExit(3000);
				
				return proc.ExitCode == 0 && output.Length > 0 ? output : null;
			}
			catch
			{
				return null; // git 미설치 등 — 버전 정보 없이도 호출측이 진행할 수 있게 한다
			}
		}

		/// <summary>해당 asset 경로를 마지막으로 건드린 커밋의 전체 해시. 이력이 없거나 git 실패 시 'null'.</summary>
		public static string? LatestCommitOf(string assetPath) =>
			Run("log", "-1", "--format=%H", "--", assetPath);
	}
}

using System.Diagnostics;
using System.IO;
using UnityEngine;

// 에디터 툴이 공용으로 쓰는 git 실행 헬퍼.
// 에셋의 마지막 커밋 해시를 읽는 툴(복사본 최신성 검사 등)이 함께 쓴다.
internal static class EditorGit
{
	// git을 실행해 표준출력을 돌려준다. git이 없거나 저장소가 아니면 'null'.
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
			{
				psi.ArgumentList.Add(a);
			}
			
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

	// 해당 asset 경로를 마지막으로 건드린 커밋의 전체 해시. 이력이 없거나 git 실패 시 'null'.
	public static string? LatestCommitOf(string assetPath) =>
		Run("log", "-1", "--format=%H", "--", assetPath);
}

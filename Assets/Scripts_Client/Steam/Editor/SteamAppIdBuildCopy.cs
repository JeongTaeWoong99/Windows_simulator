using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
	// 빌드가 끝나면 프로젝트 루트 'steam_appid.txt'를 exe 옆에 복사한다 (T-211).
	// exe를 직접 실행할 때 스팀이 이 파일로 AppID를 안다 — 없으면 'SteamAPI.InitEx'가 실패해 [접속하기]가 "스팀이 켜져 있지 않다"로 떨어진다.
	//
	// ⚠️ 스팀에 올리는 빌드(③ 라이브러리 실행)에는 넣지 않는다 — 스팀이 AppID를 넘긴다. 우리 AppID가 나오면(T-210) 올리는 빌드에서 빼는 길을 만든다.
	internal sealed class SteamAppIdBuildCopy : IPostprocessBuildWithReport
	{
		private const string FileName = "steam_appid.txt";

		public int callbackOrder => 0;

		public void OnPostprocessBuild(BuildReport report)
		{
			var target = report.summary.platform;

			if (target != BuildTarget.StandaloneWindows && target != BuildTarget.StandaloneWindows64)
			{
				return;
			}

			var source = Path.Combine(Directory.GetCurrentDirectory(), FileName);

			if (!File.Exists(source))
			{
				Debug.LogWarning($"[스팀] 프로젝트 루트에 {FileName}이 없어 빌드에 복사하지 못했다 — exe 직접 실행은 스팀 초기화에 실패한다.");

				return;
			}

			var buildDir = Path.GetDirectoryName(report.summary.outputPath)!;
			File.Copy(source, Path.Combine(buildDir, FileName), overwrite: true);
			Debug.Log($"[스팀] {FileName}을 빌드 폴더에 복사했다 — {buildDir}");
		}
	}
}

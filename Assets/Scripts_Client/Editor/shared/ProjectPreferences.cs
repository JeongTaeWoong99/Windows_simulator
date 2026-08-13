using UnityEditor;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
	/// <summary>
	/// '환경 설정(Preferences)' 목록 **맨 위**에 두는 이 프로젝트 전용 설정 그룹의 뿌리.
	/// 새 설정은 경로를 'RootPath + "/하위이름"'으로 잡아 이 아래에 붙인다.
	///
	/// ★ 정렬은 **경로 조각**으로, 표시는 **label**로 한다(유니티 'SettingsTreeView' 구현).
	///   경로의 '__' 접두사는 순전히 정렬용이다 — 문화권 비교에서 기호가 숫자·문자보다 앞서므로
	///   유니티 기본 '일반'(경로가 '_General')보다도 위에 온다. 화면에는 label만 나온다.
	/// </summary>
	internal static class ProjectPreferences
	{
		/// <summary>하위 설정들의 경로 접두사. 화면에 보이는 이름은 'GroupLabel'이다.</summary>
		public const string RootPath = "Preferences/__DesktopWindowControl";

		private const string GroupLabel = "데스크탑 윈도우 컨트롤";

		/// <summary>안내 문구에 쓰는 사람이 읽는 경로(한글 에디터 기준).</summary>
		public const string MenuHint = "편집 > 환경 설정 > " + GroupLabel;

		[SettingsProvider]
		private static SettingsProvider CreateGroup()
		{
			return new SettingsProvider(RootPath, SettingsScope.User)
			{
				label = GroupLabel,

				keywords = new[] { "프로젝트", "전용", "에디터", "Desktop", "Window", "Control" },

				guiHandler = _ =>
				{
					EditorGUILayout.Space();

					EditorGUILayout.HelpBox
						("이 아래는 이 프로젝트 전용으로 만든 에디터 설정이다.\n" +
						 "모두 이 컴퓨터에만 저장되며(EditorPrefs) 커밋되지 않는다 — 사람마다 따로 잡는다.\n\n" +
						 "왼쪽에서 항목을 골라 설정한다.",
						 MessageType.Info);
				},
			};
		}
	}
}

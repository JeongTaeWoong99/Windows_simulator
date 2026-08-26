using UnityEditor;
using UnityEngine;

// '환경 설정(Preferences)' 목록 **맨 위**에 두는 이 프로젝트 전용 설정 그룹의 뿌리.
// 새 설정은 경로를 'RootPath + "/하위이름"'으로 잡아 이 아래에 붙인다.
//
// ★ 그룹 이름은 'Application.productName'(Player Settings의 Product Name)에서 가져온다 —
//   프로젝트 이름을 코드에 박지 않아야 어느 프로젝트에 복사해도 그대로 맞는다.
//   그래서 'const'가 아니라 'static readonly'다(프로퍼티라 컴파일 타임 상수가 될 수 없다).
//
// ★ 정렬은 **경로 조각**으로, 표시는 **label**로 한다(유니티 'SettingsTreeView' 구현).
//   경로의 '__' 접두사는 순전히 정렬용이다 — 문화권 비교에서 기호가 숫자·문자보다 앞서므로
//   유니티 기본 '일반'(경로가 '_General')보다도 위에 온다. 화면에는 label만 나온다.
internal static class ProjectPreferences
{
	// 화면에 보이는 그룹 이름.
	private static readonly string GroupLabel = Application.productName;

	// 하위 설정들의 경로 접두사. 경로에는 공백이 들어가면 안 되므로 걷어낸다.
	public static readonly string RootPath = "Preferences/__" + GroupLabel.Replace(" ", "");

	// 안내 문구에 쓰는 사람이 읽는 경로(한글 에디터 기준).
	public static readonly string MenuHint = "편집 > 환경 설정 > " + GroupLabel;

	// 'EditorPrefs' 키에 붙일 접두사. 'EditorPrefs'는 프로젝트가 아니라 **머신 전역**이라,
	// 접두사가 없으면 다른 프로젝트의 같은 이름 설정과 값이 섞인다.
	// ★ 이미 쓰고 있던 키의 접두사는 바꾸지 않는다 — 바꾸면 사람들이 잡아 둔 설정이 초기화된다.
	public static readonly string PrefsPrefix = GroupLabel.Replace(" ", "") + ".";

	[SettingsProvider]
	private static SettingsProvider CreateGroup()
	{
		return new SettingsProvider(RootPath, SettingsScope.User)
		{
			label = GroupLabel,

			keywords = new[] { "프로젝트", "전용", "에디터", GroupLabel },

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

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 씬 더티 추적기의 개인 설정. 'EditorPrefs'에 담아 **머신 로컬**로 둔다 — 커밋되지 않는다.
// 환경 설정의 프로젝트 전용 그룹('ProjectPreferences') 아래에 토글을 띄운다.
internal static class SceneDirtyTracerSettings
{
	// ★ 'const'이 아니라 'static readonly'다 — 'PrefsPrefix'가 'Application.productName'에서 오는
	//   런타임 값이라 컴파일 타임 상수가 될 수 없다(CS0133).
	private static readonly string EnabledKey = ProjectPreferences.PrefsPrefix + "SceneDirtyTracer.Enabled";

	private static readonly string SettingsPath = ProjectPreferences.RootPath + "/SceneDirtyTracer";

	private const string SettingsLabel = "씬 더티 추적";

	// 추적을 켤지. ★ 기본은 '꺼짐'이다 — 켜 두면 편집 중 상시로 콘솔이 도배된다.
	public static bool Enabled
	{
		get => EditorPrefs.GetBool(EnabledKey, false);

		set
		{
			EditorPrefs.SetBool(EnabledKey, value);

			// 에디터를 다시 켜지 않고 바로 먹도록 구독을 즉시 맞춘다.
			SceneDirtyTracer.Apply(value);
		}
	}

	[SettingsProvider]
	private static SettingsProvider Create()
	{
		return new SettingsProvider(SettingsPath, SettingsScope.User)
		{
			label = SettingsLabel,

			// Preferences 검색창에 걸리도록 — 한글·영문 둘 다 넣는다.
			keywords = new HashSet<string> { "씬", "더티", "추적", "저장", "Scene", "Dirty", "Tracer" },

			guiHandler = _ =>
			{
				EditorGUILayout.Space();

				EditorGUI.BeginChangeCheck();

				var enabled = EditorGUILayout.Toggle
					(new GUIContent("씬 더티 추적",
									"씬이 더티가 된 순간의 스택트레이스와, 그때 바뀐 오브젝트를 콘솔에 남긴다."),
					 Enabled);

				if (EditorGUI.EndChangeCheck())
				{
					Enabled = enabled;
				}

				EditorGUILayout.Space();

				EditorGUILayout.HelpBox
					("씬을 만진 적이 없는데 제목 옆에 '*'가 붙고, 저장해도 diff 가 안 잡힐 때 켠다.\n\n" +
					 "쓰는 법 — 켠 다음 씬을 한 번 저장해 깨끗하게 만들고, 증상을 재현한다.\n" +
					 "'깨끗 → 더티' 전환에서만 스택트레이스가 나오므로 이 순서가 중요하다.\n\n" +
					 "평소에는 꺼 둔다. 켜 두면 편집 중 상시로 콘솔이 도배된다.\n" +
					 "이 설정은 이 컴퓨터에만 저장되며 커밋되지 않는다.",
					 MessageType.Info);
			},
		};
	}
}

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
	/// <summary>
	/// 씬 복사 툴의 개인 설정. 'EditorPrefs'에 담아 **머신 로컬**로 둔다 — 커밋되지 않으므로
	/// 협업자끼리 서로의 설정에 영향을 주지 않는다(서버 담당은 켜고, 클라 담당은 끄는 식).
	/// 환경 설정의 프로젝트 전용 그룹('ProjectPreferences') 아래에 토글을 띄운다.
	/// </summary>
	internal static class SceneCopySettings
	{
		// EditorPrefs는 프로젝트가 아니라 머신 전역이라, 다른 툴과 겹치지 않게 'DWC.' 프리픽스를 붙인다.
		private const string AutoCheckKey = "DWC.SceneCopy.AutoCheck";

		private const string SettingsPath  = ProjectPreferences.RootPath + "/SceneCopy";
		private const string SettingsLabel = "오리지널 씬 복사";

		/// <summary>복사본 최신성 자동 검사·팝업을 켤지. 기본은 '켜짐'(설정을 만진 적 없는 사람은 지금까지대로).</summary>
		public static bool AutoCheckEnabled
		{
			get => EditorPrefs.GetBool(AutoCheckKey, true);
			set => EditorPrefs.SetBool(AutoCheckKey, value);
		}

		/// <summary>다시 켜는 위치 안내 — 팝업에서 끈 직후에도 같은 문구를 쓴다.</summary>
		public const string ReEnableHint =
			ProjectPreferences.MenuHint + " > " + SettingsLabel + " 에서 다시 켤 수 있다.";

		[SettingsProvider]
		private static SettingsProvider Create()
		{
			return new SettingsProvider(SettingsPath, SettingsScope.User)
			{
				label = SettingsLabel,

				// Preferences 검색창에 걸리도록 — 한글·영문 둘 다 넣는다.
				keywords = new HashSet<string> { "씬", "복사", "최신성", "알림", "Scene", "Copy", "Freshness" },

				guiHandler = _ =>
				{
					// 페이지 제목은 창이 label로 이미 그린다 — 여기서 또 찍지 않는다.
					EditorGUILayout.Space();

					EditorGUI.BeginChangeCheck();

					var enabled = EditorGUILayout.Toggle
						(new GUIContent("복사본 최신성 자동 알림",
										"유니티로 포커스가 돌아올 때 'Test Copy'가 낡았는지 검사해 팝업을 띄운다."),
						 AutoCheckEnabled);

					if (EditorGUI.EndChangeCheck())
					{
						AutoCheckEnabled = enabled;
					}

					EditorGUILayout.Space();

					EditorGUILayout.HelpBox
						("오리지널 씬을 받아 쓰는 쪽(서버 작업)에는 최신 복사본을 유지하도록 알림이 필요하지만,\n" +
						 "씬을 직접 만드는 쪽(클라 작업)에는 자기 커밋에 대한 잔소리가 된다.\n\n" +
						 "꺼도 상단 툴바의 '오리지널 씬 복사' 버튼은 그대로 남아 언제든 수동 복사할 수 있다.\n" +
						 "이 설정은 이 컴퓨터에만 저장되며 커밋되지 않는다.",
						 MessageType.Info);
				},
			};
		}
	}
}

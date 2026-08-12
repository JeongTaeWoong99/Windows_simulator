using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DesktopWindowControl.EditorTools
{
	/// <summary>
	/// 상단 메인 툴바 오른쪽(클라우드 아이콘 옆)에 '오리지널 씬 복사' 버튼을 얹는다.
	/// 유니티가 메인 툴바 확장을 공식 API로 열어 주지 않아, 내부 'UnityEditor.Toolbar'의
	/// UIElements 루트를 리플렉션으로 찾아 오른쪽 영역('ToolbarZoneRightAlign') 맨 앞에 붙인다.
	/// ⚠️ 내부 구조에 기대므로 유니티 버전이 올라가면 조용히 안 붙을 수 있다.
	///    그때도 메뉴('Tools/오리지널 씬 복사')와 단축키는 그대로 동작한다.
	/// </summary>
	[InitializeOnLoad]
	public static class OriginalSceneCopyToolbarButton
	{
		private static readonly Type? ToolbarType =
			typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.Toolbar");

		private static VisualElement? _button; // 붙여 둔 버튼. 도메인 리로드 뒤엔 null로 초기화된다

		static OriginalSceneCopyToolbarButton()
		{
			EditorApplication.update -= TryAttach;
			EditorApplication.update += TryAttach;
		}

		// 툴바는 에디터가 완전히 뜬 뒤에야 존재한다. 붙을 때까지 매 프레임 시도하고, 붙으면 구독을 끊는다.
		private static void TryAttach()
		{
			if (_button != null || ToolbarType == null)
			{
				EditorApplication.update -= TryAttach;
				return;
			}

			var toolbars = Resources.FindObjectsOfTypeAll(ToolbarType);
			if (toolbars.Length == 0)
				return; // 아직 툴바가 생성되기 전 — 다음 프레임에 다시

			var rootField = ToolbarType.GetField("m_Root", BindingFlags.NonPublic | BindingFlags.Instance);
			if (rootField?.GetValue(toolbars[0]) is not VisualElement root)
				return;

			var rightZone = root.Q("ToolbarZoneRightAlign");
			if (rightZone == null)
				return;

			_button = BuildButton();
			rightZone.Insert(0, _button); // 맨 앞에 넣어 오른쪽 아이콘 묶음(클라우드 등) 왼편에 붙인다
			EditorApplication.update -= TryAttach;
		}

		private static VisualElement BuildButton()
		{
			var button = new Button(OriginalSceneCopier.Copy)
			{
				text    = "오리지널 씬 복사",
				tooltip = "Scenes/Original → Scenes/Test Copy 로 최신본 복사 (Ctrl+Shift+C)",
			};
			button.style.height         = 22;
			button.style.marginLeft     = 6;
			button.style.marginRight    = 6;
			button.style.paddingLeft    = 8;
			button.style.paddingRight   = 8;
			button.style.unityTextAlign = TextAnchor.MiddleCenter;
			return button;
		}
	}
}

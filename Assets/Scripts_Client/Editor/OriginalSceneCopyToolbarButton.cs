using UnityEditor.Toolbars;

namespace DesktopWindowControl.EditorTools
{
	/// <summary>
	/// 상단 메인 툴바 오른쪽(클라우드 아이콘 옆)에 '오리지널 씬 복사' 버튼을 얹는다.
	/// Unity 6.1+ 공식 메인 툴바 API('[MainToolbarElement]' + 'MainToolbarButton')를 쓴다.
	/// ★ 리플렉션으로 내부 툴바에 끼우면 Unity 6.3부터 '지원되지 않는 요소'로 감지돼 숨겨진다.
	/// 동작은 'OriginalSceneCopier'가 갖고, 여기는 버튼 등록만 한다.
	/// </summary>
	internal static class OriginalSceneCopyToolbarButton
	{
		// 어트리뷰트가 붙은 정적 메서드가 요소를 만들어 돌려주면 유니티가 툴바에 등록한다.
		// 경로의 마지막 조각이 툴바 커스터마이즈 메뉴에 뜨는 이름이다. Right 도크의 앞(클라우드 근처)에 둔다.
		[MainToolbarElement("DesktopWindowControl/오리지널 씬 복사",
			defaultDockPosition = MainToolbarDockPosition.Right, defaultDockIndex = 0)]
		private static MainToolbarElement Create() =>
			new MainToolbarButton(
				new MainToolbarContent("오리지널 씬 복사", "Scenes/Original → Scenes/Test Copy 로 최신본 복사 (Ctrl+Shift+C)"),
				OriginalSceneCopier.Copy);
	}
}

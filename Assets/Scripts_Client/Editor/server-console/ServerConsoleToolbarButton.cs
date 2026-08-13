using UnityEditor.Toolbars;

namespace DesktopWindowControl.EditorTools
{
	/// <summary>
	/// 상단 메인 툴바 오른쪽에 '서버 콘솔' 버튼을 얹는다(씬 복사 버튼 옆).
	/// Unity 6.1+ 공식 메인 툴바 API('[MainToolbarElement]' + 'MainToolbarButton')를 쓴다.
	/// ★ 리플렉션으로 내부 툴바에 끼우면 Unity 6.3부터 '지원되지 않는 요소'로 감지돼 숨겨진다.
	/// 창·실행 로직은 'ServerConsoleWindow'/'ServerRunner'가 갖고, 여기는 버튼 등록만 한다.
	/// </summary>
	internal static class ServerConsoleToolbarButton
	{
		[MainToolbarElement("DesktopWindowControl/서버 콘솔",
			defaultDockPosition = MainToolbarDockPosition.Right, defaultDockIndex = 1)]

		private static MainToolbarElement Create() =>
			new MainToolbarButton
				(new MainToolbarContent("서버 콘솔", "WSGameServer 실행/종료 및 로그"),
				 ServerConsoleWindow.Open);
	}
}

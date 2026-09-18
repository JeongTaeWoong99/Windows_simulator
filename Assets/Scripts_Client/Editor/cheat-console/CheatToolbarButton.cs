using UnityEditor.Toolbars;

namespace DesktopWindowControl.EditorTools
{
	// 상단 메인 툴바 오른쪽에 '치트' 버튼을 얹는다. 이 프로젝트 전용 툴바 버튼은 이것 하나다 —
	// 씬 복사·서버 콘솔은 치트 창 도구 줄로 모았다(→ cheat-console 규칙.md).
	// Unity 6.1+ 공식 메인 툴바 API('[MainToolbarElement]' + 'MainToolbarButton')를 쓴다.
	// 창·전송 로직은 'CheatWindow'/'CheatSender'가 갖고, 여기는 버튼 등록만 한다.
	internal static class CheatToolbarButton
	{
		// 톱니(설정) 아이콘. 'EditorIcons'(Common — 툴킷 사본)에 올리지 않고 이 툴에만 둔다.
		private const string Icon = "SettingsIcon";

		[MainToolbarElement("DesktopWindowControl/치트",
			defaultDockPosition = MainToolbarDockPosition.Right, defaultDockIndex = 0)]

		private static MainToolbarElement Create() =>
			new MainToolbarButton
				(new MainToolbarContent("치트", EditorIcons.Get(Icon), "admin 전용 치트 창 (Play + 로그인 필요)"),
				 CheatWindow.Open);
	}
}

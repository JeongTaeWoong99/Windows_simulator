using UnityEditor;
using UnityEditor.Toolbars;

namespace DesktopWindowControl.EditorTools
{
	/// <summary>
	/// 상단 메인 툴바 오른쪽에 에디터 메모리 사용량을 얹고 1초마다 갱신한다.
	/// Unity 6.1+ 공식 메인 툴바 API('[MainToolbarElement]' + 'MainToolbarButton')를 쓴다.
	/// ★ 리플렉션으로 내부 툴바에 끼우면 Unity 6.3부터 '지원되지 않는 요소'로 감지돼 숨겨진다.
	/// 수치 읽기·정리는 'EditorMemoryMeter'가 갖고, 여기는 표시와 갱신만 한다.
	/// </summary>
	internal static class EditorMemoryToolbarButton
	{
		private const string ElementPath   = "DesktopWindowControl/메모리 사용량";
		private const double RefreshPeriod = 1.0;   // 초 — 매 프레임 다시 읽는 건 낭비다

		private static MainToolbarElement? _element;
		private static double              _nextRefreshTime;

		// 어트리뷰트가 붙은 정적 메서드가 요소를 만들어 돌려주면 유니티가 툴바에 등록한다.
		// 기존 버튼들이 Right 도크 0·1을 쓰므로 그 뒤(2)에 붙인다.
		[MainToolbarElement(ElementPath,
			defaultDockPosition = MainToolbarDockPosition.Right, defaultDockIndex = 2)]

		private static MainToolbarElement Create()
		{
			_element = new MainToolbarButton(BuildContent(), EditorMemoryMeter.Cleanup);

			// 도메인 리로드(스크립트 재컴파일)마다 static이 날아가고 이 메서드가 다시 불린다.
			// 툴바 갱신 요청으로도 다시 불릴 수 있으므로, 빼고 다시 걸어 중복 구독을 막는다.
			EditorApplication.update -= Tick;
			EditorApplication.update += Tick;

			return _element;
		}

		// 1초에 한 번 라벨·툴팁을 새 수치로 바꾼다 (EditorApplication.update 구독)
		private static void Tick()
		{
			if (EditorApplication.timeSinceStartup < _nextRefreshTime)
			{
				return;
			}

			_nextRefreshTime = EditorApplication.timeSinceStartup + RefreshPeriod;

			// 도메인 리로드 직후 등 아직 요소가 만들어지기 전이면 건너뛴다.
			if (_element == null)
			{
				return;
			}

			_element.content = BuildContent();
			MainToolbar.Refresh(ElementPath);   // 내용이 바뀌었음을 툴바에 알리는 공식 경로
		}

		// 지금 수치로 버튼에 보일 라벨·툴팁·아이콘을 만든다
		private static MainToolbarContent BuildContent()
		{
			EditorMemoryMeter.Snapshot memory = EditorMemoryMeter.Take();

			// 버튼에는 '에디터가 지금 쓰는 전체'만 크게 보이고, 쪼갠 내역은 툴팁에서 본다.
			string label = EditorMemoryMeter.Format(memory.ProcessBytes);

			return new MainToolbarContent(label, EditorIcons.Get(EditorIcons.Memory), BuildTooltip(memory));
		}

		// 수치가 각각 무엇인지 풀어서 적는다 (UI Toolkit 라벨이라 탭으로는 열이 맞지 않아 '이름: 값' 줄로 쓴다)
		private static string BuildTooltip(EditorMemoryMeter.Snapshot memory)
		{
			return
				$"■ 유니티 에디터 전체: {EditorMemoryMeter.Format(memory.ProcessBytes)}\n"                +
				"   OS가 재는 값 — 이 프로세스가 지금 물리 메모리에 올려 둔 양이다.\n"                    +
				"   아래 항목을 더한 값이 아니다.\n"                                                       +
				"\n"                                                                                       +
				"■ 유니티가 따로 세는 몫 (실제 사용량 / 예약 Reserved)\n"                                 +
				$"   · 에셋 · 씬 등 (네이티브): {EditorMemoryMeter.Format(memory.UnityAllocated)}"        +
				$" / {EditorMemoryMeter.Format(memory.UnityReserved)}\n"                                   +
				$"   · C# 스크립트 (Mono 힙): {EditorMemoryMeter.Format(memory.MonoUsed)}"                 +
				$" / {EditorMemoryMeter.Format(memory.MonoHeap)}\n"                                        +
				$"   · 그래픽 드라이버 (텍스처 · 메시): {EditorMemoryMeter.Format(memory.GraphicsDriver)}\n" +
				"\n"                                                                                       +
				"뒤의 값은 예약(Reserved) — 유니티가 OS에서 미리 확보해 둔 힙이다.\n"                     +
				"사용량은 이 안에서 오르내리고, 한 번 늘어난 예약은 반납되지 않는다\n"                     +
				"(Mono의 GC가 비압축식이라 힙을 줄일 수 없다 — 에디터 재시작이 유일한 방법).\n"           +
				"\n"                                                                                       +
				"세 항목의 합은 전체와 맞지 않는다. 재는 대상이 서로 달라서다 —\n"                        +
				"전체에만 있는 몫: 에디터 UI · 플러그인 · DLL 등 유니티가 세지 않는 것.\n"                +
				"전체에서 빠지는 몫: 예약분 중 물리에 안 올라간 것, 그래픽 드라이버의 VRAM 몫.\n"          +
				"작업 관리자의 메모리 열(프라이빗 작업 집합)보다는 공유 DLL 몫만큼 크게 나온다.\n"         +
				"OS가 워킹셋을 정리하면(창 최소화 등) 값이 떨어지지만 반납한 건 아니다.\n"                 +
				"\n"                                                                                       +
				"클릭하면 미사용 에셋 언로드 + GC로 정리한다(예약은 그대로 남는다).";
		}
	}
}

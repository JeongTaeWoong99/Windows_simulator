using UnityEditor;
using UnityEditor.Toolbars;

// 상단 메인 툴바 오른쪽에 에디터 메모리 사용량을 얹고 1초마다 갱신한다.
// Unity 6.1+ 공식 메인 툴바 API('[MainToolbarElement]' + 'MainToolbarButton')를 쓴다.
// ★ 리플렉션으로 내부 툴바에 끼우면 Unity 6.3부터 '지원되지 않는 요소'로 감지돼 숨겨진다.
// 수치 읽기·정리는 'EditorMemoryMeter'가 갖고, 여기는 표시와 갱신만 한다.
internal static class EditorMemoryToolbarButton
{
	private const string ElementPath   = "Arca/메모리 사용량";
	private const double RefreshPeriod = 1.0;   // 초 — 매 프레임 다시 읽는 건 낭비다

	private static MainToolbarElement? _element;
	private static double              _nextRefreshTime;

	// 마지막으로 툴바에 올린 라벨. 같은 값이면 갱신을 건너뛴다('Tick' 주석).
	private static string _lastLabel = string.Empty;

	// 어트리뷰트가 붙은 정적 메서드가 요소를 만들어 돌려주면 유니티가 툴바에 등록한다.
	// 기존 버튼들이 Right 도크 0·1을 쓰므로 그 뒤(2)에 붙인다.
	[MainToolbarElement(ElementPath,
		defaultDockPosition = MainToolbarDockPosition.Right, defaultDockIndex = 2)]

	private static MainToolbarElement Create()
	{
		_element = new MainToolbarButton(BuildContent(out _lastLabel), EditorMemoryMeter.Cleanup);

		// 도메인 리로드(스크립트 재컴파일)마다 static이 날아가고 이 메서드가 다시 불린다.
		// 툴바 갱신 요청으로도 다시 불릴 수 있으므로, 빼고 다시 걸어 중복 구독을 막는다.
		EditorApplication.update -= Tick;
		EditorApplication.update += Tick;

		return _element;
	}

	// 1초에 한 번 라벨·툴팁을 새 수치로 바꾼다 (EditorApplication.update 구독)
	//
	// ★ 라벨이 그대로면 툴바를 건드리지 않는다. 'MainToolbar.Refresh'는 UI Toolkit에게
	//   버튼 텍스트 메시를 다시 만들게 하는데, 그 생성은 지연 실행이라 그 사이에 유니티가
	//   폰트 아틀라스 Material을 언로드하면(플레이 모드를 빠져나올 때 자동으로 돈다)
	//   에디터 콘솔에 'MissingReferenceException: ... Material ... has been destroyed'가 뜬다.
	//   유니티 내부의 레이스라 우리가 막을 수는 없지만, 바뀐 것도 없이 매초 다시 만들며
	//   그 창을 계속 열어 둘 이유도 없다 (2026-08-30, 플레이 종료 18회 중 2회 관측).
	//
	// ※ 트레이드오프 — 라벨(워킹셋)이 같은 동안에는 툴팁의 세부 수치도 갱신되지 않는다.
	//   툴팁은 네이티브가 그려 이 문제와 무관하고, 라벨이 같다는 건 메모리가 MB 단위로
	//   그대로라는 뜻이라 감수한다.
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

		MainToolbarContent content = BuildContent(out string label);

		if (label == _lastLabel)
		{
			return;
		}

		_lastLabel       = label;
		_element.content = content;
		MainToolbar.Refresh(ElementPath);   // 내용이 바뀌었음을 툴바에 알리는 공식 경로
	}

	// 지금 수치로 버튼에 보일 라벨·툴팁·아이콘을 만든다.
	// 라벨을 따로 돌려주는 이유 — 호출부가 '바뀌었나'를 판정해야 하는데, 'MainToolbarContent'는
	// 그 비교를 위한 공개 접근을 보장하지 않는다.
	private static MainToolbarContent BuildContent(out string label)
	{
		EditorMemoryMeter.Snapshot memory = EditorMemoryMeter.Take();

		// 버튼에는 '에디터가 지금 쓰는 전체'만 크게 보이고, 쪼갠 내역은 툴팁에서 본다.
		label = EditorMemoryMeter.Format(memory.ProcessBytes);

		return new MainToolbarContent(label, EditorIcons.Get(EditorIcons.Memory), BuildTooltip(memory));
	}

	// 수치가 각각 무엇인지 풀어서 적는다.
	// ★ 유니티 내장 툴팁은 네이티브가 그려서 폭 상한이 고정이다 — USS·공개 API로 넓힐 수 없다.
	//   그래서 문장 중간에서 꺾이지 않게 한 줄을 한글 24자 이내로 직접 끊는다.
	//   가변폭 폰트라 공백 패딩으로는 열이 안 맞으므로, 수치는 '이름 줄 / 값 줄'로 나눠 세로로 세운다.
	//
	// ★ 용어를 '할당 / 확보'로 적는다. 프로파일러의 Reserved를 '예약'으로 옮기면 Win32의
	//   예약(Reserve — 주소만 찜해 접근하면 죽는 상태)으로 오해된다. 실제로는 이미 커밋된 풀이다.
	//   용어 정의와 포함 관계는 'memory-meter 규칙.md'의 "용어" 절에 표로 있다.
	private static string BuildTooltip(EditorMemoryMeter.Snapshot memory)
	{
		return
			$"■ 버튼 값 = 워킹셋 : {EditorMemoryMeter.Format(memory.ProcessBytes)}\n" +
			"   지금 물리 RAM에 올라온 양.\n"                                           +
			"   OS가 잰다. 아래와 겹친다.\n"                                            +
			"\n"                                                                        +
			"■ 유니티 할당자 (할당 / 확보)\n"                                           +
			"   에셋 · 씬 등 네이티브\n"                                                +
			$"      {EditorMemoryMeter.Format(memory.UnityAllocated)}"                  +
			$" / {EditorMemoryMeter.Format(memory.UnityReserved)}\n"                    +
			"   C# 스크립트 (Mono 힙)\n"                                                +
			$"      {EditorMemoryMeter.Format(memory.MonoUsed)}"                        +
			$" / {EditorMemoryMeter.Format(memory.MonoHeap)}\n"                         +
			"   그래픽 드라이버 (VRAM)\n"                                               +
			$"      {EditorMemoryMeter.Format(memory.GraphicsDriver)}\n"                +
			"\n"                                                                        +
			"   확보 = OS에서 커밋해 받은 풀\n"                                         +
			"   할당 = 그 풀에서 실제 쓰는 양\n"                                        +
			"   할당 ≤ 확보\n"                                                          +
			"\n"                                                                        +
			"■ 왜 위아래가 안 맞나\n"                                                   +
			"   위는 'RAM에 있나',\n"                                                   +
			"   아래는 '얼마나 확보했나'다.\n"                                          +
			"   안 쓰는 페이지는 OS가 빼내므로\n"                                       +
			"   위에서만 빠진다. 그래서\n"                                              +
			"   아래가 더 클 수 있다.\n"                                                +
			"   빼낸 페이지도 RAM에 남아(대기)\n"                                       +
			"   다시 쓰면 즉시 돌아온다.\n"                                             +
			"\n"                                                                        +
			"   확보분은 반납되지 않는다\n"                                             +
			"   (Mono GC가 비압축식이라 그렇다).\n"                                     +
			"\n"                                                                        +
			"   작업 관리자 → '자세히' 탭 →\n"                                          +
			"   열 머리글 우클릭 → 열 선택 →\n"                                        +
			"   '커밋 크기'를 켜서 견준다.\n"                                           +
			"\n"                                                                        +
			"클릭 = 미사용 에셋 언로드 + GC";
	}
}

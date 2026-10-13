using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
	// 서버 치트를 한 화면에 모아 보내는 에디터 창. 인스펙터처럼 도킹해 두고 쓴다.
	// 왼쪽은 검색창 + 분류 목록, 오른쪽은 고른 분류의 칸, 아래는 결과 로그(높이를 끌어 바꾼다).
	// 칸은 'CheatSection'을 상속한 클래스를 찾아 모은다 — 치트를 늘려도 이 창은 고치지 않는다('cheat-console 규칙.md').
	// 보내기·응답 대기·로그는 'CheatSender', 보내기 전 검사·팝업은 'CheatGuard'가 한다 — 이 창은 그리기만 한다.
	internal sealed class CheatWindow : EditorWindow
	{
		private const double StatusPollInterval = 0.5;   // 상태 줄(서버·클라·로그인)을 다시 확인하는 주기(초)
		private const float  ToolDividerWidth   = 2f;    // 도구 줄 구분선 두께(px)
		private const float  ToolGapPadding     = 5f;    // 구분선 양옆 여백(px)
		private const float  SidebarWidth       = 116f;
		private const float  SidebarItemHeight  = 22f;
		private const float  DefaultLogHeight   = 140f;
		private const float  MinLogHeight       = 48f;
		private const float  MinContentHeight   = 160f;  // 로그를 끌어 올려도 칸 영역은 이만큼 남긴다
		private const float  SplitterHeight     = 5f;

		// 어두운 스킨·밝은 스킨 둘 다에서 보이도록 검정을 반투명으로 쓴다.
		private static readonly Color ToolDividerColor     = new(0f, 0f, 0f, 0.55f);
		private static readonly Color SidebarDividerColor  = new(0f, 0f, 0f, 0.35f);
		private static readonly Color SidebarSelectedColor = new(0.24f, 0.49f, 0.91f, 0.35f);

		private static readonly ECheatCategory[] Categories = (ECheatCategory[])Enum.GetValues(typeof(ECheatCategory));

		// 칸 입력값·고른 분류·로그 높이 — 플레이 진입(도메인 리로드)에도 남도록 직렬화한다.
		[SerializeReference] private List<CheatSection> _sections = new();
		[SerializeField]     private ECheatCategory     _category;
		[SerializeField]     private bool               _isAllSelected;
		[SerializeField]     private string             _search    = "";
		[SerializeField]     private float              _logHeight = DefaultLogHeight;

		private Vector2 _scroll;
		private Vector2 _logScroll;
		private bool    _logScrollToBottom = true;
		private double  _nextStatusPoll;

		private CheatGuard.Step _completedSteps;

		private static GUIStyle? _countStyle;

		// 사이드바 줄 오른쪽 개수 — 오른쪽 정렬 작은 글씨
		private static GUIStyle CountStyle => _countStyle ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };

		// 치트 창을 연다 (툴바 버튼·메뉴)
		[MenuItem("Window/DesktopWindowControl/치트")]
		public static void Open()
		{
			var window = GetWindow<CheatWindow>("치트");
			window.minSize = new Vector2(440, 360);
			window.Show();
		}

		// 칸 모으기 · 로그·상태 갱신 구독 (Unity 메시지 — 창 열기·도메인 리로드)
		private void OnEnable()
		{
			CollectSections();

			CheatSender.LogChanged   += OnLogChanged;
			EditorApplication.update += OnUpdate;
			_logScrollToBottom        = true;
			_completedSteps           = CheatGuard.GetCompletedSteps();
		}

		// 구독 해제 (Unity 메시지)
		private void OnDisable()
		{
			CheatSender.LogChanged   -= OnLogChanged;
			EditorApplication.update -= OnUpdate;
		}

		// 창 그리기 (Unity 메시지)
		private void OnGUI()
		{
			DrawToolRow();
			DrawStatusBar();

			using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandHeight(true)))
			{
				DrawSidebar();
				DrawSidebarDivider();
				DrawSections();
			}

			DrawLogSplitter();
			DrawLog();
		}

		#region 갱신

		// 'CheatSection'을 상속한 클래스를 전부 찾아 하나씩 만든다. 이미 있는 칸은 입력값째 그대로 둔다.
		// 클래스를 지웠으면 [SerializeReference]가 null로 돌아오므로 걷어 낸다.
		private void CollectSections()
		{
			_sections.RemoveAll(section => section == null);

			foreach (var type in TypeCache.GetTypesDerivedFrom<CheatSection>())
			{
				if (type.IsAbstract || _sections.Exists(section => section.GetType() == type))
				{
					continue;
				}

				_sections.Add((CheatSection)Activator.CreateInstance(type)!);
			}

			_sections.Sort((a, b) => a.Category != b.Category ? a.Category.CompareTo(b.Category) : a.Order.CompareTo(b.Order));
		}

		// 새 로그가 쌓였다 (CheatSender.LogChanged 구독)
		private void OnLogChanged()
		{
			_logScrollToBottom = true;
			Repaint();
		}

		// 준비 단계를 주기적으로 확인하고, 바뀌었을 때만 다시 그린다 (EditorApplication.update 구독)
		// 로그인 뒤에는 보유 캐릭터·해금 상태가 계속 바뀌므로 주기마다 다시 그린다.
		private void OnUpdate()
		{
			if (EditorApplication.timeSinceStartup < _nextStatusPoll)
			{
				return;
			}

			_nextStatusPoll = EditorApplication.timeSinceStartup + StatusPollInterval;

			var completedSteps = CheatGuard.GetCompletedSteps();
			var isLoggedIn     = completedSteps.HasFlag(CheatGuard.Step.Login);

			if (completedSteps == _completedSteps && !isLoggedIn)
			{
				return;
			}

			_completedSteps = completedSteps;
			Repaint();
		}

		#endregion

		#region 그리기 — 도구 줄 · 상태 줄

		// 이 프로젝트 전용 도구 — 씬 복사와 서버 콘솔. 메인 툴바에서 옮겨 와 여기 한 곳에만 둔다.
		// ★ 전부 오른쪽에 모은다(메인 툴바와 같은 방향). 도구가 늘면 'DrawToolGap' 다음에 그룹 하나를 덧붙인다.
		private void DrawToolRow()
		{
			using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
			{
				GUILayout.FlexibleSpace();

				DrawSceneCopyTool();
				DrawToolGap();
				DrawServerConsoleTool();
				DrawToolGap();
				DrawPeriodicLogTool();
			}
		}

		// 도구 사이 구분선 — 붙어 있는 버튼끼리는 한 도구, 어두운 세로줄 너머는 다른 도구다.
		// 줄 위아래를 조금 비워 툴바 테두리와 겹치지 않게 한다.
		private static void DrawToolGap()
		{
			GUILayout.Space(ToolGapPadding);

			var rect = GUILayoutUtility.GetRect(ToolDividerWidth, EditorGUIUtility.singleLineHeight,
			                                    GUILayout.Width(ToolDividerWidth), GUILayout.ExpandHeight(true));
			EditorGUI.DrawRect(new Rect(rect.x, rect.y + 3f, rect.width, rect.height - 6f), ToolDividerColor);

			GUILayout.Space(ToolGapPadding);
		}

		// '[●]오리지널 씬 복사' — 붙어 있는 두 버튼이다: [●]/[○]는 복사본 최신성 자동 알림 켜기·끄기, 나머지는 복사.
		private static void DrawSceneCopyTool()
		{
			var isAutoCheck = SceneCopySettings.AutoCheckEnabled;
			var alertContent = new GUIContent
				(isAutoCheck ? "[●]" : "[○]",
				 isAutoCheck ? "씬 복사 자동 알림 켜짐 — 누르면 끈다" : "씬 복사 자동 알림 꺼짐 — 누르면 켠다");

			var previous = GUI.contentColor;
			GUI.contentColor = isAutoCheck ? CheatGui.DoneColor : previous;

			if (GUILayout.Button(alertContent, EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
			{
				SceneCopySettings.AutoCheckEnabled = !isAutoCheck;
			}

			GUI.contentColor = previous;

			var copyContent = new GUIContent
				(" 오리지널 씬 복사", EditorIcons.Get(EditorIcons.Duplicate), "Scenes/Original → Scenes/Test Copy 로 최신본 복사");

			if (GUILayout.Button(copyContent, EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
			{
				// 확인 팝업을 띄우므로 OnGUI 밖에서 부른다('CheatGui.Request' 주석과 같은 이유).
				EditorApplication.delayCall += OriginalSceneCopier.CopyWithConfirm;
			}
		}

		private static void DrawServerConsoleTool()
		{
			var consoleContent = new GUIContent(" 서버 콘솔", EditorIcons.Get(EditorIcons.Console), "WSGameServer 실행/종료 및 로그");

			if (GUILayout.Button(consoleContent, EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
			{
				ServerConsoleWindow.Open();
			}
		}

		// '[●]주기 수신 로그' — 수확마다 오는 수신 4종(채취 정산·슬롯·캐릭터 동기화·계정 레벨)을 콘솔에 찍을지.
		// 평시엔 꺼 둔다 — 'Debug.Log'가 줄마다 스택을 뽑아 수확 프레임이 튄다('Log 규칙.md' 3장).
		private static void DrawPeriodicLogTool()
		{
			var isOn = PeriodicLogSettings.Enabled;
			var content = new GUIContent
				(isOn ? "[●] 주기 수신 로그" : "[○] 주기 수신 로그",
				 isOn ? "수확마다 오는 수신 로그를 찍는 중 — 누르면 끈다" : "수확마다 오는 수신 로그 꺼짐 — 누르면 켠다");

			var previous = GUI.contentColor;
			GUI.contentColor = isOn ? CheatGui.DoneColor : previous;

			if (GUILayout.Button(content, EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
			{
				PeriodicLogSettings.Enabled = !isOn;
			}

			GUI.contentColor = previous;
		}

		// 작업 순서 그대로 '서버 › 클라 › 로그인'을 보이고, 빠진 단계를 오른쪽에 글로 적는다.
		private void DrawStatusBar()
		{
			using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
			{
				DrawStep("서버", CheatGuard.Step.Server);
				GUILayout.Label("›", EditorStyles.miniLabel);
				DrawStep("클라", CheatGuard.Step.Client);
				GUILayout.Label("›", EditorStyles.miniLabel);
				DrawStep("로그인", CheatGuard.Step.Login);

				GUILayout.FlexibleSpace();

				var missing = CheatGuard.AllSteps & ~_completedSteps;
				CheatGui.DrawColoredLabel(missing == CheatGuard.Step.None ? "준비 완료" : $"{CheatGuard.NamesOf(missing)} 미완료",
				                          missing == CheatGuard.Step.None ? CheatGui.DoneColor : CheatGui.PendingColor);
			}
		}

		private void DrawStep(string label, CheatGuard.Step step)
		{
			var isDone = _completedSteps.HasFlag(step);

			CheatGui.DrawColoredLabel($"{(isDone ? "●" : "○")} {label}", isDone ? CheatGui.DoneColor : CheatGui.PendingColor);
		}

		#endregion

		#region 그리기 — 사이드바 · 칸

		// 검색창 + '전체' + 분류 목록. 검색 중에는 분류와 상관없이 맞는 칸을 다 보이므로 고른 줄 표시를 끈다.
		private void DrawSidebar()
		{
			using (new EditorGUILayout.VerticalScope(GUILayout.Width(SidebarWidth)))
			{
				GUILayout.Space(4f);
				_search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField);
				GUILayout.Space(4f);

				var isSearching = _search.Length > 0;

				if (DrawSidebarItem("전체", _sections.Count, !isSearching && _isAllSelected))
				{
					SelectCategory(null);
				}

				foreach (var category in Categories)
				{
					var count = _sections.FindAll(section => section.Category == category).Count;

					if (DrawSidebarItem(CategoryLabel.Get(category), count, !isSearching && !_isAllSelected && _category == category))
					{
						SelectCategory(category);
					}
				}

				GUILayout.FlexibleSpace();
			}
		}

		// 'null'이면 전체. 고르면 검색을 비운다 — 검색이 남아 있으면 고른 분류가 안 보인다.
		private void SelectCategory(ECheatCategory? category)
		{
			_isAllSelected = category == null;
			_category      = category ?? _category;
			_search        = "";
			_scroll        = Vector2.zero;

			GUI.FocusControl(null);   // 검색창이 입력 중이면 비운 글자가 화면에 남는다
		}

		// 사이드바 한 줄 — 이름 · 오른쪽 개수. 눌렸는지 돌려준다.
		// 서버 전체 표시는 여기 달지 않는다 — 칸 머리 배지에서만 본다(목록에 아이콘이 섞이면 분류 이름이 어수선하다).
		private static bool DrawSidebarItem(string label, int count, bool isSelected)
		{
			var rect = GUILayoutUtility.GetRect(SidebarWidth, SidebarItemHeight, GUILayout.ExpandWidth(true));

			if (isSelected)
			{
				EditorGUI.DrawRect(rect, SidebarSelectedColor);
			}

			GUI.Label(new Rect(rect.x + 6f, rect.y, rect.width - 30f, rect.height), label,
			          isSelected ? EditorStyles.boldLabel : EditorStyles.label);

			GUI.Label(new Rect(rect.xMax - 28f, rect.y, 22f, rect.height), count.ToString(), CountStyle);

			return GUI.Button(rect, GUIContent.none, GUIStyle.none);
		}

		private static void DrawSidebarDivider()
		{
			var rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.Width(1f), GUILayout.ExpandHeight(true));
			EditorGUI.DrawRect(rect, SidebarDividerColor);
		}

		// 고른 분류(또는 검색 결과)의 칸. 여러 분류가 섞여 보일 때는 분류가 바뀌는 자리에 이름 줄을 끼운다.
		private void DrawSections()
		{
			_scroll = EditorGUILayout.BeginScrollView(_scroll);

			var isSearching  = _search.Length > 0;
			var isMixed      = isSearching || _isAllSelected;
			var lastCategory = (ECheatCategory?)null;
			var shownCount   = 0;

			foreach (var section in _sections)
			{
				var isShown = isSearching ? section.Matches(_search) : _isAllSelected || section.Category == _category;

				if (!isShown)
				{
					continue;
				}

				if (isMixed && section.Category != lastCategory)
				{
					DrawCategoryHeading(section.Category, lastCategory != null);
					lastCategory = section.Category;
				}

				CheatGui.BeginSection(section);
				section.Draw();
				CheatGui.EndSection(section.Accent);
				shownCount++;
			}

			if (shownCount == 0)
			{
				EditorGUILayout.Space(8f);
				EditorGUILayout.LabelField(isSearching ? $"'{_search}'에 맞는 치트가 없다" : "이 분류에는 치트가 없다", EditorStyles.centeredGreyMiniLabel);
			}

			EditorGUILayout.Space(6f);
			EditorGUILayout.EndScrollView();
		}

		private static void DrawCategoryHeading(ECheatCategory category, bool isFollowing)
		{
			EditorGUILayout.Space(isFollowing ? 12f : 4f);
			EditorGUILayout.LabelField(CategoryLabel.Get(category), EditorStyles.miniBoldLabel);
		}

		#endregion

		#region 그리기 — 로그

		// 칸 영역과 로그 사이의 끌기 손잡이 — 위아래로 끌어 로그 높이를 바꾼다.
		// 창을 줄였을 때도 칸 영역이 'MinContentHeight'만큼은 남게 매번 다시 자른다.
		private void DrawLogSplitter()
		{
			var maxLogHeight = Mathf.Max(MinLogHeight, position.height - MinContentHeight);
			_logHeight = Mathf.Clamp(_logHeight, MinLogHeight, maxLogHeight);

			var rect      = GUILayoutUtility.GetRect(0f, SplitterHeight, GUILayout.ExpandWidth(true));
			var controlId = GUIUtility.GetControlID(FocusType.Passive);
			var current   = Event.current;

			EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeVertical);

			switch (current.GetTypeForControl(controlId))
			{
				case EventType.MouseDown when current.button == 0 && rect.Contains(current.mousePosition):
					GUIUtility.hotControl = controlId;
					current.Use();
					break;

				case EventType.MouseDrag when GUIUtility.hotControl == controlId:
					_logHeight = Mathf.Clamp(_logHeight - current.delta.y, MinLogHeight, maxLogHeight);
					current.Use();
					Repaint();
					break;

				case EventType.MouseUp when GUIUtility.hotControl == controlId:
					GUIUtility.hotControl = 0;
					current.Use();
					break;

				case EventType.Repaint:
					EditorGUI.DrawRect(new Rect(rect.x, rect.center.y, rect.width, 1f), SidebarDividerColor);
					break;
			}
		}

		private void DrawLog()
		{
			using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
			{
				GUILayout.Label("결과 로그", EditorStyles.miniBoldLabel);
				GUILayout.FlexibleSpace();

				if (GUILayout.Button("지우기", EditorStyles.toolbarButton))
				{
					CheatSender.ClearLog();
				}
			}

			if (_logScrollToBottom)
			{
				_logScroll.y       = float.MaxValue;   // 스크롤 뷰가 끝으로 잘라 준다
				_logScrollToBottom = false;
			}

			_logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.Height(_logHeight));

			var previous = GUI.contentColor;

			foreach (var entry in CheatSender.Log)
			{
				GUI.contentColor = entry.IsError ? CheatGui.ErrorColor : previous;
				EditorGUILayout.LabelField($"{entry.Time}  {entry.Text}", EditorStyles.miniLabel);
			}

			GUI.contentColor = previous;

			EditorGUILayout.EndScrollView();
		}

		#endregion
	}
}

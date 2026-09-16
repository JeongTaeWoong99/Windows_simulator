using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
	// WSGameServer를 켜고/끄고(토글), 서버 로그를 터미널처럼 실시간으로 보여주는 에디터 창.
	// 실행/종료 자체는 'ServerRunner'가 하고, 이 창은 상태 표시 + 로그 파일 tail만 담당한다.
	// ★ 로그는 'ServerRunner'가 남기는 파일을 주기적으로 읽어 붙인다 —
	//    도메인 리로드로 이 창이 다시 만들어져도 파일을 이어 읽어 로그 연속성이 유지된다.
	// ★ 로그는 화면에 걸친 줄만 그린다(가상 스크롤). 전체를 한 문자열로 IMGUI에 넘기면
	//    줄이 늘 때마다 텍스트 캐시가 빗나가 전체를 다시 레이아웃해 에디터가 멈칫한다(→ server-console 규칙.md).
	internal sealed class ServerConsoleWindow : EditorWindow
	{
		private const int    MaxLines     = 10000;             // 메모리에 유지할 최근 줄 수(표시 비용과는 무관)
		private const double PollInterval = 0.5;               // 로그 파일을 다시 읽는 주기(초)
		private const long   TailBytes    = 2 * 1024 * 1024;   // 창을 열 때 파일 끝에서부터 읽어 올 최대 바이트
		private const float  RowSpacing   = 2f;                // 줄 사이 여백(px)

		// 서버 로그는 "시각·레벨·스레드" 컬럼을 공백 패딩으로 세로 정렬한다(ServerLog 포맷).
		// 이 정렬은 고정폭 폰트에서만 성립한다 → 프로젝트에 포함된 네오둥근모(한글·영문 지원)를 쓴다.
		//
		// ★ OS 시스템 폰트(굴림체 등)를 Font.CreateDynamicFontFromOSFont로 참조하지 않는다.
		//   해당 폰트 데이터가 없으면 매 OnGUI의 CalcSize에서
		//   "Unable to find a font file..."·"No Font Asset has been assigned" 오류가 반복된다.
		//   프로젝트 폰트 에셋(TTF)만 AssetDatabase로 직접 로드해 쓴다.
		// ★ neodgm은 "16픽셀 격자" 도트 폰트라 16의 배수(16·32…)에서 글자 폭이 격자에 떨어진다.
		private const string LogFontAssetPath = "Assets/Resources/Fonts/neodgm_pro.ttf";
		private const int    LogFontSize      = 16;   // neodgm 격자 크기(16의 배수 권장)

		private static readonly Color SelectionColor = new(0.24f, 0.48f, 0.90f, 0.35f);

		private readonly List<string> _lines      = new();
		private readonly GUIContent   _rowContent = new();   // 줄 그리기용 재사용 컨텐츠(매 줄 할당 방지)

		private string _pending = "";       // 아직 개행이 안 온 마지막 줄 조각
		private long   _readOffset;         // 다음에 읽기 시작할 파일의 바이트 위치
		private bool   _skipPartialLine;    // 파일 중간부터 읽기 시작했으면 첫 개행까지는 잘린 줄이라 버린다

		private Vector2   _scroll;
		private float     _rowHeight = LogFontSize + RowSpacing;
		private float     _maxLineWidth;     // 지금까지 그린 줄 중 가장 긴 폭(가로 스크롤 범위)
		private bool      _autoScroll = true;
		private bool      _scrollToBottom;
		private double    _nextPoll;
		private bool      _running;
		private GUIStyle? _logStyle;
		private bool      _fontWarned;       // 폰트 미탐 경고를 1회만 남기기 위한 상태

		private int _selectionAnchor = -1;   // 선택을 시작한 줄(-1 = 선택 없음)
		private int _selectionEnd    = -1;   // 선택이 끝난 줄(앵커보다 앞일 수 있다)

		// 화면에 그릴 줄 수 — 개행이 아직 안 온 조각도 마지막 한 줄로 보여 준다.
		private int RowCount => _lines.Count + (_pending.Length > 0 ? 1 : 0);

		private bool HasSelection => _selectionAnchor >= 0;

		// 서버 콘솔 창을 연다(툴바 버튼이 부른다).
		public static void Open()
		{
			var window = GetWindow<ServerConsoleWindow>("서버 콘솔");
			window.minSize = new Vector2(420, 240);
			window.Show();
		}

		// 로그 이력 복원 + 폴링 등록 (Unity 메시지 — 창 열기·도메인 리로드)
		private void OnEnable()
		{
			_lines.Clear();
			_pending      = "";
			_maxLineWidth = 0f;
			ClearSelection();

			// 파일 전체가 아니라 끝부분만 읽는다 — 오래 돌린 서버의 수 MB 로그를 리로드마다 읽으면 멈칫한다.
			_readOffset      = TailStartOffset();
			_skipPartialLine = _readOffset > 0;

			_running = ServerRunner.IsRunning;
			PollLog();
			_scrollToBottom = true;
			EditorApplication.update += OnUpdate;
		}

		// 폴링 해제 (Unity 메시지)
		private void OnDisable()
		{
			EditorApplication.update -= OnUpdate;
		}

		// 주기적으로 실행 상태와 로그 파일을 확인하고, 바뀐 게 있을 때만 다시 그린다 (EditorApplication.update 구독)
		private void OnUpdate()
		{
			if (EditorApplication.timeSinceStartup < _nextPoll)
			{
				return;
			}
			_nextPoll = EditorApplication.timeSinceStartup + PollInterval;

			var wasRunning = _running;
			_running = ServerRunner.IsRunning;
			var hasNewLog = PollLog();

			if (!hasNewLog && _running == wasRunning)
			{
				return;
			}

			if (hasNewLog && _autoScroll)
			{
				_scrollToBottom = true;
			}
			Repaint();
		}

		// 창 그리기 (Unity 메시지)
		private void OnGUI()
		{
			EnsureLogStyle();

			DrawToolbar();
			DrawLog();
		}

		#region 로그 읽기

		// 로그 파일에서 지난번 이후 늘어난 부분만 읽어 줄 목록에 붙인다. 읽은 게 있으면 true.
		private bool PollLog()
		{
			var path = ServerRunner.LogFilePath;

			if (!File.Exists(path))
			{
				return false;
			}

			try
			{
				// cmd가 쓰는 중인 파일이라 공유 모드를 열어 둔다.
				using var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
					FileShare.ReadWrite | FileShare.Delete);

				// 파일이 줄었으면 새 세션이 시작돼 잘렸다는 뜻 — 처음부터 다시 읽는다.
				if (fs.Length < _readOffset)
				{
					_readOffset      = 0;
					_skipPartialLine = false;
					_lines.Clear();
					_pending      = "";
					_maxLineWidth = 0f;
					ClearSelection();
				}

				if (fs.Length == _readOffset)
				{
					return false;
				}

				fs.Seek(_readOffset, SeekOrigin.Begin);
				using var reader = new StreamReader(fs, Encoding.UTF8);
				var chunk = reader.ReadToEnd();
				_readOffset = fs.Length;
				AppendChunk(chunk);

				return true;
			}
			catch
			{
				// 읽기 실패(순간적 잠금 등)는 다음 주기에 다시 시도한다.
				return false;
			}
		}

		// 읽어 온 조각을 줄 단위로 잘라 붙인다. 인덱스로 전진해 남은 문자열을 줄마다 복사하지 않는다.
		private void AppendChunk(string chunk)
		{
			var text  = _pending + chunk.Replace("\r\n", "\n").Replace('\r', '\n');
			var start = 0;
			int newline;

			while ((newline = text.IndexOf('\n', start)) >= 0)
			{
				if (_skipPartialLine)
				{
					_skipPartialLine = false;
				}
				else
				{
					_lines.Add(text.Substring(start, newline - start));
				}
				start = newline + 1;
			}

			_pending = start == 0 ? text : text.Substring(start);
			TrimOverflow();
		}

		// 상한을 넘은 앞줄을 버린다. 스크롤·선택은 버린 줄 수만큼 당겨 보던 위치가 튀지 않게 한다.
		private void TrimOverflow()
		{
			var overflow = _lines.Count - MaxLines;

			if (overflow <= 0)
			{
				return;
			}

			_lines.RemoveRange(0, overflow);

			if (!_autoScroll)
			{
				_scroll.y = Mathf.Max(0f, _scroll.y - overflow * _rowHeight);
			}

			ShiftSelection(-overflow);
		}

		// 창을 열 때 읽기 시작할 위치 — 파일이 TailBytes보다 크면 끝에서 TailBytes만큼 앞.
		private static long TailStartOffset()
		{
			var length = FileEndOffset();

			return length > TailBytes ? length - TailBytes : 0;
		}

		private static long FileEndOffset()
		{
			try
			{
				var path = ServerRunner.LogFilePath;

				return File.Exists(path) ? new FileInfo(path).Length : 0;
			}
			catch
			{
				return 0;
			}
		}

		#endregion

		#region 그리기

		// 로그 표시용 폰트를 프로젝트 에셋(TTF)에서만 불러온다. OS 시스템 폰트는 참조하지 않는다.
		// 못 찾으면 null을 돌려주고, 호출부는 폰트를 지정하지 않은 기본 스타일로 안전하게 그린다.
		private static Font? LoadLogFont() =>
			AssetDatabase.LoadAssetAtPath<Font>(LogFontAssetPath);

		// 로그 스타일을 보장한다. 도메인 리로드·에디터 재시작으로 _logStyle이 null이 되거나
		// (에셋 임포트 전이라) 폰트가 아직 안 붙은 경우, OnGUI 진입 때마다 폰트 로드를 재시도한다.
		// 폰트를 못 붙여도 에디터 기본 폰트로 그린다(CalcSize가 null 폰트로 오류 나지 않음).
		private void EnsureLogStyle()
		{
			// 이미 폰트까지 정상적으로 붙었으면 그대로 쓴다.
			if (_logStyle is { font: not null })
			{
				return;
			}

			var font = LoadLogFont();

			_logStyle = new GUIStyle(EditorStyles.label)
			{
				fontSize  = LogFontSize,
				richText  = false,
				wordWrap  = false,
				alignment = TextAnchor.MiddleLeft,
				padding   = new RectOffset(4, 4, 0, 0),
				margin    = new RectOffset(0, 0, 0, 0),
			};

			if (font != null)
			{
				_logStyle.font = font;
				_maxLineWidth  = 0f;   // 폰트가 바뀌었으니 폭을 다시 잰다
			}
			else if (!_fontWarned)
			{
				// 폰트를 못 찾아도 기본 폰트로 계속 동작한다 — 경고는 한 번만 남긴다(매 프레임 도배 방지).
				_fontWarned = true;
				Debug.LogWarning($"[서버 콘솔] 로그 폰트를 찾지 못했다: {LogFontAssetPath} — 에디터 기본 폰트로 표시한다.");
			}

			_rowHeight = Mathf.Ceil(_logStyle.lineHeight) + RowSpacing;
		}

		private void DrawToolbar()
		{
			using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
			{
				// 상태 점
				var prev = GUI.color;
				GUI.color = _running ? new Color(0.35f, 0.85f, 0.35f) : new Color(0.7f, 0.7f, 0.7f);
				GUILayout.Label(_running ? "● 실행 중" : "● 정지", EditorStyles.toolbarButton, GUILayout.Width(70));
				GUI.color = prev;

				// 시작/정지 토글
				if (_running)
				{
					if (GUILayout.Button("■ 서버 정지", EditorStyles.toolbarButton, GUILayout.Width(90)))
					{
						ServerRunner.Stop();
						_running = ServerRunner.IsRunning;
					}
				}
				else
				{
					if (GUILayout.Button("▶ 서버 시작", EditorStyles.toolbarButton, GUILayout.Width(90)))
					{
						ServerRunner.Start();
						_running = ServerRunner.IsRunning;
						_scrollToBottom = true;
					}
				}

				GUILayout.FlexibleSpace();

				_autoScroll = GUILayout.Toggle(_autoScroll, "자동 스크롤", EditorStyles.toolbarButton, GUILayout.Width(80));

				if (GUILayout.Button("지우기", EditorStyles.toolbarButton, GUILayout.Width(50)))
				{
					// 화면 표시만 비운다(파일은 건드리지 않는다). 다음 tail은 파일 끝에서 이어 간다.
					_lines.Clear();
					_pending         = "";
					_skipPartialLine = false;
					_readOffset      = FileEndOffset();
					_maxLineWidth    = 0f;
					ClearSelection();
				}

				if (GUILayout.Button("파일 열기", EditorStyles.toolbarButton, GUILayout.Width(65)))
				{
					EditorUtility.RevealInFinder(ServerRunner.LogFilePath);
				}
			}
		}

		// 로그 영역 — 스크롤 전체 높이는 '줄 수 × 줄 높이'로 잡고, 화면에 걸친 줄만 그린다.
		private void DrawLog()
		{
			// 컨트롤 ID는 Layout 포함 모든 이벤트에서 같은 순서로 받아야 드래그·키보드 포커스가 유지된다.
			var controlId = GUIUtility.GetControlID(FocusType.Keyboard);
			var area      = GUILayoutUtility.GetRect(0f, 0f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
			var evt       = Event.current;

			// Layout 이벤트의 GetRect는 임시 크기라 스크롤을 계산하면 위치가 틀어진다.
			if (evt.type == EventType.Layout)
			{
				return;
			}

			var style    = _logStyle ?? EditorStyles.label;
			var rowCount = RowCount;
			var viewRect = new Rect(0f, 0f, Mathf.Max(_maxLineWidth, 1f), rowCount * _rowHeight);

			if (_scrollToBottom)
			{
				_scroll.y       = viewRect.height;   // BeginScrollView가 최대치로 클램프해 맨 아래로 붙는다
				_scrollToBottom = false;
			}

			_scroll = GUI.BeginScrollView(area, _scroll, viewRect);

			if (evt.type == EventType.Repaint && rowCount > 0)
			{
				var first = Mathf.Max(0, Mathf.FloorToInt(_scroll.y / _rowHeight));
				var last  = Mathf.Min(rowCount - 1, Mathf.CeilToInt((_scroll.y + area.height) / _rowHeight));
				DrawRows(first, last, style, Mathf.Max(viewRect.width, area.width));
			}

			GUI.EndScrollView();

			// 스크롤바가 쓰지 않고 남긴 마우스·키 입력만 선택으로 처리한다(스크롤바가 쓴 이벤트는 Used로 바뀐다).
			HandleSelectionInput(controlId, area, rowCount);
		}

		// first~last 줄을 그린다. 그리면서 잰 폭으로 가로 스크롤 범위를 넓힌다.
		private void DrawRows(int first, int last, GUIStyle style, float rowWidth)
		{
			var widthGrew = false;

			for (var i = first; i <= last; i++)
			{
				var rowRect = new Rect(0f, i * _rowHeight, rowWidth, _rowHeight);

				if (IsSelected(i))
				{
					EditorGUI.DrawRect(rowRect, SelectionColor);
				}

				_rowContent.text = RowText(i);
				style.Draw(rowRect, _rowContent, false, false, false, false);

				// 방금 그린 텍스트와 캐시를 공유하므로 추가 비용이 거의 없다. 전체 줄을 미리 재면 수 초가 걸린다.
				var width = style.CalcSize(_rowContent).x;

				if (width > _maxLineWidth)
				{
					_maxLineWidth = width;
					widthGrew     = true;
				}
			}

			if (widthGrew)
			{
				Repaint();   // 늘어난 폭을 다음 프레임 스크롤 범위에 반영
			}
		}

		private string RowText(int row) => row < _lines.Count ? _lines[row] : _pending;

		#endregion

		#region 선택 · 복사

		// 줄 단위 선택(클릭·Shift+클릭·드래그)과 복사(Ctrl+C·Ctrl+A·우클릭 메뉴)를 처리한다.
		private void HandleSelectionInput(int controlId, Rect area, int rowCount)
		{
			var evt = Event.current;

			switch (evt.type)
			{
				case EventType.MouseDown when evt.button == 0 && area.Contains(evt.mousePosition):
				{
					var row = RowAt(evt.mousePosition.y, area);

					if (row >= rowCount)
					{
						ClearSelection();
					}
					else if (evt.shift && HasSelection)
					{
						_selectionEnd = row;
					}
					else
					{
						_selectionAnchor = row;
						_selectionEnd    = row;
					}

					GUIUtility.hotControl      = controlId;
					GUIUtility.keyboardControl = controlId;
					evt.Use();
					Repaint();
					break;
				}

				case EventType.MouseDrag when GUIUtility.hotControl == controlId && HasSelection:
				{
					// 영역 밖으로 끌면 한 줄씩 따라 스크롤한다.
					if (evt.mousePosition.y < area.y)
					{
						_scroll.y = Mathf.Max(0f, _scroll.y - _rowHeight);
					}
					else if (evt.mousePosition.y > area.yMax)
					{
						_scroll.y += _rowHeight;
					}

					_selectionEnd = Mathf.Clamp(RowAt(evt.mousePosition.y, area), 0, Mathf.Max(rowCount - 1, 0));
					evt.Use();
					Repaint();
					break;
				}

				case EventType.MouseUp when GUIUtility.hotControl == controlId:
				{
					GUIUtility.hotControl = 0;
					evt.Use();
					break;
				}

				case EventType.ContextClick when area.Contains(evt.mousePosition):
				{
					var row = RowAt(evt.mousePosition.y, area);

					// 선택 밖 줄을 우클릭하면 그 줄을 선택하고 메뉴를 띄운다.
					if (row < rowCount && !IsSelected(row))
					{
						_selectionAnchor = row;
						_selectionEnd    = row;
					}

					ShowContextMenu(rowCount);
					evt.Use();
					Repaint();
					break;
				}

				case EventType.ValidateCommand when evt.commandName is "Copy" or "SelectAll":
				{
					evt.Use();
					break;
				}

				case EventType.ExecuteCommand when evt.commandName == "Copy":
				{
					CopySelection();
					evt.Use();
					break;
				}

				case EventType.ExecuteCommand when evt.commandName == "SelectAll" && rowCount > 0:
				{
					_selectionAnchor = 0;
					_selectionEnd    = rowCount - 1;
					evt.Use();
					Repaint();
					break;
				}
			}
		}

		private void ShowContextMenu(int rowCount)
		{
			var menu = new GenericMenu();

			if (HasSelection)
			{
				menu.AddItem(new GUIContent("선택 줄 복사"), false, CopySelection);
			}
			else
			{
				menu.AddDisabledItem(new GUIContent("선택 줄 복사"));
			}

			if (rowCount > 0)
			{
				menu.AddItem(new GUIContent("전체 복사"), false, () => CopyRows(0, rowCount - 1));
			}
			else
			{
				menu.AddDisabledItem(new GUIContent("전체 복사"));
			}

			menu.ShowAsContext();
		}

		// 창 좌표의 y를 줄 번호로 바꾼다(스크롤 반영).
		private int RowAt(float mouseY, Rect area) =>
			Mathf.FloorToInt((mouseY - area.y + _scroll.y) / _rowHeight);

		private bool IsSelected(int row) =>
			HasSelection && row >= Mathf.Min(_selectionAnchor, _selectionEnd) && row <= Mathf.Max(_selectionAnchor, _selectionEnd);

		private void CopySelection()
		{
			if (!HasSelection)
			{
				return;
			}

			CopyRows(Mathf.Min(_selectionAnchor, _selectionEnd), Mathf.Max(_selectionAnchor, _selectionEnd));
		}

		// from~to 줄을 개행으로 이어 클립보드에 넣는다(RowCount를 넘는 범위는 잘라 낸다).
		private void CopyRows(int from, int to)
		{
			var lastRow = Mathf.Min(to, RowCount - 1);

			if (from > lastRow)
			{
				return;
			}

			var sb = new StringBuilder();

			for (var i = from; i <= lastRow; i++)
			{
				if (i > from)
				{
					sb.Append('\n');
				}
				sb.Append(RowText(i));
			}

			EditorGUIUtility.systemCopyBuffer = sb.ToString();
		}

		// 앞줄이 잘린 만큼 선택 줄 번호를 당긴다. 선택 전체가 잘려 나갔으면 해제한다.
		private void ShiftSelection(int delta)
		{
			if (!HasSelection)
			{
				return;
			}

			_selectionAnchor += delta;
			_selectionEnd    += delta;

			if (_selectionAnchor < 0 && _selectionEnd < 0)
			{
				ClearSelection();

				return;
			}

			_selectionAnchor = Mathf.Max(0, _selectionAnchor);
			_selectionEnd    = Mathf.Max(0, _selectionEnd);
		}

		private void ClearSelection()
		{
			_selectionAnchor = -1;
			_selectionEnd    = -1;
		}

		#endregion
	}
}

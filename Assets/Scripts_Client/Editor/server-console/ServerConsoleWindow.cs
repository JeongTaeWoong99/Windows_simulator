using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
	/// <summary>
	/// WSGameServer를 켜고/끄고(토글), 서버 로그를 터미널처럼 실시간으로 보여주는 에디터 창.
	/// 실행/종료 자체는 'ServerRunner'가 하고, 이 창은 상태 표시 + 로그 파일 tail만 담당한다.
	/// ★ 로그는 'ServerRunner'가 남기는 파일을 주기적으로 읽어 붙인다 —
	///    도메인 리로드로 이 창이 다시 만들어져도 파일을 이어 읽어 로그 연속성이 유지된다.
	/// </summary>
	internal sealed class ServerConsoleWindow : EditorWindow
	{
		private const int    MaxLines     = 5000;   // 로그가 무한히 쌓이지 않게 최근 N줄만 유지
		private const double PollInterval = 0.5;    // 로그 파일을 다시 읽는 주기(초)

		// 서버 로그는 "시각·레벨·스레드" 컬럼을 공백 패딩으로 세로 정렬한다(ServerLog 포맷).
		// 이 정렬은 고정폭 폰트에서만 성립한다 → 프로젝트에 포함된 네오둥근모(한글·영문 지원)를 쓴다.
		//
		// ★ OS 시스템 폰트(굴림체 등)를 Font.CreateDynamicFontFromOSFont로 참조하지 않는다.
		//   해당 폰트 데이터가 없으면 매 OnGUI의 CalcHeight에서
		//   "Unable to find a font file..."·"No Font Asset has been assigned" 오류가 반복된다.
		//   프로젝트 폰트 에셋(TTF)만 AssetDatabase로 직접 로드해 쓴다.
		// ★ neodgm은 "16픽셀 격자" 도트 폰트라 16의 배수(16·32…)에서 글자 폭이 격자에 떨어진다.
		private const string LogFontAssetPath = "Assets/Resources/Fonts/neodgm_pro.ttf";
		private const int    LogFontSize      = 16;   // neodgm 격자 크기(16의 배수 권장)

		private readonly List<string> _lines = new();
		private          string       _pending = "";              // 아직 개행이 안 온 마지막 줄 조각
		private          long         _readOffset;                // 다음에 읽기 시작할 파일의 바이트 위치
		private          string       _cachedText = "";           // 표시용으로 합쳐 둔 로그(줄이 바뀔 때만 갱신)
		private          bool         _cacheDirty;

		private Vector2   _scroll;
		private bool      _autoScroll = true;
		private bool      _scrollToBottom;
		private double    _nextPoll;
		private bool      _running;
		private GUIStyle? _logStyle;
		private bool      _fontWarned;   // 폰트 미탐 경고를 1회만 남기기 위한 상태

		/// <summary>서버 콘솔 창을 연다(툴바 버튼이 부른다).</summary>
		public static void Open()
		{
			var window = GetWindow<ServerConsoleWindow>("서버 콘솔");
			window.minSize = new Vector2(420, 240);
			window.Show();
		}

		private void OnEnable()
		{
			// 이미 쌓여 있던 로그를 처음부터 읽어 온다(창을 새로 열거나 리로드로 다시 만들어진 경우 이력 복원).
			_readOffset = 0;
			_lines.Clear();
			_pending = "";
			_running = ServerRunner.IsRunning;
			PollLog();
			_scrollToBottom = true;
			EditorApplication.update += OnUpdate;
		}

		private void OnDisable()
		{
			EditorApplication.update -= OnUpdate;
		}

		private void OnUpdate()
		{
			if (EditorApplication.timeSinceStartup < _nextPoll)
				return;
			_nextPoll = EditorApplication.timeSinceStartup + PollInterval;

			var before = _lines.Count;
			var wasRunning = _running;
			_running = ServerRunner.IsRunning;
			PollLog();

			if (_lines.Count != before || _running != wasRunning)
			{
				if (_autoScroll && _lines.Count != before)
					_scrollToBottom = true;
				Repaint();
			}
		}

		// 로그 파일에서 지난번 이후 늘어난 부분만 읽어 줄 목록에 붙인다.
		private void PollLog()
		{
			var path = ServerRunner.LogFilePath;
			if (!File.Exists(path))
				return;

			try
			{
				// cmd가 쓰는 중인 파일이라 공유 모드를 열어 둔다.
				using var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
					FileShare.ReadWrite | FileShare.Delete);

				// 파일이 줄었으면 새 세션이 시작돼 잘렸다는 뜻 — 처음부터 다시 읽는다.
				if (fs.Length < _readOffset)
				{
					_readOffset = 0;
					_lines.Clear();
					_pending = "";
					_cacheDirty = true;
				}

				if (fs.Length == _readOffset)
					return;

				fs.Seek(_readOffset, SeekOrigin.Begin);
				using var reader = new StreamReader(fs, Encoding.UTF8);
				var chunk = reader.ReadToEnd();
				_readOffset = fs.Length;
				AppendChunk(chunk);
			}
			catch
			{
				// 읽기 실패(순간적 잠금 등)는 다음 주기에 다시 시도한다.
			}
		}

		private void AppendChunk(string chunk)
		{
			_pending += chunk.Replace("\r\n", "\n").Replace('\r', '\n');

			int nl;
			while ((nl = _pending.IndexOf('\n')) >= 0)
			{
				_lines.Add(_pending.Substring(0, nl));
				_pending = _pending.Substring(nl + 1);
			}

			if (_lines.Count > MaxLines)
				_lines.RemoveRange(0, _lines.Count - MaxLines);

			_cacheDirty = true;
		}

		private void RebuildCacheIfNeeded()
		{
			if (!_cacheDirty)
				return;

			var sb = new StringBuilder();
			foreach (var line in _lines)
				sb.Append(line).Append('\n');
			if (_pending.Length > 0)
				sb.Append(_pending);
			_cachedText = sb.ToString();
			_cacheDirty = false;
		}

		// 로그 표시용 폰트를 프로젝트 에셋(TTF)에서만 불러온다. OS 시스템 폰트는 참조하지 않는다.
		// 못 찾으면 null을 돌려주고, 호출부는 폰트를 지정하지 않은 기본 스타일로 안전하게 그린다.
		private static Font? LoadLogFont() =>
			AssetDatabase.LoadAssetAtPath<Font>(LogFontAssetPath);

		// 로그 스타일을 보장한다. 도메인 리로드·에디터 재시작으로 _logStyle이 null이 되거나
		// (에셋 임포트 전이라) 폰트가 아직 안 붙은 경우, OnGUI 진입 때마다 폰트 로드를 재시도한다.
		// 폰트를 못 붙여도 에디터 기본 폰트로 그린다(CalcHeight가 null 폰트로 오류 나지 않음).
		private void EnsureLogStyle()
		{
			// 이미 폰트까지 정상적으로 붙었으면 그대로 쓴다.
			if (_logStyle is { font: not null })
				return;

			var font = LoadLogFont();

			_logStyle = new GUIStyle(EditorStyles.textArea)
			{
				fontSize = LogFontSize,
				richText = false,
				wordWrap = false,
			};

			if (font != null)
			{
				_logStyle.font = font;
			}
			else if (!_fontWarned)
			{
				// 폰트를 못 찾아도 기본 폰트로 계속 동작한다 — 경고는 한 번만 남긴다(매 프레임 도배 방지).
				_fontWarned = true;
				Debug.LogWarning($"[서버 콘솔] 로그 폰트를 찾지 못했다: {LogFontAssetPath} — 에디터 기본 폰트로 표시한다.");
			}
		}

		private void OnGUI()
		{
			EnsureLogStyle();

			DrawToolbar();
			RebuildCacheIfNeeded();
			DrawLog();
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
					_pending = "";
					_readOffset = FileEndOffset();
					_cacheDirty = true;
				}

				if (GUILayout.Button("파일 열기", EditorStyles.toolbarButton, GUILayout.Width(65)))
					EditorUtility.RevealInFinder(ServerRunner.LogFilePath);
			}
		}

		private void DrawLog()
		{
			_scroll = EditorGUILayout.BeginScrollView(_scroll);

			// EnsureLogStyle이 항상 채워 주지만, 만일을 대비해 기본 스타일로도 안전하게 동작하게 한다.
			var style  = _logStyle ?? EditorStyles.textArea;
			var width  = Mathf.Max(position.width - 24, 100);
			var height = style.CalcHeight(new GUIContent(_cachedText), width);
			EditorGUILayout.SelectableLabel(_cachedText, style,
				GUILayout.ExpandWidth(true), GUILayout.MinHeight(height));

			EditorGUILayout.EndScrollView();

			if (_scrollToBottom && Event.current.type == EventType.Repaint)
			{
				_scroll.y = float.MaxValue;   // BeginScrollView가 최대치로 클램프해 맨 아래로 붙는다
				_scrollToBottom = false;
				Repaint();
			}
		}

		private long FileEndOffset()
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
	}
}

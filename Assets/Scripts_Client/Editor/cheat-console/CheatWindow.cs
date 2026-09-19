using System;
using System.Collections.Generic;
using GameData;
using MikaProtocol;
using UnityEditor;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
	// 서버 치트를 한 화면에 모아 보내는 에디터 창. 인스펙터처럼 도킹해 두고 쓴다.
	// 보내기·응답 대기·로그는 'CheatSender', 보내기 전 검사·팝업은 'CheatGuard'가 한다 — 이 창은 그리기만 한다.
	// 치트를 추가하는 절차는 'cheat-console 규칙.md'.
	internal sealed class CheatWindow : EditorWindow
	{
		private const double StatusPollInterval = 0.5;   // 상태 줄(서버·클라·로그인)을 다시 확인하는 주기(초)
		private const float  LabelWidth         = 64f;
		private const float  ButtonWidth        = 56f;
		private const float  LogHeight          = 140f;
		private const float  AccentWidth        = 4f;    // 칸 왼쪽 색 띠 두께(px)
		private const float  ToolDividerWidth   = 2f;    // 도구 줄 구분선 두께(px)
		private const float  ToolGapPadding     = 5f;    // 구분선 양옆 여백(px)
		private const int    MaxCharacterCount  = 10;    // 서버 'User.CheatMaxCharacterCount'와 같다
		private const int    MaxSettleJudges    = 100;   // 서버 'User.CheatMaxSettleJudges'와 같다 — 넘기면 InvalidCheatArgs

		private static readonly Color DoneColor    = new(0.45f, 0.85f, 0.45f);
		private static readonly Color PendingColor = new(0.95f, 0.65f, 0.25f);
		private static readonly Color ErrorColor   = new(1.00f, 0.45f, 0.45f);

		// 어두운 스킨·밝은 스킨 둘 다에서 보이도록 검정을 반투명으로 쓴다.
		private static readonly Color ToolDividerColor = new(0f, 0f, 0f, 0.55f);

		// 칸마다 색을 달리해 접지 않고도 경계가 한눈에 보이게 한다.
		private static readonly Color CurrencyAccent  = new(0.95f, 0.78f, 0.25f);   // 재화 — 금색
		private static readonly Color ItemAccent      = new(0.40f, 0.80f, 0.45f);   // 자원 지급 — 초록
		private static readonly Color CharacterAccent = new(0.35f, 0.65f, 1.00f);   // 캐릭터 지급 — 파랑
		private static readonly Color ExpAccent       = new(0.70f, 0.50f, 0.95f);   // 경험치 — 보라
		private static readonly Color EquipAccent     = new(0.90f, 0.45f, 0.70f);   // 장비 지급 — 분홍
		private static readonly Color SettleAccent    = new(0.30f, 0.80f, 0.80f);   // 정산 — 청록
		private static readonly Color UnlockAccent    = new(0.95f, 0.55f, 0.30f);   // 해금 — 주황

		private static readonly long[] GoldQuickAmounts = { 1_000, 100_000, -1_000 };
		private static readonly long[] DiaQuickAmounts  = { 100, 1_000, -100 };

		// 입력값 — 플레이 진입(도메인 리로드)에도 남도록 직렬화한다.
		[SerializeField] private long      _goldAmount      = 1_000;
		[SerializeField] private long      _diaAmount       = 100;
		[SerializeField] private TidPicker _itemPicker      = new();
		[SerializeField] private int       _itemCount       = 10;
		[SerializeField] private TidPicker _characterPicker = new();
		[SerializeField] private int       _characterCount  = 1;
		[SerializeField] private long      _expCharacterId;
		[SerializeField] private int       _expAmount       = 100;
		[SerializeField] private TidPicker _equipPicker     = new();
		[SerializeField] private int       _settleJudges    = 1;
		[SerializeField] private int       _unlockTid;

		private Vector2 _scroll;
		private Vector2 _logScroll;
		private bool    _logScrollToBottom = true;
		private double  _nextStatusPoll;

		private CheatGuard.Step _completedSteps;

		// 치트 창을 연다 (툴바 버튼·메뉴)
		[MenuItem("Window/DesktopWindowControl/치트")]
		public static void Open()
		{
			var window = GetWindow<CheatWindow>("치트");
			window.minSize = new Vector2(360, 320);
			window.Show();
		}

		// 로그·상태 갱신 구독 (Unity 메시지 — 창 열기·도메인 리로드)
		private void OnEnable()
		{
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

			_scroll = EditorGUILayout.BeginScrollView(_scroll);

			DrawCurrency();
			DrawItem();
			DrawCharacter();
			DrawCharacterExp();
			DrawEquip();
			DrawSettle();
			DrawUnlock();

			EditorGUILayout.Space(6f);
			EditorGUILayout.EndScrollView();

			DrawLog();
		}

		#region 갱신

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

		// 치트를 다음 에디터 틱에 보낸다 (각 [지급] 버튼)
		// ★ 버튼 처리 중(OnGUI 안)에 모달 팝업을 띄우면 레이아웃 그룹이 어긋나 오류가 난다 — 'CheatGuard' 팝업을 OnGUI 밖으로 뺀다.
		private static void Request(ECheatCommand command, long arg1 = 0, long arg2 = 0)
		{
			EditorApplication.delayCall += () => CheatSender.Send(command, arg1, arg2);
		}

		#endregion

		#region 그리기 — 도구 줄 · 상태 줄 · 로그

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
			GUI.contentColor = isAutoCheck ? DoneColor : previous;

			if (GUILayout.Button(alertContent, EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
			{
				SceneCopySettings.AutoCheckEnabled = !isAutoCheck;
			}

			GUI.contentColor = previous;

			var copyContent = new GUIContent
				(" 오리지널 씬 복사", EditorIcons.Get(EditorIcons.Duplicate), "Scenes/Original → Scenes/Test Copy 로 최신본 복사");

			if (GUILayout.Button(copyContent, EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
			{
				// 확인 팝업을 띄우므로 OnGUI 밖에서 부른다('Request' 주석과 같은 이유).
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
				DrawColoredLabel(missing == CheatGuard.Step.None ? "준비 완료" : $"{CheatGuard.NamesOf(missing)} 미완료",
				                 missing == CheatGuard.Step.None ? DoneColor : PendingColor);
			}
		}

		private void DrawStep(string label, CheatGuard.Step step)
		{
			var isDone = _completedSteps.HasFlag(step);

			DrawColoredLabel($"{(isDone ? "●" : "○")} {label}", isDone ? DoneColor : PendingColor);
		}

		private static void DrawColoredLabel(string text, Color color)
		{
			var previous = GUI.contentColor;
			GUI.contentColor = color;

			GUILayout.Label(text, EditorStyles.miniLabel);

			GUI.contentColor = previous;
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

			_logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.Height(LogHeight));

			var previous = GUI.contentColor;

			foreach (var entry in CheatSender.Log)
			{
				GUI.contentColor = entry.IsError ? ErrorColor : previous;
				EditorGUILayout.LabelField($"{entry.Time}  {entry.Text}", EditorStyles.miniLabel);
			}

			GUI.contentColor = previous;

			EditorGUILayout.EndScrollView();
		}

		#endregion

		#region 그리기 — 치트 칸

		private void DrawCurrency()
		{
			BeginSection("재화", CurrencyAccent);

			DrawCurrencyRow("골드", ECheatCommand.GiveGold, ref _goldAmount, GoldQuickAmounts);
			EditorGUILayout.Space(2f);
			DrawCurrencyRow("다이아", ECheatCommand.GiveDia, ref _diaAmount, DiaQuickAmounts);

			EndSection(CurrencyAccent);
		}

		private static void DrawCurrencyRow(string label, ECheatCommand command, ref long amount, long[] quickAmounts)
		{
			using (new EditorGUILayout.HorizontalScope())
			{
				EditorGUILayout.LabelField(label, GUILayout.Width(LabelWidth));
				amount = EditorGUILayout.LongField(amount);

				if (GUILayout.Button("지급", GUILayout.Width(ButtonWidth)))
				{
					Request(command, amount);
				}
			}

			using (new EditorGUILayout.HorizontalScope())
			{
				GUILayout.Space(LabelWidth + 4f);

				foreach (var quickAmount in quickAmounts)
				{
					var text = quickAmount > 0 ? $"+{quickAmount:N0}" : $"{quickAmount:N0}";

					if (GUILayout.Button(text, EditorStyles.miniButton))
					{
						Request(command, quickAmount);
					}
				}
			}
		}

		private void DrawItem()
		{
			BeginSection("자원 지급", ItemAccent);

			var rows = GameDataLoader.IsLoaded ? GameTable.ItemTable.All : null;
			_itemPicker.Draw(rows, row => row.ItemTID, row => row.Name);

			using (new EditorGUILayout.HorizontalScope())
			{
				EditorGUILayout.LabelField("개수", GUILayout.Width(LabelWidth));
				_itemCount = EditorGUILayout.IntField(_itemCount);

				if (GUILayout.Button("지급", GUILayout.Width(ButtonWidth)))
				{
					Request(ECheatCommand.GiveItem, _itemPicker.Tid, _itemCount);
				}
			}

			EndSection(ItemAccent);
		}

		private void DrawCharacter()
		{
			BeginSection("캐릭터 지급", CharacterAccent);

			var rows = GameDataLoader.IsLoaded ? GameTable.CharacterTable.All : null;
			_characterPicker.Draw(rows, row => row.CharacterTID, row => row.Name);

			using (new EditorGUILayout.HorizontalScope())
			{
				EditorGUILayout.LabelField("장수", GUILayout.Width(LabelWidth));
				_characterCount = EditorGUILayout.IntSlider(_characterCount, 1, MaxCharacterCount);

				if (GUILayout.Button("지급", GUILayout.Width(ButtonWidth)))
				{
					Request(ECheatCommand.GiveCharacter, _characterPicker.Tid, _characterCount);
				}
			}

			EndSection(CharacterAccent);
		}

		// 경험치는 캐릭터 '종류'가 아니라 내가 가진 '개체'에 준다 — 목록은 테이블이 아니라 보유 캐릭터다.
		private void DrawCharacterExp()
		{
			BeginSection("캐릭터 경험치", ExpAccent);

			var model = CheatGuard.FindLoggedInModel();

			using (new EditorGUILayout.HorizontalScope())
			{
				EditorGUILayout.LabelField("캐릭터", GUILayout.Width(LabelWidth));

				if (model != null && model.Characters.Count > 0)
				{
					_expCharacterId = DrawOwnedCharacterPopup(model, _expCharacterId);
				}
				else
				{
					// 목록이 없어도 버튼은 누를 수 있게 둔다 — 누르면 'CheatGuard'가 왜 안 되는지 알려 준다.
					_expCharacterId = EditorGUILayout.LongField(_expCharacterId);
					GUILayout.Label(model == null ? "(로그인 후 목록)" : "(보유 없음)", EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
				}
			}

			using (new EditorGUILayout.HorizontalScope())
			{
				EditorGUILayout.LabelField("경험치", GUILayout.Width(LabelWidth));
				_expAmount = EditorGUILayout.IntField(_expAmount);

				if (GUILayout.Button("지급", GUILayout.Width(ButtonWidth)))
				{
					Request(ECheatCommand.GiveCharacterExp, _expCharacterId, _expAmount);
				}
			}

			EndSection(ExpAccent);
		}

		private static long DrawOwnedCharacterPopup(PlayerDataModel model, long selectedId)
		{
			var characters = model.Characters;
			var labels     = new string[characters.Count];
			var selected   = 0;

			for (var i = 0; i < characters.Count; i++)
			{
				var character = characters[i];
				labels[i] = $"{model.GetCharacterName(character.CharacterId)}  Lv{character.Level}  Exp{character.Exp}  (#{character.CharacterId})";

				if (character.CharacterId == selectedId)
				{
					selected = i;
				}
			}

			var index = EditorGUILayout.Popup(selected, labels);

			return characters[index].CharacterId;
		}

		// 장비는 개수 칸이 없다 — 서버 'CheatGiveEquip'이 한 번에 개체 1개만 만든다(창고 첫 빈 칸).
		private void DrawEquip()
		{
			BeginSection("장비 지급", EquipAccent);

			var rows = GameDataLoader.IsLoaded ? GameTable.EquipTable.All : null;
			_equipPicker.Draw(rows, row => row.EquipTID, row => row.Name);

			using (new EditorGUILayout.HorizontalScope())
			{
				GUILayout.FlexibleSpace();

				if (GUILayout.Button("지급", GUILayout.Width(ButtonWidth)))
				{
					Request(ECheatCommand.GiveEquip, _equipPicker.Tid);
				}
			}

			EndSection(EquipAccent);
		}

		// 판정 횟수만큼 작업량을 앞당겨 정산한다 — 쌓인 진행도만 정산하면 스케줄러(0.1초)가 먼저 가져가 늘 0개다.
		private void DrawSettle()
		{
			BeginSection("정산", SettleAccent);

			using (new EditorGUILayout.HorizontalScope())
			{
				EditorGUILayout.LabelField("판정 횟수", GUILayout.Width(LabelWidth));
				_settleJudges = EditorGUILayout.IntSlider(_settleJudges, 1, MaxSettleJudges);
			}

			if (GUILayout.Button("지금 정산 (작업슬롯 주기를 기다리지 않는다)"))
			{
				Request(ECheatCommand.Settle, _settleJudges);
			}

			EndSection(SettleAccent);
		}

		private void DrawUnlock()
		{
			BeginSection("해금", UnlockAccent);

			if (GameDataLoader.IsLoaded)
			{
				DrawUnlockRows();
			}
			else
			{
				using (new EditorGUILayout.HorizontalScope())
				{
					EditorGUILayout.LabelField("UnlockTID", GUILayout.Width(LabelWidth));
					_unlockTid = EditorGUILayout.IntField(_unlockTid);

					if (GUILayout.Button("열기", GUILayout.Width(ButtonWidth)))
					{
						Request(ECheatCommand.Unlock, _unlockTid);
					}
				}

				EditorGUILayout.LabelField("목록은 Play 중 테이블을 읽은 뒤 뜬다", EditorStyles.miniLabel);
			}

			EndSection(UnlockAccent);
		}

		private static void DrawUnlockRows()
		{
			var model = CheatGuard.FindLoggedInModel();

			foreach (var row in GameTable.UnlockTable.All)
			{
				// 로그인 전에는 열림 여부를 모른다 — '?'로 두고 버튼은 살린다(누르면 'CheatGuard'가 막는다).
				var isUnlocked = model != null && model.IsUnlocked(row.UnlockTID);

				using (new EditorGUILayout.HorizontalScope())
				{
					if (model == null)
					{
						DrawColoredLabel("[ ? ]", GUI.contentColor);
					}
					else
					{
						DrawColoredLabel(isUnlocked ? "[열림]" : "[잠김]", isUnlocked ? DoneColor : PendingColor);
					}

					EditorGUILayout.LabelField($"{row.Name} ({row.UnlockTID})");

					using (new EditorGUI.DisabledScope(isUnlocked))
					{
						if (GUILayout.Button("열기", GUILayout.Width(ButtonWidth)))
						{
							Request(ECheatCommand.Unlock, row.UnlockTID);
						}
					}
				}
			}
		}

		// 칸 머리 — 색 띠 + 옅게 물든 바탕 + 굵은 제목. 본문은 'EndSection'까지 상자 안에 그린다.
		private static void BeginSection(string title, Color accent)
		{
			EditorGUILayout.Space(6f);

			var header = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 2f);
			EditorGUI.DrawRect(header, new Color(accent.r, accent.g, accent.b, 0.22f));
			EditorGUI.DrawRect(new Rect(header.x, header.y, AccentWidth, header.height), accent);

			var titleRect = new Rect(header.x + AccentWidth + 6f, header.y, header.width - AccentWidth - 6f, header.height);
			EditorGUI.LabelField(titleRect, title, EditorStyles.boldLabel);

			EditorGUILayout.BeginVertical(EditorStyles.helpBox);
		}

		// 칸 본문을 닫고, 본문 상자 왼쪽에도 같은 색 띠를 그어 머리와 이어 보이게 한다.
		// 상자 크기는 레이아웃이 끝난 뒤에야 알 수 있어 Repaint 때만 그린다.
		private static void EndSection(Color accent)
		{
			EditorGUILayout.EndVertical();

			if (Event.current.type != EventType.Repaint)
			{
				return;
			}

			var body = GUILayoutUtility.GetLastRect();
			EditorGUI.DrawRect(new Rect(body.x, body.y, AccentWidth, body.height), accent);
		}

		#endregion

		// 테이블에서 TID 하나를 고르는 칸 — 검색어로 거른 '이름 (TID)' 목록.
		// 테이블을 아직 읽지 않았으면(플레이 전) TID를 숫자로 직접 받는다.
		[Serializable]
		private sealed class TidPicker
		{
			public string Filter = "";
			public int    Tid;

			// 검색어·원본이 그대로면 목록을 다시 만들지 않는다 — OnGUI는 이벤트마다 여러 번 불린다.
			[NonSerialized] private string?  _cachedFilter;
			[NonSerialized] private object?  _cachedSource;
			[NonSerialized] private int[]    _tids   = Array.Empty<int>();
			[NonSerialized] private string[] _labels = Array.Empty<string>();

			public void Draw<TRow>(IReadOnlyList<TRow>? rows, Func<TRow, int> tidOf, Func<TRow, string> nameOf)
			{
				using (new EditorGUILayout.HorizontalScope())
				{
					EditorGUILayout.LabelField("TID", GUILayout.Width(LabelWidth));

					if (rows == null)
					{
						Tid = EditorGUILayout.IntField(Tid);
						GUILayout.Label("(Play 중 목록)", EditorStyles.miniLabel, GUILayout.ExpandWidth(false));

						return;
					}

					Filter = EditorGUILayout.TextField(Filter, EditorStyles.toolbarSearchField, GUILayout.Width(100f));

					if (!ReferenceEquals(rows, _cachedSource) || Filter != _cachedFilter)
					{
						Rebuild(rows, tidOf, nameOf);
					}

					var index = EditorGUILayout.Popup(Array.IndexOf(_tids, Tid), _labels);

					if (index >= 0 && index < _tids.Length)
					{
						Tid = _tids[index];
					}
				}
			}

			private void Rebuild<TRow>(IReadOnlyList<TRow> rows, Func<TRow, int> tidOf, Func<TRow, string> nameOf)
			{
				var tids   = new List<int>();
				var labels = new List<string>();

				foreach (var row in rows)
				{
					var tid   = tidOf(row);
					var label = $"{nameOf(row)} ({tid})";

					if (Filter.Length > 0 && label.IndexOf(Filter, StringComparison.OrdinalIgnoreCase) < 0)
					{
						continue;
					}

					tids.Add(tid);
					labels.Add(label);
				}

				_tids         = tids.ToArray();
				_labels       = labels.ToArray();
				_cachedFilter = Filter;
				_cachedSource = rows;
			}
		}
	}
}

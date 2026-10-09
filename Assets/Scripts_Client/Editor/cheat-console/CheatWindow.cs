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
		private const float  StatusWidth        = 42f;   // 해금 줄의 '[열림]'·'[잠김]' 고정 폭 — 이름 열을 맞춘다
		private const float  IndustryHeadHeight = 18f;   // 특성 칸 안 산업 이름 띠 높이(px)
		private const float  RowIndent          = 10f;   // 칸 안 줄 들여쓰기(px) — 산업 띠와 줄이 같은 선에서 시작한다
		private const float  GroupButtonWidth   = 96f;   // 산업 띠 오른쪽 끝 일괄 버튼 폭(px)
		private const float  TraitLevelWidth    = 56f;   // 특성 줄의 'Lv 지금/최대' 고정 폭 — 버튼 열을 맞춘다
		private const float  StepButtonWidth    = 34f;   // 특성 줄의 [기본]·[-1]·[+1]·[최대] 폭(px)

		// 전역 배수 천분율 범위 — 서버 'SetGatherSpeed'가 받는 범위와 같다(밖이면 InvalidCheatArgs).
		private const int MinGatherSpeedPermille = 100;
		private const int MaxGatherSpeedPermille = 100_000;

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
		private static readonly Color MailAccent      = new(0.60f, 0.60f, 0.65f);   // 우편 — 회색
		private static readonly Color TraitAccent     = new(0.65f, 0.85f, 0.30f);   // 특성 레벨 — 연두
		private static readonly Color TimeAccent      = new(0.85f, 0.30f, 0.35f);   // 시간 — 진홍

		private static readonly long[] GoldQuickAmounts = { 1_000, 100_000, -1_000 };

		// 시간 넘기기 단축 버튼 — 48시간은 경매 등록 기간, 7일은 받은 우편 보관 기간이다.
		private static readonly (string Label, long Seconds)[] TimeQuickSteps =
		{
			("+1시간", 3_600), ("+1일", 86_400), ("+2일", 172_800), ("+7일", 604_800),
		};

		// 입력값 — 플레이 진입(도메인 리로드)에도 남도록 직렬화한다.
		[SerializeField] private long      _goldAmount      = 1_000;
		[SerializeField] private TidPicker _itemPicker      = new();
		[SerializeField] private int       _itemCount       = 10;
		[SerializeField] private TidPicker _characterPicker = new();
		[SerializeField] private int       _characterCount  = 1;
		[SerializeField] private long      _expCharacterId;
		[SerializeField] private int       _expAmount       = 100;
		[SerializeField] private int       _condenseCount;
		[SerializeField] private TidPicker _equipPicker     = new();
		[SerializeField] private int       _settleJudges    = 1;
		[SerializeField] private int       _unlockTid;
		[SerializeField] private TidPicker _mailPicker      = new();
		[SerializeField] private long      _mailTargetUid;  // 0이면 전체 우편
		[SerializeField] private long      _advanceSeconds  = 3_600;
		[SerializeField] private int       _gatherSpeedPermille = 2_000;

		private Vector2 _scroll;
		private Vector2 _logScroll;
		private bool    _logScrollToBottom = true;
		private double  _nextStatusPoll;

		private CheatGuard.Step _completedSteps;

		// 슬라이더 상한 — 서버와 같은 'Constants.xlsx' 값을 읽는다(넘기면 서버가 InvalidCheatArgs로 거절).
		// 테이블은 Play에 들어가야 적재되므로 그 전에는 1로 둔다 — 그때는 어차피 보낼 수 없다.
		private static int MaxCharacterCount => GameDataLoader.IsLoaded ? (int)Constants.CheatMaxCharacterCount : 1;
		private static int MaxSettleJudges   => GameDataLoader.IsLoaded ? (int)Constants.CheatMaxSettleJudges   : 1;

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
			DrawTime();
			DrawMail();
			DrawUnlock();
			DrawTraitLevel();

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

		// '[●]주기 수신 로그' — 수확마다 오는 수신 4종(채취 정산·슬롯·캐릭터 동기화·계정 레벨)을 콘솔에 찍을지.
		// 평시엔 꺼 둔다 — 'Debug.Log'가 줄마다 스택을 뽑아 수확 프레임이 튄다('Log 규칙.md' 3장).
		private static void DrawPeriodicLogTool()
		{
			var isOn = PeriodicLogSettings.Enabled;
			var content = new GUIContent
				(isOn ? "[●] 주기 수신 로그" : "[○] 주기 수신 로그",
				 isOn ? "수확마다 오는 수신 로그를 찍는 중 — 누르면 끈다" : "수확마다 오는 수신 로그 꺼짐 — 누르면 켠다");

			var previous = GUI.contentColor;
			GUI.contentColor = isOn ? DoneColor : previous;

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
				DrawColoredLabel(missing == CheatGuard.Step.None ? "준비 완료" : $"{CheatGuard.NamesOf(missing)} 미완료",
				                 missing == CheatGuard.Step.None ? DoneColor : PendingColor);
			}
		}

		private void DrawStep(string label, CheatGuard.Step step)
		{
			var isDone = _completedSteps.HasFlag(step);

			DrawColoredLabel($"{(isDone ? "●" : "○")} {label}", isDone ? DoneColor : PendingColor);
		}

		// 색 입힌 한 줄. 'width'를 주면 고정 폭으로 그린다 — 여러 줄의 다음 칸을 세로로 맞출 때 쓴다.
		private static void DrawColoredLabel(string text, Color color, float width = 0f)
		{
			var previous = GUI.contentColor;
			GUI.contentColor = color;

			if (width > 0f)
			{
				GUILayout.Label(text, EditorStyles.miniLabel, GUILayout.Width(width));
			}
			else
			{
				GUILayout.Label(text, EditorStyles.miniLabel);
			}

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
		// 응축 누적도 같은 개체에 정하므로 한 묶음에 둔다 — 서버 'SetCondenseCount'(Arg1 = CharacterId · Arg2 = 누적 수, T-130).
		private void DrawCharacterExp()
		{
			BeginSection("캐릭터 경험치 · 응축", ExpAccent);

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

			// 누적 0~최고 ★ 기준. 서버가 같은 범위로 자른다 — 슬라이더는 표가 안 읽혔을 때만 넉넉히 연다.
			var maxCount = GameDataLoader.IsLoaded ? GameDataLoader.GetCondenseThreshold(GameDataLoader.CondenseMaxStar) : 999;

			using (new EditorGUILayout.HorizontalScope())
			{
				EditorGUILayout.LabelField("응축 누적", GUILayout.Width(LabelWidth));
				_condenseCount = EditorGUILayout.IntSlider(_condenseCount, 0, maxCount);

				if (GUILayout.Button("정하기", GUILayout.Width(ButtonWidth)))
				{
					Request(ECheatCommand.SetCondenseCount, _expCharacterId, _condenseCount);
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
				labels[i] = $"{model.GetCharacterName(character.CharacterId)}  Lv{character.Level}  Exp{character.Exp}  {character.Star}성({character.CondenseCount})  (#{character.CharacterId})";

				if (character.CharacterId == selectedId)
				{
					selected = i;
				}
			}

			var index = EditorGUILayout.Popup(selected, labels);

			return characters[index].CharacterId;
		}

		// 장비는 개수 칸이 없다 — 서버 'CheatGiveEquip'이 한 번에 개체 1개만 만든다(인벤토리 첫 빈 칸).
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

			DrawGatherSpeed();

			EndSection(SettleAccent);
		}

		// 서버 전체의 채취 전역 배수 — 서버 'SetGatherSpeed'(Arg1 = 천분율, 100~100,000). 정산 칸 안에 붙인다.
		// 바꾸면 서버가 접속 중인 모두에게 슬롯을 다시 보내 속도·주기가 바로 바뀐다.
		// ※ 서버는 저장하지 않는다 — 재시작하면 ×1.0이다. 지금 값은 받은 슬롯의 'GatherSpeedPermille'을 읽는다.
		// ※ 배수가 아무리 커도 판정 주기는 'Constants.MinCycleMs' 아래로 내려가지 않는다 — 연출 한 바퀴 보장(T-102).
		private void DrawGatherSpeed()
		{
			EditorGUILayout.Space(4f);

			var model   = CheatGuard.FindLoggedInModel();
			var current = model != null && model.WorkStationSlots.Count > 0 ? model.WorkStationSlots[0].GatherSpeedPermille : 0;
			DrawColoredLabel(current > 0 ? $"전역 배수: ×{current / 1000f:0.00}" : "전역 배수: (로그인 후 표시)", GUI.contentColor);

			using (new EditorGUILayout.HorizontalScope())
			{
				EditorGUILayout.LabelField("천분율", GUILayout.Width(LabelWidth));
				_gatherSpeedPermille = Mathf.Clamp(EditorGUILayout.IntField(_gatherSpeedPermille), MinGatherSpeedPermille, MaxGatherSpeedPermille);

				if (GUILayout.Button("적용", GUILayout.Width(ButtonWidth)))
				{
					Request(ECheatCommand.SetGatherSpeed, _gatherSpeedPermille);
				}

				if (GUILayout.Button("×1", GUILayout.Width(ButtonWidth)))
				{
					Request(ECheatCommand.SetGatherSpeed, 1000);
				}
			}

			EditorGUILayout.LabelField("1000 = ×1.0 · 서버 전체 · 재시작하면 ×1.0", EditorStyles.miniLabel);
			EditorGUILayout.LabelField("배수를 올려도 판정은 최소 주기(엑셀 MinCycleMs)보다 짧아지지 않는다", EditorStyles.miniLabel);
		}

		// 서버 전체의 게임 시계를 앞으로 넘긴다 — 서버 'AdvanceTime'(Arg1 = 초, 1초~1년, 누적) · 'ResetTime'(오프셋 0).
		// 넘기면 서버가 'S_ServerTimeResponse'를 다시 보내 'ServerClock'이 따라가고, 남은 시간 · 삭제까지 표시가 함께 움직인다.
		//
		// ※ 채취는 쌓이지 않는다 — 서버가 슬롯 기준 시각도 같이 민다. 판정을 당기려면 위 '정산' 칸.
		// ※ 받은 우편 7일 삭제는 로그인 때 판정한다 — 넘긴 뒤 다시 접속해야 사라진다.
		private void DrawTime()
		{
			BeginSection("시간", TimeAccent);

			var isLoggedIn = CheatGuard.FindLoggedInModel() != null;
			DrawColoredLabel(isLoggedIn ? $"게임 시계: {FormatOffset(ServerClock.Offset)}" : "게임 시계: (로그인 후 표시)",
			                 GUI.contentColor);

			using (new EditorGUILayout.HorizontalScope())
			{
				foreach (var (label, seconds) in TimeQuickSteps)
				{
					if (GUILayout.Button(label, EditorStyles.miniButton))
					{
						Request(ECheatCommand.AdvanceTime, seconds);
					}
				}
			}

			using (new EditorGUILayout.HorizontalScope())
			{
				EditorGUILayout.LabelField("초", GUILayout.Width(LabelWidth));
				_advanceSeconds = Math.Max(1L, EditorGUILayout.LongField(_advanceSeconds));

				if (GUILayout.Button("넘기기", GUILayout.Width(ButtonWidth)))
				{
					Request(ECheatCommand.AdvanceTime, _advanceSeconds);
				}
			}

			if (GUILayout.Button("실제 시각으로 되돌리기"))
			{
				Request(ECheatCommand.ResetTime);
			}

			EditorGUILayout.LabelField("되돌려도 넘긴 동안 저장된 시각(우편 도착 · 경매 등록)은 미래로 남는다", EditorStyles.miniLabel);

			EndSection(TimeAccent);
		}

		// 게임 시계가 PC보다 얼마나 앞섰나 — "실제 시각" · "+2일 3시간 5분".
		// 차이에는 PC 시계 오차도 섞이므로 1분 아래는 실제 시각으로 본다.
		private static string FormatOffset(TimeSpan offset)
		{
			if (Math.Abs(offset.TotalMinutes) < 1d)
			{
				return "실제 시각";
			}

			var sign = offset < TimeSpan.Zero ? "-" : "+";
			var abs  = offset.Duration();

			return abs.TotalDays >= 1d
				? $"{sign}{(int)abs.TotalDays}일 {abs.Hours}시간 {abs.Minutes}분"
				: $"{sign}{abs.Hours}시간 {abs.Minutes}분";
		}

		// 우편 템플릿 한 줄을 보낸다. 받는 UID가 0이면 전체 우편이다 — 템플릿의 PeriodDays 동안 로그인하는 모두가 받는다
		// (PeriodDays = 0인 템플릿은 전체로 보낼 수 없어 서버가 거절한다). 넘침 보관 템플릿(2)은 첨부가 비어 있다.
		private void DrawMail()
		{
			BeginSection("우편 발송", MailAccent);

			var rows = GameDataLoader.IsLoaded ? GameTable.MailTemplateTable.All : null;
			_mailPicker.Draw(rows, row => row.MailTemplateTID, row => row.Title);

			using (new EditorGUILayout.HorizontalScope())
			{
				EditorGUILayout.LabelField("받는 UID", GUILayout.Width(LabelWidth));
				_mailTargetUid = Math.Max(0L, EditorGUILayout.LongField(_mailTargetUid));

				if (GUILayout.Button("발송", GUILayout.Width(ButtonWidth)))
				{
					Request(ECheatCommand.SendMail, _mailPicker.Tid, _mailTargetUid);
				}
			}

			EditorGUILayout.LabelField(_mailTargetUid == 0L ? "0 = 전체 우편 (접속 중인 유저 + 기간 안에 로그인하는 유저)" : "개인 우편",
				EditorStyles.miniLabel);

			EndSection(MailAccent);
		}

		// 해금 칸 — 지금 'UnlockTable'은 작업슬롯 6줄뿐이라 묶음 없이 줄을 바로 늘어놓는다.
		// 특성은 해금을 쓰지 않는다(T-108) → 아래 '특성 레벨' 칸.
		private void DrawUnlock()
		{
			BeginSection("해금", UnlockAccent);

			if (GameDataLoader.IsLoaded)
			{
				var model = CheatGuard.FindLoggedInModel();
				var rows  = new List<UnlockTableRow>(GameTable.UnlockTable.All);

				DrawUnlockAllButton("해금 전부 열기", rows, model);
				EditorGUILayout.Space(2f);

				foreach (var row in rows)
				{
					DrawUnlockRow(row, model);
				}
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

		// 일괄 열기 버튼 — 누르면 아직 잠긴 줄만 보낸다. 다 열렸으면 잠근다.
		//
		// ※ 열린 줄을 빼고 보낸다 — 섞어 보내면 로그가 'AlreadyUnlocked' 거절로 뒤덮인다.
		//   로그인 전에는 무엇이 열렸는지 모르므로 버튼만 살려 둔다(누르면 'CheatGuard'가 막는다).
		private static void DrawUnlockAllButton(string label, List<UnlockTableRow> rows, PlayerDataModel? model)
		{
			var locked = new List<(long, long)>();

			foreach (var row in rows)
			{
				if (model == null || !model.IsUnlocked(row.UnlockTID))
				{
					locked.Add((row.UnlockTID, 0L));
				}
			}

			using (new EditorGUI.DisabledScope(model != null && locked.Count == 0))
			{
				if (GUILayout.Button(label))
				{
					RequestAll(ECheatCommand.Unlock, locked);
				}
			}
		}

		// 해금 한 줄 — 상태 · 이름(TID) · [열기].
		private static void DrawUnlockRow(UnlockTableRow row, PlayerDataModel? model)
		{
			// 로그인 전에는 열림 여부를 모른다 — '?'로 두고 버튼은 살린다(누르면 'CheatGuard'가 막는다).
			var isUnlocked = model != null && model.IsUnlocked(row.UnlockTID);

			using (new EditorGUILayout.HorizontalScope())
			{
				GUILayout.Space(RowIndent);

				if (model == null)
				{
					DrawColoredLabel("[ ? ]", GUI.contentColor, StatusWidth);
				}
				else
				{
					DrawColoredLabel(isUnlocked ? "[열림]" : "[잠김]", isUnlocked ? DoneColor : PendingColor, StatusWidth);
				}

				// 'EditorGUILayout.LabelField'는 앞머리 라벨 자리에 그려서 이름이 멀찍이 떨어진다 — 바로 붙여 쓴다.
				GUILayout.Label($"{row.Name} ({row.UnlockTID})");

				using (new EditorGUI.DisabledScope(isUnlocked))
				{
					if (GUILayout.Button("열기", GUILayout.Width(ButtonWidth)))
					{
						Request(ECheatCommand.Unlock, row.UnlockTID);
					}
				}
			}
		}

		// 특성 레벨을 조건·포인트 없이 정한다 — 서버 'SetTraitLevel'(Arg1 = UserTraitTID · 0이면 전부, Arg2 = 레벨).
		//
		// ■ 서버가 기본~최대로 자른다
		// 그래서 [전부 최대]는 테이블에서 가장 큰 최대 레벨을 보내면 특성마다 제 최대로 잘린다.
		// 바뀐 것이 있으면 서버가 'S_UserTraitListResponse'를 다시 보내 특성 화면·산업 레벨 잠금이 그 자리에서 갱신된다.
		//
		// ※ 산업 개척을 내려도 이미 그 레벨로 돌던 슬롯은 그대로 돈다 — 잠금은 다시 배치할 때 걸린다(서버 결정, #51).
		private void DrawTraitLevel()
		{
			BeginSection("특성 레벨", TraitAccent);

			if (!GameDataLoader.IsLoaded)
			{
				EditorGUILayout.LabelField("목록은 Play 중 테이블을 읽은 뒤 뜬다", EditorStyles.miniLabel);
				EndSection(TraitAccent);

				return;
			}

			var model  = CheatGuard.FindLoggedInModel();
			var traits = GameDataLoader.UserTraits;

			using (new EditorGUILayout.HorizontalScope())
			{
				if (GUILayout.Button("전부 최대"))
				{
					Request(ECheatCommand.SetTraitLevel, 0L, MaxTraitLevelOf(traits));
				}

				if (GUILayout.Button("전부 기본"))
				{
					Request(ECheatCommand.SetTraitLevel, 0L, 0L);
				}
			}

			EditorGUILayout.Space(2f);

			// 공통(산업 없음)을 맨 위에, 그 아래 산업 순서대로 — 특성 화면의 줄 순서와 같다.
			var isFollowing = false;

			for (var industry = EIndustryType.None; industry <= EIndustryType.Hunting; industry++)
			{
				var rows = new List<UserTraitTableRow>();

				foreach (var trait in traits)
				{
					if ((EIndustryType)(byte)trait.Industry == industry)
					{
						rows.Add(trait);
					}
				}

				if (rows.Count == 0)
				{
					continue;
				}

				DrawTraitIndustryHead(industry == EIndustryType.None ? "공통" : IndustryLabel.Get(industry), isFollowing, rows);
				isFollowing = true;

				foreach (var trait in rows)
				{
					DrawTraitRow(trait, model);
				}
			}

			EndSection(TraitAccent);
		}

		// 특성 한 줄 — 이름(TID) · Lv 지금/최대 · [기본] [-1] [+1] [최대].
		// 끝에 닿은 쪽 버튼은 끈다. 로그인 전에는 지금 레벨을 모르니 '?'로 두고 버튼은 살린다(누르면 'CheatGuard'가 막는다).
		private static void DrawTraitRow(UserTraitTableRow trait, PlayerDataModel? model)
		{
			var level = model?.GetTraitLevel(trait.UserTraitTID) ?? trait.BaseLevel;

			using (new EditorGUILayout.HorizontalScope())
			{
				GUILayout.Space(RowIndent);
				GUILayout.Label($"{trait.Name} ({trait.UserTraitTID})");

				var levelText = model == null ? $"Lv ?/{trait.MaxLevel}" : $"Lv {level}/{trait.MaxLevel}";
				var isMax     = model != null && level >= trait.MaxLevel;

				DrawColoredLabel(levelText, isMax ? DoneColor : GUI.contentColor, TraitLevelWidth);

				var canLower = model == null || level > trait.BaseLevel;
				var canRaise = model == null || level < trait.MaxLevel;

				DrawTraitStepButton("기본", trait, trait.BaseLevel, canLower);
				DrawTraitStepButton("-1",   trait, level - 1,       canLower);
				DrawTraitStepButton("+1",   trait, level + 1,       canRaise);
				DrawTraitStepButton("최대", trait, trait.MaxLevel,  canRaise);
			}
		}

		private static void DrawTraitStepButton(string label, UserTraitTableRow trait, int targetLevel, bool isEnabled)
		{
			using (new EditorGUI.DisabledScope(!isEnabled))
			{
				if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Width(StepButtonWidth)))
				{
					Request(ECheatCommand.SetTraitLevel, trait.UserTraitTID, targetLevel);
				}
			}
		}

		// 산업 이름 띠 + 오른쪽 끝 [산업 최대] — 그 산업 특성을 각자 최대로 보낸다.
		private static void DrawTraitIndustryHead(string name, bool isFollowing, List<UserTraitTableRow> rows)
		{
			var head       = DrawIndustryBand(name, isFollowing, TraitAccent);
			var buttonRect = new Rect(head.xMax - GroupButtonWidth, head.y + 1f, GroupButtonWidth, head.height - 2f);

			if (GUI.Button(buttonRect, "산업 최대", EditorStyles.miniButton))
			{
				var commands = new List<(long, long)>();

				foreach (var trait in rows)
				{
					commands.Add((trait.UserTraitTID, trait.MaxLevel));
				}

				RequestAll(ECheatCommand.SetTraitLevel, commands);
			}
		}

		// 테이블에서 가장 큰 최대 레벨 — [전부 최대]의 Arg2. 서버가 특성마다 제 최대로 자른다.
		private static long MaxTraitLevelOf(IReadOnlyList<UserTraitTableRow> traits)
		{
			var max = 0;

			foreach (var trait in traits)
			{
				max = Math.Max(max, trait.MaxLevel);
			}

			return max;
		}

		// 같은 명령 여러 개를 한 번에 보낸다 (해금 전부 열기 · 산업 최대).
		//
		// ⚠️ 가드를 **먼저 한 번만** 본다 — 'CheatSender.Send'마다 보게 두면 로그인 전에 누른 순간
		//    경고 팝업이 줄 수만큼 뜬다.
		private static void RequestAll(ECheatCommand command, List<(long Arg1, long Arg2)> args)
		{
			EditorApplication.delayCall += () =>
			{
				if (!CheatGuard.CanSend())
				{
					return;
				}

				foreach (var (arg1, arg2) in args)
				{
					CheatSender.Send(command, arg1, arg2);
				}
			};
		}

		// 칸 안을 가르는 산업 이름 띠 — 칸 머리('BeginSection')와 같은 언어를 더 얇게 쓴다. 띠 영역을 돌려준다(오른쪽 끝 버튼 자리).
		// 옅은 바탕 + 아래 실선 한 줄. 이름만 덩그러니 띄우면 어느 쪽에 붙는 줄인지 안 보인다.
		//
		//   'isFollowing' : 앞에 다른 띠가 있었으면 위에 숨을 한 번 준다(첫 띠는 칸 머리에 바로 붙인다).
		//
		// ※ 'EditorGUI.IndentLevelScope'를 쓰지 않는다 — indentLevel은 'GUILayout' 줄에 먹지 않아
		//   띠만 밀리고 줄은 제자리에 남는다. 둘 다 'RowIndent'로 직접 민다.
		private static Rect DrawIndustryBand(string name, bool isFollowing, Color accent)
		{
			if (isFollowing)
			{
				EditorGUILayout.Space(4f);
			}

			var head = EditorGUILayout.GetControlRect(false, IndustryHeadHeight);

			head.x     += RowIndent;
			head.width -= RowIndent;

			EditorGUI.DrawRect(head, new Color(accent.r, accent.g, accent.b, 0.10f));
			EditorGUI.DrawRect(new Rect(head.x, head.yMax - 1f, head.width, 1f), new Color(accent.r, accent.g, accent.b, 0.45f));

			EditorGUI.LabelField(new Rect(head.x + 4f, head.y, head.width - 4f, head.height), name, EditorStyles.miniBoldLabel);

			return head;
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

using System;
using System.Collections.Generic;
using System.Linq;
using GameData;
using MikaProtocol;
using UnityEditor;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
	// 상자 결과 미리 세기 — 상자 N개를 지급·차감 없이 열었다고 치고 무엇이 몇 개 나오는지 표로 본다 (T-213 · 이슈 #154).
	// 서버 'SimulateBoxOpen'(Arg1 = 상자 ItemTID · Arg2 = 1~10,000회)이 실제 개봉과 같은 규칙으로 굴려
	// 'Message'에 '보상종류:TID:수량'을 쉼표로 이어 준다. 보상 종류는 'EGachaRewardType' 숫자 · 골드는 TID 0.
	[Serializable]
	internal sealed class BoxSimulateCheatSection : CheatSection
	{
		private const int MaxOpens = 10000;
		private const int MaxShown = 40;   // 표에 그리는 줄 — 넘으면 나머지를 한 줄로 묶는다

		[SerializeField] private TidPicker _picker = new();
		[SerializeField] private int       _opens  = 1000;

		[NonSerialized] private IReadOnlyList<ItemTableRow>? _boxes;
		[NonSerialized] private object?                      _boxSource;

		// 마지막 결과 — 응답은 칸 인스턴스가 아니라 'CheatSender'로 오므로 정적으로 둔다
		private static readonly List<(string Label, long Count, EGachaRewardType Type)> Result = new();
		private static string _pendingTitle = "";
		private static string _resultTitle  = "";
		private static long   _resultGold;

		static BoxSimulateCheatSection()
		{
			CheatSender.Responded -= OnResponded;
			CheatSender.Responded += OnResponded;
		}

		public override ECheatCategory Category => ECheatCategory.Give;
		public override int            Order    => 6;
		public override string         Title    => "상자 결과 미리 세기";
		public override Color          Accent   => new(0.95f, 0.80f, 0.35f);   // 금
		public override string         Keywords => "box chest gacha 확률 시뮬 simulate 열기";
		public override string         Hint     => "지급·차감 없이 서버가 실제 개봉 규칙으로 굴린다 (1~10,000회)\n상자를 갖고 있지 않아도 된다";

		public override void Draw()
		{
			_picker.Draw(BoxRows(), row => row.ItemTID, row => row.Name);

			using (new EditorGUILayout.HorizontalScope())
			{
				CheatGui.FieldLabel("횟수");
				_opens = Mathf.Clamp(EditorGUILayout.IntField(_opens), 1, MaxOpens);

				if (CheatGui.ActionButton("세기"))
				{
					_pendingTitle = $"{ItemName(_picker.Tid)} × {_opens:N0}";
					CheatGui.Request(ECheatCommand.SimulateBoxOpen, _picker.Tid, _opens);
				}
			}

			DrawResult();
		}

		// 열 수 있는 것(상자·유물 — 'OpenGachaId'가 있는 아이템)만 고른다
		private IReadOnlyList<ItemTableRow>? BoxRows()
		{
			if (!GameDataLoader.IsLoaded)
			{
				return null;
			}

			// TidPicker는 목록 참조로 캐시한다 — 매 프레임 새 목록을 넘기면 매번 다시 만든다
			if (!ReferenceEquals(_boxSource, GameTable.ItemTable.All))
			{
				_boxSource = GameTable.ItemTable.All;
				_boxes     = GameTable.ItemTable.All.Where(row => row.OpenGachaId > 0).ToList();
			}

			return _boxes;
		}

		private static void DrawResult()
		{
			if (Result.Count == 0 && _resultGold == 0)
			{
				return;
			}

			EditorGUILayout.Space(2f);
			GUILayout.Label(_resultTitle, EditorStyles.boldLabel);

			var total = Result.Sum(r => r.Count);

			if (_resultGold > 0)
			{
				Line("골드", _resultGold.ToString("N0"), "");
			}

			foreach (var (label, count, _) in Result.Take(MaxShown))
			{
				Line(label, count.ToString("N0"), total > 0 ? $"{count * 100.0 / total:0.##}%" : "");
			}

			if (Result.Count > MaxShown)
			{
				var rest = Result.Skip(MaxShown).Sum(r => r.Count);
				Line($"… 그 밖 {Result.Count - MaxShown}종", rest.ToString("N0"), "");
			}
		}

		private static void Line(string label, string count, string share)
		{
			using (new EditorGUILayout.HorizontalScope())
			{
				GUILayout.Label(label, EditorStyles.miniLabel);
				GUILayout.Label(count, EditorStyles.miniLabel, GUILayout.Width(70f));
				GUILayout.Label(share, EditorStyles.miniLabel, GUILayout.Width(50f));
			}
		}

		// 'CheatSender.Responded' 구독 — 이 명령의 성공 응답만 표로 바꾼다
		private static void OnResponded(S_CheatResponse res)
		{
			if (res.Command != ECheatCommand.SimulateBoxOpen || res.Result != EResultCode.Ok)
			{
				return;
			}

			Result.Clear();
			_resultGold  = 0;
			_resultTitle = _pendingTitle;

			foreach (var part in res.Message.Split(',', StringSplitOptions.RemoveEmptyEntries))
			{
				var fields = part.Trim().Split(':');

				if (fields.Length != 3 || !byte.TryParse(fields[0], out var type) || !int.TryParse(fields[1], out var tid) || !long.TryParse(fields[2], out var count))
				{
					continue;   // 알 수 없는 줄은 건너뛴다 — 원문은 결과 로그에 남아 있다
				}

				var rewardType = (EGachaRewardType)type;

				if (rewardType == EGachaRewardType.Gold)
				{
					_resultGold += count;

					continue;
				}

				Result.Add((RewardLabel(rewardType, tid), count, rewardType));
			}

			// 종류 → 많이 나온 순
			Result.Sort((a, b) => a.Type != b.Type ? a.Type.CompareTo(b.Type) : b.Count.CompareTo(a.Count));
		}

		private static string RewardLabel(EGachaRewardType type, int tid) => type switch
		{
			EGachaRewardType.Item      => "자원 · " + ItemName(tid),
			EGachaRewardType.Character => "캐릭터 · " + CharacterName(tid),
			EGachaRewardType.Equip     => $"장비 · {GameDataLoader.GetEquipName(tid)} [{GameDataLoader.GetEquipRarity(tid)}]",
			EGachaRewardType.Artifact  => "아티팩트 · " + GameDataLoader.GetArtifactName(tid),
			_                          => $"{type} · {tid}",
		};

		private static string ItemName(int tid)
			=> GameDataLoader.IsLoaded && GameTable.ItemTable.All.FirstOrDefault(r => r.ItemTID == tid) is { } row ? row.Name : tid.ToString();

		private static string CharacterName(int tid)
			=> GameDataLoader.IsLoaded && GameTable.CharacterTable.All.FirstOrDefault(r => r.CharacterTID == tid) is { } row ? row.Name : tid.ToString();
	}
}

using System;
using System.Collections.Generic;
using GameData;
using MikaProtocol;
using UnityEditor;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
	// 특성 레벨을 조건·포인트 없이 정한다 — 서버 'SetTraitLevel'(Arg1 = UserTraitTID · 0이면 전부, Arg2 = 레벨).
	//
	// ■ 서버가 기본~최대로 자른다
	// 그래서 [전부 최대]는 테이블에서 가장 큰 최대 레벨을 보내면 특성마다 제 최대로 잘린다.
	// 바뀐 것이 있으면 서버가 'S_UserTraitListResponse'를 다시 보내 특성 화면·산업 레벨 잠금이 그 자리에서 갱신된다.
	//
	// ※ 산업 개척을 내려도 이미 그 레벨로 돌던 슬롯은 그대로 돈다 — 잠금은 다시 배치할 때 걸린다(서버 결정, #51).
	[Serializable]
	internal sealed class TraitLevelCheatSection : CheatSection
	{
		private const float LevelWidth      = 56f;   // 'Lv 지금/최대' 고정 폭 — 버튼 열을 맞춘다
		private const float StepButtonWidth = 34f;   // [기본]·[-1]·[+1]·[최대] 폭(px)

		private static readonly Color TraitAccent = new(0.65f, 0.85f, 0.30f);   // 연두

		public override ECheatCategory Category => ECheatCategory.Growth;
		public override string         Title    => "특성 레벨";
		public override Color          Accent   => TraitAccent;
		public override int            Order    => 1;
		public override string         Keywords => "trait 산업 개척";
		public override string         Hint     => "조건·포인트 없이 정한다 — 서버가 기본~최대로 자른다\n산업 개척을 내려도 이미 돌던 슬롯은 그대로 돈다(잠금은 다시 배치할 때)";

		public override void Draw()
		{
			if (!GameDataLoader.IsLoaded)
			{
				EditorGUILayout.LabelField("목록은 Play 중 테이블을 읽은 뒤 뜬다", EditorStyles.miniLabel);

				return;
			}

			var model  = CheatGuard.FindLoggedInModel();
			var traits = GameDataLoader.UserTraits;

			using (new EditorGUILayout.HorizontalScope())
			{
				if (GUILayout.Button("전부 최대"))
				{
					CheatGui.Request(ECheatCommand.SetTraitLevel, 0L, MaxTraitLevelOf(traits));
				}

				if (GUILayout.Button("전부 기본"))
				{
					CheatGui.Request(ECheatCommand.SetTraitLevel, 0L, 0L);
				}
			}

			EditorGUILayout.Space(2f);

			// 공통(산업 없음)을 맨 위에, 그 아래 산업 순서대로 — 특성 화면의 줄 순서와 같다.
			var isFollowing = false;

			for (var i = -1; i < IndustryLabel.DisplayOrder.Length; i++)
			{
				var industry = i < 0 ? EIndustryType.None : IndustryLabel.DisplayOrder[i];

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

				DrawIndustryHead(industry == EIndustryType.None ? "공통" : IndustryLabel.Get(industry), isFollowing, rows);
				isFollowing = true;

				foreach (var trait in rows)
				{
					DrawTraitRow(trait, model);
				}
			}
		}

		// 특성 한 줄 — 이름(TID) · Lv 지금/최대 · [기본] [-1] [+1] [최대].
		// 끝에 닿은 쪽 버튼은 끈다. 로그인 전에는 지금 레벨을 모르니 '?'로 두고 버튼은 살린다(누르면 'CheatGuard'가 막는다).
		private static void DrawTraitRow(UserTraitTableRow trait, PlayerDataModel? model)
		{
			var level = model?.GetTraitLevel(trait.UserTraitTID) ?? trait.BaseLevel;

			using (new EditorGUILayout.HorizontalScope())
			{
				GUILayout.Space(CheatGui.RowIndent);
				GUILayout.Label($"{trait.Name} ({trait.UserTraitTID})");

				var levelText = model == null ? $"Lv ?/{trait.MaxLevel}" : $"Lv {level}/{trait.MaxLevel}";
				var isMax     = model != null && level >= trait.MaxLevel;

				CheatGui.DrawColoredLabel(levelText, isMax ? CheatGui.DoneColor : GUI.contentColor, LevelWidth);

				var canLower = model == null || level > trait.BaseLevel;
				var canRaise = model == null || level < trait.MaxLevel;

				DrawStepButton("기본", trait, trait.BaseLevel, canLower);
				DrawStepButton("-1",   trait, level - 1,       canLower);
				DrawStepButton("+1",   trait, level + 1,       canRaise);
				DrawStepButton("최대", trait, trait.MaxLevel,  canRaise);
			}
		}

		private static void DrawStepButton(string label, UserTraitTableRow trait, int targetLevel, bool isEnabled)
		{
			using (new EditorGUI.DisabledScope(!isEnabled))
			{
				if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Width(StepButtonWidth)))
				{
					CheatGui.Request(ECheatCommand.SetTraitLevel, trait.UserTraitTID, targetLevel);
				}
			}
		}

		// 산업 이름 띠 + 오른쪽 끝 [산업 최대] — 그 산업 특성을 각자 최대로 보낸다.
		private static void DrawIndustryHead(string name, bool isFollowing, List<UserTraitTableRow> rows)
		{
			var head       = CheatGui.DrawBand(name, isFollowing, TraitAccent);
			var buttonRect = new Rect(head.xMax - CheatGui.GroupButtonWidth, head.y + 1f, CheatGui.GroupButtonWidth, head.height - 2f);

			if (GUI.Button(buttonRect, "산업 최대", EditorStyles.miniButton))
			{
				var commands = new List<(long, long)>();

				foreach (var trait in rows)
				{
					commands.Add((trait.UserTraitTID, trait.MaxLevel));
				}

				CheatGui.RequestAll(ECheatCommand.SetTraitLevel, commands);
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
	}
}

using System;
using GameData;
using MikaProtocol;
using UnityEditor;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
	// 건물 레벨을 상한·비용 없이 정한다 — 서버 'SetBuildingLevel'(Arg1 = BuildingTID · 0이면 전부, Arg2 = 레벨).
	//
	// ■ 서버가 0~최대로 자른다
	// 바뀌면 서버가 'S_BuildingListResponse'를 다시 보내 거점 화면과 건물 포인트가 그 자리에서 갱신된다.
	[Serializable]
	internal sealed class BuildingLevelCheatSection : CheatSection
	{
		private const float LevelWidth      = 64f;   // 'Lv 지금/최대' 고정 폭 — 버튼 열을 맞춘다
		private const float StepButtonWidth = 34f;   // [0]·[-1]·[+1]·[+10]·[최대] 폭(px)

		private static readonly Color BuildingAccent = new(0.91f, 0.64f, 0.23f);   // 주황

		public override ECheatCategory Category => ECheatCategory.Growth;
		public override string         Title    => "건물 레벨";
		public override Color          Accent   => BuildingAccent;
		public override int            Order    => 2;
		public override string         Keywords => "building 거점 outpost";
		public override string         Hint     => "계정 레벨 상한·비용 없이 정한다 — 서버가 0~최대로 자른다\n건물 레벨이 곧 그 건물의 특성 포인트다";

		public override void Draw()
		{
			if (!GameDataLoader.IsLoaded)
			{
				EditorGUILayout.LabelField("목록은 Play 중 테이블을 읽은 뒤 뜬다", EditorStyles.miniLabel);

				return;
			}

			var model = CheatGuard.FindLoggedInModel();

			foreach (var building in GameDataLoader.Buildings)
			{
				DrawBuildingRow(building, model);
			}
		}

		// 건물 한 줄 — 이름(TID) · Lv 지금/최대 · [0] [-1] [+1] [+10] [최대].
		// 로그인 전에는 지금 레벨을 모르니 '?'로 두고 버튼은 살린다(누르면 'CheatGuard'가 막는다).
		private static void DrawBuildingRow(BuildingTableRow building, PlayerDataModel? model)
		{
			var level = model?.GetBuildingLevel(building.BuildingTID) ?? 0;

			using (new EditorGUILayout.HorizontalScope())
			{
				GUILayout.Space(CheatGui.RowIndent);
				GUILayout.Label($"{building.Name} ({building.BuildingTID})");

				var levelText = model == null ? $"Lv ?/{building.MaxLevel}" : $"Lv {level}/{building.MaxLevel}";
				var isMax     = model != null && level >= building.MaxLevel;

				CheatGui.DrawColoredLabel(levelText, isMax ? CheatGui.DoneColor : GUI.contentColor, LevelWidth);

				var canLower = model == null || level > 0;
				var canRaise = model == null || level < building.MaxLevel;

				DrawStepButton("0",    building, 0,                 canLower);
				DrawStepButton("-1",   building, level - 1,         canLower);
				DrawStepButton("+1",   building, level + 1,         canRaise);
				DrawStepButton("+10",  building, level + 10,        canRaise);
				DrawStepButton("최대", building, building.MaxLevel, canRaise);
			}
		}

		private static void DrawStepButton(string label, BuildingTableRow building, int targetLevel, bool isEnabled)
		{
			using (new EditorGUI.DisabledScope(!isEnabled))
			{
				if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Width(StepButtonWidth)))
				{
					CheatGui.Request(ECheatCommand.SetBuildingLevel, building.BuildingTID, targetLevel);
				}
			}
		}
	}
}

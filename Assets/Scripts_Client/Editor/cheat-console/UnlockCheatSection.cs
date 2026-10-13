using System;
using System.Collections.Generic;
using GameData;
using MikaProtocol;
using UnityEditor;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
	// 해금 — 지금 'UnlockTable'은 작업슬롯 6줄뿐이라 묶음 없이 줄을 바로 늘어놓는다.
	// 특성은 해금을 쓰지 않는다(T-108) → '특성 레벨' 칸.
	[Serializable]
	internal sealed class UnlockCheatSection : CheatSection
	{
		[SerializeField] private int _unlockTid;

		public override ECheatCategory Category => ECheatCategory.Growth;
		public override string         Title    => "해금";
		public override Color          Accent   => new(0.95f, 0.55f, 0.30f);   // 주황
		public override int            Order    => 2;
		public override string         Keywords => "unlock 작업슬롯 슬롯 열기";
		public override string         Hint     => "[해금 전부 열기]는 아직 잠긴 줄만 보낸다";

		public override void Draw()
		{
			if (!GameDataLoader.IsLoaded)
			{
				DrawManualRow();

				return;
			}

			var model = CheatGuard.FindLoggedInModel();
			var rows  = new List<UnlockTableRow>(GameTable.UnlockTable.All);

			DrawUnlockAllButton("해금 전부 열기", rows, model);
			EditorGUILayout.Space(2f);

			foreach (var row in rows)
			{
				DrawUnlockRow(row, model);
			}
		}

		// 테이블을 읽기 전 — TID를 숫자로 받는다.
		private void DrawManualRow()
		{
			using (new EditorGUILayout.HorizontalScope())
			{
				CheatGui.FieldLabel("UnlockTID");
				_unlockTid = EditorGUILayout.IntField(_unlockTid);

				if (CheatGui.ActionButton("열기"))
				{
					CheatGui.Request(ECheatCommand.Unlock, _unlockTid);
				}
			}

			EditorGUILayout.LabelField("목록은 Play 중 테이블을 읽은 뒤 뜬다", EditorStyles.miniLabel);
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
					CheatGui.RequestAll(ECheatCommand.Unlock, locked);
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
				GUILayout.Space(CheatGui.RowIndent);

				if (model == null)
				{
					CheatGui.DrawColoredLabel("[ ? ]", GUI.contentColor, CheatGui.StatusWidth);
				}
				else
				{
					CheatGui.DrawColoredLabel(isUnlocked ? "[열림]" : "[잠김]", isUnlocked ? CheatGui.DoneColor : CheatGui.PendingColor, CheatGui.StatusWidth);
				}

				// 'EditorGUILayout.LabelField'는 앞머리 라벨 자리에 그려서 이름이 멀찍이 떨어진다 — 바로 붙여 쓴다.
				GUILayout.Label($"{row.Name} ({row.UnlockTID})");

				using (new EditorGUI.DisabledScope(isUnlocked))
				{
					if (CheatGui.ActionButton("열기"))
					{
						CheatGui.Request(ECheatCommand.Unlock, row.UnlockTID);
					}
				}
			}
		}
	}
}

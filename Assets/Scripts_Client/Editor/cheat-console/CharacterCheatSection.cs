using System;
using GameData;
using MikaProtocol;
using UnityEditor;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
	// 캐릭터 지급 — 테이블에서 골라 장수만큼.
	[Serializable]
	internal sealed class CharacterCheatSection : CheatSection
	{
		[SerializeField] private TidPicker _picker = new();
		[SerializeField] private int       _count  = 1;

		public override ECheatCategory Category => ECheatCategory.Give;
		public override string         Title    => "캐릭터 지급";
		public override Color          Accent   => new(0.35f, 0.65f, 1.00f);   // 파랑
		public override int            Order    => 2;
		public override string         Keywords => "character 응축 재료 전종류 전체";

		// 슬라이더 상한 — 서버와 같은 'Constants.xlsx' 값을 읽는다(넘기면 서버가 InvalidCheatArgs로 거절).
		// 테이블은 Play에 들어가야 적재되므로 그 전에는 1로 둔다 — 그때는 어차피 보낼 수 없다.
		private static int MaxCount => GameDataLoader.IsLoaded ? (int)Constants.CheatMaxCharacterCount : 1;

		public override void Draw()
		{
			var rows = GameDataLoader.IsLoaded ? GameTable.CharacterTable.All : null;
			_picker.Draw(rows, row => row.CharacterTID, row => row.Name);

			using (new EditorGUILayout.HorizontalScope())
			{
				CheatGui.FieldLabel("장수");
				_count = EditorGUILayout.IntSlider(_count, 1, MaxCount);

				if (CheatGui.ActionButton("지급"))
				{
					CheatGui.Request(ECheatCommand.GiveCharacter, _picker.Tid, _count);
				}
			}

			// 전 종류 1장씩 — 창고 → 인벤토리 → 우편 순서로 들어간다. 칸이 모자라도 거절하지 않는다(T-190).
			using (new EditorGUILayout.HorizontalScope())
			{
				GUILayout.FlexibleSpace();

				if (CheatGui.ActionButton("전 종류 1장씩"))
				{
					CheatGui.Request(ECheatCommand.GiveAllCharacters, 0, 0);
				}
			}
		}
	}
}

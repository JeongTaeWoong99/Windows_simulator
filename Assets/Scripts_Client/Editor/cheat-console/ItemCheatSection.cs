using System;
using GameData;
using MikaProtocol;
using UnityEditor;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
	// 자원(아이템) 지급 — 테이블에서 골라 개수만큼.
	[Serializable]
	internal sealed class ItemCheatSection : CheatSection
	{
		[SerializeField] private TidPicker _picker = new();
		[SerializeField] private int       _count  = 10;

		public override ECheatCategory Category => ECheatCategory.Give;
		public override string         Title    => "자원 지급";
		public override Color          Accent   => new(0.40f, 0.80f, 0.45f);   // 초록
		public override int            Order    => 1;
		public override string         Keywords => "아이템 item 재료";

		public override void Draw()
		{
			var rows = GameDataLoader.IsLoaded ? GameTable.ItemTable.All : null;
			_picker.Draw(rows, row => row.ItemTID, row => row.Name);

			using (new EditorGUILayout.HorizontalScope())
			{
				CheatGui.FieldLabel("개수");
				_count = EditorGUILayout.IntField(_count);

				if (CheatGui.ActionButton("지급"))
				{
					CheatGui.Request(ECheatCommand.GiveItem, _picker.Tid, _count);
				}
			}
		}
	}
}

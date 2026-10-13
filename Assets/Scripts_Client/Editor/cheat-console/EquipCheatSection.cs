using System;
using GameData;
using MikaProtocol;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
	// 장비 지급 — 개수 칸이 없다. 서버 'CheatGiveEquip'이 한 번에 개체 1개만 만든다(인벤토리 첫 빈 칸).
	[Serializable]
	internal sealed class EquipCheatSection : CheatSection
	{
		[SerializeField] private TidPicker _picker = new();

		public override ECheatCategory Category => ECheatCategory.Give;
		public override string         Title    => "장비 지급";
		public override Color          Accent   => new(0.90f, 0.45f, 0.70f);   // 분홍
		public override int            Order    => 3;
		public override string         Keywords => "equip 무기 방어구";
		public override string         Hint     => "한 번에 1개 — 인벤토리 첫 빈 칸에 만든다. 여러 개면 여러 번 누른다";

		public override void Draw()
		{
			var rows = GameDataLoader.IsLoaded ? GameTable.EquipTable.All : null;

			if (_picker.Draw(rows, row => row.EquipTID, row => row.Name, "지급"))
			{
				CheatGui.Request(ECheatCommand.GiveEquip, _picker.Tid);
			}
		}
	}
}

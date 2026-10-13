using System;
using MikaProtocol;
using UnityEditor;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
	// 재화 — 입력값 지급 + 자주 쓰는 금액 단축 버튼.
	[Serializable]
	internal sealed class CurrencyCheatSection : CheatSection
	{
		private static readonly long[] GoldQuickAmounts = { 1_000, 100_000, -1_000 };

		[SerializeField] private long _goldAmount = 1_000;

		public override ECheatCategory Category => ECheatCategory.Give;
		public override string         Title    => "재화";
		public override Color          Accent   => new(0.95f, 0.78f, 0.25f);   // 금색
		public override int            Order    => 0;
		public override string         Keywords => "골드 gold";
		public override string         Hint     => "음수를 넣으면 깎는다";

		public override void Draw()
		{
			DrawCurrencyRow("골드", ECheatCommand.GiveGold, ref _goldAmount, GoldQuickAmounts);
		}

		private static void DrawCurrencyRow(string label, ECheatCommand command, ref long amount, long[] quickAmounts)
		{
			using (new EditorGUILayout.HorizontalScope())
			{
				CheatGui.FieldLabel(label);
				amount = EditorGUILayout.LongField(amount);

				if (CheatGui.ActionButton("지급"))
				{
					CheatGui.Request(command, amount);
				}
			}

			using (new EditorGUILayout.HorizontalScope())
			{
				GUILayout.Space(CheatGui.LabelWidth + 4f);

				foreach (var quickAmount in quickAmounts)
				{
					var text = quickAmount > 0 ? $"+{quickAmount:N0}" : $"{quickAmount:N0}";

					if (GUILayout.Button(text, EditorStyles.miniButton))
					{
						CheatGui.Request(command, quickAmount);
					}
				}
			}
		}
	}
}

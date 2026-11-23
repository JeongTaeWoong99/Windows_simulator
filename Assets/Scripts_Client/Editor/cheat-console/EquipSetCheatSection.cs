using System;
using System.Collections.Generic;
using GameData;
using MikaProtocol;
using UnityEditor;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
	// 장비 세트 지급 — 레벨 · 등급 · 산업을 골라 무기 · 장신구 · 보석 한 벌을 받는다 (T-213 4번).
	//
	// ※ 새 서버 명령 없이 'GiveEquip'을 부위마다 한 번씩 보낸다 — 장비 지급 칸과 같은 길이다.
	// ※ 고른 산업에 그 부위가 없으면 같은 레벨 · 등급의 **공통**(산업 없음) 장비로, 그것도 없으면 아무 산업으로 대신한다.
	//   대신한 것은 줄에 적어 둔다 — 무엇을 받았는지 모르면 시험이 엇나간다.
	[Serializable]
	internal sealed class EquipSetCheatSection : CheatSection
	{
		private static readonly EquipKind[] Kinds = { EquipKind.Weapon, EquipKind.Accessory, EquipKind.Gem };

		[SerializeField] private int          _levelIndex;
		[SerializeField] private GlobalRarity _rarity   = GlobalRarity.Common;
		[SerializeField] private IndustryType _industry = IndustryType.None;
		[SerializeField] private int          _setCount = 1;

		public override ECheatCategory Category => ECheatCategory.Give;
		public override string         Title    => "장비 세트";
		public override Color          Accent   => new(0.80f, 0.40f, 0.85f);   // 보라 — 장비 지급(분홍) 옆
		public override int            Order    => 4;
		public override string         Keywords => "equip set 무기 장신구 보석 한 벌";
		public override string         Hint     => "무기 · 장신구 · 보석 한 벌을 인벤토리 빈 칸에 받는다\n고른 산업에 없는 부위는 공통 → 아무 산업 순으로 대신한다";

		public override void Draw()
		{
			if (!GameDataLoader.IsLoaded)
			{
				EditorGUILayout.LabelField("목록은 Play 중 테이블을 읽은 뒤 뜬다", EditorStyles.miniLabel);

				return;
			}

			var levels = CollectLevels();

			if (levels.Count == 0)
			{
				EditorGUILayout.LabelField("장비 표가 비어 있다", EditorStyles.miniLabel);

				return;
			}

			var labels = new string[levels.Count];

			for (int i = 0; i < levels.Count; i++)
			{
				labels[i] = $"Lv{levels[i]}";
			}

			using (new EditorGUILayout.HorizontalScope())
			{
				CheatGui.FieldLabel("레벨");
				_levelIndex = EditorGUILayout.Popup(Mathf.Clamp(_levelIndex, 0, levels.Count - 1), labels);
			}

			using (new EditorGUILayout.HorizontalScope())
			{
				CheatGui.FieldLabel("등급");
				_rarity = (GlobalRarity)EditorGUILayout.EnumPopup(_rarity);
			}

			using (new EditorGUILayout.HorizontalScope())
			{
				CheatGui.FieldLabel("산업");
				_industry = (IndustryType)EditorGUILayout.EnumPopup(_industry);
			}

			int level = levels[Mathf.Clamp(_levelIndex, 0, levels.Count - 1)];
			var picks = new List<EquipTableRow>();

			foreach (var kind in Kinds)
			{
				var row = FindRow(kind, level, _rarity, _industry);

				using (new EditorGUILayout.HorizontalScope())
				{
					GUILayout.Space(CheatGui.RowIndent);

					if (row == null)
					{
						CheatGui.DrawColoredLabel($"{EquipLabel.GetKindName(kind)} — 없음", CheatGui.ErrorColor);
					}
					else
					{
						var substituted = row.Industry != _industry ? $"  (대신: {CheatGui.IndustryGroup(row.Industry)})" : "";
						GUILayout.Label($"{EquipLabel.GetKindName(kind)} — {row.Name} ({row.EquipTID}){substituted}");
						picks.Add(row);
					}
				}
			}

			using (new EditorGUILayout.HorizontalScope())
			{
				CheatGui.FieldLabel("벌 수");
				_setCount = EditorGUILayout.IntSlider(_setCount, 1, 5);

				using (new EditorGUI.DisabledScope(picks.Count == 0))
				{
					if (CheatGui.ActionButton("지급"))
					{
						var requests = new List<(ECheatCommand, long, long)>();

						for (int n = 0; n < _setCount; n++)
						{
							foreach (var row in picks)
							{
								requests.Add((ECheatCommand.GiveEquip, row.EquipTID, 0L));
							}
						}

						CheatGui.RequestSequence(requests);
					}
				}
			}
		}

		// 표에 있는 아이템 레벨 (작은 순 · 중복 없이)
		private static List<int> CollectLevels()
		{
			var set = new SortedSet<int>();

			foreach (var row in GameTable.EquipTable.All)
			{
				set.Add(row.ItemLevel);
			}

			return new List<int>(set);
		}

		// 부위 · 레벨 · 등급이 맞는 장비 — 고른 산업 → 공통 → 아무 산업 순. 없으면 null.
		private static EquipTableRow? FindRow(EquipKind kind, int level, GlobalRarity rarity, IndustryType industry)
		{
			EquipTableRow? common = null;
			EquipTableRow? any    = null;

			foreach (var row in GameTable.EquipTable.All)
			{
				if (row.EquipKind != kind || row.ItemLevel != level || row.GlobalRarity != rarity)
				{
					continue;
				}

				if (row.Industry == industry)
				{
					return row;
				}

				if (row.Industry == IndustryType.None)
				{
					common ??= row;
				}

				any ??= row;
			}

			return common ?? any;
		}
	}
}

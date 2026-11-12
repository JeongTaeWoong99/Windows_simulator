using System;
using GameData;
using MikaProtocol;
using UnityEditor;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
	// 아티팩트 지급 — 감정 없이 아티팩트 칸(인벤토리 장비 탭)에 바로 넣는다. 칸이 모자라면 서버가 우편 한 통으로 보낸다.
	// 도감 장착은 하지 않는다 — 장착 흐름도 시험할 수 있게(서버 'GiveArtifact' · T-198 · 이슈 #112).
	[Serializable]
	internal sealed class ArtifactCheatSection : CheatSection
	{
		[SerializeField] private TidPicker _picker = new();
		[SerializeField] private int       _count  = 1;

		public override ECheatCategory Category => ECheatCategory.Give;
		public override string         Title    => "아티팩트 지급";
		public override Color          Accent   => new(0.55f, 0.85f, 0.95f);   // 하늘
		public override int            Order    => 4;
		public override string         Keywords => "artifact 유물 도감";
		public override string         Hint     => "감정 없이 아티팩트 칸에 넣는다 · 칸이 모자라면 우편 · 도감 장착은 하지 않는다";

		public override void Draw()
		{
			var rows = GameDataLoader.IsLoaded ? GameTable.ArtifactTable.All : null;
			_picker.Draw(rows, row => row.ArtifactTID, row => row.Name);

			using (new EditorGUILayout.HorizontalScope())
			{
				CheatGui.FieldLabel("개수");

				// 서버 범위는 1~칸 하나 상한('ArtifactMaxStack') — 테이블 적재 전에는 위로 자르지 않는다(서버가 거절한다)
				int max = GameDataLoader.IsLoaded ? (int)Constants.ArtifactMaxStack : int.MaxValue;
				_count = Mathf.Clamp(EditorGUILayout.IntField(_count), 1, max);

				if (CheatGui.ActionButton("지급"))
				{
					CheatGui.Request(ECheatCommand.GiveArtifact, _picker.Tid, _count);
				}
			}
		}
	}
}

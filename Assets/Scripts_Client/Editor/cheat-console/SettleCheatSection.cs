using System;
using GameData;
using MikaProtocol;
using UnityEditor;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
	// 판정 횟수만큼 작업량을 앞당겨 정산한다 — 쌓인 진행도만 정산하면 스케줄러(0.1초)가 먼저 가져가 늘 0개다.
	[Serializable]
	internal sealed class SettleCheatSection : CheatSection
	{
		[SerializeField] private int _judges = 1;

		public override ECheatCategory Category => ECheatCategory.World;
		public override string         Title    => "정산";
		public override Color          Accent   => new(0.30f, 0.80f, 0.80f);   // 청록
		public override int            Order    => 0;
		public override string         Keywords => "settle 채취 판정 수확";
		public override string         Hint     => "가동 슬롯에 판정 N회분 작업량을 얹고 바로 정산한다 — 작업슬롯 주기를 기다리지 않는다";

		// 슬라이더 상한 — 서버와 같은 'Constants.xlsx' 값을 읽는다(넘기면 서버가 InvalidCheatArgs로 거절).
		private static int MaxJudges => GameDataLoader.IsLoaded ? (int)Constants.CheatMaxSettleJudges : 1;

		public override void Draw()
		{
			using (new EditorGUILayout.HorizontalScope())
			{
				CheatGui.FieldLabel("판정 횟수");
				_judges = EditorGUILayout.IntSlider(_judges, 1, MaxJudges);

				if (CheatGui.ActionButton("정산"))
				{
					CheatGui.Request(ECheatCommand.Settle, _judges);
				}
			}
		}
	}
}

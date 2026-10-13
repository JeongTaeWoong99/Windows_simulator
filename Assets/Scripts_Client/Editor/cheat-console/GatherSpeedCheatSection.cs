using System;
using MikaProtocol;
using UnityEditor;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
	// 서버 전체의 채취 전역 배수 — 서버 'SetGatherSpeed'(Arg1 = 천분율, 100~100,000).
	// 바꾸면 서버가 접속 중인 모두에게 슬롯을 다시 보내 속도·주기가 바로 바뀐다.
	// ※ 서버는 저장하지 않는다 — 재시작하면 ×1.0이다. 지금 값은 받은 슬롯의 'GatherSpeedPermille'을 읽는다.
	// ※ 배수가 아무리 커도 판정 주기는 'Constants.MinCycleMs' 아래로 내려가지 않는다 — 연출 한 바퀴 보장(T-102).
	[Serializable]
	internal sealed class GatherSpeedCheatSection : CheatSection
	{
		// 서버 'SetGatherSpeed'가 받는 범위와 같다(밖이면 InvalidCheatArgs).
		private const int MinPermille     = 100;
		private const int MaxPermille     = 100_000;
		private const int DefaultPermille = 1_000;

		[SerializeField] private int _permille = 2_000;

		public override ECheatCategory Category     => ECheatCategory.World;
		public override string         Title        => "전역 배수";
		public override Color          Accent       => new(0.25f, 0.55f, 0.75f);   // 짙은 청록
		public override int            Order        => 1;
		public override string         Keywords     => "채취 속도 speed gather 배속";
		public override bool           IsServerWide => true;
		public override string         Hint         => "1000 = ×1.0 · 서버는 저장하지 않는다(재시작하면 ×1.0)\n배수를 올려도 판정은 최소 주기(엑셀 MinCycleMs)보다 짧아지지 않는다";

		public override void Draw()
		{
			var model   = CheatGuard.FindLoggedInModel();
			var current = model != null && model.WorkStationSlots.Count > 0 ? model.WorkStationSlots[0].GatherSpeedPermille : 0;
			CheatGui.DrawColoredLabel(current > 0 ? $"지금: ×{current / 1000f:0.00}" : "지금: (로그인 후 표시)", GUI.contentColor);

			using (new EditorGUILayout.HorizontalScope())
			{
				CheatGui.FieldLabel("천분율");
				_permille = Mathf.Clamp(EditorGUILayout.IntField(_permille), MinPermille, MaxPermille);

				if (CheatGui.ActionButton("적용"))
				{
					CheatGui.Request(ECheatCommand.SetGatherSpeed, _permille);
				}

				if (CheatGui.ActionButton("×1"))
				{
					CheatGui.Request(ECheatCommand.SetGatherSpeed, DefaultPermille);
				}
			}
		}
	}
}

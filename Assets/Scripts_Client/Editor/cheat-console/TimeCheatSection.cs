using System;
using MikaProtocol;
using UnityEditor;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
	// 서버 전체의 게임 시계를 앞으로 넘긴다 — 서버 'AdvanceTime'(Arg1 = 초, 1초~1년, 누적) · 'ResetTime'(오프셋 0).
	// 넘기면 서버가 'S_ServerTimeResponse'를 다시 보내 'ServerClock'이 따라가고, 남은 시간 · 삭제까지 표시가 함께 움직인다.
	//
	// ※ 채취는 쌓이지 않는다 — 서버가 슬롯 기준 시각도 같이 민다. 판정을 당기려면 '정산' 칸.
	// ※ 받은 우편 7일 삭제는 로그인 때 판정한다 — 넘긴 뒤 다시 접속해야 사라진다.
	[Serializable]
	internal sealed class TimeCheatSection : CheatSection
	{
		// 단축 버튼 — 48시간은 경매 등록 기간, 7일은 받은 우편 보관 기간이다.
		private static readonly (string Label, long Seconds)[] QuickSteps =
		{
			("+1시간", 3_600), ("+1일", 86_400), ("+2일", 172_800), ("+7일", 604_800),
		};

		[SerializeField] private long _advanceSeconds = 3_600;

		public override ECheatCategory Category     => ECheatCategory.World;
		public override string         Title        => "시간";
		public override Color          Accent       => new(0.85f, 0.30f, 0.35f);   // 진홍
		public override int            Order        => 2;
		public override string         Keywords     => "time 시계 경매 만료 넘기기";
		public override bool           IsServerWide => true;
		public override string         Hint         =>
			"+2일 = 경매 등록 기간 · +7일 = 받은 우편 보관 기간\n" +
			"채취는 쌓이지 않는다 — 판정을 당기려면 '정산'\n" +
			"받은 우편 7일 삭제는 로그인 때 판정한다 — 다시 접속해야 사라진다\n" +
			"되돌려도 넘긴 동안 저장된 시각(우편 도착 · 경매 등록)은 미래로 남는다";

		public override void Draw()
		{
			var isLoggedIn = CheatGuard.FindLoggedInModel() != null;

			using (new EditorGUILayout.HorizontalScope())
			{
				CheatGui.DrawColoredLabel(isLoggedIn ? $"게임 시계: {FormatOffset(ServerClock.Offset)}" : "게임 시계: (로그인 후 표시)",
				                          GUI.contentColor);
				GUILayout.FlexibleSpace();

				if (CheatGui.ActionButton("되돌리기"))
				{
					CheatGui.Request(ECheatCommand.ResetTime);
				}
			}

			using (new EditorGUILayout.HorizontalScope())
			{
				GUILayout.Space(CheatGui.LabelWidth + 4f);

				foreach (var (label, seconds) in QuickSteps)
				{
					if (GUILayout.Button(label, EditorStyles.miniButton))
					{
						CheatGui.Request(ECheatCommand.AdvanceTime, seconds);
					}
				}
			}

			using (new EditorGUILayout.HorizontalScope())
			{
				CheatGui.FieldLabel("초");
				_advanceSeconds = Math.Max(1L, EditorGUILayout.LongField(_advanceSeconds));

				if (CheatGui.ActionButton("넘기기"))
				{
					CheatGui.Request(ECheatCommand.AdvanceTime, _advanceSeconds);
				}
			}
		}

		// 게임 시계가 PC보다 얼마나 앞섰나 — "실제 시각" · "+2일 3시간 5분".
		// 차이에는 PC 시계 오차도 섞이므로 1분 아래는 실제 시각으로 본다.
		private static string FormatOffset(TimeSpan offset)
		{
			if (Math.Abs(offset.TotalMinutes) < 1d)
			{
				return "실제 시각";
			}

			var sign = offset < TimeSpan.Zero ? "-" : "+";
			var abs  = offset.Duration();

			return abs.TotalDays >= 1d
				? $"{sign}{(int)abs.TotalDays}일 {abs.Hours}시간 {abs.Minutes}분"
				: $"{sign}{abs.Hours}시간 {abs.Minutes}분";
		}
	}
}

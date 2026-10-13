using System;
using GameData;
using MikaProtocol;
using UnityEditor;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
	// 우편 템플릿 한 줄을 보낸다. 받는 UID가 0이면 전체 우편이다 — 템플릿의 PeriodDays 동안 로그인하는 모두가 받는다
	// (PeriodDays = 0인 템플릿은 전체로 보낼 수 없어 서버가 거절한다). 넘침 보관 템플릿(2)은 첨부가 비어 있다.
	[Serializable]
	internal sealed class MailCheatSection : CheatSection
	{
		[SerializeField] private TidPicker _picker = new();
		[SerializeField] private long      _targetUid;   // 0이면 전체 우편

		public override ECheatCategory Category     => ECheatCategory.Mail;
		public override string         Title        => "우편 발송";
		public override Color          Accent       => new(0.60f, 0.60f, 0.65f);   // 회색
		public override int            Order        => 0;
		public override string         Keywords     => "mail 템플릿 전체";
		public override bool           IsServerWide => _targetUid == 0L;   // 받는 UID 0 = 전체 우편
		public override string         Hint         =>
			"받는 UID 0 = 전체 우편 — 접속 중인 유저 + 템플릿 PeriodDays 안에 로그인하는 유저\n" +
			"PeriodDays = 0인 템플릿은 전체로 보낼 수 없다\n" +
			"넘침 보관 템플릿(2)은 첨부가 비어 있다";

		public override void Draw()
		{
			var rows = GameDataLoader.IsLoaded ? GameTable.MailTemplateTable.All : null;
			_picker.Draw(rows, row => row.MailTemplateTID, row => row.Title);

			using (new EditorGUILayout.HorizontalScope())
			{
				CheatGui.FieldLabel("받는 UID");
				_targetUid = Math.Max(0L, EditorGUILayout.LongField(_targetUid));
				GUILayout.Label(_targetUid == 0L ? "전체" : "개인", EditorStyles.miniLabel, GUILayout.ExpandWidth(false));

				if (CheatGui.ActionButton("발송"))
				{
					CheatGui.Request(ECheatCommand.SendMail, _picker.Tid, _targetUid);
				}
			}
		}
	}
}

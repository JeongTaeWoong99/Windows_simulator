using System;
using MikaProtocol;
using UnityEditor;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
	// 경험치는 캐릭터 '종류'가 아니라 내가 가진 '개체'에 준다 — 목록은 테이블이 아니라 보유 캐릭터다.
	// 응축 누적도 같은 개체에 정하므로 한 칸에 둔다 — 서버 'SetCondenseCount'(Arg1 = CharacterId · Arg2 = 누적 수, T-130).
	[Serializable]
	internal sealed class CharacterExpCheatSection : CheatSection
	{
		private const int UnloadedMaxCondense = 999;   // 표가 안 읽혔을 때 슬라이더 상한 — 넉넉히 연다

		[SerializeField] private long _characterId;
		[SerializeField] private int  _expAmount = 100;
		[SerializeField] private int  _condenseCount;

		public override ECheatCategory Category => ECheatCategory.Growth;
		public override string         Title    => "캐릭터 경험치 · 응축";
		public override Color          Accent   => new(0.70f, 0.50f, 0.95f);   // 보라
		public override int            Order    => 0;
		public override string         Keywords => "exp 레벨 condense 별 성";
		public override string         Hint     => "캐릭터 종류가 아니라 보유 개체에 준다\n응축 누적은 서버가 0~최고 ★ 문턱으로 자른다 — 재료가 필요하면 '캐릭터 지급'";

		public override void Draw()
		{
			var model = CheatGuard.FindLoggedInModel();

			using (new EditorGUILayout.HorizontalScope())
			{
				CheatGui.FieldLabel("캐릭터");

				if (model != null && model.Characters.Count > 0)
				{
					_characterId = DrawOwnedCharacterPopup(model, _characterId);
				}
				else
				{
					// 목록이 없어도 버튼은 누를 수 있게 둔다 — 누르면 'CheatGuard'가 왜 안 되는지 알려 준다.
					_characterId = EditorGUILayout.LongField(_characterId);
					GUILayout.Label(model == null ? "(로그인 후 목록)" : "(보유 없음)", EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
				}
			}

			using (new EditorGUILayout.HorizontalScope())
			{
				CheatGui.FieldLabel("경험치");
				_expAmount = EditorGUILayout.IntField(_expAmount);

				if (CheatGui.ActionButton("지급"))
				{
					CheatGui.Request(ECheatCommand.GiveCharacterExp, _characterId, _expAmount);
				}
			}

			var maxCount = GameDataLoader.IsLoaded ? GameDataLoader.GetCondenseThreshold(GameDataLoader.CondenseMaxStar) : UnloadedMaxCondense;

			using (new EditorGUILayout.HorizontalScope())
			{
				CheatGui.FieldLabel("응축 누적");
				_condenseCount = EditorGUILayout.IntSlider(_condenseCount, 0, maxCount);

				if (CheatGui.ActionButton("정하기"))
				{
					CheatGui.Request(ECheatCommand.SetCondenseCount, _characterId, _condenseCount);
				}
			}
		}

		private static long DrawOwnedCharacterPopup(PlayerDataModel model, long selectedId)
		{
			var characters = model.Characters;
			var labels     = new string[characters.Count];
			var selected   = 0;

			for (var i = 0; i < characters.Count; i++)
			{
				var character = characters[i];
				labels[i] = $"{model.GetCharacterName(character.CharacterId)}  Lv{character.Level}  Exp{character.Exp}  {character.Star}성({character.CondenseCount})  (#{character.CharacterId})";

				if (character.CharacterId == selectedId)
				{
					selected = i;
				}
			}

			var index = EditorGUILayout.Popup(selected, labels);

			return characters[index].CharacterId;
		}
	}
}

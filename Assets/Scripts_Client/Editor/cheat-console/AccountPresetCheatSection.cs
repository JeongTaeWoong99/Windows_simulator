using System;
using System.Collections.Generic;
using GameData;
using MikaProtocol;
using UnityEditor;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
	// 계정 상태 한 번에 — 신규 · 중반 · 후반 버튼 하나로 골드 · 계정 레벨 · 특성 · 건물 · 해금을 맞춘다 (T-213 1번).
	//
	// ■ 새 서버 명령 없이 있는 치트를 이어 보낸다
	//   'GiveGold' · 'GiveAccountExp' · 'SetTraitLevel' · 'SetBuildingLevel' · 'Unlock' · 'GiveAllCharacters'를 순서대로 보낸다('CheatGui.RequestSequence').
	//   그래서 값은 지금 상태와의 **차이**로 만든다 — 골드는 목표 - 지금, 계정 경험치는 목표 레벨까지 남은 양.
	//
	// ■ 되돌리지 못하는 것
	//   계정 레벨 · 연 해금 · 받은 캐릭터·장비는 서버에 내리는 치트가 없다. [신규]는 골드 · 특성 · 건물만 바닥으로 내린다 —
	//   진짜 처음 상태가 필요하면 새 Id로 로그인한다.
	[Serializable]
	internal sealed class AccountPresetCheatSection : CheatSection
	{
		// 단계별 목표값 — 테스트하며 바꿔 쓴다(T-213 "값은 섹션 상단 상수")
		private const long MidGold          = 1_000_000;
		private const int  MidAccountLevel  = 30;
		private const int  MidTraitLevel    = 5;     // 특성마다 기본~최대로 서버가 자른다
		private const int  MidBuildingLevel = 30;

		private const long LateGold          = 100_000_000;
		private const int  LateAccountLevel  = 150;   // 표 끝을 넘으면 표 끝까지만 올린다
		private const int  MaxLevelArg       = 9_999; // 특성·건물 '전부 최대' — 서버가 저마다의 최대로 자른다

		private static readonly Color PresetAccent = new(0.95f, 0.55f, 0.25f);   // 주황

		public override ECheatCategory Category => ECheatCategory.Growth;
		public override string         Title    => "계정 상태 한 번에";
		public override Color          Accent   => PresetAccent;
		public override int            Order    => -1;   // 성장 분류 맨 위
		public override string         Keywords => "preset 프리셋 신규 중반 후반 account";
		public override string         Hint     =>
			$"신규 — 골드 0 · 특성 기본 · 건물 0 (계정 레벨·해금·보유물은 내리지 못한다)\n" +
			$"중반 — 골드 {MidGold:N0} · 계정 Lv{MidAccountLevel} · 특성 Lv{MidTraitLevel} · 건물 Lv{MidBuildingLevel} · 해금 전부\n" +
			$"후반 — 골드 {LateGold:N0} · 계정 Lv{LateAccountLevel} · 특성·건물 최대 · 해금 전부 · 캐릭터 전 종류 1장씩\n" +
			"올리는 값은 지금보다 낮을 때만 보낸다";

		public override void Draw()
		{
			if (!GameDataLoader.IsLoaded)
			{
				EditorGUILayout.LabelField("Play 중 테이블을 읽은 뒤 쓸 수 있다", EditorStyles.miniLabel);

				return;
			}

			var model = CheatGuard.FindLoggedInModel();

			if (model == null)
			{
				EditorGUILayout.LabelField("(로그인 후 표시) — 지금 상태와의 차이로 보낸다", EditorStyles.miniLabel);

				return;
			}

			EditorGUILayout.LabelField($"지금: 골드 {model.Gold:N0} · 계정 Lv{model.AccountLevel}", EditorStyles.miniLabel);

			using (new EditorGUILayout.HorizontalScope())
			{
				if (GUILayout.Button("신규"))
				{
					CheatGui.RequestSequence(BuildFresh(model));
				}

				if (GUILayout.Button("중반"))
				{
					CheatGui.RequestSequence(BuildRaise(model, MidGold, MidAccountLevel, MidTraitLevel, MidBuildingLevel, giveCharacters: false));
				}

				if (GUILayout.Button("후반"))
				{
					CheatGui.RequestSequence(BuildRaise(model, LateGold, LateAccountLevel, MaxLevelArg, MaxLevelArg, giveCharacters: true));
				}
			}
		}

		// [신규] — 골드 0 · 특성 전부 기본 · 건물 전부 0.
		private static List<(ECheatCommand, long, long)> BuildFresh(PlayerDataModel model)
		{
			var requests = new List<(ECheatCommand, long, long)>();

			if (model.Gold > 0)
			{
				requests.Add((ECheatCommand.GiveGold, -model.Gold, 0L));   // 음수 = 차감
			}

			requests.Add((ECheatCommand.SetTraitLevel,    0L, 0L));   // 0 = 전 특성 · 0레벨 → 기본으로 잘린다
			requests.Add((ECheatCommand.SetBuildingLevel, 0L, 0L));   // 0 = 전 건물

			return requests;
		}

		// [중반]·[후반] — 지금보다 낮은 것만 목표까지 올린다.
		// ※ 특성·건물은 '전부 N'으로 한 번에 보낸다 — 이미 N보다 높은 것도 N으로 내려간다(단계 상태를 맞추는 버튼이라 의도대로다).
		private static List<(ECheatCommand, long, long)> BuildRaise(PlayerDataModel model, long gold, int accountLevel,
		                                                            int traitLevel, int buildingLevel, bool giveCharacters)
		{
			var requests = new List<(ECheatCommand, long, long)>();

			if (model.Gold < gold)
			{
				requests.Add((ECheatCommand.GiveGold, gold - model.Gold, 0L));
			}

			long exp = ExpToReach(model.AccountLevel, model.AccountExp, accountLevel);

			if (exp > 0)
			{
				requests.Add((ECheatCommand.GiveAccountExp, exp, 0L));
			}

			requests.Add((ECheatCommand.SetTraitLevel,    0L, traitLevel));
			requests.Add((ECheatCommand.SetBuildingLevel, 0L, buildingLevel));

			// 해금 — 열린 것은 빼고 보낸다(섞으면 로그가 'AlreadyUnlocked'로 뒤덮인다 · 해금 칸과 같다)
			foreach (var row in GameTable.UnlockTable.All)
			{
				if (!model.IsUnlocked(row.UnlockTID))
				{
					requests.Add((ECheatCommand.Unlock, row.UnlockTID, 0L));
				}
			}

			if (giveCharacters)
			{
				requests.Add((ECheatCommand.GiveAllCharacters, 0L, 0L));
			}

			return requests;
		}

		// 지금(레벨 · 그 레벨에서 쌓은 양)에서 목표 레벨까지 남은 계정 경험치. 표 끝을 넘으면 표 끝까지. 이미 넘었으면 0.
		private static long ExpToReach(int level, long expInLevel, int targetLevel)
		{
			long total = 0L;

			for (int next = level + 1; next <= targetLevel; next++)
			{
				if (!GameDataLoader.TryGetAccountRequiredExp(next, out long required))
				{
					break;   // 만렙
				}

				total += required;
			}

			return Math.Max(0L, total - expInLevel);
		}
	}
}

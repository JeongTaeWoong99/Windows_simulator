using System;
using GameData;
using MikaProtocol;
using UnityEditor;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
	// 경매장에 더미 매물을 한 번에 깐다 — 서버 'SeedAuctionListings'(Arg1 = 개수 1~5,000, 0이면 더미 전부 삭제 · Arg2 = ItemTID, 0이면 자원을 돌아가며).
	// 판매자는 실제 계정이 아닌 '더미상점'이라 경매장을 보는 모두에게 보인다 — 서버 전체 칸이다.
	//
	// ※ 응답이 두 번 온다(접수 · 끝남). 두 번째 줄의 Message가 결과다 — 로그에 그대로 두 줄이 남는다.
	// ※ 경매장에는 1초에 100개씩 올라간다 — 1,000개면 약 10초 뒤 검색에 다 보인다.
	[Serializable]
	internal sealed class AuctionDummyCheatSection : CheatSection
	{
		private const int MaxCount = 5_000;   // 서버 상한과 같다

		// 단축 버튼 — 1,050은 검색 결과 상한(1,000) 넘김 확인용이다.
		private static readonly int[] QuickCounts = { 100, 1_050 };

		[SerializeField] private TidPicker _picker  = new();
		[SerializeField] private bool      _isMixed = true;   // 참이면 Arg2 = 0 (자원을 돌아가며)
		[SerializeField] private int       _count   = 1_050;

		public override ECheatCategory Category     => ECheatCategory.Auction;
		public override string         Title        => "더미 매물";
		public override Color          Accent       => new(0.65f, 0.45f, 0.30f);   // 갈색
		public override int            Order        => 0;
		public override string         Keywords     => "auction 경매 매물 더미 seed 검색 부하";
		public override bool           IsServerWide => true;
		public override string         Hint         =>
			"판매자 '더미상점' — 팔려도 대금이 아무에게도 가지 않는다 · 자원 매물만 만든다\n" +
			"유저 상한 · 빈도 제한 · 등록비를 거치지 않는다\n" +
			"응답이 두 번 온다 — 두 번째 줄이 결과다\n" +
			"1초에 100개씩 올라간다 — 1,000개면 약 10초 뒤 검색에 다 보인다\n" +
			"삭제 뒤 '남음 N'(구매 중)이 있으면 한 번 더 누른다";

		public override void Draw()
		{
			using (new EditorGUILayout.HorizontalScope())
			{
				CheatGui.FieldLabel("자원");
				_isMixed = EditorGUILayout.ToggleLeft("여러 종 돌아가며", _isMixed);
			}

			if (!_isMixed)
			{
				var rows = GameDataLoader.IsLoaded ? GameTable.ItemTable.All : null;
				_picker.Draw(rows, row => row.ItemTID, row => row.Name);
			}

			var itemTid = _isMixed ? 0L : _picker.Tid;

			using (new EditorGUILayout.HorizontalScope())
			{
				GUILayout.Space(CheatGui.LabelWidth + 4f);

				foreach (var count in QuickCounts)
				{
					if (GUILayout.Button($"+{count:N0}", EditorStyles.miniButton))
					{
						CheatGui.Request(ECheatCommand.SeedAuctionListings, count, itemTid);
					}
				}
			}

			using (new EditorGUILayout.HorizontalScope())
			{
				CheatGui.FieldLabel("개수");
				_count = Mathf.Clamp(EditorGUILayout.IntField(_count), 1, MaxCount);

				if (CheatGui.ActionButton("올리기"))
				{
					CheatGui.Request(ECheatCommand.SeedAuctionListings, _count, itemTid);
				}
			}

			using (new EditorGUILayout.HorizontalScope())
			{
				GUILayout.FlexibleSpace();

				if (CheatGui.ActionButton("전부 삭제"))
				{
					CheatGui.Request(ECheatCommand.SeedAuctionListings);
				}
			}
		}
	}
}

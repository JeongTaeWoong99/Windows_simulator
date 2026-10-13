using System;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
	// 치트 창 왼쪽 목록의 분류 — 선언 순서가 곧 목록 순서다.
	internal enum ECheatCategory
	{
		Give,     // 지급 — 재화·자원·캐릭터·장비
		Growth,   // 성장 — 경험치·응축·특성·해금
		World,    // 월드 — 정산·전역 배수·시간
		Mail,     // 우편
	}

	// 치트 칸 하나. 이 클래스를 상속하기만 하면 'CheatWindow'가 찾아 분류에 넣는다 — 창은 고치지 않는다.
	// 입력값은 [SerializeField]로 둔다 — 창이 [SerializeReference]로 들고 있어 플레이 진입(도메인 리로드)에도 남는다.
	// ★ 상속 클래스에도 [Serializable]을 붙인다. 없으면 [SerializeReference]가 저장하지 못해 리로드마다 입력이 초기화된다.
	[Serializable]
	internal abstract class CheatSection
	{
		public abstract ECheatCategory Category { get; }
		public abstract string         Title    { get; }
		public abstract Color          Accent   { get; }   // 칸 왼쪽 색 띠 — 다른 칸과 겹치지 않게

		public virtual int    Order        => 0;    // 같은 분류 안 순서(작을수록 위)
		public virtual string Keywords     => "";   // 검색에 걸릴 낱말 — 제목·분류 이름은 따로 넣지 않아도 걸린다
		public virtual string Hint         => "";   // 머리 오른쪽 ⓘ 툴팁 — 칸 아래 회색 안내문 대신 여기 쓴다
		public virtual bool   IsServerWide => false;   // 접속 중인 모두에게 걸리면 머리에 '서버 전체' 배지

		// 칸 본문 — 머리와 상자는 창이 그린다.
		public abstract void Draw();

		// 검색어가 제목·분류·키워드 중 하나에 들어 있나 (창의 검색창)
		public bool Matches(string query)
		{
			return Title.Contains(query, StringComparison.OrdinalIgnoreCase)
			    || CategoryLabel.Get(Category).Contains(query, StringComparison.OrdinalIgnoreCase)
			    || Keywords.Contains(query, StringComparison.OrdinalIgnoreCase);
		}
	}

	internal static class CategoryLabel
	{
		public static string Get(ECheatCategory category)
		{
			return category switch
			{
				ECheatCategory.Give   => "지급",
				ECheatCategory.Growth => "성장",
				ECheatCategory.World  => "월드",
				ECheatCategory.Mail   => "우편",
				_                     => category.ToString(),
			};
		}
	}
}

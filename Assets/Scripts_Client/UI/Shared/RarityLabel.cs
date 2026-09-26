using GameData;

// 등급('GlobalRarity') → 사용자에게 보일 이름.
//
// ■ 왜 따로 두는가
// 창고 도구 줄의 일괄 담기 드롭다운('StorageToolPresenter')과 창고 칸 툴팁('StorageSlotSource')이
// 같은 이름을 쓴다. 한쪽에만 적어 두면 같은 등급이 두 화면에서 다르게 읽힌다.
// 색은 'RarityPalette', 이름은 여기 — 'IndustryLabel'과 같은 부류다.
//
// ⚠️ **여기가 출처가 아니라 사본이다.** 진짜 출처는 'Enum.xlsx'의 등급 주석이다 —
//   표시 이름을 엑셀에서 읽게 되면(T-085, 옛 T-047) 이 파일은 지운다.
public static class RarityLabel
{
    // 이 등급의 표시 이름. 모르는 값이면 영문 이름을 그대로 돌려준다 —
    // 빈 문자열로 떨어뜨리면 화면에서 사라져 무엇이 빠졌는지 알 수 없다('IndustryLabel'과 같다).
    public static string Get(GlobalRarity rarity) => rarity switch
    {
        GlobalRarity.Common    => "일반",
        GlobalRarity.Uncommon  => "고급",
        GlobalRarity.Rare      => "희귀",
        GlobalRarity.Epic      => "영웅",
        GlobalRarity.Legendary => "전설",
        GlobalRarity.Mythic    => "신화",
        _                      => rarity.ToString(),
    };
}

using GameData;
using MikaProtocol;

namespace WSGameServer;

// 검색 속성 — 경매장은 GameData를 모르니 거를 값을 메인이 숫자로 만든다 (T-155).
// 실제 등록과 더미 매물(T-204)이 같은 함수를 쓴다 — 갈라지면 더미가 필터 검색에 안 걸린다.
public static class AuctionAttrs
{
    public static Dictionary<int, long> Of(Equip equip)
    {
        var enchantSpeed = equip.EnchantOptions.Where(o => o.OptionType == EnchantOptionType.Speed).Sum(o => (long)o.Value);
        var enchantExp   = equip.EnchantOptions.Where(o => o.OptionType == EnchantOptionType.CharacterExp).Sum(o => (long)o.Value);

        return new Dictionary<int, long>
        {
            [(int)EAuctionAttr.EquipIndustry]       = (long)equip.Industry,
            [(int)EAuctionAttr.EquipSpeed]          = equip.SpeedAddPermille + enchantSpeed,
            [(int)EAuctionAttr.EnchantSpeed]        = enchantSpeed,
            [(int)EAuctionAttr.EnchantCharacterExp] = enchantExp,
        };
    }

    public static Dictionary<int, long> Of(Character character, int star)
    {
        var attrs = new Dictionary<int, long>
        {
            [(int)EAuctionAttr.CharacterLevel] = character.Level,
            [(int)EAuctionAttr.CharacterStar]  = star,
        };
        foreach (var industry in new[] { IndustryType.Farming, IndustryType.Excavation, IndustryType.Mining, IndustryType.Logging, IndustryType.Hunting })
        {
            attrs[(int)EAuctionAttr.AptitudeFarming - 1 + (int)industry] = character.GetAptitude(industry);
        }

        return attrs;
    }
}

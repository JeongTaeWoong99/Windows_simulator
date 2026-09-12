using GameData;
using MikaProtocol;
using UnityEngine;

// 등급('GlobalRarity')을 화면에 보일 색으로 바꾼다.
// 서버 enum을 화면에 그대로 노출하지 않기 위한 표다 — 'ResultMessages'와 같은 부류라 나란히 둔다.
//
// 색 값의 주인은 엑셀 'Enum.xlsx'의 등급 주석이다. 등급이 늘면 여기 한 줄을 더한다.
//
// TODO: 등급 스프라이트가 만들어지면 색 대신 'Sprite'를 돌려주도록 바꾼다.
//       지금 호출부('SlotView.Bind')는 'Rarity Image'의 color만 건드리므로,
//       이 표와 그 한 줄을 sprite 대입으로 갈아끼우면 끝난다.
public static class RarityPalette
{
    // 등급을 모를 때 쓰는 색. 'None'과 표에 없는 값이 여기로 떨어진다.
    // 'SlotView.Clear'가 칸을 비울 때 되돌리는 색이기도 하다.
    public static readonly Color Unknown = new Color32(0x4A, 0x4A, 0x4A, 0xFF);

    // 테이블 등급에 대응하는 색. 모르는 등급도 예외 없이 'Unknown'으로 떨어진다(표시용이다).
    public static Color Get(GlobalRarity rarity) => rarity switch
    {
        GlobalRarity.Common    => new Color32(0x9D, 0x9D, 0x9D, 0xFF), // 일반, 흰색/회색
        GlobalRarity.Uncommon  => new Color32(0x1E, 0xFF, 0x00, 0xFF), // 고급, 초록
        GlobalRarity.Rare      => new Color32(0x00, 0x70, 0xDD, 0xFF), // 희귀, 파랑
        GlobalRarity.Epic      => new Color32(0xA3, 0x35, 0xEE, 0xFF), // 영웅, 보라
        GlobalRarity.Legendary => new Color32(0xFF, 0x80, 0x00, 0xFF), // 전설, 주황/금색
        GlobalRarity.Mythic    => new Color32(0xE6, 0xCC, 0x80, 0xFF), // 신화, 빨강/핑크
        _                      => Unknown,
    };

    // 패킷 등급('EGlobalRarity')을 테이블 등급으로 옮긴다. 가챠 보상('GachaRewardInfo.Rarity')이 이쪽으로 온다.
    //
    // 두 enum은 이름·값이 1:1로 맞춰져 있고 'PacketEnumTest'(서버 테스트)가 그것을 강제한다.
    // 한쪽만 늘리면 테스트가 먼저 깨지므로 캐스팅해도 조용히 어긋나지 않는다.
    // 캐스팅을 부르는 쪽마다 흩어 두지 않으려고 이 한 줄에 모아 둔다.
    public static GlobalRarity ToTableRarity(EGlobalRarity rarity) => (GlobalRarity)rarity;
}

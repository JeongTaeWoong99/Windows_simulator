using GameData;

namespace WSGameServer.User;

public partial class User
{
    /// <summary>신규 유저에게 지급하는 기본 캐릭터. 2~1000번은 예약 대역이고 일반 캐릭터는 1001부터다.</summary>
    public const int DefaultCharacterTid = 1;

    /// <summary>
    /// 유저가 소유한 캐릭터 개체들. 키는 <b>개체 PK</b>(<c>t_character.character_id</c>)이며 TID가 아니다.
    /// 같은 캐릭터를 여러 장 가질 수 있으므로 TID로는 유일하게 못 찾는다.
    /// </summary>
    private readonly Dictionary<long, Character.Character> _characters = new();

    public IReadOnlyCollection<Character.Character> Characters => _characters.Values;

    /// <summary>DB에서 읽은 캐릭터를 적재한다(로그인 시 1회).</summary>
    public void LoadCharacters(IEnumerable<Character.Character> characters)
    {
        _characters.Clear();
        foreach (var character in characters)
            _characters[character.Id] = character;
    }

    public bool TryGetCharacter(long characterId, out Character.Character character)
        => _characters.TryGetValue(characterId, out character!);

    /// <summary>
    /// 이 캐릭터를 해당 산업에 배치할 수 있는지. <b>적성 0이면 배치하지 못한다.</b>
    /// 배치 자체를 막아야 "이 캐릭터는 낚시를 못 한다"가 규칙으로 성립한다 —
    /// 허용하고 아주 느리게 두면 슬롯이 남을 때 아무나 꽂게 되어 배치에 선택이 사라진다.
    /// </summary>
    public bool CanAssignCharacter(long characterId, ItemType industry)
    {
        if (industry == ItemType.None || characterId == 0)
            return true;   // 배치 해제는 언제나 허용한다

        return TryGetCharacter(characterId, out var character) && character.CanWork(industry);
    }
}

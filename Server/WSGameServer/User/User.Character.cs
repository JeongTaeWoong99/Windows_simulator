using GameData;
using MikaProtocol;

namespace WSGameServer;

public partial class User
{
    /// <summary>
    /// 신규 유저에게 지급하는 시작 캐릭터. 2~1000번은 예약 대역이고 일반 캐릭터는 1001부터다.
    /// <b>전 산업 적성이 1 이상인 캐릭터여야 한다</b> — 적성 0인 산업은 배치가 막히므로,
    /// 시작 캐릭터에 0이 섞이면 신규 유저는 그 산업을 아예 시작하지 못한다.
    /// </summary>
    public const int DefaultCharacterTid = 1001;

    /// <summary>
    /// 유저가 소유한 캐릭터 개체들. 키는 <b>개체 PK</b>(<c>t_character.character_id</c>)이며 TID가 아니다.
    /// 같은 캐릭터를 여러 장 가질 수 있으므로 TID로는 유일하게 못 찾는다.
    /// </summary>
    private readonly Dictionary<long, Character> _characters = new();

    public IReadOnlyCollection<Character> Characters => _characters.Values;

    /// <summary>DB에서 읽은 캐릭터 Row를 도메인으로 변환해 적재한다(로그인 시 1회).</summary>
    public void LoadCharacters(IReadOnlyList<CharacterRow> rows)
    {
        _characters.Clear();
        foreach (var r in rows)
        {
            // 테이블에 없는 TID는 건너뛴다. 여기서 예외를 던지면 데이터 한 줄 때문에 로그인이 막힌다.
            if (!GameTable.CharacterTable.TryGet(r.character_tid, out var row))
            {
                ServerLog.Warn("로그인", $"CharacterTable에 없는 TID, 건너뜀: {r.character_tid} (개체 {r.character_id})");
                continue;
            }

            _characters[r.character_id] = new Character(r.character_id, row, r.level, r.exp);
        }
    }

    /// <summary>
    /// 가챠로 뽑은 캐릭터를 지급한다. 개체 PK는 DB가 발급하므로 결과는
    /// <see cref="OnGachaCharactersGranted"/>로 돌아온다. <b>같은 TID가 여러 번 들어와도 된다</b> —
    /// 중복은 개체를 늘리는 것으로 처리한다(캐릭터 기획 1.1).
    /// </summary>
    public void GrantGachaCharacters(IReadOnlyList<int> characterTids)
    {
        PostDBTask(new GrantCharacterRepository(this, characterTids, CharacterGrantReason.Gacha));
    }

    /// <summary>
    /// 가챠 지급이 끝나면 불린다(로직 스레드). 적재 후 목록을 다시 내려보낸다 —
    /// 클라이언트가 <b>재로그인 없이</b> 뽑은 캐릭터를 배치할 수 있어야 한다.
    /// </summary>
    public void OnGachaCharactersGranted(IReadOnlyList<(long Id, int Tid)> granted)
    {
        foreach (var (characterId, characterTid) in granted)
        {
            AddCharacter(characterId, characterTid);
        }

        SendCharacters();
    }

    /// <summary>
    /// 지급받은 캐릭터 하나를 메모리에 올린다.
    /// 테이블에 없는 TID는 <see cref="LoadCharacters"/>와 같은 정책으로 경고만 남기고 건너뛴다.
    /// </summary>
    private void AddCharacter(long characterId, int characterTid)
    {
        if (!GameTable.CharacterTable.TryGet(characterTid, out var row))
        {
            ServerLog.Warn("캐릭터", $"CharacterTable에 없는 TID, 지급 건너뜀: {characterTid} (개체 {characterId})");
            return;
        }

        _characters[characterId] = new Character(characterId, row, level: 1, exp: 0);
    }

    /// <summary>
    /// 보유 캐릭터 전체 스냅샷을 보낸다(로그인 직후).
    /// 클라이언트는 여기서 받은 CharacterId로 슬롯 배치를 요청한다.
    /// </summary>
    public void SendCharacters()
    {
        Send(new S_CharacterListResponse
        {
            Characters = _characters.Values.Select(ToCharacterInfo).ToList(),
        });
    }

    /// <summary>
    /// 경험치를 캐릭터에 더한다(판정 정산·치트가 같은 경로를 쓴다). 바뀐 것이 있으면 <b>확정값을 저장하고 개체를 밀어 준다.</b>
    /// 만렙이라 아무것도 안 바뀌면 저장도 푸시도 하지 않는다.
    /// </summary>
    public void GrantCharacterExp(Character character, int amount, bool notify)
    {
        var levelBefore = character.Level;
        var expBefore   = character.Exp;

        character.GainExp(amount, _characterLevels);

        if (character.Level == levelBefore && character.Exp == expBefore)
        {
            return;
        }

        PostDBTask(new SaveCharacterGrowthRepository(this, character));

        if (notify)
        {
            Send(new S_CharacterSyncResponse { Character = ToCharacterInfo(character) });
        }
    }

    private static CharacterInfo ToCharacterInfo(Character c)
    {
        return new CharacterInfo
        {
            CharacterId  = c.Id,
            CharacterTid = c.Tid,
            Level        = c.Level,
            Exp          = c.Exp,
            Aptitudes    = ToAptitudeInfos(c),
        };
    }

    // 적성을 산업과 짝지어 내려보낸다 — 클라가 CharacterTable을 직접 읽지 않게 하려는 것이다.
    // 보정이 생겨도 GetAptitude 한 곳만 바뀌고 이 변환은 그대로다.
    private static List<AptitudeInfo> ToAptitudeInfos(Character character)
    {
        return Character.Industries
            .Select(industry => new AptitudeInfo
            {
                Industry = (EIndustryType)industry,
                Value    = (byte)character.GetAptitude(industry),
            })
            .ToList();
    }

    public bool TryGetCharacter(long characterId, out Character character)
        => _characters.TryGetValue(characterId, out character!);

    /// <summary>
    /// 이 캐릭터를 해당 산업에 배치할 수 있는지. <b>적성 0이면 배치하지 못한다.</b>
    /// 배치 자체를 막아야 "이 캐릭터는 낚시를 못 한다"가 규칙으로 성립한다 —
    /// 허용하고 아주 느리게 두면 슬롯이 남을 때 아무나 꽂게 되어 배치에 선택이 사라진다.
    /// </summary>
    public bool CanAssignCharacter(long characterId, IndustryType industry)
    {
        if (industry == IndustryType.None || characterId == 0)
        {
            return true;   // 배치 해제는 언제나 허용한다
        }

        return TryGetCharacter(characterId, out var character) && character.CanWork(industry);
    }
}

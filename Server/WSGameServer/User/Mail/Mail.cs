using System.Text.Json;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 우편 한 통의 첨부. 아이템은 (TID, 수량), 캐릭터·장비는 한 개체 = 한 원소다.
/// <c>EquipTids</c>·<c>CharacterTids</c>는 받을 때 <b>새로 만들고</b>, <c>LockedEquips</c>·<c>LockedCharacters</c>는
/// 이미 있는 개체(경매)의 잠금을 푼다 — 인챈트·레벨이 그대로 간다.
/// </summary>
public sealed record MailAttachment(
    long Gold,
    List<(int Tid, int Count)> Items,
    List<int> CharacterTids,
    List<int> EquipTids,
    List<MailEquip>? LockedEquips = null,
    List<MailCharacter>? LockedCharacters = null)
{
    public static MailAttachment Empty => new(0, new(), new(), new());

    public IReadOnlyList<MailEquip> Equips => LockedEquips ?? (IReadOnlyList<MailEquip>)Array.Empty<MailEquip>();

    public IReadOnlyList<MailCharacter> Characters => LockedCharacters ?? (IReadOnlyList<MailCharacter>)Array.Empty<MailCharacter>();

    // DB 컬럼(JSON) 왕복. 아이템은 [[TID, 수량], ...] — 튜플은 JSON 배열로 직접 안 나가서 int[]로 옮긴다.
    public string ItemsJson => JsonSerializer.Serialize(Items.Select(i => new[] { i.Tid, i.Count }));
    public string CharacterTidsJson => JsonSerializer.Serialize(CharacterTids);
    public string EquipTidsJson => JsonSerializer.Serialize(EquipTids);
    public string EquipIdsJson => JsonSerializer.Serialize(Equips);
    public string CharacterIdsJson => JsonSerializer.Serialize(Characters);

    public static MailAttachment FromRow(UserMailRow row)
    {
        var items = JsonSerializer.Deserialize<List<int[]>>(row.items) ?? new();
        return new MailAttachment(
            row.gold,
            items.Select(pair => (pair[0], pair[1])).ToList(),
            JsonSerializer.Deserialize<List<int>>(row.character_tids) ?? new(),
            JsonSerializer.Deserialize<List<int>>(row.equip_tids) ?? new(),
            JsonSerializer.Deserialize<List<MailEquip>>(row.equip_ids) ?? new(),
            JsonSerializer.Deserialize<List<MailCharacter>>(row.character_ids) ?? new());
    }
}

/// <summary>우편에 실린 장비 개체. 장비 행은 잠긴 채 받는 사람 소유이고, 받을 때 잠금을 풀고 창고 칸을 준다.</summary>
public sealed record MailEquip(long EquipId, int EquipTid, int EnchantGrade, List<int> Options)
{
    public EquipInfo ToInfo()
    {
        return new EquipInfo { EquipId = EquipId, EquipTid = EquipTid, EnchantGrade = EnchantGrade, EnchantOptions = Options.ToList() };
    }
}

/// <summary>우편에 실린 캐릭터 개체. 행은 잠긴 채 받는 사람 소유이고, 받을 때 잠금을 푼다. 레벨·적성은 잠긴 동안 바뀌지 않는다.</summary>
// CondenseCount는 뒤에 붙였다 — 그 전에 JSON으로 저장된 우편·경매 매물은 0으로 읽힌다.
public sealed record MailCharacter(long CharacterId, int CharacterTid, int Level, int Exp, int CondenseCount = 0);

/// <summary>우편 한 통. <see cref="ClaimedAt"/>이 null이면 안 받음 — 안 받은 우편은 만료되지 않는다.</summary>
public sealed class Mail(long id, int templateTid, MailAttachment attachment, DateTime receivedAt, DateTime? claimedAt)
{
    /// <summary>받은 우편을 남겨 두는 기간. 지나면 로그인 때 지운다 — 공용 상수 시트(T-077)가 서면 옮긴다.</summary>
    public static readonly TimeSpan ClaimedRetention = TimeSpan.FromDays(7);

    public long           Id          { get; } = id;
    public int            TemplateTid { get; } = templateTid;
    public MailAttachment Attachment  { get; } = attachment;
    public DateTime       ReceivedAt  { get; } = receivedAt;
    public DateTime?      ClaimedAt   { get; private set; } = claimedAt;

    public bool IsClaimed => ClaimedAt.HasValue;

    public void MarkClaimed(DateTime now) => ClaimedAt = now;

    /// <summary>클라 형식. 캐릭터 개체는 적성 계산에 유저의 레벨 곡선이 필요해 변환을 호출자가 준다.</summary>
    public MailInfo ToInfo(Func<MailCharacter, CharacterInfo?> toCharacter)
    {
        return new MailInfo
        {
            MailId           = Id,
            TemplateTid      = TemplateTid,
            Gold             = Attachment.Gold,
            Items            = Attachment.Items.Select(i => new ItemInfo { ItemId = i.Tid, Count = i.Count }).ToList(),
            CharacterTids    = Attachment.CharacterTids.ToList(),
            EquipTids        = Attachment.EquipTids.ToList(),
            Equips           = Attachment.Equips.Select(e => e.ToInfo()).ToList(),
            Characters       = Attachment.Characters.Select(toCharacter).OfType<CharacterInfo>().ToList(),
            ReceivedAtUnixMs = ToUnixMs(ReceivedAt),
            ClaimedAtUnixMs  = ClaimedAt.HasValue ? ToUnixMs(ClaimedAt.Value) : 0,
        };
    }

    private static long ToUnixMs(DateTime utc) => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
}

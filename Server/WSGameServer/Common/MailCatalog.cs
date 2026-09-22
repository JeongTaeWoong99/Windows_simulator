using GameData;
using MikaUtils;

namespace WSGameServer;

/// <summary>
/// 우편 템플릿(<c>MailTemplateTable</c>) 인덱스. 제목·본문·첨부·전체 발송 기간을 한 줄에 담는다 → 기획 우편 3장.
/// 로드할 때 <c>ItemTIDs</c>와 <c>ItemCounts</c>의 개수가 다르면 기동을 멈춘다 — 조용히 돌면 엉뚱한 수량이 나간다.
/// </summary>
public sealed class MailCatalog : Singleton<MailCatalog>
{
    private readonly Dictionary<int, MailTemplateTableRow> _templates = new();

    public int Count => _templates.Count;

    /// <summary>반드시 <c>GameTable.LoadAll</c> 이후에 부른다.</summary>
    public void LoadAll() => Load(GameTable.MailTemplateTable.All);

    public void Load(IEnumerable<MailTemplateTableRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        _templates.Clear();
        foreach (var row in rows)
        {
            if (row.ItemTIDs.Length != row.ItemCounts.Length)
            {
                throw new InvalidDataException(
                    $"MailTemplateTable {row.MailTemplateTID}: ItemTIDs {row.ItemTIDs.Length}개 ≠ ItemCounts {row.ItemCounts.Length}개");
            }

            if (row.ItemCounts.Any(count => count <= 0))
            {
                throw new InvalidDataException($"MailTemplateTable {row.MailTemplateTID}: ItemCounts에 0 이하 수량");
            }

            _templates[row.MailTemplateTID] = row;
        }
    }

    public bool TryGet(int templateTid, out MailTemplateTableRow row) => _templates.TryGetValue(templateTid, out row!);

    /// <summary>템플릿의 첨부를 우편 한 통의 첨부로 옮긴다. 보낸 순간 복사하므로 나중에 템플릿을 고쳐도 보낸 우편은 그대로다.</summary>
    public static MailAttachment AttachmentOf(MailTemplateTableRow row)
    {
        return new MailAttachment(
            row.Gold,
            row.Dia,
            row.ItemTIDs.Zip(row.ItemCounts, (tid, count) => (tid, count)).ToList(),
            row.CharacterTIDs.ToList(),
            row.EquipTIDs.ToList());
    }
}

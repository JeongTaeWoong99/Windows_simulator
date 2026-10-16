using ClosedXML.Excel;
using static ExcelGenerator.ExcelGenerator;

namespace ExcelGenerator;

/// <summary>
/// 데이터 시트의 lstring 컬럼을 번역 파일(Localization.xlsx)과 맞추고, 언어별 값을 담은 LocalizationTable을 만든다.
/// - LocalizationTable.Item 등 : 원본 테이블마다 한 시트. 생성기가 시트와 행을 만든다(Key · KR · Status). 사람은 언어 칸(EN …)만 쓴다
/// - LocalizationTable.UI      : 사람이 키까지 쓴다 — 클라 고정 문구
/// </summary>
public static class Localization
{
    public const string FileName      = "Localization.xlsx";
    public const string TableName     = "LocalizationTable";
    public const string UiSheet       = TableName + ".UI";
    public const string LocalizedType = "lstring";

    private const int MaxSheetName = 31;   // 엑셀 시트 이름 한도

    private const string KeyColumn    = "Key";
    private const string SourceColumn = "KR";   // 기준 언어 — 데이터 시트에 적힌 원문
    private const string StatusColumn = "Status";

    private const string StalePrefix  = "🔄 ";
    private const string EmptySuffix  = " 없음";
    private const string EmptyPrefix  = "⚠️ ";
    private const string StatusSeparator = " · ";

    /// <summary>lstring 셀 하나. Template은 보간 전 원문이다.</summary>
    public sealed record Source(string Key, TableData Table, string[] Row, string Column, string Template);

    /// <summary>
    /// 생성기 진입점. 번역 파일이 없으면 아무것도 만들지 않는다.
    /// TooltipInterpolator.Apply보다 먼저 불러야 한다 — 원문 템플릿을 읽어야 한다.
    /// </summary>
    public static void Apply(string excelDir, List<TableData> tables, bool strict)
    {
        var path = Path.Combine(excelDir, FileName);
        if (!File.Exists(path))
        {
            Console.WriteLine($"[번역] {FileName}이 없어 번역 테이블을 만들지 않는다.");
            return;
        }

        var sources = Collect(tables);

        using var workbook = OpenWorkbook(path);
        var uiSheet = RequireSheet(workbook, UiSheet);
        var ui = ParseSheet(uiSheet);
        var dataSheets = workbook.Worksheets.Where(IsDataSheet).ToList();
        var data = Merge(ui, dataSheets.Select(ParseSheet));

        var synced = Sync(sources, data);
        if (WriteSheets(uiSheet, dataSheets, data.columnInfos, synced))
        {
            workbook.Save();
            Console.WriteLine($"[번역] 번역 시트 갱신: {synced.Count}행 → {path}");
        }

        tables.Add(Build(sources, data with { Rows = synced }, ui, strict));
    }

    /// <summary>모든 테이블의 lstring 셀을 모은다. 키 = 테이블명(끝 Table 뺌).첫 컬럼 값.컬럼명</summary>
    public static List<Source> Collect(IReadOnlyList<TableData> tables)
    {
        var sources = new List<Source>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var table in tables)
        {
            var localized = Enumerable.Range(0, table.columnInfos.Count).Where(i => table.columnInfos[i].IsLocalized).ToList();
            if (localized.Count == 0)
            {
                continue;
            }

            foreach (var row in table.Rows)
            {
                foreach (var i in localized)
                {
                    var key = KeyOf(table, row, table.columnInfos[i].Name);
                    if (!seen.Add(key))
                    {
                        throw new InvalidDataException($"번역 키 '{key}'가 두 번 나온다 — {table.Name}의 첫 컬럼 값이 겹친다.");
                    }

                    sources.Add(new Source(key, table, row, table.columnInfos[i].Name, row[i]));
                }
            }
        }

        return sources;
    }

    /// <summary>
    /// 번역 시트의 새 행 목록(시트 컬럼 순서)을 만든다. 원본 순서를 따르고 사라진 키는 뺀다.
    /// 원문이 바뀐 줄의 "🔄 EN"은 사람이 상태 칸을 지울 때까지 남는다 — 재번역했는지 생성기는 알 수 없다.
    /// </summary>
    public static List<string[]> Sync(IReadOnlyList<Source> sources, TableData sheet)
    {
        var layout = Layout.Of(sheet);
        var existing = sheet.Rows.ToDictionary(r => r[layout.Key], StringComparer.Ordinal);

        var rows = new List<string[]>(sources.Count);
        foreach (var source in sources.Where(s => s.Template.Length > 0))   // 빈 원문은 번역할 것이 없다
        {
            var row = new string[sheet.columnInfos.Count];
            Array.Fill(row, "");
            row[layout.Key]    = source.Key;
            row[layout.Source] = source.Template;

            var stale = new HashSet<string>(StringComparer.Ordinal);
            if (existing.TryGetValue(source.Key, out var old))
            {
                foreach (var (language, index) in layout.Languages)
                {
                    row[index] = old[index];
                }

                stale.UnionWith(StaleLanguages(old[layout.Status]));
                if (old[layout.Source] != source.Template)
                {
                    stale.UnionWith(layout.Languages.Select(l => l.Name));
                }
            }

            row[layout.Status] = Status(layout, row, stale);
            rows.Add(row);
        }

        return rows;
    }

    /// <summary>
    /// 런타임용 LocalizationTable(Key · KR · 언어…)을 만든다. Status는 뺀다.
    /// Tooltip은 언어마다 보간하고, 빈 번역은 KR로 대신한다(strict면 멈춘다).
    /// </summary>
    public static TableData Build(IReadOnlyList<Source> sources, TableData data, TableData ui, bool strict)
    {
        var layout = Layout.Of(data);
        AssertSameColumns(data, ui);

        var byKey = data.Rows.ToDictionary(r => r[layout.Key], StringComparer.Ordinal);
        var missing = new List<string>();
        var rows = new List<string[]>();

        foreach (var source in sources)
        {
            if (source.Template.Length == 0)
            {
                rows.Add(Enumerable.Repeat("", 1 + layout.Languages.Count).Prepend(source.Key).ToArray());
                continue;
            }

            var sheetRow = byKey[source.Key];
            var output = new List<string> { source.Key, Render(source, byKey, layout, layout.Source) };
            foreach (var (language, index) in layout.Languages)
            {
                if (sheetRow[index].Length == 0)
                {
                    missing.Add($"{source.Key}({language})");
                    output.Add(output[1]);
                    continue;
                }

                AssertSameTokens(source, sheetRow, layout, language, index);
                output.Add(Render(source, byKey, layout, index));
            }
            rows.Add(output.ToArray());
        }

        foreach (var uiRow in ui.Rows)
        {
            var output = new List<string> { uiRow[layout.Key], uiRow[layout.Source] };
            foreach (var (language, index) in layout.Languages)
            {
                if (uiRow[index].Length == 0)
                {
                    missing.Add($"{uiRow[layout.Key]}({language})");
                }
                output.Add(uiRow[index].Length > 0 ? uiRow[index] : uiRow[layout.Source]);
            }
            rows.Add(output.ToArray());
        }

        if (missing.Count > 0)
        {
            var sample = string.Join(", ", missing.Take(5));
            if (strict)
            {
                throw new InvalidDataException($"번역이 빈 칸 {missing.Count}개 — {sample} …");
            }
            Console.WriteLine($"[경고] 번역이 빈 칸 {missing.Count}개는 KR로 대신했다 — {sample} …");
        }

        var columns = new List<ColumnInfo> { data.columnInfos[layout.Key], data.columnInfos[layout.Source] };
        columns.AddRange(layout.Languages.Select(l => data.columnInfos[l.Index]));
        return new TableData(TableName, columns, rows);
    }

    /// <summary>키가 들어갈 번역 시트 — 키 앞부분이 시트 라벨이다(Item.100061.Name → LocalizationTable.Item).</summary>
    public static string SheetOf(string key)
    {
        var name = $"{TableName}.{key.Split('.', 2)[0]}";
        if (name == UiSheet)
        {
            throw new InvalidDataException($"번역 키 '{key}'가 {UiSheet} 시트와 겹친다 — 테이블 이름을 바꾼다.");
        }
        if (name.Length > MaxSheetName)
        {
            throw new InvalidDataException($"번역 시트 이름 '{name}'이 엑셀 한도 {MaxSheetName}자를 넘는다 — 테이블 이름을 줄인다.");
        }

        return name;
    }

    /// <summary>번역 키 앞부분 — 테이블명에서 끝의 Table을 뺀다(ItemTable → Item). 생성 코드의 LocKey 속성도 이걸 쓴다.</summary>
    public static string KeyPrefix(string tableName)
    {
        return tableName.EndsWith("Table", StringComparison.Ordinal) ? tableName[..^"Table".Length] : tableName;
    }

    private static string KeyOf(TableData table, string[] row, string column)
    {
        return $"{KeyPrefix(table.Name)}.{row[0]}.{column}";
    }

    /// <summary>한 셀을 그 언어로 만든다. Tooltip만 보간하고, {Name} 같은 lstring 토큰은 같은 언어 값으로 채운다.</summary>
    private static string Render(Source source, Dictionary<string, string[]> byKey, Layout layout, int languageIndex)
    {
        var template = byKey[source.Key][languageIndex];
        if (source.Column != TooltipInterpolator.ColumnName)
        {
            return template;
        }

        var row = (string[])source.Row.Clone();
        for (var i = 0; i < source.Table.columnInfos.Count; i++)
        {
            var column = source.Table.columnInfos[i];
            if (!column.IsLocalized || column.Name == TooltipInterpolator.ColumnName)
            {
                continue;
            }

            if (byKey.TryGetValue(KeyOf(source.Table, source.Row, column.Name), out var translated))
            {
                row[i] = translated[languageIndex].Length > 0 ? translated[languageIndex] : translated[layout.Source];
            }
        }

        return TooltipInterpolator.Interpolate(source.Table, row, template);
    }

    private static void AssertSameTokens(Source source, string[] sheetRow, Layout layout, string language, int index)
    {
        if (source.Column != TooltipInterpolator.ColumnName)
        {
            return;
        }

        var expected = TooltipInterpolator.Tokens(sheetRow[layout.Source]);
        var actual   = TooltipInterpolator.Tokens(sheetRow[index]);
        if (!expected.SetEquals(actual))
        {
            throw new InvalidDataException(
                $"{source.Key} {language} \"{sheetRow[index]}\": 토큰이 KR과 다르다 — KR {{{string.Join("}{", expected.Order())}}} / {language} {{{string.Join("}{", actual.Order())}}}");
        }
    }

    private static IEnumerable<string> StaleLanguages(string status)
    {
        return status.Split(StatusSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(s => s.StartsWith(StalePrefix, StringComparison.Ordinal))
            .Select(s => s[StalePrefix.Length..].Trim());
    }

    private static string Status(Layout layout, string[] row, HashSet<string> stale)
    {
        var parts = new List<string>();
        foreach (var (language, index) in layout.Languages)
        {
            if (row[index].Length == 0)
            {
                parts.Add(EmptyPrefix + language + EmptySuffix);
            }
            else if (stale.Contains(language))
            {
                parts.Add(StalePrefix + language);
            }
        }

        return string.Join(StatusSeparator, parts);
    }

    private static bool SameRows(IReadOnlyList<string[]> a, IReadOnlyList<string[]> b)
    {
        return a.Count == b.Count && a.Zip(b).All(p => p.First.SequenceEqual(p.Second));
    }

    private static void AssertSameColumns(TableData expected, TableData actual)
    {
        var a = expected.columnInfos.Select(c => c.Name);
        var b = actual.columnInfos.Select(c => c.Name);
        if (!a.SequenceEqual(b))
        {
            throw new InvalidDataException($"{actual.Name}의 컬럼({string.Join(",", b)})이 {expected.Name}({string.Join(",", a)})와 다르다.");
        }
    }

    private static bool IsDataSheet(IXLWorksheet sheet)
    {
        return sheet.Name.StartsWith(TableName + ".", StringComparison.Ordinal) && sheet.Name != UiSheet;
    }

    /// <summary>번역 시트들을 한 표로 모은다. 컬럼은 UI 시트와 같아야 한다.</summary>
    private static TableData Merge(TableData ui, IEnumerable<TableData> sheets)
    {
        var rows = new List<string[]>();
        foreach (var sheet in sheets)
        {
            AssertSameColumns(ui, sheet);
            rows.AddRange(sheet.Rows);
        }

        return new TableData(TableName, ui.columnInfos, rows);
    }

    /// <summary>
    /// 행을 키 앞부분별 시트에 나눠 쓴다. 없는 시트는 UI 시트를 복사해 만들고(머리 행·서식), 원본에서 사라진 테이블의 시트는 지운다.
    /// 내용이 같은 시트는 건드리지 않는다. 하나라도 바뀌면 true.
    /// </summary>
    private static bool WriteSheets(IXLWorksheet uiSheet, List<IXLWorksheet> dataSheets, IReadOnlyList<ColumnInfo> columns, List<string[]> rows)
    {
        var changed = false;
        var groups = rows.GroupBy(r => SheetOf(r[0])).ToList();
        foreach (var group in groups)
        {
            var groupRows = group.ToList();
            var sheet = dataSheets.FirstOrDefault(s => s.Name == group.Key);
            if (sheet != null && SameRows(ParseSheet(sheet).Rows, groupRows))
            {
                continue;
            }

            sheet ??= uiSheet.CopyTo(group.Key, uiSheet.Position);
            WriteRows(sheet, columns, groupRows);
            changed = true;
        }

        foreach (var orphan in dataSheets.Where(s => groups.All(g => g.Key != s.Name)))
        {
            orphan.Delete();
            changed = true;
        }

        return changed;
    }

    private static IXLWorksheet RequireSheet(XLWorkbook workbook, string name)
    {
        if (!workbook.TryGetWorksheet(name, out var sheet))
        {
            throw new InvalidDataException($"{FileName}에 '{name}' 시트가 없다.");
        }

        return sheet;
    }

    /// <summary>컬럼명 행 아래 데이터를 지우고 rows를 쓴다. 컬럼명 행 찾는 규칙은 ParseSheet와 같다.</summary>
    private static void WriteRows(IXLWorksheet sheet, IReadOnlyList<ColumnInfo> columns, IReadOnlyList<string[]> rows)
    {
        var lastRow = sheet.LastRowUsed()!.RowNumber();
        var fieldNameRow = 0;
        for (var r = 1; r <= lastRow; r++)
        {
            if (sheet.Cell(r, 1).GetString().Trim().Length > 0)
            {
                fieldNameRow = r;
            }
        }
        fieldNameRow++;

        var columnNumbers = columns.Select(c => sheet.Row(fieldNameRow).CellsUsed().First(cell => cell.GetString().Trim() == c.Name).Address.ColumnNumber).ToArray();
        if (lastRow > fieldNameRow)
        {
            sheet.Rows(fieldNameRow + 1, lastRow).Delete();
        }

        for (var i = 0; i < rows.Count; i++)
        {
            for (var c = 0; c < columnNumbers.Length; c++)
            {
                var cell = sheet.Cell(fieldNameRow + 1 + i, columnNumbers[c]);
                cell.SetValue(rows[i][c]);
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            }
        }
    }

    /// <summary>번역 시트에서 Key · KR · Status와 언어 컬럼(나머지 전부)의 위치</summary>
    private sealed record Layout(int Key, int Source, int Status, IReadOnlyList<(string Name, int Index)> Languages)
    {
        public static Layout Of(TableData sheet)
        {
            int Find(string name)
            {
                for (var i = 0; i < sheet.columnInfos.Count; i++)
                {
                    if (sheet.columnInfos[i].Name == name)
                    {
                        return i;
                    }
                }
                throw new InvalidDataException($"{sheet.Name}: {name} 컬럼이 없다 — 컬럼은 {KeyColumn} · {SourceColumn} · 언어… · {StatusColumn}이다.");
            }

            var key    = Find(KeyColumn);
            var source = Find(SourceColumn);
            var status = Find(StatusColumn);
            if (key != 0)
            {
                throw new InvalidDataException($"{sheet.Name}: 첫 컬럼은 {KeyColumn}이어야 한다 — 이름이 곧 키다.");
            }

            var languages = Enumerable.Range(0, sheet.columnInfos.Count)
                .Where(i => i != key && i != source && i != status)
                .Select(i => (sheet.columnInfos[i].Name, i))
                .ToList();
            return new Layout(key, source, status, languages);
        }
    }
}

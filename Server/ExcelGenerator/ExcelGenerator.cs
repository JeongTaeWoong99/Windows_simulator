using ClosedXML.Excel;

namespace ExcelGenerator;

public static class ExcelGenerator
{
    private static List<TableData> _tables = new();
    
    public static void LoadExcel(string excelDir)
    {
        var excels = Directory.EnumerateFiles(excelDir, "*.xlsx")
            .Where(path => !Path.GetFileName(path).StartsWith("~$"))
            .Where(path => !string.Equals(Path.GetFileName(path), "Enum.xlsx", StringComparison.OrdinalIgnoreCase));
        
        foreach (var excel in excels)
        {
            using var workbook = new XLWorkbook(excel);

            foreach (var worksheet in workbook.Worksheets)
            {
                _tables.Add(ParseSheet(worksheet));
            }
        }
        
        Console.WriteLine("");
        
    }

    public static void GenerateData()
    {
        
    }

    public static void GenerateCode()
    {
        
    }
    

    public record ColumnInfo(
        string                  Name,
        ColumnInfo.RecordType   Type,
        string                  RawType,
        ColumnInfo.Platform     CS,
        int?                    Min,           //
        int?                    Max,
        string?                 DefaultValue
        )
    {
        public enum RecordType : byte
        {
            Ignore      = 0b00000000,
            Int         = 0b00000001,
            Float       = 0b00000010,
            String      = 0b00000100,
            ID          = 0b00000101,
            EnumType    = 0b00000110,
            Long        = 0b00000111,
            Bool        = 0b00001000,
            ArrayString = 0b00001001,
            ArrayNumber = 0b00001010,
            ArrayFloat  = 0b00001011,
            ArrayEnum   = 0b00001100,
            Time        = 0b00001101,
        }

        public enum Platform : byte
        {
            Client          = 0b00000001,
            Server          = 0b00000010,
            ServerClient    = 0b00000011, 
        }
    }
    
    private static ColumnInfo.RecordType MapRecordType(string rawType) => rawType switch
    {
        "int"                           => ColumnInfo.RecordType.Int,
        "long"                          => ColumnInfo.RecordType.Long,
        "float"                         => ColumnInfo.RecordType.Float,
        "string"                        => ColumnInfo.RecordType.String,
        "bool"                          => ColumnInfo.RecordType.Bool,
        "ID"                            => ColumnInfo.RecordType.ID,
        _ when rawType.StartsWith("e")  => ColumnInfo.RecordType.EnumType,  // eItemType, eItemRarity
        _                               => ColumnInfo.RecordType.Ignore,
    };
    

    public record TableData(string Name, IReadOnlyList<ColumnInfo> columnInfos, IReadOnlyList<string[]> Rows);

    public static TableData ParseSheet(IXLWorksheet ws)
    {
        var lastRow = ws.LastRowUsed()!.RowNumber();

        // 1) A열 스캔 → 마커→행 맵
        var markerRows = new Dictionary<string, int>();
        for (var row = 1; row <= lastRow; row++)
        {
            var marker = ws.Cell(row, 1).GetString().Trim();
            if (!string.IsNullOrEmpty(marker))
                markerRows[marker] = row;
        }

        // 2) 필드명 행 / 데이터 시작 행
        var fieldNameRow  = markerRows.Values.Max() + 1;
        var dataStartRow  = fieldNameRow + 1;

        int    Row(string m)          => markerRows.GetValueOrDefault(m, 0);
        string Cell(int r, int c)     => r > 0 ? ws.Cell(r, c).GetString().Trim() : "";

        // 3) 필드 열(B~) 순회 → 스키마 조립
        var columnInfos  = new List<ColumnInfo>();
        var lastCol  = ws.Row(fieldNameRow).LastCellUsed()!.Address.ColumnNumber;
        for (var col = 2; col <= lastCol; col++)
        {
            var name = Cell(fieldNameRow, col);
            if (string.IsNullOrEmpty(name)) continue;   // 빈 열 스킵

            var rawType = Cell(Row("Type"), col);
            columnInfos.Add(new ColumnInfo(
                Name:           name,
                Type:           MapRecordType(rawType),         // "int"→Int, "eItemType"→EnumType ...
                RawType:        rawType,
                CS:             ParsePlatform(Cell(Row("C&S"), col)),   // "a"/"c"/"s" → Platform
                Min:            ParseIntOrNull(Cell(Row("Min"), col)),
                Max:            ParseIntOrNull(Cell(Row("Max"), col)),
                DefaultValue:   Cell(Row("Default(Null)"), col) is { Length: > 0 } d ? d : null));
        }
        
        // 4) 데이터 행 읽기 (스키마 순서대로 셀 수집)
        var rows = new List<string[]>();
        for (var row = dataStartRow; row <= lastRow; row++)
        {
            if (string.IsNullOrEmpty(ws.Cell(row, 2).GetString().Trim()))
                continue;   // 첫 필드(TID) 비면 빈 행

            var cells = new string[columnInfos.Count];
            for (var i = 0; i < columnInfos.Count; i++)
                cells[i] = ws.Cell(row, 2 + i).GetString().Trim();
            rows.Add(cells);
        }

        return new TableData(ws.Name, columnInfos, rows);
    }
    
    static ColumnInfo.Platform ParsePlatform(string s) => s.ToLower() switch
    {
        "c" => ColumnInfo.Platform.Client,
        "s" => ColumnInfo.Platform.Server,
        _   => ColumnInfo.Platform.ServerClient,   // "a"/공백 = 양쪽
    };

    static int? ParseIntOrNull(string s) => int.TryParse(s, out var n) ? n : null;
}
using ClosedXML.Excel;

namespace ExcelGenerator;

public static class ExcelGenerator
{
    private static List<TableData> _tables = new();

    /// <summary>파싱된 테이블들. ExcelDataPacker가 재파싱 후 패킹할 때도 사용한다.</summary>
    public static IReadOnlyList<TableData> Tables => _tables;

    public static void LoadExcel(string excelDir)
    {
        _tables.Clear();

        var excels = Directory.EnumerateFiles(excelDir, "*.xlsx")
            .Where(path => !Path.GetFileName(path).StartsWith("~$"))
            .Where(path => !string.Equals(Path.GetFileName(path), "Enum.xlsx", StringComparison.OrdinalIgnoreCase));

        foreach (var excel in excels)
        {
            using var workbook = OpenWorkbook(excel);

            foreach (var worksheet in workbook.Worksheets)
            {
                _tables.Add(ParseSheet(worksheet));
            }
        }
    }

    /// <summary>
    /// 워크북을 연다. 파일이 잠겨 있으면(대개 Excel에서 그 파일을 열어 둔 상태) 원인을 명확히 밝히고 즉시 실패한다.
    /// 예외를 삼키고 지나가면 뒤에서 "소스가 null" 같은 엉뚱한 오류로 번지므로 여기서 fail-fast 한다.
    /// </summary>
    public static XLWorkbook OpenWorkbook(string path)
    {
        try
        {
            return new XLWorkbook(path);
        }
        catch (IOException ex)
        {
            throw new IOException(
                $"엑셀 파일을 열 수 없습니다: '{path}'. " +
                $"Excel에서 이 파일이 열려 있으면 닫고 다시 실행하세요. (원인: {ex.Message})", ex);
        }
    }

    // 데이터(바이너리) 생성은 ExcelDataPacker 소관이다.
    // MemoryPack은 소스 제너레이터 기반이라 여기서 생성한 Row 클래스가 컴파일된 뒤에야
    // 직렬화할 수 있으므로, "코드 생성(여기) → 패커 빌드+실행(ExcelDataPacker)" 2단계로 나눈다.

    /// <summary>파싱된 테이블들로부터 공유 정의(Row/GameTable/TableSet)는 gameDataDir에,
    /// 툴 전용 Packer 코드는 packerDir에 생성한다.</summary>
    public static void GenerateCode(string gameDataDir, string packerDir)
    {
        TableCodeGenerator.Generate(_tables, gameDataDir, packerDir, ColumnInfo.Platform.ServerClient);
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
        _ when rawType.EndsWith("[]")   => MapArrayType(rawType),           // int[]/long[]/float[]/string[]/eXxx[]
        _ when rawType.StartsWith("e")  => ColumnInfo.RecordType.EnumType,  // eItemType, eItemRarity
        _                               => ColumnInfo.RecordType.Ignore,
    };

    /// <summary>배열 표기 "element[]"의 원소 타입으로 Array 계열 RecordType을 고른다.</summary>
    private static ColumnInfo.RecordType MapArrayType(string rawType)
    {
        var element = rawType[..^2];   // "[]" 제거
        return element switch
        {
            "int" or "long"                => ColumnInfo.RecordType.ArrayNumber,   // 정수 배열(int/long은 원본 타입으로 구분)
            "float"                        => ColumnInfo.RecordType.ArrayFloat,
            "string"                       => ColumnInfo.RecordType.ArrayString,
            _ when element.StartsWith("e") => ColumnInfo.RecordType.ArrayEnum,      // eItemType[] 등
            _                              => ColumnInfo.RecordType.Ignore,
        };
    }
    

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
        //    빈 열을 스킵해도 셀 읽기가 어긋나지 않도록 실제 컬럼 번호를 함께 기록한다.
        var columnInfos  = new List<ColumnInfo>();
        var colNumbers   = new List<int>();
        var lastCol  = ws.Row(fieldNameRow).LastCellUsed()!.Address.ColumnNumber;
        for (var col = 2; col <= lastCol; col++)
        {
            var name = Cell(fieldNameRow, col);
            if (string.IsNullOrEmpty(name)) continue;   // 빈 열 스킵

            var rawType = Cell(Row("Type"), col);
            colNumbers.Add(col);
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
            if (string.IsNullOrEmpty(ws.Cell(row, colNumbers[0]).GetString().Trim()))
                continue;   // 첫 필드(TID) 비면 빈 행

            var cells = new string[columnInfos.Count];
            for (var i = 0; i < columnInfos.Count; i++)
                cells[i] = ws.Cell(row, colNumbers[i]).GetString().Trim();
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
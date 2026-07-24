using System.Runtime.CompilerServices;

namespace ExcelGenerator;

public class Program
{
    public static void Main(string[] args)
    {
        // Excel 폴더는 프로젝트 루트(Program.cs가 있는 곳)의 하위에 있으므로
        // 실행 경로(bin/Debug/...)가 아니라 소스 위치 기준으로 잡는다.
        var enumFilePath = ResolvePath("Excel/Enum.xlsx");
        // 공유 정의(Enum/Row/GameTable/TableSet)는 GameData 프로젝트로, 툴 전용 Packer는 로컬 Output으로 출력한다.
        var gameDataDir = ResolvePath("../GameData");
        var enumOutputPath = Path.Combine(gameDataDir, "Enum.cs");
        var packerDir = ResolvePath("Output/Code/Packer");

        var excelDir = ResolvePath("Excel");


        EnumGenerator.GenerateEnumSource(enumFilePath);
        //Console.OutputEncoding = System.Text.Encoding.UTF8;

        // 생성된 enum 소스를 Output/Code/Enum.cs로 저장
        EnumGenerator.MakeEnumCode(enumOutputPath);

        Console.WriteLine($"ItemType.Farming 존재? {EnumGenerator.HasMember("ItemType", "Farming")}");
        Console.WriteLine($"ItemType.NotExist 존재? {EnumGenerator.HasMember("ItemType", "NotExist")}");
        Console.WriteLine($"ItemRarity.Mythic 존재? {EnumGenerator.HasMember("ItemRarity", "Mythic")}");

        // 데이터 테이블 파싱 → Row 클래스/Packer 코드 생성.
        // 바이너리(.bytes) 생성은 생성된 코드를 컴파일하는 ExcelDataPacker가 담당한다(generate-tables.ps1 참고).
        ExcelGenerator.LoadExcel(excelDir);
        ExcelGenerator.GenerateCode(gameDataDir, packerDir);
    }
    

    /// <summary>
    /// 프로젝트 루트(이 소스 파일이 있는 디렉터리) 기준 상대 경로를 절대 경로로 변환한다.
    /// bin/obj 어디서 실행하든 컴파일 시점의 소스 위치를 사용하므로 안전하다.
    /// </summary>
    public static string ResolvePath(string relativePath, [CallerFilePath] string sourceFilePath = "")
    {
        var projectRoot = Path.GetDirectoryName(sourceFilePath)!;
        return Path.GetFullPath(Path.Combine(projectRoot, relativePath));
    }
}

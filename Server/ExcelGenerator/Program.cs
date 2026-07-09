using System.Runtime.CompilerServices;

namespace ExcelGenerator;

public class Program
{
    public static void Main(string[] args)
    {
        // Excel 폴더는 프로젝트 루트(Program.cs가 있는 곳)의 하위에 있으므로
        // 실행 경로(bin/Debug/...)가 아니라 소스 위치 기준으로 잡는다.
        var enumFilePath = ResolvePath("Excel/Enum.xlsx");
        var enumOutputPath = ResolvePath("Output/Code/Enum.cs");

        var excelDir = ResolvePath("Excel");


        EnumGenerator.GenerateEnumSource(enumFilePath);
        //Console.OutputEncoding = System.Text.Encoding.UTF8;

        // 생성된 enum 소스를 Output/Code/Enum.cs로 저장
        EnumGenerator.MakeEnumCode(enumOutputPath);

        Console.WriteLine($"ItemType.Farming 존재? {EnumGenerator.HasMember("ItemType", "Farming")}");
        Console.WriteLine($"ItemType.NotExist 존재? {EnumGenerator.HasMember("ItemType", "NotExist")}");
        Console.WriteLine($"ItemRarity.Mythic 존재? {EnumGenerator.HasMember("ItemRarity", "Mythic")}");

        ExcelGenerator.LoadExcel(excelDir);
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

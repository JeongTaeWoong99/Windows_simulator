using System.Runtime.CompilerServices;

namespace ExcelGenerator;

public class Program
{
    public static int Main(string[] args)
    {
        // 기획 데이터(엑셀)는 서버 툴 소스가 아니라 저장소 루트의 공용 폴더(GameDesign)에 둔다.
        // 서버/클라이언트 어느 쪽 담당 폴더에도 속하지 않는 공용 입력이라 중립 위치가 맞다.
        // 경로는 실행 경로(bin/Debug/...)가 아니라 소스 위치 기준으로 잡는다.
        var excelDir = ResolvePath("../../GameDesign/Excel");
        var enumFilePath = Path.Combine(excelDir, "Enum.xlsx");
        // 공유 정의(Enum/Row/GameTable/TableSet)는 GameData 프로젝트로, 툴 전용 Packer는 로컬 Output으로 출력한다.
        var gameDataDir = ResolvePath("../GameData");
        var enumOutputPath = Path.Combine(gameDataDir, "Enum.cs");
        var packerDir = ResolvePath("Output/Code/Packer");

        // .bytes 출력 = Server/Shared/Data (서버는 Content로 bin에 복사, Unity는 파이프라인이 StreamingAssets로 복사)
        var dataDir = ResolvePath("../Shared/Data");
        // 사람이 읽는 JSON 사이드카(엑셀 대조/리뷰용)는 원본 엑셀 옆에 둔다 = GameDesign/DataLog
        var logDir = ResolvePath("../../GameDesign/DataLog");

        try
        {
            // 1) Enum 생성 (Enum.xlsx → GameData/Enum.cs)
            EnumGenerator.GenerateEnumSource(enumFilePath);
            EnumGenerator.MakeEnumCode(enumOutputPath);

            Console.WriteLine($"ItemType.Farming 존재? {EnumGenerator.HasMember("ItemType", "Farming")}");
            Console.WriteLine($"ItemRarity.Mythic 존재? {EnumGenerator.HasMember("ItemRarity", "Mythic")}");

            // 2) 데이터 테이블 파싱 → Row/Packer 코드 생성
            ExcelGenerator.LoadExcel(excelDir);
            ExcelGenerator.GenerateCode(gameDataDir, packerDir);

            // 3) 생성 코드를 런타임 컴파일해 .bytes 생성 (구 ExcelDataPacker 역할 흡수) + JSON 사이드카(DataLog)
            ExcelGenerator.GenerateData(gameDataDir, packerDir, dataDir, logDir);

            Console.WriteLine("[완료] 코드(GameData) + 바이너리(Shared/Data) + 로그(DataLog) 생성 완료");
            return 0;
        }
        catch (Exception ex)
        {
            // 데이터 오류·파일 잠금 등은 여기서 원인 체인을 찍고 비0으로 종료한다(파이프라인이 멈추도록).
            Console.Error.WriteLine($"[오류] {ex.Message}");
            for (var inner = ex.InnerException; inner is not null; inner = inner.InnerException)
                Console.Error.WriteLine($"       └ {inner.Message}");
            return 1;
        }
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

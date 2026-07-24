using System.Runtime.CompilerServices;
using GameData;

namespace ExcelDataPacker;

/// <summary>
/// 파이프라인 2단계: 엑셀 재파싱 → 생성된 강타입 Packer로 변환 → MemoryPack 바이너리(.bytes) 기록.
/// 1단계(ExcelGenerator 실행 = 코드 생성)가 선행돼야 한다 — generate-tables.ps1이 순서를 보장한다.
/// </summary>
public class Program
{
    public static int Main(string[] args)
    {
        // 인자로 경로를 덮어쓸 수 있다(테스트/CI용): [0]=엑셀 폴더, [1]=바이너리 출력 폴더
        // 기본 출력은 Server/Shared/Data — 서버는 여기서 bin으로 Content 복사, Unity는 파이프라인이 StreamingAssets로 복사한다.
        var excelDir = args.Length > 0 ? args[0] : ResolvePath("../ExcelGenerator/Excel");
        var dataDir  = args.Length > 1 ? args[1] : ResolvePath("../Shared/Data");

        ExcelGenerator.ExcelGenerator.LoadExcel(excelDir);
        Directory.CreateDirectory(dataDir);

        foreach (var table in ExcelGenerator.ExcelGenerator.Tables)
        {
            if (!TableRegistry.Tables.TryGetValue(table.Name, out var entry))
            {
                // 코드 생성 이후 엑셀에 시트가 추가된 경우 — 1단계부터 다시 돌려야 한다.
                Console.Error.WriteLine($"[오류] '{table.Name}' 패커가 없습니다. ExcelGenerator를 먼저 실행해 코드를 재생성하세요.");
                return 1;
            }

            byte[] bytes;
            try
            {
                bytes = entry.Pack(table.Rows);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[오류] {ex.Message}");
                for (var inner = ex.InnerException; inner is not null; inner = inner.InnerException)
                    Console.Error.WriteLine($"       └ {inner.Message}");
                return 1;
            }

            var outputPath = Path.Combine(dataDir, $"{table.Name}.bytes");
            File.WriteAllBytes(outputPath, bytes);

            // 기록 직후 역직렬화 라운드트립으로 자가검증한다.
            var count = entry.Verify(bytes);
            Console.WriteLine($"[데이터 생성] {table.Name}: {count}행, {bytes.Length:N0} bytes → {outputPath}");
            Console.WriteLine($"    첫 행: {entry.Preview(bytes)}");
        }

        return 0;
    }

    /// <summary>이 소스 파일 위치 기준 상대 경로를 절대 경로로 변환한다(bin/obj 실행 위치 무관).</summary>
    private static string ResolvePath(string relativePath, [CallerFilePath] string sourceFilePath = "")
    {
        var projectRoot = Path.GetDirectoryName(sourceFilePath)!;
        return Path.GetFullPath(Path.Combine(projectRoot, relativePath));
    }
}

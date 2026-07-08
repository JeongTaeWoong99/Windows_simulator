namespace ExcelGenerator;

public class Program
{
    public static void Main(string[] args)
    {
        var localPath = AppContext.BaseDirectory;
        var enumFilePath = "../Excel/Enum.xlsx";

        var enumSource = EnumGenerator.GenerateEnumSource(enumFilePath);
    }

    public static string ResolvePath(string file)
    {
        
    }
}
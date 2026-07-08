using System.Text;
using ClosedXML.Excel;

namespace ExcelGenerator;

public static class EnumGenerator
{
    public static string GenerateEnumSource(string enumFilePath)
    {
        StringBuilder sb = new();
        
        using var workbook = new XLWorkbook(enumFilePath);

        foreach (var worksheet in workbook.Worksheets)
        {
            Console.WriteLine(worksheet);
        }

        return sb.ToString();
    }
}
using System.Globalization;
using System.Text;
using static ExcelGenerator.ExcelGenerator;

namespace ExcelGenerator;

/// <summary>
/// 플레이어용 설명 컬럼(Tooltip)의 {컬럼} · {컬럼:permille} 토큰을 같은 행 값으로 치환한다.
/// 숫자는 실제 컬럼 한 곳에만 두고 설명이 따라오게 한다 — 잘못 쓴 토큰은 화면에 나가기 전에 멈춘다.
/// </summary>
public static class TooltipInterpolator
{
    public const string ColumnName = "Tooltip";

    private const string PermilleFormat = "permille";

    /// <summary>모든 테이블의 Tooltip 셀을 제자리에서 치환한다. 다른 컬럼은 건드리지 않는다.</summary>
    public static void Apply(IReadOnlyList<TableData> tables)
    {
        foreach (var table in tables)
        {
            var target = IndexOf(table, ColumnName);
            if (target < 0)
            {
                continue;
            }

            foreach (var row in table.Rows)
            {
                row[target] = Interpolate(table, row, row[target]);
            }
        }
    }

    private static string Interpolate(TableData table, string[] row, string template)
    {
        var sb = new StringBuilder(template.Length);
        var i = 0;
        while (i < template.Length)
        {
            var c = template[i];
            if (c == '{' && At(template, i + 1, '{'))
            {
                sb.Append('{');
                i += 2;
                continue;
            }
            if (c == '}' && At(template, i + 1, '}'))
            {
                sb.Append('}');
                i += 2;
                continue;
            }
            if (c == '}')
            {
                throw Fail(table, row, template, "짝 없는 '}' — 글자로 쓰려면 '}}'");
            }
            if (c != '{')
            {
                sb.Append(c);
                i++;
                continue;
            }

            var close = template.IndexOf('}', i + 1);
            if (close < 0)
            {
                throw Fail(table, row, template, "'{'가 닫히지 않았다 — 글자로 쓰려면 '{{'");
            }

            sb.Append(Resolve(table, row, template, template[(i + 1)..close]));
            i = close + 1;
        }

        return sb.ToString();
    }

    /// <summary>토큰 하나("컬럼" 또는 "컬럼:지정자")를 값으로 바꾼다.</summary>
    private static string Resolve(TableData table, string[] row, string template, string token)
    {
        var parts  = token.Split(':', 2);
        var column = parts[0].Trim();
        var format = parts.Length > 1 ? parts[1].Trim() : "";

        if (column == ColumnName)
        {
            throw Fail(table, row, template, $"{{{ColumnName}}}는 자기 자신이다");
        }

        var index = IndexOf(table, column);
        if (index < 0)
        {
            throw Fail(table, row, template, $"같은 행에 '{column}' 컬럼이 없다");
        }

        var value = row[index];
        if (value.Length == 0 && table.columnInfos[index].DefaultValue is { } fallback)
        {
            value = fallback;
        }

        if (format.Length == 0)
        {
            return value;
        }
        if (format != PermilleFormat)
        {
            throw Fail(table, row, template, $"모르는 지정자 ':{format}' — 쓸 수 있는 것은 ':{PermilleFormat}'");
        }
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var permille))
        {
            throw Fail(table, row, template, $"'{column}' 값 '{value}'은 정수가 아니라 :{PermilleFormat}를 쓸 수 없다");
        }

        // 천분율 → 퍼센트. 15‰ = 1.5%, 500‰ = 50%
        return (permille / 10m).ToString("0.#", CultureInfo.InvariantCulture) + "%";
    }

    private static bool At(string s, int i, char c)
    {
        return i < s.Length && s[i] == c;
    }

    private static int IndexOf(TableData table, string column)
    {
        for (var i = 0; i < table.columnInfos.Count; i++)
        {
            if (table.columnInfos[i].Name == column)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>테이블 · 행 키(첫 컬럼) · 원문을 담아 어느 셀인지 바로 찾게 한다.</summary>
    private static InvalidDataException Fail(TableData table, string[] row, string template, string reason)
    {
        return new InvalidDataException(
            $"{table.Name} {table.columnInfos[0].Name}={row[0]} {ColumnName} \"{template}\": {reason}");
    }
}

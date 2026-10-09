using System.Globalization;
using System.Text;

namespace Chromaticity.Tools.Services;

public enum CsvInputKind { Spectrum, Lab, Color }
public sealed record CsvTemplate(string FileName, string Csv);
public sealed record DifferenceInput(string Standards, string Samples, int StandardRows, int SampleRows)
{
    public bool Paired => StandardRows > 1;
}

/// <summary>CSV files use positional data columns; header labels and text encoding are not part of the schema.</summary>
public static class CsvDataFiles
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private const int MaxFileBytes = ToolCalculations.MaxTextLength * 4 + 4;

    public static async Task<string> ReadAsync(string fileName, Stream stream, CsvInputKind kind, InputSpace space = InputSpace.Lab) =>
        Normalize(await ReadTextAsync(fileName, stream), kind, space);

    public static async Task<DifferenceInput> ReadDifferenceAsync(string fileName, Stream stream) =>
        ParseDifference(await ReadTextAsync(fileName, stream));

    private static async Task<string> ReadTextAsync(string fileName, Stream stream)
    {
        if (!string.Equals(Path.GetExtension(fileName), ".csv", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("仅支持导入 .csv 文件。");
        using var bytes = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(buffer)) != 0)
        {
            if (bytes.Length + count > MaxFileBytes) throw TooLarge();
            bytes.Write(buffer, 0, count);
        }
        return Decode(bytes.ToArray());
    }

    private static string Decode(byte[] bytes)
    {
        // Office/WPS may save UTF-8, UTF-16 or a local ANSI encoding. Decode the
        // common variants without requiring a particular BOM or rejecting labels.
        var data = bytes.AsSpan();
        if (data.StartsWith(new byte[] { 0xFF, 0xFE, 0, 0 })) return Encoding.UTF32.GetString(data[4..]);
        if (data.StartsWith(new byte[] { 0, 0, 0xFE, 0xFF })) return new UTF32Encoding(true, false).GetString(data[4..]);
        if (data.StartsWith(new byte[] { 0xFF, 0xFE })) return Encoding.Unicode.GetString(data[2..]);
        if (data.StartsWith(new byte[] { 0xFE, 0xFF })) return Encoding.BigEndianUnicode.GetString(data[2..]);
        if (data.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF })) return Encoding.UTF8.GetString(data[3..]);
        var evenZeros = 0;
        var oddZeros = 0;
        for (var i = 0; i < Math.Min(data.Length, 512); i++)
            if (data[i] == 0) { if (i % 2 == 0) evenZeros++; else oddZeros++; }
        if (oddZeros >= 2 && oddZeros > evenZeros * 2) return Encoding.Unicode.GetString(data);
        if (evenZeros >= 2 && evenZeros > oddZeros * 2) return Encoding.BigEndianUnicode.GetString(data);
        try { return new UTF8Encoding(false, true).GetString(data); }
        catch (DecoderFallbackException)
        {
            return CodePagesEncodingProvider.Instance.GetEncoding(54936)!.GetString(data); // GB18030 / GBK
        }
    }

    public static string Normalize(string text, CsvInputKind kind, InputSpace space = InputSpace.Lab)
    {
        var rows = ReadRows(text);
        var columns = kind == CsvInputKind.Spectrum ? rows[0].Cells.Length : kind == CsvInputKind.Color && space == InputSpace.HEX ? 1 : 3;
        if (kind == CsvInputKind.Spectrum && columns is not (1 or 2))
            throw new ArgumentException("光谱 CSV 需要一列反射率，或两列波长、反射率。");
        foreach (var (cells, line) in rows)
        {
            if (cells.Length != columns) throw new ArgumentException($"第 {line} 行需要恰好 {columns} 列数据。");
            if (cells.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException($"第 {line} 行有空单元格。");
            if (kind == CsvInputKind.Color && space == InputSpace.HEX)
            {
                if (cells[0].Length != 7 || cells[0][0] != '#' || !int.TryParse(cells[0][1..], NumberStyles.HexNumber, Invariant, out _))
                    throw new ArgumentException($"第 {line} 行需要 #RRGGBB 格式。");
            }
            else
            {
                for (var column = 0; column < cells.Length; column++)
                    ToolCalculations.Number(cells[column], $"第 {line} 行第 {column + 1} 列");
            }
        }
        // The text fields retain the existing paste/calculation behavior.
        return string.Join('\n', rows.Select(row => string.Join(',', row.Cells)));
    }

    public static DifferenceInput ParseDifference(string text)
    {
        var rows = ReadRows(text, ignoreEmptySixColumnRows: true);
        var standards = new List<string>();
        var samples = new List<string>();
        var unpairedLines = new List<int>();
        foreach (var (cells, line) in rows)
        {
            if (cells.Length != 6) throw new ArgumentException($"第 {line} 行需要恰好 6 列数据。");
            var standard = cells[..3];
            var sample = cells[3..];
            var hasStandard = ReadLab(standard, "标准 Lab", line, 0);
            var hasSample = ReadLab(sample, "样品 Lab", line, 3);
            if (hasStandard) standards.Add(string.Join(',', standard));
            if (hasSample) samples.Add(string.Join(',', sample));
            if (!hasStandard || !hasSample) unpairedLines.Add(line);
        }
        if (standards.Count == 0) throw new ArgumentException("请至少填写一行完整的标准 Lab 数据。");
        if (samples.Count == 0) throw new ArgumentException("请至少填写一行完整的样品 Lab 数据。");
        if (standards.Count > 1)
        {
            if (standards.Count != samples.Count) throw new ArgumentException("逐行配对时，标准和样品的行数必须相同。");
            if (unpairedLines.Count > 0) throw new ArgumentException($"第 {unpairedLines[0]} 行缺少配对数据，多行标准与对应样品必须填写在同一行。");
        }
        return new(string.Join('\n', standards), string.Join('\n', samples), standards.Count, samples.Count);
    }

    private static bool ReadLab(string[] cells, string label, int line, int offset)
    {
        if (cells.All(string.IsNullOrWhiteSpace)) return false;
        if (cells.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException($"第 {line} 行{label}的三列必须同时填写或同时留空。");
        for (var column = 0; column < cells.Length; column++)
            ToolCalculations.Number(cells[column], $"第 {line} 行第 {column + offset + 1} 列");
        return true;
    }

    private static List<(string[] Cells, int Line)> ReadRows(string text, bool ignoreEmptySixColumnRows = false)
    {
        if (text.Length > ToolCalculations.MaxTextLength) throw TooLarge();
        var rows = ParseCsv(text.TrimStart('\uFEFF'));
        if (ignoreEmptySixColumnRows) rows.RemoveAll(row => row.Cells.Length == 6 && row.Cells.All(string.IsNullOrWhiteSpace));
        // Only the first nonblank record can be a header. Never discard an
        // invalid data row containing numbers, NaN/Infinity or a HEX candidate.
        if (rows.Count > 0 && rows[0].Cells.Any(cell => !string.IsNullOrWhiteSpace(cell)) &&
            rows[0].Cells.All(cell => !IsDataCell(cell))) rows.RemoveAt(0);
        if (rows.Count == 0) throw new ArgumentException("CSV 中没有数据行，请填写模板中的数据。");
        if (rows.Count > ToolCalculations.MaxRows) throw new ArgumentException($"一次最多处理 {ToolCalculations.MaxRows} 行。");
        return rows;
    }

    private static bool IsDataCell(string cell) => cell.StartsWith('#') ||
        double.TryParse(cell, NumberStyles.Float, Invariant, out _);

    private static List<(string[] Cells, int Line)> ParseCsv(string text)
    {
        var rows = new List<(string[] Cells, int Line)>();
        var cells = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        var closedQuote = false;
        var line = 1;
        var recordLine = 1;
        // Regional Office CSV exports may use semicolons. Chinese commas are
        // also accepted; tabs and spaces remain available for direct paste only.
        var delimiter = DetectDelimiter(text);
        void FinishField()
        {
            cells.Add(field.ToString().Trim()); field.Clear(); closedQuote = false;
        }
        void FinishRecord()
        {
            FinishField();
            if (cells.Count != 1 || cells[0].Length != 0) rows.Add((cells.ToArray(), recordLine));
            cells.Clear();
            if (rows.Count > ToolCalculations.MaxRows + 1) throw new ArgumentException($"一次最多处理 {ToolCalculations.MaxRows} 行。");
        }
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else { quoted = false; closedQuote = true; }
                }
                else
                {
                    field.Append(c);
                    if (c == '\n' || c == '\r' && (i + 1 == text.Length || text[i + 1] != '\n')) line++;
                }
            }
            else if (c == delimiter || delimiter == ',' && c == '，') FinishField();
            else if (c is '\r' or '\n' or '\u2028' or '\u2029')
            {
                FinishRecord();
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                line++; recordLine = line;
            }
            else if (c == '"' && !closedQuote && string.IsNullOrWhiteSpace(field.ToString()))
            {
                field.Clear(); quoted = true;
            }
            else if (c == '"' || closedQuote && !char.IsWhiteSpace(c))
                throw new ArgumentException($"第 {line} 行的 CSV 引号格式不正确。");
            else field.Append(c);
        }
        if (quoted) throw new ArgumentException($"第 {recordLine} 行的 CSV 引号未闭合。");
        if (field.Length > 0 || cells.Count > 0 || closedQuote) FinishRecord();
        return rows;
    }

    private static char DetectDelimiter(string text)
    {
        var quoted = false;
        foreach (var c in text)
        {
            if (c == '"') quoted = !quoted;
            if (!quoted && c is ',' or '，') return ',';
            if (!quoted && c == ';') return ';';
        }
        return ',';
    }

    private static ArgumentException TooLarge() => new("文件内容过大，请拆分为不超过 2,000,000 个字符的 CSV 文件。");

    public static CsvTemplate SpectrumTemplate(int start, int end, int step, bool fraction)
    {
        if (step <= 0 || start < 360 || end > 830 || end <= start || (end - start) % step != 0)
            throw new ArgumentException("波长需在 360–830 nm 内，结束大于起始，且范围可被正整数间隔整除。荧光光源限 380–780 nm。");
        var rows = Enumerable.Range(0, (end - start) / step + 1)
            .Select(i => new[] { (start + i * step).ToString(Invariant), fraction ? "0.18" : "18" }).ToArray();
        return Template("spectrum", ["Wavelength_nm", fraction ? "Reflectance_fraction" : "Reflectance_percent"], rows);
    }

    public static CsvTemplate DifferenceTemplate() => Template("difference",
        ["Standard_L*", "Standard_a*", "Standard_b*", "Sample_L*", "Sample_a*", "Sample_b*"],
        [["50", "20", "-30", "52", "18", "-28"], ["", "", "", "60", "10", "-20"]]);

    public static CsvTemplate ColorTemplate(InputSpace space) => space switch
    {
        InputSpace.XYZ => Template("conversion-xyz", ["X", "Y", "Z"], [["21.4643", "18.4187", "40.4654"], ["0", "0", "0"]]),
        InputSpace.Lab => Template("conversion-lab", ["L*", "a*", "b*"], [["50", "20", "-30"], ["75", "-15", "25"]]),
        InputSpace.Luv => Template("conversion-luv", ["L*", "u*", "v*"], [["50", "10", "-20"], ["0", "0", "0"]]),
        InputSpace.xyY => Template("conversion-xyy", ["x", "y", "Y"], [["0.3127", "0.3290", "50"], ["0.3", "0.3", "20"]]),
        InputSpace.sRGB => Template("conversion-srgb", ["R", "G", "B"], [["255", "0", "0"], ["32", "160", "144"]]),
        InputSpace.HEX => Template("conversion-hex", ["HEX"], [["#FF0000"], ["#20A090"]]),
        _ => throw new ArgumentOutOfRangeException(nameof(space))
    };

    private static CsvTemplate Template(string name, string[] headers, string[][] rows) =>
        new($"chromaticity-{name}-template.csv", new CalculationTable(headers, rows, "").ToCsv());
}

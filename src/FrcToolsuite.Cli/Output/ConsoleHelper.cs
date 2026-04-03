namespace FrcToolsuite.Cli.Output;

public static class ConsoleHelper
{
    public static void WriteSuccess(string message)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(message);
        Console.ForegroundColor = prev;
    }

    public static void WriteError(string message)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine(message);
        Console.ForegroundColor = prev;
    }

    public static void WriteWarning(string message)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(message);
        Console.ForegroundColor = prev;
    }

    public static void WriteInfo(string message)
    {
        Console.WriteLine(message);
    }

    public static string FormatSize(long bytes)
    {
        if (bytes < 0) return "0 B";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }

    public static void WriteTable(string[] headers, List<string[]> rows)
    {
        if (headers.Length == 0) return;

        var columnWidths = new int[headers.Length];
        for (int i = 0; i < headers.Length; i++)
        {
            columnWidths[i] = headers[i].Length;
        }

        foreach (var row in rows)
        {
            for (int i = 0; i < Math.Min(row.Length, headers.Length); i++)
            {
                columnWidths[i] = Math.Max(columnWidths[i], (row[i] ?? "").Length);
            }
        }

        // Header row
        var headerLine = FormatRow(headers, columnWidths);
        Console.WriteLine(headerLine);

        // Separator
        var separator = string.Join("  ", columnWidths.Select(w => new string('-', w)));
        Console.WriteLine(separator);

        // Data rows
        foreach (var row in rows)
        {
            Console.WriteLine(FormatRow(row, columnWidths));
        }
    }

    private static string FormatRow(string[] cells, int[] columnWidths)
    {
        var parts = new string[columnWidths.Length];
        for (int i = 0; i < columnWidths.Length; i++)
        {
            var cell = i < cells.Length ? (cells[i] ?? "") : "";
            parts[i] = cell.PadRight(columnWidths[i]);
        }
        return string.Join("  ", parts);
    }

    public static void WriteProgressBar(string label, long current, long total, int barWidth = 30)
    {
        if (total <= 0)
        {
            Console.Write($"\r{label}: {FormatSize(current)}...");
            return;
        }

        var fraction = Math.Min(1.0, (double)current / total);
        var filled = (int)(fraction * barWidth);
        var empty = barWidth - filled;
        var bar = new string('#', filled) + new string('-', empty);
        var percent = (int)(fraction * 100);

        Console.Write($"\r{label}: [{bar}] {percent}% ({FormatSize(current)}/{FormatSize(total)})");

        if (current >= total)
        {
            Console.WriteLine();
        }
    }
}

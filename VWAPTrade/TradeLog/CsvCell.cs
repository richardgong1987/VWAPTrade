using System.Globalization;

namespace cAlgo.Robots;

// How one CSV cell is written. Shared by the trade CSV and debug.csv, so the same reading looks
// the same in both files.
public static class CsvCell {
    // Readings can be negative (a slope against the trade, a narrowing gap), so only a value that
    // could not be computed (NaN, infinity, no snapshot) is left blank — never 0, which is a real reading.
    public static string Number(double? value) {
        if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
            return "";

        return value.Value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    public static string Escape(string value) {
        if (string.IsNullOrEmpty(value))
            return "";

        bool mustQuote = value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r");

        return mustQuote ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }
}

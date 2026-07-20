using System.Text;

namespace FactoryOS.Connectors.Csv;

/// <summary>
/// A minimal RFC 4180-style CSV field splitter. It honours quoted fields (so a delimiter inside quotes
/// is literal) and doubled quotes as an escaped quote. Records spanning multiple physical lines are not
/// supported; each line is one record.
/// </summary>
public static class CsvRowParser
{
    /// <summary>Splits a single CSV line into its fields.</summary>
    /// <param name="line">The line to split.</param>
    /// <param name="delimiter">The field delimiter.</param>
    /// <returns>The parsed fields, with surrounding quotes removed and escaped quotes collapsed.</returns>
    public static IReadOnlyList<string> ParseLine(string line, char delimiter)
    {
        ArgumentNullException.ThrowIfNull(line);

        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var current = line[index];

            if (inQuotes)
            {
                if (current == '"')
                {
                    if (index + 1 < line.Length && line[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(current);
                }
            }
            else if (current == '"')
            {
                inQuotes = true;
            }
            else if (current == delimiter)
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else
            {
                field.Append(current);
            }
        }

        fields.Add(field.ToString());
        return fields;
    }
}

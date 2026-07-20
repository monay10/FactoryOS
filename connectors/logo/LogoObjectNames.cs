using System.Globalization;

namespace FactoryOS.Connectors.Logo;

/// <summary>
/// Encodes Logo's firm/period table-naming convention — knowledge a generic SQL connector does not have.
/// Logo names its tables <c>LG_{firm:000}_ITEMS</c> for firm-scoped masters and
/// <c>LG_{firm:000}_{period:00}_...</c> for period-scoped tables.
/// </summary>
public static class LogoObjectNames
{
    /// <summary>Builds the item-master table name for a firm (for example <c>LG_001_ITEMS</c>).</summary>
    /// <param name="firmNumber">The Logo firm number.</param>
    /// <returns>The firm-scoped items table name.</returns>
    public static string Items(int firmNumber) =>
        $"LG_{Firm(firmNumber)}_ITEMS";

    /// <summary>Builds the stock-totals table name for a firm and period (for example <c>LG_001_01_STINVTOT</c>).</summary>
    /// <param name="firmNumber">The Logo firm number.</param>
    /// <param name="periodNumber">The Logo period number.</param>
    /// <returns>The period-scoped stock-totals table name.</returns>
    public static string StockTotals(int firmNumber, int periodNumber) =>
        $"LG_{Firm(firmNumber)}_{Period(periodNumber)}_STINVTOT";

    private static string Firm(int firmNumber)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(firmNumber);
        return firmNumber.ToString("000", CultureInfo.InvariantCulture);
    }

    private static string Period(int periodNumber)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(periodNumber);
        return periodNumber.ToString("00", CultureInfo.InvariantCulture);
    }
}

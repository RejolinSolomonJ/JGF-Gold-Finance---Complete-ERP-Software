using System.Text.RegularExpressions;

namespace JGF.Desktop.Common;

public static class ValidationHelper
{
    private static readonly Regex MobileRegex = new(@"^[6-9]\d{9}$", RegexOptions.Compiled);
    private static readonly Regex AadhaarRegex = new(@"^\d{12}$", RegexOptions.Compiled);
    private static readonly Regex PanRegex = new(@"^[A-Z]{5}[0-9]{4}[A-Z]$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PincodeRegex = new(@"^\d{6}$", RegexOptions.Compiled);

    public static bool IsValidMobile(string? mobile)
    {
        if (string.IsNullOrWhiteSpace(mobile)) return false;
        var clean = mobile.Trim().Replace(" ", "").Replace("-", "");
        return MobileRegex.IsMatch(clean);
    }

    public static bool IsValidAadhaar(string? aadhaar)
    {
        if (string.IsNullOrWhiteSpace(aadhaar)) return true; // Optional field
        var clean = aadhaar.Trim().Replace(" ", "").Replace("-", "");
        return AadhaarRegex.IsMatch(clean);
    }

    public static bool IsValidPan(string? pan)
    {
        if (string.IsNullOrWhiteSpace(pan)) return true; // Optional field
        return PanRegex.IsMatch(pan.Trim());
    }

    public static bool IsValidPincode(string? pincode)
    {
        if (string.IsNullOrWhiteSpace(pincode)) return true; // Optional field
        return PincodeRegex.IsMatch(pincode.Trim());
    }

    public static bool IsNonNegativeAmount(decimal amount) => amount >= 0m;
    public static bool IsPositiveAmount(decimal amount) => amount > 0m;
    public static bool IsValidWeight(decimal weight) => weight >= 0m;

    public static string FormatCurrency(decimal amount) => $"₹ {amount:N2}";
    public static string FormatWeight(decimal weight) => $"{weight:F3} g";
    public static string FormatDate(DateOnly date) => date.ToString("dd-MMM-yyyy");
    public static string FormatDate(DateOnly? date) => date.HasValue ? date.Value.ToString("dd-MMM-yyyy") : "-";
    public static string FormatDate(DateTime date) => date.ToString("dd-MMM-yyyy");
    public static string FormatDate(DateTime? date) => date.HasValue ? date.Value.ToString("dd-MMM-yyyy") : "-";
}


using JGF.Desktop.Common;
using Xunit;

namespace JGF.Tests;

public class ValidationHelperTests
{
    [Theory]
    [InlineData("9876543210", true)]
    [InlineData("8123456789", true)]
    [InlineData("7000000000", true)]
    [InlineData("6999999999", true)]
    [InlineData("98765 43210", true)]
    [InlineData("9876-543-210", true)]
    [InlineData("5123456789", false)] // Invalid start digit
    [InlineData("987654321", false)]   // Only 9 digits
    [InlineData("98765432100", false)] // 11 digits
    [InlineData("abcdefghij", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidMobile_ValidatesCorrectly(string? mobile, bool expected)
    {
        var result = ValidationHelper.IsValidMobile(mobile);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("123456789012", true)]
    [InlineData("1234 5678 9012", true)]
    [InlineData("1234-5678-9012", true)]
    [InlineData("", true)] // Optional
    [InlineData(null, true)] // Optional
    [InlineData("12345678901", false)] // 11 digits
    [InlineData("1234567890123", false)] // 13 digits
    [InlineData("12345678901A", false)] // Alphanumeric
    public void IsValidAadhaar_ValidatesCorrectly(string? aadhaar, bool expected)
    {
        var result = ValidationHelper.IsValidAadhaar(aadhaar);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("ABCDE1234F", true)]
    [InlineData("abcde1234f", true)]
    [InlineData("", true)] // Optional
    [InlineData(null, true)] // Optional
    [InlineData("ABCDE12345", false)] // Wrong checksum
    [InlineData("ABCD12345F", false)] // Wrong format
    [InlineData("ABCDEFGHIK", false)] // All letters
    public void IsValidPan_ValidatesCorrectly(string? pan, bool expected)
    {
        var result = ValidationHelper.IsValidPan(pan);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("600001", true)]
    [InlineData("629001", true)]
    [InlineData("", true)] // Optional
    [InlineData(null, true)] // Optional
    [InlineData("60001", false)]  // 5 digits
    [InlineData("6000001", false)] // 7 digits
    [InlineData("60000A", false)]
    public void IsValidPincode_ValidatesCorrectly(string? pincode, bool expected)
    {
        var result = ValidationHelper.IsValidPincode(pincode);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormattingHelpers_FormatWithCorrectDecimalsAndSymbols()
    {
        Assert.Equal("₹ 1,234.50", ValidationHelper.FormatCurrency(1234.50m));
        Assert.StartsWith("₹ ", ValidationHelper.FormatCurrency(100000m));
        Assert.EndsWith(".00", ValidationHelper.FormatCurrency(100000m));
        Assert.Equal("18.500 g", ValidationHelper.FormatWeight(18.5m));
        Assert.Equal("0.250 g", ValidationHelper.FormatWeight(0.25m));

        var testDate = new DateOnly(2026, 11, 9);
        Assert.Equal("09-Nov-2026", ValidationHelper.FormatDate(testDate));
        Assert.Equal("09-Nov-2026", ValidationHelper.FormatDate((DateOnly?)testDate));
        Assert.Equal("-", ValidationHelper.FormatDate((DateOnly?)null));
    }
}

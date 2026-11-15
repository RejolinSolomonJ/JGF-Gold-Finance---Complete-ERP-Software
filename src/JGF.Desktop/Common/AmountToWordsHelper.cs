using System;
using System.Text;

namespace JGF.Desktop.Common;

/// <summary>
/// Converts monetary amounts into Indian numbering system words (Lakhs, Crores, Thousands, Hundreds).
/// </summary>
public static class AmountToWordsHelper
{
    private static readonly string[] UnitsMap =
    {
        "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten",
        "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen"
    };

    private static readonly string[] TensMap =
    {
        "Zero", "Ten", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety"
    };

    public static string ToIndianRupees(decimal amount)
    {
        long wholePart = (long)Math.Floor(amount);
        int paise = (int)Math.Round((amount - wholePart) * 100);

        if (wholePart == 0 && paise == 0)
            return "Zero Rupees Only";

        var sb = new StringBuilder();

        if (wholePart > 0)
        {
            sb.Append(ConvertWholeNumberToWords(wholePart));
            sb.Append(" Rupees");
        }

        if (paise > 0)
        {
            if (wholePart > 0) sb.Append(" and ");
            sb.Append(ConvertWholeNumberToWords(paise));
            sb.Append(" Paise");
        }

        sb.Append(" Only");
        return sb.ToString().Trim();
    }

    private static string ConvertWholeNumberToWords(long number)
    {
        if (number == 0) return "Zero";

        var sb = new StringBuilder();

        if (number >= 10000000) // Crores
        {
            long crores = number / 10000000;
            sb.Append(ConvertWholeNumberToWords(crores)).Append(" Crore ");
            number %= 10000000;
        }

        if (number >= 100000) // Lakhs
        {
            long lakhs = number / 100000;
            sb.Append(ConvertWholeNumberToWords(lakhs)).Append(" Lakh ");
            number %= 100000;
        }

        if (number >= 1000) // Thousands
        {
            long thousands = number / 1000;
            sb.Append(ConvertWholeNumberToWords(thousands)).Append(" Thousand ");
            number %= 1000;
        }

        if (number >= 100) // Hundreds
        {
            long hundreds = number / 100;
            sb.Append(UnitsMap[hundreds]).Append(" Hundred ");
            number %= 100;
        }

        if (number > 0)
        {
            if (number < 20)
            {
                sb.Append(UnitsMap[number]).Append(" ");
            }
            else
            {
                sb.Append(TensMap[number / 10]).Append(" ");
                if (number % 10 > 0)
                {
                    sb.Append(UnitsMap[number % 10]).Append(" ");
                }
            }
        }

        return sb.ToString().Trim();
    }
}


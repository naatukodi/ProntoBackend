using System.Text.RegularExpressions;
using Valuation.Api.Models;

namespace Valuation.Api.Services
{
    public static class VehicleValuationParser
    {
        // Handles: "₹35 K", "₹7.5 L", "₹8L", "₹35,000"
        public static decimal ParseINR(string s)
        {
            s = s.Trim();
            bool isKilo = s.EndsWith("K", StringComparison.OrdinalIgnoreCase);
            bool isLakh = s.EndsWith("L", StringComparison.OrdinalIgnoreCase);
            if (isKilo || isLakh)
                s = s[..^1].Trim();
            s = Regex.Replace(s, @"[^\d\.]", "");
            if (!decimal.TryParse(s, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var d))
                return 0m;
            if (isKilo) return d * 1000m;
            if (isLakh) return d * 100000m;
            return d;
        }

        // Matches labels like "LOW PRICE RANGE", "Low Range", "Low:", "low -"
        // then captures the first ₹ value on that segment (e.g. "₹35 K", "₹7.5 L")
        public static VehicleValuation ParseRanges(string text)
        {
            var lowMatch  = Regex.Match(text, @"(?i)low[^\n₹]*₹\s*([\d,\.]+\s*[KkLl]?)");
            var midMatch  = Regex.Match(text, @"(?i)mid[^\n₹]*₹\s*([\d,\.]+\s*[KkLl]?)");
            var highMatch = Regex.Match(text, @"(?i)high[^\n₹]*₹\s*([\d,\.]+\s*[KkLl]?)");

            return new VehicleValuation
            {
                LowRange    = lowMatch.Success  ? ParseINR(lowMatch.Groups[1].Value)  : 0m,
                MidRange    = midMatch.Success  ? ParseINR(midMatch.Groups[1].Value)  : 0m,
                HighRange   = highMatch.Success ? ParseINR(highMatch.Groups[1].Value) : 0m,
                RawResponse = text
            };
        }
    }
}

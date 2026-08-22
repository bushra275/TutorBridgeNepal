namespace TutorBridgeNepal.Helpers;

public static class DeviceHelper
{
    // Cheap, dependency-free User-Agent parsing - good enough to show
    // "Chrome on Windows" without pulling in a UA-parsing package.
    public static string DescribeUserAgent(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return "Unknown device";

        var ua = userAgent;

        string os =
            ua.Contains("Windows") ? "Windows" :
            ua.Contains("Mac OS X") || ua.Contains("Macintosh") ? "macOS" :
            ua.Contains("Android") ? "Android" :
            ua.Contains("iPad") ? "iPad" :
            ua.Contains("iPhone") ? "iPhone" :
            ua.Contains("Linux") ? "Linux" :
            "an unknown OS";

        string browser =
            ua.Contains("Edg/") ? "Edge" :
            ua.Contains("OPR/") || ua.Contains("Opera") ? "Opera" :
            ua.Contains("CriOS") ? "Chrome" :
            ua.Contains("Chrome/") ? "Chrome" :
            ua.Contains("Firefox/") ? "Firefox" :
            ua.Contains("Safari/") ? "Safari" :
            "Unknown browser";

        return $"{browser} on {os}";
    }
}
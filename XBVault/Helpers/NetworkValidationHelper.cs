using System;
using System.Net;

namespace XBVault.Helpers;

public static class NetworkValidationHelper
{
    public static string ValidateAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return "Address is required";

        var trimmed = address.Trim();

        if (IPAddress.TryParse(trimmed, out _))
            return string.Empty;

        if (trimmed.Contains(':'))
        {
            var parts = trimmed.Split(':', 2);
            if (parts.Length == 2 && int.TryParse(parts[1], out _))
                return "Remove the port from the address field — use the Port field below";
            return "Invalid address format";
        }

        if (Uri.CheckHostName(trimmed) != UriHostNameType.Unknown)
            return string.Empty;

        return "Enter a valid IP address (e.g. 192.168.1.100) or hostname";
    }

    public static string ValidatePort(string? port)
    {
        if (string.IsNullOrWhiteSpace(port))
            return "Port is required";

        if (!int.TryParse(port, out var portVal) || portVal < 1 || portVal > 65535)
            return "Must be 1-65535";

        return string.Empty;
    }
}

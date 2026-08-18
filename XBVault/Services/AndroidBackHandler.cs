using System;

namespace XBVault.Services;

public static class AndroidBackHandler
{
    /// <summary>
    /// Set by MobileMainWindow to handle Android back button.
    /// Returns true if the overlay consumed the back press (don't close Activity).
    /// </summary>
    public static Func<bool>? OnBack { get; set; }
}

using XBVault.Helpers;

namespace XBVault.Tests;

public class NetworkValidationHelperTests
{
    // ---- ValidateAddress ----

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateAddress_NullOrEmpty_ReturnsRequired(string? input)
    {
        Assert.Equal("Address is required", NetworkValidationHelper.ValidateAddress(input));
    }

    [Theory]
    [InlineData("192.168.1.100")]
    [InlineData("10.0.0.1")]
    [InlineData("::1")]
    [InlineData("2001:db8::1")]
    public void ValidateAddress_ValidIPv4OrIPv6_ReturnsEmpty(string address)
    {
        Assert.Equal(string.Empty, NetworkValidationHelper.ValidateAddress(address));
    }

    [Theory]
    [InlineData("xbox")]
    [InlineData("xbox.local")]
    [InlineData("my-xbox.dev")]
    [InlineData("xbox.example.com")]
    public void ValidateAddress_ValidHostname_ReturnsEmpty(string hostname)
    {
        Assert.Equal(string.Empty, NetworkValidationHelper.ValidateAddress(hostname));
    }

    [Theory]
    [InlineData("192.168.1.100:11443", "Remove the port from the address field")]
    [InlineData("10.0.0.1:443", "Remove the port from the address field")]
    [InlineData("xbox.local:8080", "Remove the port from the address field")]
    public void ValidateAddress_WithPort_ReturnsPortError(string input, string expectedContains)
    {
        var result = NetworkValidationHelper.ValidateAddress(input);
        Assert.Contains(expectedContains, result);
    }

    [Fact]
    public void ValidateAddress_ColonNonNumeric_InvalidFormat()
    {
        var result = NetworkValidationHelper.ValidateAddress("abc:notaport");
        Assert.Equal("Invalid address format", result);
    }

    [Theory]
    [InlineData("not_a_valid_address!")]
    [InlineData("invalid host name")]
    public void ValidateAddress_InvalidAddress_ReturnsError(string input)
    {
        var result = NetworkValidationHelper.ValidateAddress(input);
        Assert.Contains("valid IP address", result);
    }

    // ---- ValidatePort ----

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidatePort_NullOrEmpty_ReturnsRequired(string? input)
    {
        Assert.Equal("Port is required", NetworkValidationHelper.ValidatePort(input));
    }

    [Theory]
    [InlineData("11443")]
    [InlineData("443")]
    [InlineData("1")]
    [InlineData("65535")]
    [InlineData("80")]
    public void ValidatePort_ValidPort_ReturnsEmpty(string port)
    {
        Assert.Equal(string.Empty, NetworkValidationHelper.ValidatePort(port));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("99999")]
    public void ValidatePort_OutOfRange_ReturnsError(string port)
    {
        Assert.Equal("Must be 1-65535", NetworkValidationHelper.ValidatePort(port));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("11.443")]
    [InlineData("11443:extra")]
    public void ValidatePort_NonNumeric_ReturnsError(string port)
    {
        Assert.Equal("Must be 1-65535", NetworkValidationHelper.ValidatePort(port));
    }
}

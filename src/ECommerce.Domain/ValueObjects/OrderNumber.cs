using System.Security.Cryptography;

namespace ECommerce.Domain.ValueObjects;

/// <summary>
/// Generates human readable, sortable order references such as
/// <c>ORD-20261004-8F2A1C</c>.
/// </summary>
public static class OrderNumberGenerator
{
    private const int SuffixLength = 6;

    public static string Generate(DateTime utcNow) =>
        $"ORD-{utcNow:yyyyMMdd}-{CreateSuffix()}";

    /// <summary>Random, non-sequential component that prevents order numbers being guessable.</summary>
    private static string CreateSuffix() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(SuffixLength))[..SuffixLength];
}
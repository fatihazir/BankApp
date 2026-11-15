namespace WebApi.IntegrationTests.Infrastructure;

public static class TestData
{
    /// <summary>Builds a checksum-valid 11-digit national ID from a seed (0..99999999).</summary>
    public static string NationalId(int seed)
    {
        int[] d = (100_000_000 + seed).ToString().Select(c => c - '0').ToArray();
        int odd = d[0] + d[2] + d[4] + d[6] + d[8];
        int even = d[1] + d[3] + d[5] + d[7];
        int tenth = ((odd * 7 - even) % 10 + 10) % 10;
        int eleventh = (d.Sum() + tenth) % 10;
        return string.Concat(d) + tenth + eleventh;
    }
}

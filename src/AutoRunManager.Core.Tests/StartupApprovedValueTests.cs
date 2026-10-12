using AutoRunManager.Core.Parsing;

namespace AutoRunManager.Core.Tests;

public class StartupApprovedValueTests
{
    [Theory]
    [InlineData(new byte[] { 0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, true)]
    [InlineData(new byte[] { 0x06, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, true)]
    [InlineData(new byte[] { 0x03, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 1 }, false)]
    [InlineData(new byte[] { 0x07, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, false)]
    [InlineData(new byte[0], true)]
    public void IsEnabled_ReadsLowBitOfFirstByte(byte[] data, bool expected)
    {
        Assert.Equal(expected, StartupApprovedValue.IsEnabled(data));
    }

    [Fact]
    public void IsEnabled_MissingValueMeansEnabled()
    {
        Assert.True(StartupApprovedValue.IsEnabled(null));
    }

    [Fact]
    public void Create_Enabled_IsTwoFollowedByZeros()
    {
        byte[] data = StartupApprovedValue.Create(true, new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new byte[] { 0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, data);
    }

    [Fact]
    public void Create_Disabled_RecordsTimeOfDisabling()
    {
        DateTime now = new(2026, 10, 12, 8, 30, 0, DateTimeKind.Utc);

        byte[] data = StartupApprovedValue.Create(false, now);

        Assert.Equal(StartupApprovedValue.Length, data.Length);
        Assert.Equal(0x03, data[0]);
        Assert.False(StartupApprovedValue.IsEnabled(data));
        Assert.Equal(now, StartupApprovedValue.DisabledAtUtc(data));
    }

    [Fact]
    public void DisabledAtUtc_IsNullForEnabledOrZeroTime()
    {
        Assert.Null(StartupApprovedValue.DisabledAtUtc(StartupApprovedValue.Create(true, DateTime.UtcNow)));
        Assert.Null(StartupApprovedValue.DisabledAtUtc([0x03, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]));
        Assert.Null(StartupApprovedValue.DisabledAtUtc([0x03]));
    }
}

using AutoRunManager.Core.Parsing;

namespace AutoRunManager.Core.Tests;

public class ServiceRulesTests
{
    private const string Windows = @"C:\Windows";

    [Theory]
    [InlineData("NoMachine", @"""C:\Program Files\NoMachine\bin\nxservice64.exe""", 0x10, true)]
    [InlineData("Shared", @"C:\Program Files\Vendor\svc.exe -k", 0x20, true)]
    [InlineData("PerUser", @"C:\Program Files\Vendor\svc.exe", 0x50, true)]
    [InlineData("Spooler", @"C:\WINDOWS\System32\spoolsv.exe", 0x10, false)]
    [InlineData("Dhcp", @"%SystemRoot%\system32\svchost.exe -k LocalServiceNetworkRestricted", 0x20, false)]
    [InlineData("Kernel", @"\SystemRoot\System32\drivers\x.sys", 0x10, false)]
    [InlineData("Relative", @"system32\x.exe", 0x10, false)]
    [InlineData("veracrypt", @"System32\drivers\veracrypt.sys", 0x01, false)]
    [InlineData("WinDefend", @"""C:\ProgramData\Microsoft\Windows Defender\platform\MsMpEng.exe""", 0x10, false)]
    [InlineData("NoImage", null, 0x10, false)]
    [InlineData("WindowsApps", @"C:\WindowsApps\x.exe", 0x10, true)]
    public void IsListed_KeepsThirdPartyWin32Services(string name, string? imagePath, int type, bool expected)
    {
        Assert.Equal(expected, ServiceRules.IsListed(name, imagePath, type, Windows));
    }

    [Fact]
    public void OffMode_IsManual()
    {
        Assert.Equal(new ServiceStartMode(ServiceStartType.Manual), ServiceRules.OffMode);
        Assert.False(ServiceRules.OffMode.IsAutoStart);
    }

    [Fact]
    public void OnMode_RestoresSavedDelayedAutomatic()
    {
        ServiceStartMode saved = new(ServiceStartType.Automatic, DelayedStart: true);

        Assert.Equal(saved, ServiceRules.OnMode(saved));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(ServiceStartType.Manual)]
    [InlineData(ServiceStartType.Disabled)]
    [InlineData(ServiceStartType.Boot)]
    public void OnMode_FallsBackToAutomatic(ServiceStartType? saved)
    {
        ServiceStartMode? mode = saved is { } type ? new ServiceStartMode(type) : null;

        Assert.Equal(ServiceStartMode.Automatic, ServiceRules.OnMode(mode));
    }

    [Theory]
    [InlineData("NoMachine Device Server (nxservice)", "x", "NoMachine Device Server")]
    [InlineData("Plain", "x", "Plain")]
    [InlineData(null, "svcname", "svcname")]
    [InlineData("@%SystemRoot%\\x.dll,-100", "svcname", "svcname")]
    public void CleanDisplayName_DropsTrailingNote(string? display, string name, string expected)
    {
        Assert.Equal(expected, ServiceRules.CleanDisplayName(display, name));
    }

    [Theory]
    [InlineData(ServiceStartType.Automatic, false, "Automatic", true)]
    [InlineData(ServiceStartType.Automatic, true, "Automatic (delayed start)", true)]
    [InlineData(ServiceStartType.Manual, false, "Manual", false)]
    [InlineData(ServiceStartType.Disabled, false, "Disabled", false)]
    public void StartMode_DisplayAndAutoStart(ServiceStartType type, bool delayed, string display, bool autoStart)
    {
        ServiceStartMode mode = new(type, delayed);

        Assert.Equal(display, mode.Display);
        Assert.Equal(autoStart, mode.IsAutoStart);
    }
}

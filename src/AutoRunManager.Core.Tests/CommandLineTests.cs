using AutoRunManager.Core.Parsing;

namespace AutoRunManager.Core.Tests;

public class CommandLineTests
{
    private const string Windows = @"C:\Windows";

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(@"C:\Tools\app.exe", @"C:\Tools\app.exe")]
    [InlineData(@"""C:\Program Files\Intel\Graphics.exe"" -s", @"C:\Program Files\Intel\Graphics.exe")]
    [InlineData(@"C:\Program Files (x86)\Skillbrains\lightshot\Lightshot.exe", @"C:\Program Files (x86)\Skillbrains\lightshot\Lightshot.exe")]
    [InlineData(@"C:\Program Files\App\app.exe --minimized", @"C:\Program Files\App\app.exe")]
    [InlineData(@"rundll32.exe C:\x\y.dll,Start", "rundll32.exe")]
    [InlineData(@"C:\x\y.dll,Start", @"C:\x\y.dll")]
    [InlineData(@"\??\C:\Program Files\NoMachine\nxfs.sys", @"C:\Program Files\NoMachine\nxfs.sys")]
    [InlineData(@"\SystemRoot\System32\drivers\nxusbf.sys", @"C:\Windows\System32\drivers\nxusbf.sys")]
    [InlineData(@"System32\drivers\veracrypt.sys", @"C:\Windows\System32\drivers\veracrypt.sys")]
    [InlineData(@"""C:\Unclosed\app.exe", @"C:\Unclosed\app.exe")]
    [InlineData(@"""""C:\Doubled\app.exe"""" -background", @"C:\Doubled\app.exe")]
    [InlineData(@"C:\NoExtension\tool", @"C:\NoExtension\tool")]
    [InlineData(@"C:\Startup\Script.lnk", @"C:\Startup\Script.lnk")]
    public void GetExecutablePath_ExtractsProgram(string? command, string? expected)
    {
        Assert.Equal(expected, CommandLine.GetExecutablePath(command, Windows));
    }

    [Fact]
    public void GetExecutablePath_ExpandsEnvironmentVariables()
    {
        string expected = Environment.GetEnvironmentVariable("LOCALAPPDATA") + @"\App\app.exe";

        Assert.Equal(expected, CommandLine.GetExecutablePath(@"%LOCALAPPDATA%\App\app.exe /background", Windows));
    }

    [Fact]
    public void GetExecutablePath_AcceptsWindowsDirectoryWithTrailingSlash()
    {
        Assert.Equal(@"C:\Windows\System32\a.sys", CommandLine.GetExecutablePath(@"\SystemRoot\System32\a.sys", @"C:\Windows\"));
    }
}

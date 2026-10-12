using AutoRunManager.Core.Parsing;

namespace AutoRunManager.Core.Tests;

public class PackagedStartupTasksTests
{
    private const string Manifest = """
        <?xml version="1.0" encoding="utf-8"?>
        <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
                 xmlns:uap5="http://schemas.microsoft.com/appx/manifest/uap/windows10/5"
                 xmlns:desktop="http://schemas.microsoft.com/appx/manifest/desktop/windows10">
          <Properties>
            <DisplayName>ms-resource:AppName</DisplayName>
          </Properties>
          <Applications>
            <Application Id="App" Executable="app\Main.exe" EntryPoint="Windows.FullTrustApplication">
              <Extensions>
                <uap5:Extension Category="windows.startupTask">
                  <uap5:StartupTask TaskId="MainStartup" Enabled="false" DisplayName="Main app" />
                </uap5:Extension>
                <desktop:Extension Category="windows.startupTask" Executable="helper\Helper.exe" EntryPoint="Windows.FullTrustApplication">
                  <desktop:StartupTask TaskId="HelperStartup" Enabled="true" DisplayName="ms-resource:Helper" />
                </desktop:Extension>
                <uap5:Extension Category="windows.appExecutionAlias">
                  <uap5:AppExecutionAlias />
                </uap5:Extension>
              </Extensions>
            </Application>
          </Applications>
        </Package>
        """;

    [Fact]
    public void ParseManifest_FindsStartupTasksInAnyNamespace()
    {
        (string? packageName, IReadOnlyList<ManifestStartupTask> tasks) = PackagedStartupTasks.ParseManifest(Manifest);

        Assert.Equal("ms-resource:AppName", packageName);
        Assert.Equal(
            [
                new ManifestStartupTask("MainStartup", @"app\Main.exe", "Main app", false),
                new ManifestStartupTask("HelperStartup", @"helper\Helper.exe", "ms-resource:Helper", true),
            ],
            tasks);
    }

    [Fact]
    public void ParseManifest_WithoutStartupTasks_ReturnsEmpty()
    {
        const string manifest = """<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"><Applications /></Package>""";

        Assert.Empty(PackagedStartupTasks.ParseManifest(manifest).Tasks);
    }

    [Theory]
    [InlineData(null, true, true, true)]
    [InlineData(null, false, false, true)]
    [InlineData(0, true, false, true)]
    [InlineData(1, true, false, true)]
    [InlineData(2, false, true, true)]
    [InlineData(3, true, false, false)]
    [InlineData(4, false, true, false)]
    public void Evaluate_MapsStateAndManifestDefault(int? state, bool byDefault, bool enabled, bool canToggle)
    {
        Assert.Equal((enabled, canToggle), PackagedStartupTasks.Evaluate(state, byDefault));
    }

    [Fact]
    public void StateFor_WritesWhatSettingsWrites()
    {
        Assert.Equal(PackagedTaskState.Enabled, PackagedStartupTasks.StateFor(true));
        Assert.Equal(PackagedTaskState.DisabledByUser, PackagedStartupTasks.StateFor(false));
    }

    [Theory]
    [InlineData("5319275A.WhatsAppDesktop_2.2540.5.0_x64__cv1g1gvanyjgm", "5319275A.WhatsAppDesktop_cv1g1gvanyjgm")]
    [InlineData("Not_A_Full_Name", null)]
    public void FamilyNameFromFullName_DropsVersionArchAndResource(string fullName, string? expected)
    {
        Assert.Equal(expected, PackagedStartupTasks.FamilyNameFromFullName(fullName));
    }
}

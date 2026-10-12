using AutoRunManager.Core.Parsing;

namespace AutoRunManager.Core.Tests;

public class TaskNamesTests
{
    [Theory]
    [InlineData("GoogleUpdateTaskUserS-1-5-21-1-2-3-1001Core", "GoogleUpdateTaskUserS-1-5-21-1-2-3-1001Core")]
    [InlineData("OneDrive Reporting Task-S-1-5-21-1111-2222-3333-1001", "OneDrive Reporting Task")]
    [InlineData("MicrosoftEdgeUpdateTaskMachineCore{7B1D7E4B-5C2A-4F1E-9C0D-2E4F6A8B0C1D}", "MicrosoftEdgeUpdateTaskMachineCore")]
    [InlineData("Updater", "Updater")]
    [InlineData("{7B1D7E4B-5C2A-4F1E-9C0D-2E4F6A8B0C1D}", "{7B1D7E4B-5C2A-4F1E-9C0D-2E4F6A8B0C1D}")]
    public void Clean_DropsGuidsAndSidSuffix(string name, string expected)
    {
        Assert.Equal(expected, TaskNames.Clean(name));
    }

    [Theory]
    [InlineData(@"\Updater", @"\", "Updater")]
    [InlineData(@"\Vendor\Sub\Updater", @"\Vendor\Sub", "Updater")]
    [InlineData("Updater", @"\", "Updater")]
    public void Split_SeparatesFolderAndName(string path, string folder, string name)
    {
        Assert.Equal((folder, name), TaskNames.Split(path));
    }

    [Theory]
    [InlineData(@"\Microsoft\Windows\Defrag\ScheduledDefrag", true)]
    [InlineData(@"\microsoft\Office\Telemetry", true)]
    [InlineData(@"\MicrosoftEdgeUpdateTaskMachineCore", false)]
    [InlineData(@"\Vendor\Updater", false)]
    public void IsWindowsTask_MatchesMicrosoftFolderOnly(string path, bool expected)
    {
        Assert.Equal(expected, TaskNames.IsWindowsTask(path));
    }
}

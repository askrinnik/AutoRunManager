using System.ComponentModel;
using System.Runtime.InteropServices;

using AutoRunManager.Core.Parsing;

namespace AutoRunManager.Core.Platform;

public interface IServiceControl
{
    /// <summary>Changes the start type through the Service Control Manager (needs admin rights).</summary>
    void SetStartMode(string serviceName, ServiceStartMode mode);
}

public sealed partial class ServiceControl : IServiceControl
{
    private const uint ScManagerConnect = 0x0001;
    private const uint ServiceChangeConfig = 0x0002;
    private const uint ServiceNoChange = 0xFFFFFFFF;
    private const uint ServiceConfigDelayedAutoStartInfo = 3;

    public void SetStartMode(string serviceName, ServiceStartMode mode)
    {
        IntPtr manager = OpenSCManagerW(null, null, ScManagerConnect);
        if (manager == IntPtr.Zero)
        {
            throw new Win32Exception();
        }

        try
        {
            IntPtr service = OpenServiceW(manager, serviceName, ServiceChangeConfig);
            if (service == IntPtr.Zero)
            {
                throw new Win32Exception();
            }

            try
            {
                if (!ChangeServiceConfigW(service, ServiceNoChange, (uint)mode.Type, ServiceNoChange, null, null, IntPtr.Zero, null, null, null, null))
                {
                    throw new Win32Exception();
                }

                if (mode.Type == ServiceStartType.Automatic)
                {
                    int delayed = mode.DelayedStart ? 1 : 0;
                    if (!ChangeServiceConfig2W(service, ServiceConfigDelayedAutoStartInfo, ref delayed))
                    {
                        throw new Win32Exception();
                    }
                }
            }
            finally
            {
                CloseServiceHandle(service);
            }
        }
        finally
        {
            CloseServiceHandle(manager);
        }
    }

    [LibraryImport("advapi32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr OpenSCManagerW(string? machineName, string? databaseName, uint desiredAccess);

    [LibraryImport("advapi32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr OpenServiceW(IntPtr manager, string serviceName, uint desiredAccess);

    [LibraryImport("advapi32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ChangeServiceConfigW(
        IntPtr service, uint serviceType, uint startType, uint errorControl, string? binaryPathName, string? loadOrderGroup,
        IntPtr tagId, string? dependencies, string? serviceStartName, string? password, string? displayName);

    // SERVICE_DELAYED_AUTO_START_INFO is a single BOOL.
    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ChangeServiceConfig2W(IntPtr service, uint infoLevel, ref int info);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseServiceHandle(IntPtr handle);
}

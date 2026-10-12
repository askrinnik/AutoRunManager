using System.ComponentModel;
using System.Security;
using System.Security.Principal;

namespace AutoRunManager.Core.Platform;

public static class Elevation
{
    public static bool IsElevated()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>Whether an exception from a write means "access denied".</summary>
    public static bool IsAccessDenied(Exception e) => e switch
    {
        UnauthorizedAccessException or SecurityException => true,
        Win32Exception { NativeErrorCode: 5 } => true,
        System.Runtime.InteropServices.COMException { HResult: unchecked((int)0x80070005) } => true,
        System.Management.ManagementException { ErrorCode: System.Management.ManagementStatus.AccessDenied } => true,
        _ => e.InnerException is { } inner && IsAccessDenied(inner),
    };
}

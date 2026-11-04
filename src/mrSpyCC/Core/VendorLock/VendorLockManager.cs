using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using mrSpyCC.Core.Device;

namespace mrSpyCC.Core.VendorLock;

public enum LockStatus
{
    NotConnected,
    SharedMode,
    ExclusiveAcquired,
    VendorServiceConflict,
    RequiresElevation
}

public sealed class VendorLockManager
{
    private const string DEFAULT_VENDOR_SERVICE = "GCUBridge";

    public static bool IsElevated => NativeService.IsAdministrator();

    public static string? DetectVendorService()
    {
        // Check default service "GCUBridge" directly
        var hScm = NativeService.OpenSCManager(null, null, NativeService.SC_MANAGER_CONNECT);
        if (hScm == IntPtr.Zero) return null;

        try
        {
            var hSvc = NativeService.OpenService(hScm, DEFAULT_VENDOR_SERVICE, NativeService.SERVICE_QUERY_STATUS);
            if (hSvc != IntPtr.Zero)
            {
                NativeService.CloseServiceHandle(hSvc);
                return DEFAULT_VENDOR_SERVICE;
            }
        }
        finally
        {
            NativeService.CloseServiceHandle(hScm);
        }

        return null;
    }

    public static bool IsVendorServiceRunning(string serviceName)
    {
        var hScm = NativeService.OpenSCManager(null, null, NativeService.SC_MANAGER_CONNECT);
        if (hScm == IntPtr.Zero) return false;

        try
        {
            var hSvc = NativeService.OpenService(hScm, serviceName, NativeService.SERVICE_QUERY_STATUS);
            if (hSvc == IntPtr.Zero) return false;

            try
            {
                if (NativeService.QueryServiceStatus(hSvc, out var status))
                {
                    return status.dwCurrentState == NativeService.SERVICE_RUNNING;
                }
            }
            finally
            {
                NativeService.CloseServiceHandle(hSvc);
            }
        }
        finally
        {
            NativeService.CloseServiceHandle(hScm);
        }

        return false;
    }

    /// <summary>
    /// Executes the safe exclusive lock protocol:
    /// If GCUBridge is running, temporarily stops it, takes exclusive handle (dwShareMode = 0), and restarts GCUBridge.
    /// </summary>
    public static LockStatus AcquireLock(IKeyboardDevice device, bool allowSharedFallback = false)
    {
        // 1. Try direct exclusive connect
        if (device.Connect(requireExclusive: true))
        {
            return LockStatus.ExclusiveAcquired;
        }

        // 2. If failed, check if vendor service is the culprit
        string? vendorSvc = DetectVendorService();
        bool isVendorRunning = vendorSvc != null && IsVendorServiceRunning(vendorSvc);

        if (!isVendorRunning)
        {
            // Device cannot be opened, but not because of GCUBridge
            if (allowSharedFallback && device.Connect(requireExclusive: false))
                return LockStatus.SharedMode;

            return LockStatus.NotConnected;
        }

        // 3. Vendor service is holding the device. Can we stop it?
        if (!IsElevated)
        {
            return LockStatus.RequiresElevation;
        }

        // 4. We are elevated: Stop vendor service, snatch exclusive lock, restart vendor service
        bool stopped = StopService(vendorSvc!);
        bool connected = false;

        try
        {
            if (stopped)
            {
                // Progressive retry to allow handle cleanup
                for (int attempt = 1; attempt <= 4; attempt++)
                {
                    Thread.Sleep(attempt * 250);
                    if (device.Connect(requireExclusive: true))
                    {
                        connected = true;
                        break;
                    }
                }
            }
        }
        finally
        {
            // Always restore vendor service so laptop cooling/fan tables continue running!
            StartService(vendorSvc!);
        }

        if (connected && device.IsExclusiveLockHeld)
        {
            return LockStatus.ExclusiveAcquired;
        }

        if (allowSharedFallback && device.Connect(requireExclusive: false))
        {
            return LockStatus.SharedMode;
        }

        return LockStatus.VendorServiceConflict;
    }

    public static bool StopService(string serviceName, int timeoutSeconds = 10)
    {
        var hScm = NativeService.OpenSCManager(null, null, NativeService.SC_MANAGER_CONNECT);
        if (hScm == IntPtr.Zero) return false;

        try
        {
            var hSvc = NativeService.OpenService(hScm, serviceName, NativeService.SERVICE_STOP | NativeService.SERVICE_QUERY_STATUS);
            if (hSvc == IntPtr.Zero) return false;

            try
            {
                if (NativeService.QueryServiceStatus(hSvc, out var status))
                {
                    if (status.dwCurrentState == NativeService.SERVICE_STOPPED)
                        return true;

                    NativeService.ControlService(hSvc, NativeService.SERVICE_CONTROL_STOP, out status);
                    var sw = Stopwatch.StartNew();
                    while (sw.Elapsed.TotalSeconds < timeoutSeconds)
                    {
                        Thread.Sleep(100);
                        if (NativeService.QueryServiceStatus(hSvc, out status) &&
                            status.dwCurrentState == NativeService.SERVICE_STOPPED)
                        {
                            return true;
                        }
                    }
                }
            }
            finally
            {
                NativeService.CloseServiceHandle(hSvc);
            }
        }
        finally
        {
            NativeService.CloseServiceHandle(hScm);
        }

        return false;
    }

    public static bool StartService(string serviceName)
    {
        var hScm = NativeService.OpenSCManager(null, null, NativeService.SC_MANAGER_CONNECT);
        if (hScm == IntPtr.Zero) return false;

        try
        {
            var hSvc = NativeService.OpenService(hScm, serviceName, NativeService.SERVICE_START);
            if (hSvc == IntPtr.Zero) return false;

            try
            {
                return NativeService.StartService(hSvc, 0, IntPtr.Zero) ||
                       Marshal.GetLastWin32Error() == 1056; // ERROR_SERVICE_ALREADY_RUNNING
            }
            finally
            {
                NativeService.CloseServiceHandle(hSvc);
            }
        }
        finally
        {
            NativeService.CloseServiceHandle(hScm);
        }
    }

    /// <summary>
    /// Relaunches the current executable with administrator privileges (UAC prompt).
    /// </summary>
    public static bool RelaunchElevated(string? arguments = null)
    {
        string? exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe)) return false;

        var startInfo = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = arguments ?? "",
            Verb = "runas",
            UseShellExecute = true
        };

        try
        {
            Process.Start(startInfo);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

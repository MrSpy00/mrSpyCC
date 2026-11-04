using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

namespace mrSpyCC.Core.VendorLock;

/// <summary>
/// Hibrit Otomatik Başlatma Motoru (aegisSoft):
/// 1. HKCU\Software\Microsoft\Windows\CurrentVersion\Run + StartupApproved\Run
///    -> Görev Yöneticisi (Başlangıç Uygulamaları) ve Windows Ayarlar > Başlangıç görünürlüğü sağlar.
/// 2. Windows Görev Zamanlayıcı (\mrSpyCC_AutoStart, HighestAvailable)
///    -> UAC onayı sormadan Yönetici yetkisiyle başlatarak GCUBridge donanım kilidini izole eder.
/// </summary>
public static class TaskSchedulerHelper
{
    public const string TASK_NAME = "mrSpyCC_AutoStart";
    public const string RUN_VALUE_NAME = "mrSpyCC";

    private const string RUN_REGISTRY_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string STARTUP_APPROVED_KEY = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    /// <summary>
    /// Mevcut sürecin Yönetici (Administrator / High Integrity) yetkisiyle çalışıp çalışmadığını döner.
    /// </summary>
    public static bool IsRunningAsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Kullanıcının Görev Yöneticisi > Başlangıç Uygulamaları üzerinden mrSpyCC'yi devre dışı bırakıp bırakmadığını kontrol eder.
    /// </summary>
    public static bool IsStartupApprovedByWindows()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(STARTUP_APPROVED_KEY, writable: false);
            if (key?.GetValue(RUN_VALUE_NAME) is byte[] data && data.Length >= 1)
            {
                // Windows StartupApproved protokolü:
                // İlk bayt çift ise (0x02, 0x06) -> Etkin (Enabled)
                // İlk bayt tek ise (0x01, 0x03) -> Görev Yöneticisinden Devre Dışı Bırakılmış (Disabled)
                return (data[0] & 1) == 0;
            }
            return true;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    /// HKCU\...\Run kaydının mevcut olup olmadığını ve geçerli exe yolunu gösterip göstermediğini kontrol eder.
    /// </summary>
    public static bool IsRegistryRunInstalled(string? expectedExePath = null)
    {
        try
        {
            expectedExePath ??= Environment.ProcessPath;
            using var key = Registry.CurrentUser.OpenSubKey(RUN_REGISTRY_KEY, writable: false);
            if (key?.GetValue(RUN_VALUE_NAME) is not string val || string.IsNullOrWhiteSpace(val))
                return false;

            if (!string.IsNullOrEmpty(expectedExePath))
            {
                return val.Contains(expectedExePath, StringComparison.OrdinalIgnoreCase);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Görev Zamanlayıcıda (\mrSpyCC_AutoStart) görevin kurulu olup olmadığını ve geçerli exe yolunu içerip içermediğini kontrol eder.
    /// </summary>
    public static bool IsScheduledTaskInstalled(string? expectedExePath = null)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/query /tn \"{TASK_NAME}\" /xml",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var p = Process.Start(psi);
            if (p == null) return false;

            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(3000);
            if (p.ExitCode != 0) return false;

            if (!string.IsNullOrEmpty(expectedExePath))
            {
                return output.Contains(expectedExePath, StringComparison.OrdinalIgnoreCase);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Hem Başlangıç Uygulamaları (Registry Run + StartupApproved) hem de Görev Zamanlayıcı durumunu kontrol eder.
    /// </summary>
    public static bool IsTaskInstalled()
    {
        if (!IsStartupApprovedByWindows())
            return false;

        return IsRegistryRunInstalled() || IsScheduledTaskInstalled();
    }

    /// <summary>
    /// Uygulama açılışında eğer oto-başlatma aktifse, taşınabilir .exe yolu değişmişse veya
    /// Registry/TaskScheduler kayıtlarından biri eksikse otomatik olarak onarır (Self-Healing).
    /// </summary>
    public static void SyncAndRepairAutoStart(ref bool configAutoStartEnabled)
    {
        try
        {
            string? exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath)) return;

            // Eğer kullanıcı Görev Yöneticisi > Başlangıç Uygulamaları'ndan devre dışı bıraktıysa,
            // kullanıcının bu tercihine saygı duy ve config durumunu senkronize et.
            if (!IsStartupApprovedByWindows())
            {
                configAutoStartEnabled = false;
                return;
            }

            if (configAutoStartEnabled)
            {
                bool regOk = IsRegistryRunInstalled(exePath);
                bool taskOk = IsScheduledTaskInstalled(exePath);

                if (!regOk || !taskOk)
                {
                    InstallAutoStartTask(exePath);
                }
            }
        }
        catch
        {
            // Ignore errors during background self-repair
        }
    }

    /// <summary>
    /// Hem HKCU\...\Run (Görev Yöneticisi > Başlangıç Uygulamaları görünürlüğü)
    /// hem de Windows Görev Zamanlayıcı (\mrSpyCC_AutoStart - Yüksek Yetki) kaydını kurar.
    /// </summary>
    public static bool InstallAutoStartTask(string? customExePath = null)
    {
        string? exePath = customExePath ?? Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            return false;

        bool regSuccess = InstallRegistryRun(exePath);
        bool taskSuccess = InstallScheduledTaskXml(exePath);

        return regSuccess || taskSuccess;
    }

    private static bool InstallRegistryRun(string exePath)
    {
        try
        {
            using (var runKey = Registry.CurrentUser.CreateSubKey(RUN_REGISTRY_KEY, writable: true))
            {
                runKey?.SetValue(RUN_VALUE_NAME, $"\"{exePath}\" --startup", RegistryValueKind.String);
            }

            // Görev Yöneticisi > Başlangıç Uygulamaları'nda "Etkin" (Enabled) olarak işaretle
            using (var approvedKey = Registry.CurrentUser.CreateSubKey(STARTUP_APPROVED_KEY, writable: true))
            {
                byte[] enabledBytes = [0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
                approvedKey?.SetValue(RUN_VALUE_NAME, enabledBytes, RegistryValueKind.Binary);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool InstallScheduledTaskXml(string exePath)
    {
        string workDir = Path.GetDirectoryName(exePath) ?? "";
        string escapedExe = SecurityElement.Escape(exePath) ?? exePath;
        string escapedWorkDir = SecurityElement.Escape(workDir) ?? workDir;

        string currentUserId;
        try
        {
            currentUserId = WindowsIdentity.GetCurrent().Name;
        }
        catch
        {
            currentUserId = Environment.UserName;
        }
        string escapedUser = SecurityElement.Escape(currentUserId) ?? currentUserId;

        string xml = $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Author>{escapedUser}</Author>
    <Description>mrSpyCC - Advanced ITE8291 RGB Control Center by aegisSoft (https://aegissoft.com.tr)</Description>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
      <UserId>{escapedUser}</UserId>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author"">
      <UserId>{escapedUser}</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>false</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>4</Priority>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>""{escapedExe}""</Command>
      <Arguments>--startup</Arguments>
      <WorkingDirectory>{escapedWorkDir}</WorkingDirectory>
    </Exec>
  </Actions>
</Task>";

        string tempXml = Path.Combine(Path.GetTempPath(), $"{TASK_NAME}_{Environment.ProcessId}.xml");
        try
        {
            // XML başlığı UTF-16 olduğu için kesinlikle Unicode (UTF-16 LE) olarak yazılmalıdır
            File.WriteAllText(tempXml, xml, Encoding.Unicode);

            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/create /tn \"{TASK_NAME}\" /xml \"{tempXml}\" /f",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(5000);
            return p?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (File.Exists(tempXml))
            {
                try { File.Delete(tempXml); } catch { }
            }
        }
    }

    /// <summary>
    /// Standart yetkiyle başlayan --startup sürecinin, Görev Zamanlayıcıdaki yüksek yetkili (Admin)
    /// görevi UAC sormadan tetiklemesini sağlar.
    /// </summary>
    public static bool TryRunScheduledTask()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/run /tn \"{TASK_NAME}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(3000);
            return p?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Hem HKCU\...\Run kaydını hem de Görev Zamanlayıcı görevini temizler.
    /// </summary>
    public static bool RemoveAutoStartTask()
    {
        bool regRemoved = false;
        try
        {
            using var runKey = Registry.CurrentUser.OpenSubKey(RUN_REGISTRY_KEY, writable: true);
            runKey?.DeleteValue(RUN_VALUE_NAME, throwOnMissingValue: false);

            using var approvedKey = Registry.CurrentUser.OpenSubKey(STARTUP_APPROVED_KEY, writable: true);
            approvedKey?.DeleteValue(RUN_VALUE_NAME, throwOnMissingValue: false);
            regRemoved = true;
        }
        catch
        {
            // Ignore registry errors
        }

        bool taskRemoved = false;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/delete /tn \"{TASK_NAME}\" /f",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(5000);
            taskRemoved = p?.ExitCode == 0;
        }
        catch
        {
            // Ignore schtasks errors
        }

        return regRemoved || taskRemoved;
    }
}

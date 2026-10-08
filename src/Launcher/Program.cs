using System.Diagnostics;
using System.Runtime.InteropServices;
using Velopack;
using Velopack.Sources;

namespace ZTown.Launcher;

/// <summary>
/// Starts the game and keeps it up to date, like CageBoss: installed copies download new
/// versions from GitHub Releases in the background, then offer "Restart now" or install the
/// next time you quit. Offline or no release yet: stays quiet.
/// </summary>
static class Program
{
    const string Repo = "https://github.com/RileyPoirier23/Z-town";
    const string GameExe = "ZTown.Game.exe";

    [STAThread]
    static int Main(string[] args)
    {
        // Must run first: Velopack starts this exe with special arguments during install,
        // update and uninstall, and expects it to handle them and exit quickly.
        VelopackApp.Build().Run();

        var dir = AppContext.BaseDirectory;
        var game = Path.Combine(dir, GameExe);
        if (!File.Exists(game))
        {
            Native.Message($"Can't find {GameExe} next to the launcher. Try reinstalling.", "Z-Town", Native.MB_ICONERROR);
            return 1;
        }

        var psi = new ProcessStartInfo(game) { UseShellExecute = false, WorkingDirectory = dir };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var proc = Process.Start(psi)!;

        UpdateManager? mgr = null;
        UpdateInfo? update = null;
        try
        {
            mgr = new UpdateManager(new GithubSource(Repo, accessToken: null, prerelease: false));
            if (mgr.IsInstalled)
            {
                update = mgr.CheckForUpdatesAsync().GetAwaiter().GetResult();
                if (update != null)
                {
                    mgr.DownloadUpdatesAsync(update).GetAwaiter().GetResult();
                    if (!proc.HasExited)
                    {
                        var answer = Native.Message(
                            $"Z-Town v{update.TargetFullRelease.Version} is ready to install.\n\n" +
                            "Restart now to update, or it will install the next time you quit. Your saves carry over.",
                            "Z-Town update", Native.MB_YESNO | Native.MB_ICONINFORMATION | Native.MB_TOPMOST);
                        if (answer == Native.IDYES)
                        {
                            CloseGame(proc);
                            mgr.ApplyUpdatesAndRestart(update.TargetFullRelease);
                            return 0;
                        }
                    }
                }
            }
        }
        catch
        {
            // offline, GitHub down, or no release yet: try again next launch
        }

        proc.WaitForExit();
        if (mgr != null && update != null)
        {
            try { mgr.WaitExitThenApplyUpdates(update.TargetFullRelease, silent: true, restart: false); }
            catch { /* next launch */ }
        }
        return proc.ExitCode;
    }

    /// <summary>Asks the game window to close (it saves on quit), then makes sure it's gone.</summary>
    static void CloseGame(Process proc)
    {
        if (proc.HasExited) return;
        proc.CloseMainWindow();
        if (!proc.WaitForExit(15000)) proc.Kill(entireProcessTree: true);
    }
}

static class Native
{
    public const uint MB_YESNO = 0x4, MB_ICONERROR = 0x10, MB_ICONINFORMATION = 0x40, MB_TOPMOST = 0x40000;
    public const int IDYES = 6;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    public static int Message(string text, string caption, uint type) =>
        OperatingSystem.IsWindows() ? MessageBoxW(IntPtr.Zero, text, caption, type) : 0;
}

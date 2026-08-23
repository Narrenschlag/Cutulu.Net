namespace Cutulu.Core;

#if GODOT4_0_OR_GREATER
using Godot;
#endif

public static class Application
{
#if GODOT4_0_OR_GREATER
    // Set this once, early, from a point you know is main thread (e.g. autoload _Ready or _EnterTree)
    public static int MainThreadId { get; private set; } = System.Environment.CurrentManagedThreadId;

    /// <summary>
    /// Returns true if current thread is main thread
    /// </summary>
    public static bool IsOnMainThread() => MainThreadId == System.Environment.CurrentManagedThreadId;

    /// <summary>
    /// Update the main thread id to the current thread id
    /// </summary>
    public static void UpdateMainThreadId() => MainThreadId = System.Environment.CurrentManagedThreadId;

    /// <summary>
    /// Close the application
    /// </summary>
    public static void Quit()
    {
        // Notify logger
        Logging.Logging.OnCloseOrCrash();

        // Close the application
        Nodef.Main.GetTree().Quit();
    }

    /// <summary>
    /// Open url in web browser
    /// </summary>
    public static void OpenUrl(this string url, string requiredStart = "https://")
    {
        if (requiredStart.IsEmpty() || url.StartsWith(requiredStart))
            OS.ShellOpen(url.Trim());
    }

    /// <summary>
    /// Define new title for application window
    /// </summary>
    public static void SetWindowTitle(string newTitle, int windowId = 0) => DisplayServer.WindowSetTitle(newTitle, windowId);

    /// <summary>
    /// Set or Get value of Clipboard
    /// </summary>
    public static string Clipboard
    {
        set => DisplayServer.ClipboardSet(value);
        get => DisplayServer.ClipboardGet();
    }

    /// <summary>
    /// Returns true if running in headless mode
    /// </summary>
    public static bool IsHeadless
    {
        get => DisplayServer.GetName() == "headless";
    }

    /// <summary>
    /// Returns true if any --script-args were provided
    /// <para>Schema: "command-to-start-application" --script-args "arg1 arg2 arg3"</para>
    /// </summary>
    public static bool TryGetCmdLineArgs(out string[] _args)
    {
        // Get all command line arguments
        return (_args = OS.GetCmdlineArgs()).NotEmpty();
    }

    /// <summary>
    /// Returns any --script-args if provided
    /// <para>Schema: "command-to-start-application" --script-args "arg1 arg2 arg3"</para>
    /// </summary>
    public static string[] GetCmdLineArgs() => TryGetCmdLineArgs(out var args) ? args ?? [] : [];
#else
    /// <summary>
    /// Close the application
    /// </summary>
    public static void Quit()
    {
        // Notify logger
        Logging.Logging.OnCloseOrCrash();

        // Close the application
        System.Environment.Exit(69);
    }

    public static void OpenUrl(this string url)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
#endif

    public static bool IsExecutablePath(this string path)
    {
        if (OSExecFileExtension.IsEmpty()) return true;
        else return path.EndsWith(OSExecFileExtension, System.StringComparison.OrdinalIgnoreCase);
    }

    public static readonly System.OperatingSystem OperatingSystem = System.Environment.OSVersion;

    public static readonly OS_TYPE OperatingSystemType =
        System.OperatingSystem.IsMacCatalyst() ? OS_TYPE.MAC_CATALYST :
        System.OperatingSystem.IsWindows() ? OS_TYPE.WINDOWS :
        System.OperatingSystem.IsFreeBSD() ? OS_TYPE.FREEBSD :
        System.OperatingSystem.IsAndroid() ? OS_TYPE.ANDROID :
        System.OperatingSystem.IsBrowser() ? OS_TYPE.WEB :
        System.OperatingSystem.IsWatchOS() ? OS_TYPE.WATCH :
        System.OperatingSystem.IsLinux() ? OS_TYPE.LINUX :
        System.OperatingSystem.IsMacOS() ? OS_TYPE.MAC :
        System.OperatingSystem.IsWasi() ? OS_TYPE.WASI :
        System.OperatingSystem.IsTvOS() ? OS_TYPE.TV :
        System.OperatingSystem.IsIOS() ? OS_TYPE.IOS :
        OS_TYPE.UNKNOWN;

    public static readonly OS_CATEGORY OperatingSystemCategory =
        OperatingSystemType switch
        {
            OS_TYPE.MAC_CATALYST => OS_CATEGORY.DESKTOP,
            OS_TYPE.WINDOWS => OS_CATEGORY.DESKTOP,
            OS_TYPE.FREEBSD => OS_CATEGORY.DESKTOP,
            OS_TYPE.LINUX => OS_CATEGORY.DESKTOP,
            OS_TYPE.MAC => OS_CATEGORY.DESKTOP,
            OS_TYPE.ANDROID => OS_CATEGORY.MOBILE,
            OS_TYPE.IOS => OS_CATEGORY.MOBILE,
            OS_TYPE.WATCH => OS_CATEGORY.MOBILE,
            OS_TYPE.TV => OS_CATEGORY.MOBILE,
            OS_TYPE.WEB => OS_CATEGORY.WEB,
            OS_TYPE.WASI => OS_CATEGORY.WEB,
            _ => OS_CATEGORY.UNKNOWN,
        };

    /// <summary>
    /// Returns main executable file ending based on current runtime OS.
    /// </summary>
    public static readonly string OSExecFileExtension = OperatingSystemType switch
    {
        OS_TYPE.MAC_CATALYST => ".dmg",
        OS_TYPE.WINDOWS => ".exe",
        OS_TYPE.LINUX => ".x86_64",
        OS_TYPE.ANDROID => ".apk",
        OS_TYPE.MAC => ".app",
        OS_TYPE.IOS => ".ipa",
        OS_TYPE.WEB => ".html",
        _ => "",
    };
}

public enum OS_CATEGORY : byte
{
    UNKNOWN = 0,
    DESKTOP,
    MOBILE,
    WEB,

    WATCH,
    TV,
}

public enum OS_TYPE : byte
{
    UNKNOWN = 0,

    ANDROID,
    IOS,

    WINDOWS,
    FREEBSD,
    LINUX,
    MAC,

    MAC_CATALYST,
    WATCH,
    TV,

    WEB,
    WASI, // WebAssembly
}
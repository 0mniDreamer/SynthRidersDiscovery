using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(SynthRidersDiscovery.Main), "Synth Riders Discovery", "1.0.0", "Community")]
[assembly: MelonGame("Kluge Interactive", "SynthRiders")]

namespace SynthRidersDiscovery;

/// <summary>
/// A utility mod to discover all available events, managers, and hookable methods in Synth Riders.
/// Use this to find what you can hook into for your own mods.
/// 
/// Keyboard shortcuts (when enabled):
///   F9  - Run full discovery (outputs to Desktop)
///   F10 - Quick scan (console only)
///   F11 - Scan runtime instances
///   F12 - Find Synth Riders classes
/// </summary>
public class Main : MelonMod
{
    private static MelonPreferences_Category _category;
    private static MelonPreferences_Entry<bool> _enableKeyboardShortcuts;
    private static MelonPreferences_Entry<bool> _runOnStartup;
    private static MelonPreferences_Entry<bool> _runOnGameScene;
    private static MelonPreferences_Entry<string> _outputFolder;

    private bool _hasRunStartupScan = false;
    private bool _hasRunGameSceneScan = false;

    public override void OnInitializeMelon()
    {
        InitializeConfig();

        MelonLogger.Msg("╔══════════════════════════════════════════════════════════╗");
        MelonLogger.Msg("║        SYNTH RIDERS DISCOVERY TOOL v1.0.0                ║");
        MelonLogger.Msg("╠══════════════════════════════════════════════════════════╣");
        MelonLogger.Msg("║  Keyboard Shortcuts (if enabled):                        ║");
        MelonLogger.Msg("║    F9  - Full Discovery (saves to Desktop)               ║");
        MelonLogger.Msg("║    F10 - Quick Scan (console only)                       ║");
        MelonLogger.Msg("║    F11 - Runtime Instances (active managers)             ║");
        MelonLogger.Msg("║    F12 - Synth Riders Classes                            ║");
        MelonLogger.Msg("╚══════════════════════════════════════════════════════════╝");

        // Set output folder
        if (_outputFolder != null && !string.IsNullOrEmpty(_outputFolder.Value))
        {
            GameDiscovery.SetOutputFolder(_outputFolder.Value);
        }

        // Run startup scan if enabled
        if (_runOnStartup != null && _runOnStartup.Value == true)
        {
            MelonLogger.Msg("Running startup scan...");
            GameDiscovery.RunFullDiscovery();
            _hasRunStartupScan = true;
        }
    }

    private void InitializeConfig()
    {
        _category = MelonPreferences.CreateCategory("SynthRidersDiscovery", "Discovery Tool Settings");

        _enableKeyboardShortcuts = _category.CreateEntry(
            "EnableKeyboardShortcuts",
            true,
            "Enable Keyboard Shortcuts",
            "Use F9-F12 keys to trigger discovery scans"
        );

        _runOnStartup = _category.CreateEntry(
            "RunOnStartup",
            false,
            "Run On Startup",
            "Automatically run full discovery when the game starts"
        );

        _runOnGameScene = _category.CreateEntry(
            "RunOnGameScene",
            false,
            "Run On Game Scene",
            "Automatically run discovery when entering a gameplay scene"
        );

        _outputFolder = _category.CreateEntry(
            "OutputFolder",
            "",
            "Output Folder",
            "Custom folder for discovery output files (blank = Desktop)"
        );
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        MelonLogger.Msg($"Scene loaded: {sceneName} (index: {buildIndex})");

        // Run game scene scan if enabled and this looks like a gameplay scene
        if (_runOnGameScene != null && _runOnGameScene.Value && !_hasRunGameSceneScan)
        {
            if (sceneName.Contains("Game", StringComparison.OrdinalIgnoreCase) ||
                sceneName.Contains("Stage", StringComparison.OrdinalIgnoreCase) ||
                sceneName.Contains("Play", StringComparison.OrdinalIgnoreCase))
            {
                MelonLogger.Msg("Game scene detected - running runtime scan...");
                GameDiscovery.ScanRuntimeInstances();
                Il2CppDiscovery.FindSynthRidersClasses();
                _hasRunGameSceneScan = true;
            }
        }
    }

    public override void OnUpdate()
    {
        if (_enableKeyboardShortcuts == null || !_enableKeyboardShortcuts.Value) return;

        // F9 - Full discovery
        if (Input.GetKeyDown(KeyCode.F9))
        {
            MelonLogger.Msg("F9 pressed - Running full discovery...");
            GameDiscovery.RunFullDiscovery();
        }

        // F10 - Quick scan
        if (Input.GetKeyDown(KeyCode.F10))
        {
            MelonLogger.Msg("F10 pressed - Running quick scan...");
            GameDiscovery.QuickScan();
        }

        // F11 - Runtime instances
        if (Input.GetKeyDown(KeyCode.F11))
        {
            MelonLogger.Msg("F11 pressed - Scanning runtime instances...");
            GameDiscovery.ScanRuntimeInstances();
        }

        // F12 - Synth Riders classes
        if (Input.GetKeyDown(KeyCode.F12))
        {
            MelonLogger.Msg("F12 pressed - Finding Synth Riders classes...");
            Il2CppDiscovery.FindSynthRidersClasses();
        }
    }

    /// <summary>
    /// Public API for other mods to trigger discovery
    /// </summary>
    public static void RunDiscovery() => GameDiscovery.RunFullDiscovery();
    public static void QuickScan() => GameDiscovery.QuickScan();
    public static void InspectType(string typeName) => Il2CppDiscovery.InspectType(typeName);
}

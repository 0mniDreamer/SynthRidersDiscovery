# Synth Riders Discovery Tool

A standalone MelonLoader mod to discover all available events, managers, and hookable methods in Synth Riders. Perfect for mod developers looking to find what they can hook into.

## Installation

1. Install [MelonLoader](https://melonwiki.xyz/) on Synth Riders
2. Build the project or download the release
3. Copy `SynthRidersDiscovery.dll` to your `SynthRiders/Mods/` folder
4. Launch the game

## Usage

### Keyboard Shortcuts (Default: Enabled)

| Key | Action | Output |
|-----|--------|--------|
| **F9** | Full Discovery | Desktop file + console |
| **F10** | Quick Scan | Console only |
| **F11** | Runtime Instances | Console (active managers) |
| **F12** | Synth Riders Classes | Console (grouped by category) |

### Config Options

Edit `UserData/MelonPreferences.cfg`:

```ini
[SynthRidersDiscovery]
EnableKeyboardShortcuts = true
RunOnStartup = false
RunOnGameScene = false
OutputFolder = 
```

## What It Finds

### 1. C# Events
Subscribe with `+=`:
```csharp
SomeManager.OnSongStarted += (song) => { ... };
```

### 2. Singleton Managers
Access game state:
```csharp
var score = Game_ScoreManager.s_instance.currentScore;
var health = HealthManager.Instance.currentHealth;
```

### 3. UnityEvents
Use AddListener:
```csharp
stageEvents.OnNoteHit.AddListener(HandleNoteHit);
```

### 4. Event Methods
Patch with Harmony:
```csharp
[HarmonyPatch(typeof(Game_ScoreManager), "OnComboChanged")]
[HarmonyPostfix]
static void OnCombo(int combo) { ... }
```

### 5. Action/Func Delegates
Callbacks:
```csharp
SomeClass.onScoreChanged += (newScore) => { ... };
```

## Output Files

- **Desktop/SynthRiders_Discovery.txt** - Full discovery report
- **Desktop/SynthRiders_IL2CPP_Discovery.txt** - IL2CPP assembly dump

## API for Other Mods

You can call discovery methods from your own mod:

```csharp
// Reference SynthRidersDiscovery.dll in your project

// Run full discovery
SynthRidersDiscovery.Main.RunDiscovery();

// Quick console scan
SynthRidersDiscovery.Main.QuickScan();

// Inspect a specific type
SynthRidersDiscovery.Main.InspectType("GameControlManager");
SynthRidersDiscovery.Main.InspectType("StageEvents");
```

Or use the static classes directly:

```csharp
using SynthRidersDiscovery;

// Full discovery to file
GameDiscovery.RunFullDiscovery();

// Quick scan to console
GameDiscovery.QuickScan();

// Find active managers during gameplay
GameDiscovery.ScanRuntimeInstances();

// IL2CPP specific
Il2CppDiscovery.FindSynthRidersClasses();
Il2CppDiscovery.InspectType("Game_ScoreManager");
Il2CppDiscovery.CheckSingleton("GameControlManager", "s_instance");
Il2CppDiscovery.DumpAllAssemblies();
```

## Known Synth Riders Classes

Based on community modding, these are likely targets:

| Class | Purpose |
|-------|---------|
| `GameControlManager` | Main game controller, scene management |
| `Game_ScoreManager` | Score, combo, multiplier, health |
| `StageEvents` | UnityEvents for note hits, song start/end |
| `Game_InfoProvider` | Song info, difficulty, BPM |
| `TrackManager` | Note spawning, track data |
| `ModifierManager` | Active gameplay modifiers |

## Building

1. Update `Directory.Build.props` with your Synth Riders path
2. Run the game once with MelonLoader to generate unhollowed assemblies
3. Build with `dotnet build -c Release`
4. DLL auto-copies to your Mods folder

## Tips

- **Run F11 during gameplay** to find active managers
- **Use InspectType()** for deep dives into specific classes
- **Check the Desktop file** for the full report with copy-paste code examples
- **Run F12 first** to see all Synth Riders classes grouped by category

## Troubleshooting

### "Type not found"
The unhollowed assemblies might not be loaded. Try running discovery during gameplay instead of at startup.

### No keyboard shortcuts working
Check the config file and make sure `EnableKeyboardShortcuts = true`.

### Missing types
Some IL2CPP stripping may hide certain members. Use dnSpy on the raw `Il2CppAssemblies` folder for the complete picture.

## License

MIT - Use freely for your Synth Riders mods!

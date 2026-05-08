using System.Reflection;
using System.Text;
using MelonLoader;
using UnityEngine;

namespace SynthRidersDiscovery;

/// <summary>
/// Core discovery tool to find all available events, managers, and hookable methods.
/// </summary>
public static class GameDiscovery
{
    private static string _outputFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    
    private static string OutputPath => Path.Combine(_outputFolder, "SynthRiders_Discovery.txt");

    private static readonly HashSet<string> InterestingPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        // Core gameplay
        "manager", "controller", "handler", "system", "service",
        "event", "score", "combo", "health", "life", "note", "song",
        "track", "stage", "game", "player", "rail", "orb", "obstacle",
        
        // Synth Riders specific
        "synth", "experience", "xp", "force", "spin", "modifier",
        "difficulty", "chart", "beatmap", "bpm", "audio", "music",
        
        // Multiplayer
        "multiplayer", "network", "leaderboard", "rank",
        
        // UI
        "menu", "ui", "hud", "panel", "display", "overlay"
    };

    private static readonly HashSet<string> EventMethodPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "On", "Handle", "Fire", "Trigger", "Raise", "Invoke", "Broadcast", "Notify", "Dispatch"
    };

    public static void SetOutputFolder(string folder)
    {
        if (Directory.Exists(folder))
        {
            _outputFolder = folder;
            MelonLogger.Msg($"Output folder set to: {folder}");
        }
    }

    /// <summary>
    /// Run complete discovery and save to file
    /// </summary>
    public static void RunFullDiscovery()
    {
        MelonLogger.Msg("═══════════════════════════════════════════════════════════");
        MelonLogger.Msg("  RUNNING FULL DISCOVERY - This may take a moment...");
        MelonLogger.Msg("═══════════════════════════════════════════════════════════");

        var sb = new StringBuilder();
        sb.AppendLine("╔══════════════════════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║                    SYNTH RIDERS DISCOVERY REPORT                             ║");
        sb.AppendLine($"║                    Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}                               ║");
        sb.AppendLine("╚══════════════════════════════════════════════════════════════════════════════╝");
        sb.AppendLine();
        sb.AppendLine("Use this report to find events and classes you can hook into for your mods.");
        sb.AppendLine("Look for:");
        sb.AppendLine("  • C# Events → Subscribe with +=");
        sb.AppendLine("  • UnityEvents → Use AddListener()");
        sb.AppendLine("  • Singletons → Access game state via Instance/s_instance");
        sb.AppendLine("  • Event methods → Patch with Harmony");
        sb.AppendLine();

        try
        {
            sb.AppendLine(DiscoverCSharpEvents());
            sb.AppendLine(DiscoverSingletons());
            sb.AppendLine(DiscoverUnityEvents());
            sb.AppendLine(DiscoverEventMethods());
            sb.AppendLine(DiscoverDelegateFields());
            sb.AppendLine(DiscoverStaticFields());
            sb.AppendLine(DumpAllInterestingTypes());

            File.WriteAllText(OutputPath, sb.ToString());
            
            MelonLogger.Msg("═══════════════════════════════════════════════════════════");
            MelonLogger.Msg($"  ✓ DISCOVERY COMPLETE!");
            MelonLogger.Msg($"  ✓ Output saved to: {OutputPath}");
            MelonLogger.Msg("═══════════════════════════════════════════════════════════");
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"Discovery failed: {ex}");
            sb.AppendLine($"\n\nERROR DURING DISCOVERY:\n{ex}");
            
            try { File.WriteAllText(OutputPath, sb.ToString()); }
            catch { }
        }
    }

    /// <summary>
    /// Quick scan - console output only, no file
    /// </summary>
    public static void QuickScan()
    {
        MelonLogger.Msg("═══════════════════════════════════════════════════════════");
        MelonLogger.Msg("  QUICK SCAN - Looking for key game classes...");
        MelonLogger.Msg("═══════════════════════════════════════════════════════════");

        var targetTypes = new[]
        {
            "GameControlManager", "GameManager", "Game_ScoreManager", "ScoreManager",
            "StageEvents", "Game_InfoProvider", "TrackManager", "NoteManager",
            "ComboManager", "HealthManager", "LifeBar", "ModifierManager",
            "MultiplayerManager", "SongManager", "PlayerManager", "AudioManager"
        };

        int found = 0;

        foreach (var asm in GetGameAssemblies())
        {
            try
            {
                foreach (var type in asm.GetTypes())
                {
                    if (targetTypes.Any(t => type.Name.Contains(t, StringComparison.OrdinalIgnoreCase)))
                    {
                        found++;
                        MelonLogger.Msg($"┌─ FOUND: {type.FullName}");

                        // List events
                        var events = type.GetEvents(BindingFlags.Public | BindingFlags.NonPublic |
                                                     BindingFlags.Static | BindingFlags.Instance);
                        foreach (var ev in events)
                        {
                            MelonLogger.Msg($"│   🔔 EVENT: {ev.Name}");
                        }

                        // List key properties
                        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                            .Where(p => p.DeclaringType == type).Take(8);
                        foreach (var prop in props)
                        {
                            MelonLogger.Msg($"│   📌 PROP: {prop.Name} ({prop.PropertyType.Name})");
                        }

                        // Check for singleton
                        var singletonField = type.GetField("s_instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static) ??
                                             type.GetField("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        if (singletonField != null)
                        {
                            MelonLogger.Msg($"│   🎯 SINGLETON: {singletonField.Name}");
                        }

                        MelonLogger.Msg($"└───────────────────────────────────────────");
                    }
                }
            }
            catch { }
        }

        MelonLogger.Msg($"Quick scan complete. Found {found} target classes.");
    }

    /// <summary>
    /// Scan for active runtime instances (call during gameplay)
    /// </summary>
    public static void ScanRuntimeInstances()
    {
        MelonLogger.Msg("═══════════════════════════════════════════════════════════");
        MelonLogger.Msg("  RUNTIME SCAN - Finding active manager instances...");
        MelonLogger.Msg("═══════════════════════════════════════════════════════════");

        try
        {
            var allBehaviours = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();

            var managerBehaviours = allBehaviours
                .Where(b => IsInterestingName(b.GetType().Name))
                .GroupBy(b => b.GetType().FullName)
                .OrderBy(g => g.Key);

            int count = 0;
            foreach (var group in managerBehaviours)
            {
                count++;
                MelonLogger.Msg($"┌─ ACTIVE: {group.Key} ({group.Count()} instance(s))");

                var type = group.First().GetType();

                // Check for events
                var events = type.GetEvents(BindingFlags.Public | BindingFlags.NonPublic |
                                             BindingFlags.Static | BindingFlags.Instance);
                foreach (var ev in events)
                {
                    MelonLogger.Msg($"│   🔔 {ev.Name}");
                }

                // Check for interesting public methods
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(m => !m.IsSpecialName && EventMethodPrefixes.Any(p => m.Name.StartsWith(p)))
                    .Take(5);
                foreach (var m in methods)
                {
                    MelonLogger.Msg($"│   🔧 {m.Name}()");
                }

                MelonLogger.Msg($"└───────────────────────────────────────────");
            }

            MelonLogger.Msg($"Found {count} active interesting MonoBehaviours.");
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"Runtime scan failed: {ex.Message}");
        }
    }

    #region Discovery Methods

    private static string DiscoverCSharpEvents()
    {
        var sb = new StringBuilder();
        sb.AppendLine("┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓");
        sb.AppendLine("┃  1. C# EVENTS (subscribe with += operator)                                  ┃");
        sb.AppendLine("┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛");
        sb.AppendLine();
        sb.AppendLine("  Usage: SomeClass.SomeEvent += YourHandler;");
        sb.AppendLine();

        int total = 0;

        foreach (var asm in GetGameAssemblies())
        {
            try
            {
                foreach (var type in asm.GetTypes())
                {
                    var events = type.GetEvents(BindingFlags.Public | BindingFlags.NonPublic |
                                                 BindingFlags.Static | BindingFlags.Instance);

                    if (events.Length > 0)
                    {
                        sb.AppendLine($"  📦 {type.FullName}");

                        foreach (var ev in events)
                        {
                            var isStatic = ev.GetAddMethod(true)?.IsStatic ?? false;
                            var access = ev.GetAddMethod(true)?.IsPublic == true ? "public" : "private";
                            var staticMod = isStatic ? "static " : "";

                            sb.AppendLine($"      🔔 {access} {staticMod}event {ev.EventHandlerType?.Name} {ev.Name}");
                            total++;

                            // Log important ones to console too
                            if (IsInterestingName(type.Name))
                            {
                                MelonLogger.Msg($"[EVENT] {type.Name}.{ev.Name}");
                            }
                        }
                        sb.AppendLine();
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  ⚠️ Error scanning {asm.GetName().Name}: {ex.Message}");
            }
        }

        sb.AppendLine($"  ═══ Total C# events: {total} ═══");
        sb.AppendLine();
        return sb.ToString();
    }

    private static string DiscoverSingletons()
    {
        var sb = new StringBuilder();
        sb.AppendLine("┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓");
        sb.AppendLine("┃  2. SINGLETON MANAGERS (access game state)                                  ┃");
        sb.AppendLine("┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛");
        sb.AppendLine();
        sb.AppendLine("  Usage: var value = SomeManager.s_instance.SomeProperty;");
        sb.AppendLine();

        int found = 0;
        var instancePatterns = new[] { "Instance", "instance", "s_instance", "_instance", "Singleton" };

        foreach (var asm in GetGameAssemblies())
        {
            try
            {
                foreach (var type in asm.GetTypes())
                {
                    foreach (var pattern in instancePatterns)
                    {
                        var prop = type.GetProperty(pattern, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        var field = type.GetField(pattern, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

                        bool isSingleton = (prop != null && prop.PropertyType == type) ||
                                           (field != null && field.FieldType == type);

                        if (isSingleton)
                        {
                            sb.AppendLine($"  🎯 {type.FullName}");
                            sb.AppendLine($"      Access: {type.Name}.{pattern}");
                            
                            // List useful properties
                            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                .Where(p => p.DeclaringType == type && p.CanRead)
                                .Take(12);
                            
                            if (props.Any())
                            {
                                sb.AppendLine("      Properties:");
                                foreach (var p in props)
                                {
                                    sb.AppendLine($"        • {p.PropertyType.Name} {p.Name}");
                                }
                            }

                            // List useful methods
                            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                                .Where(m => !m.IsSpecialName)
                                .Take(10);

                            if (methods.Any())
                            {
                                sb.AppendLine("      Methods:");
                                foreach (var m in methods)
                                {
                                    var parms = string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name));
                                    sb.AppendLine($"        • {m.ReturnType.Name} {m.Name}({parms})");
                                }
                            }

                            sb.AppendLine();
                            found++;
                            MelonLogger.Msg($"[SINGLETON] {type.Name}.{pattern}");
                            break;
                        }
                    }
                }
            }
            catch { }
        }

        sb.AppendLine($"  ═══ Total singletons: {found} ═══");
        sb.AppendLine();
        return sb.ToString();
    }

    private static string DiscoverUnityEvents()
    {
        var sb = new StringBuilder();
        sb.AppendLine("┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓");
        sb.AppendLine("┃  3. UNITY EVENTS (use AddListener)                                          ┃");
        sb.AppendLine("┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛");
        sb.AppendLine();
        sb.AppendLine("  Usage: someObject.OnSomeEvent.AddListener(YourHandler);");
        sb.AppendLine();

        int found = 0;

        foreach (var asm in GetGameAssemblies())
        {
            try
            {
                foreach (var type in asm.GetTypes())
                {
                    var unityEventFields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                                           BindingFlags.Static | BindingFlags.Instance)
                        .Where(f => IsUnityEventType(f.FieldType))
                        .ToList();

                    if (unityEventFields.Any())
                    {
                        sb.AppendLine($"  📦 {type.FullName}");

                        foreach (var f in unityEventFields)
                        {
                            var isStatic = f.IsStatic ? "static " : "";
                            sb.AppendLine($"      ⚡ {isStatic}{f.FieldType.Name} {f.Name}");
                            found++;

                            if (IsInterestingName(type.Name))
                            {
                                MelonLogger.Msg($"[UNITY_EVENT] {type.Name}.{f.Name}");
                            }
                        }
                        sb.AppendLine();
                    }
                }
            }
            catch { }
        }

        sb.AppendLine($"  ═══ Total Unity events: {found} ═══");
        sb.AppendLine();
        return sb.ToString();
    }

    private static string DiscoverEventMethods()
    {
        var sb = new StringBuilder();
        sb.AppendLine("┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓");
        sb.AppendLine("┃  4. EVENT-LIKE METHODS (patch with Harmony)                                 ┃");
        sb.AppendLine("┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛");
        sb.AppendLine();
        sb.AppendLine("  Usage: [HarmonyPatch(typeof(SomeClass), \"OnSomething\")]");
        sb.AppendLine();

        int found = 0;

        foreach (var asm in GetGameAssemblies())
        {
            try
            {
                foreach (var type in asm.GetTypes())
                {
                    if (!IsInterestingName(type.Name)) continue;

                    var eventMethods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                                        BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                        .Where(m => !m.IsSpecialName && EventMethodPrefixes.Any(p => m.Name.StartsWith(p)))
                        .ToList();

                    if (eventMethods.Any())
                    {
                        sb.AppendLine($"  📦 {type.FullName}");

                        foreach (var m in eventMethods)
                        {
                            var isStatic = m.IsStatic ? "static " : "";
                            var access = m.IsPublic ? "public" : m.IsPrivate ? "private" : "protected";
                            var parms = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));

                            sb.AppendLine($"      🔧 {access} {isStatic}{m.Name}({parms})");
                            found++;
                        }
                        sb.AppendLine();
                    }
                }
            }
            catch { }
        }

        sb.AppendLine($"  ═══ Total event methods: {found} ═══");
        sb.AppendLine();
        return sb.ToString();
    }

    private static string DiscoverDelegateFields()
    {
        var sb = new StringBuilder();
        sb.AppendLine("┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓");
        sb.AppendLine("┃  5. ACTION/FUNC DELEGATES (callbacks)                                       ┃");
        sb.AppendLine("┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛");
        sb.AppendLine();
        sb.AppendLine("  Usage: SomeClass.onCallback += (args) => { ... };");
        sb.AppendLine();

        int found = 0;

        foreach (var asm in GetGameAssemblies())
        {
            try
            {
                foreach (var type in asm.GetTypes())
                {
                    var delegateFields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                                         BindingFlags.Static | BindingFlags.Instance)
                        .Where(f => IsDelegateType(f.FieldType))
                        .ToList();

                    if (delegateFields.Any())
                    {
                        sb.AppendLine($"  📦 {type.FullName}");

                        foreach (var f in delegateFields)
                        {
                            var isStatic = f.IsStatic ? "static " : "";
                            sb.AppendLine($"      🔗 {isStatic}{f.FieldType.Name} {f.Name}");
                            found++;
                        }
                        sb.AppendLine();
                    }
                }
            }
            catch { }
        }

        sb.AppendLine($"  ═══ Total delegate fields: {found} ═══");
        sb.AppendLine();
        return sb.ToString();
    }

    private static string DiscoverStaticFields()
    {
        var sb = new StringBuilder();
        sb.AppendLine("┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓");
        sb.AppendLine("┃  6. STATIC FIELDS (game state)                                              ┃");
        sb.AppendLine("┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛");
        sb.AppendLine();

        foreach (var asm in GetGameAssemblies())
        {
            try
            {
                foreach (var type in asm.GetTypes())
                {
                    if (!IsInterestingName(type.Name)) continue;

                    var staticFields = type.GetFields(BindingFlags.Public | BindingFlags.Static)
                        .Where(f => !f.IsLiteral)
                        .ToList();

                    if (staticFields.Any())
                    {
                        sb.AppendLine($"  📦 {type.FullName}");

                        foreach (var f in staticFields)
                        {
                            sb.AppendLine($"      📌 {f.FieldType.Name} {f.Name}");
                        }
                        sb.AppendLine();
                    }
                }
            }
            catch { }
        }

        return sb.ToString();
    }

    private static string DumpAllInterestingTypes()
    {
        var sb = new StringBuilder();
        sb.AppendLine("┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓");
        sb.AppendLine("┃  7. ALL INTERESTING TYPES (by namespace)                                    ┃");
        sb.AppendLine("┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛");
        sb.AppendLine();

        var interestingTypes = new List<Type>();

        foreach (var asm in GetGameAssemblies())
        {
            try
            {
                foreach (var type in asm.GetTypes())
                {
                    if (IsInterestingName(type.Name) || IsInterestingName(type.Namespace ?? ""))
                    {
                        interestingTypes.Add(type);
                    }
                }
            }
            catch { }
        }

        var grouped = interestingTypes
            .GroupBy(t => t.Namespace ?? "(global)")
            .OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            sb.AppendLine($"  📁 {group.Key}");

            foreach (var type in group.OrderBy(t => t.Name))
            {
                var typeKind = type.IsEnum ? "enum" :
                               type.IsInterface ? "interface" :
                               type.IsAbstract ? "abstract" :
                               type.IsClass ? "class" : "struct";

                sb.AppendLine($"      • {typeKind} {type.Name}");
            }
            sb.AppendLine();
        }

        sb.AppendLine($"  ═══ Total interesting types: {interestingTypes.Count} ═══");
        return sb.ToString();
    }

    #endregion

    #region Helpers

    internal static IEnumerable<Assembly> GetGameAssemblies()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(a =>
            {
                var name = a.GetName().Name ?? "";
                return name.Contains("Assembly-CSharp") ||
                       name.Contains("SynthRiders") ||
                       name.Contains("Synth") ||
                       name.StartsWith("Il2Cpp");
            });
    }

    internal static bool IsInterestingName(string name)
    {
        return InterestingPatterns.Any(p => name.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsUnityEventType(Type type)
    {
        if (type == null) return false;
        var typeName = type.Name;
        return typeName.Contains("UnityEvent") ||
               typeName.Contains("UnityAction") ||
               (type.BaseType != null && IsUnityEventType(type.BaseType));
    }

    private static bool IsDelegateType(Type type)
    {
        if (type == null) return false;
        var typeName = type.FullName ?? type.Name;
        return typeof(Delegate).IsAssignableFrom(type) ||
               typeName.StartsWith("System.Action") ||
               typeName.StartsWith("System.Func") ||
               typeName.StartsWith("Il2CppSystem.Action") ||
               typeName.StartsWith("Il2CppSystem.Func");
    }

    #endregion
}

using System.Reflection;
using System.Text;
using MelonLoader;

namespace SynthRidersDiscovery;

/// <summary>
/// IL2CPP-specific discovery methods for finding game classes and events.
/// </summary>
public static class Il2CppDiscovery
{
    private static string OutputPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        "SynthRiders_IL2CPP_Discovery.txt"
    );

    /// <summary>
    /// Find all Synth Riders related classes grouped by keyword
    /// </summary>
    public static void FindSynthRidersClasses()
    {
        MelonLogger.Msg("═══════════════════════════════════════════════════════════");
        MelonLogger.Msg("  SYNTH RIDERS CLASS SCAN");
        MelonLogger.Msg("═══════════════════════════════════════════════════════════");

        var targetPatterns = new Dictionary<string, List<string>>
        {
            ["Core Gameplay"] = new() { "GameControl", "StageEvent", "Score", "Combo", "Health", "LifeBar" },
            ["Notes & Track"] = new() { "Note", "Rail", "Orb", "Obstacle", "Track", "Chart" },
            ["Song & Audio"] = new() { "Song", "Audio", "Music", "Sound", "Beat", "BPM" },
            ["Managers"] = new() { "Manager", "Controller", "Handler", "Service" },
            ["Player"] = new() { "Player", "Multiplayer", "Network", "Leaderboard" },
            ["Modifiers"] = new() { "Modifier", "Force", "Spin", "Experience" },
            ["UI"] = new() { "Menu", "HUD", "Panel", "Display" }
        };

        var found = new Dictionary<string, Dictionary<string, List<string>>>();

        foreach (var category in targetPatterns)
        {
            found[category.Key] = new Dictionary<string, List<string>>();
            foreach (var pattern in category.Value)
            {
                found[category.Key][pattern] = new List<string>();
            }
        }

        foreach (var asm in GameDiscovery.GetGameAssemblies())
        {
            try
            {
                foreach (var type in asm.GetTypes())
                {
                    foreach (var category in targetPatterns)
                    {
                        foreach (var pattern in category.Value)
                        {
                            if (type.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                            {
                                found[category.Key][pattern].Add(type.FullName ?? type.Name);
                            }
                        }
                    }
                }
            }
            catch { }
        }

        // Output results
        foreach (var category in found)
        {
            var totalInCategory = category.Value.Values.Sum(l => l.Count);
            if (totalInCategory == 0) continue;

            MelonLogger.Msg($"\n┌─ {category.Key.ToUpper()} ({totalInCategory} types)");

            foreach (var pattern in category.Value.Where(p => p.Value.Count > 0))
            {
                MelonLogger.Msg($"│");
                MelonLogger.Msg($"│  [{pattern.Key}] ({pattern.Value.Count} matches)");

                foreach (var typeName in pattern.Value.Take(10))
                {
                    MelonLogger.Msg($"│    • {typeName}");
                }

                if (pattern.Value.Count > 10)
                {
                    MelonLogger.Msg($"│    ... and {pattern.Value.Count - 10} more");
                }
            }

            MelonLogger.Msg($"└───────────────────────────────────────────");
        }
    }

    /// <summary>
    /// Find IL2CPP Action callbacks
    /// </summary>
    public static void FindIl2CppCallbacks()
    {
        MelonLogger.Msg("═══════════════════════════════════════════════════════════");
        MelonLogger.Msg("  IL2CPP ACTION/CALLBACK SCAN");
        MelonLogger.Msg("═══════════════════════════════════════════════════════════");

        int found = 0;

        foreach (var asm in GameDiscovery.GetGameAssemblies())
        {
            try
            {
                foreach (var type in asm.GetTypes())
                {
                    if (!GameDiscovery.IsInterestingName(type.Name)) continue;

                    var actionFields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                                       BindingFlags.Static | BindingFlags.Instance)
                        .Where(f => f.FieldType.FullName?.Contains("Action") == true ||
                                    f.FieldType.FullName?.Contains("Func") == true);

                    foreach (var field in actionFields)
                    {
                        MelonLogger.Msg($"[CALLBACK] {type.Name}.{field.Name} : {field.FieldType.Name}");
                        found++;
                    }
                }
            }
            catch { }
        }

        MelonLogger.Msg($"\nFound {found} IL2CPP callbacks.");
    }

    /// <summary>
    /// Deep inspect a specific type by name
    /// </summary>
    public static void InspectType(string typeName)
    {
        MelonLogger.Msg("═══════════════════════════════════════════════════════════");
        MelonLogger.Msg($"  INSPECTING: {typeName}");
        MelonLogger.Msg("═══════════════════════════════════════════════════════════");

        Type targetType = null;

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                targetType = asm.GetTypes().FirstOrDefault(t =>
                    t.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase) ||
                    t.FullName?.EndsWith(typeName, StringComparison.OrdinalIgnoreCase) == true);

                if (targetType != null) break;
            }
            catch { }
        }

        if (targetType == null)
        {
            MelonLogger.Warning($"Type '{typeName}' not found!");
            MelonLogger.Msg("Try using the full type name or run FindSynthRidersClasses() first.");
            return;
        }

        MelonLogger.Msg($"Found: {targetType.FullName}");
        MelonLogger.Msg($"Assembly: {targetType.Assembly.GetName().Name}");
        MelonLogger.Msg($"Base Type: {targetType.BaseType?.FullName ?? "none"}");

        // Events
        var events = targetType.GetEvents(BindingFlags.Public | BindingFlags.NonPublic |
                                           BindingFlags.Static | BindingFlags.Instance);
        if (events.Length > 0)
        {
            MelonLogger.Msg($"\n┌─ EVENTS ({events.Length})");
            foreach (var ev in events)
            {
                var isStatic = ev.GetAddMethod(true)?.IsStatic == true ? "static " : "";
                MelonLogger.Msg($"│  🔔 {isStatic}{ev.Name} ({ev.EventHandlerType?.Name})");
            }
            MelonLogger.Msg("└───────────────────────────────────────────");
        }

        // Fields
        var fields = targetType.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                           BindingFlags.Static | BindingFlags.Instance)
            .Where(f => !f.Name.StartsWith("<"))
            .ToList();

        if (fields.Count > 0)
        {
            MelonLogger.Msg($"\n┌─ FIELDS ({fields.Count})");
            foreach (var f in fields.Take(20))
            {
                var mods = (f.IsStatic ? "static " : "") + (f.IsPublic ? "public" : "private");
                MelonLogger.Msg($"│  📌 {mods} {f.FieldType.Name} {f.Name}");
            }
            if (fields.Count > 20)
                MelonLogger.Msg($"│  ... and {fields.Count - 20} more");
            MelonLogger.Msg("└───────────────────────────────────────────");
        }

        // Properties
        var props = targetType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic |
                                              BindingFlags.Static | BindingFlags.Instance)
            .ToList();

        if (props.Count > 0)
        {
            MelonLogger.Msg($"\n┌─ PROPERTIES ({props.Count})");
            foreach (var p in props.Take(20))
            {
                var isStatic = p.GetGetMethod(true)?.IsStatic == true ? "static " : "";
                var access = p.GetGetMethod(true)?.IsPublic == true ? "public" : "private";
                MelonLogger.Msg($"│  📊 {access} {isStatic}{p.PropertyType.Name} {p.Name}");
            }
            if (props.Count > 20)
                MelonLogger.Msg($"│  ... and {props.Count - 20} more");
            MelonLogger.Msg("└───────────────────────────────────────────");
        }

        // Methods
        var methods = targetType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                             BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .ToList();

        if (methods.Count > 0)
        {
            MelonLogger.Msg($"\n┌─ METHODS ({methods.Count})");
            foreach (var m in methods.Take(25))
            {
                var isStatic = m.IsStatic ? "static " : "";
                var parms = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name}"));
                MelonLogger.Msg($"│  🔧 {isStatic}{m.ReturnType.Name} {m.Name}({parms})");
            }
            if (methods.Count > 25)
                MelonLogger.Msg($"│  ... and {methods.Count - 25} more");
            MelonLogger.Msg("└───────────────────────────────────────────");
        }

        // Check for singleton
        var singletonNames = new[] { "s_instance", "Instance", "_instance", "Singleton" };
        foreach (var name in singletonNames)
        {
            var field = targetType.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            var prop = targetType.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            if (field != null || prop != null)
            {
                MelonLogger.Msg($"\n🎯 SINGLETON ACCESS: {targetType.Name}.{name}");
                break;
            }
        }
    }

    /// <summary>
    /// Check if a singleton instance is currently active
    /// </summary>
    public static void CheckSingleton(string typeName, string fieldName = "s_instance")
    {
        MelonLogger.Msg($"Checking singleton: {typeName}.{fieldName}");

        Type targetType = null;

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                targetType = asm.GetTypes().FirstOrDefault(t =>
                    t.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase) ||
                    t.FullName?.EndsWith(typeName) == true);
                if (targetType != null) break;
            }
            catch { }
        }

        if (targetType == null)
        {
            MelonLogger.Warning($"Type '{typeName}' not found!");
            return;
        }

        // Try field
        var field = targetType.GetField(fieldName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        if (field != null)
        {
            try
            {
                var value = field.GetValue(null);
                if (value != null)
                {
                    MelonLogger.Msg($"✓ {typeName}.{fieldName} = ACTIVE");
                    
                    // Try to read some properties
                    var props = value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Where(p => p.CanRead)
                        .Take(5);

                    foreach (var p in props)
                    {
                        try
                        {
                            var propValue = p.GetValue(value);
                            MelonLogger.Msg($"    • {p.Name} = {propValue}");
                        }
                        catch { }
                    }
                }
                else
                {
                    MelonLogger.Msg($"✗ {typeName}.{fieldName} = NULL (not active yet)");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading: {ex.Message}");
            }
            return;
        }

        // Try property
        var prop = targetType.GetProperty(fieldName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        if (prop != null)
        {
            try
            {
                var value = prop.GetValue(null);
                MelonLogger.Msg($"{typeName}.{fieldName} = {(value != null ? "ACTIVE" : "NULL")}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error reading property: {ex.Message}");
            }
            return;
        }

        MelonLogger.Warning($"Field/property '{fieldName}' not found on {typeName}");
    }

    /// <summary>
    /// Dump all IL2CPP assemblies to file
    /// </summary>
    public static void DumpAllAssemblies()
    {
        MelonLogger.Msg("Dumping all IL2CPP assemblies to file...");

        var sb = new StringBuilder();
        sb.AppendLine("IL2CPP ASSEMBLY DUMP");
        sb.AppendLine($"Generated: {DateTime.Now}");
        sb.AppendLine(new string('=', 80));
        sb.AppendLine();

        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith("Il2Cpp") == true ||
                        a.GetName().Name == "Assembly-CSharp")
            .OrderBy(a => a.GetName().Name);

        foreach (var asm in assemblies)
        {
            sb.AppendLine($"════════════════════════════════════════════════════════════════════════════════");
            sb.AppendLine($"ASSEMBLY: {asm.GetName().Name}");
            sb.AppendLine($"════════════════════════════════════════════════════════════════════════════════");

            try
            {
                var types = asm.GetTypes()
                    .Where(t => !t.Name.StartsWith("<"))
                    .OrderBy(t => t.Namespace)
                    .ThenBy(t => t.Name);

                string currentNs = null;

                foreach (var type in types)
                {
                    if (type.Namespace != currentNs)
                    {
                        currentNs = type.Namespace;
                        sb.AppendLine();
                        sb.AppendLine($"  ┌─ Namespace: {currentNs ?? "(global)"}");
                    }

                    var typeKind = type.IsEnum ? "enum" :
                                   type.IsInterface ? "interface" :
                                   type.IsAbstract ? "abstract" : "class";

                    sb.AppendLine($"  │  {typeKind} {type.Name}");

                    // Only detail interesting types
                    if (GameDiscovery.IsInterestingName(type.Name))
                    {
                        var events = type.GetEvents();
                        foreach (var ev in events)
                            sb.AppendLine($"  │      event {ev.Name}");

                        var statics = type.GetFields(BindingFlags.Public | BindingFlags.Static)
                            .Where(f => !f.IsLiteral);
                        foreach (var f in statics)
                            sb.AppendLine($"  │      static {f.FieldType.Name} {f.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  ERROR: {ex.Message}");
            }

            sb.AppendLine();
        }

        File.WriteAllText(OutputPath, sb.ToString());
        MelonLogger.Msg($"Dump saved to: {OutputPath}");
    }
}

using System.Text.Json.Serialization;

namespace Mosen.GroupScheduler.Core.Data;

/// <summary>Stable non-secret data contract identifiers. Script exports never include local account IDs.</summary>
public static class DataContract
{
    public const int CurrentVersion = 1;
    public const int MaxAccounts = 300;
    public const int MaxConcurrentGroupTasks = 50;
}

public sealed record ScriptRole(
    [property: JsonPropertyName("roleKey")] Guid RoleKey,
    [property: JsonPropertyName("roleLabel")] string RoleLabel);

public sealed record ScriptStepTiming(
    [property: JsonPropertyName("preSendDelayMs")] int PreSendDelayMs,
    [property: JsonPropertyName("typingDurationMs")] int TypingDurationMs,
    [property: JsonPropertyName("postSendDelayMs")] int PostSendDelayMs)
{
    public static ScriptStepTiming Default => new(5000, 5000, 0);

    public void Validate()
    {
        if (PreSendDelayMs is < 0 or > 3_600_000 ||
            TypingDurationMs is < 0 or > 60_000 ||
            PostSendDelayMs is < 0 or > 3_600_000)
            throw new ArgumentOutOfRangeException(nameof(ScriptStepTiming),
                "Each timing value must be within its supported range.");
    }

    public long EarliestDispatchAfterMs
    {
        get
        {
            Validate();
            return (long)PreSendDelayMs + TypingDurationMs; // consecutive, never overlap
        }
    }
}

public sealed record ScriptExportManifest(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("scriptId")] Guid ScriptId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("roles")] IReadOnlyList<ScriptRole> Roles,
    [property: JsonPropertyName("steps")] IReadOnlyList<ScriptExportStep> Steps);

public sealed record ScriptExportStep(
    [property: JsonPropertyName("stepKey")] Guid StepKey,
    [property: JsonPropertyName("roleKey")] Guid RoleKey,
    [property: JsonPropertyName("body")] string Body,
    [property: JsonPropertyName("timing")] ScriptStepTiming Timing);

public static class ContractValidation
{
    public static void Validate(ScriptExportManifest manifest)
    {
        if (manifest.SchemaVersion != DataContract.CurrentVersion) throw new InvalidDataException("Unsupported export schema version.");
        if (manifest.ScriptId == Guid.Empty || string.IsNullOrWhiteSpace(manifest.Title)) throw new InvalidDataException("Invalid script metadata.");
        if (manifest.Roles.Count == 0 || manifest.Roles.Any(r => r.RoleKey == Guid.Empty || string.IsNullOrWhiteSpace(r.RoleLabel))) throw new InvalidDataException("Invalid roles.");
        if (manifest.Roles.Select(r => r.RoleKey).Distinct().Count() != manifest.Roles.Count) throw new InvalidDataException("Duplicate role keys.");
        var keys = manifest.Roles.Select(r => r.RoleKey).ToHashSet();
        if (manifest.Steps.Any(s => s.StepKey == Guid.Empty || !keys.Contains(s.RoleKey))) throw new InvalidDataException("Unknown step role.");
        if (manifest.Steps.Select(s => s.StepKey).Distinct().Count() != manifest.Steps.Count) throw new InvalidDataException("Duplicate step keys.");
        foreach (var s in manifest.Steps) s.Timing.Validate();
    }
}

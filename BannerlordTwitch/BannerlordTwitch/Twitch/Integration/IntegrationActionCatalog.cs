using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BannerlordTwitch.Util;
using BannerlordTwitch.Localization;

namespace BannerlordTwitch.Integration
{
    public sealed class IntegrationActionCatalog
    {
        private readonly Dictionary<string, IntegrationActionDefinition> actions;
        public string ManifestJson { get; }

        private IntegrationActionCatalog(string json, IntegrationActionManifest manifest)
        {
            ManifestJson = json;
            actions = manifest.Actions.ToDictionary(action => action.Id, StringComparer.OrdinalIgnoreCase);
        }

        public static IntegrationActionCatalog Load()
        {
            string path = Path.Combine(Path.GetDirectoryName(typeof(IntegrationActionCatalog).Assembly.Location) ?? ".", "..", "..", "TwitchIntegration", "action-manifest.json");
            string json = File.ReadAllText(path);
            var manifest = JsonSerializer.Deserialize<IntegrationActionManifest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (manifest == null || manifest.ProtocolVersion != IntegrationProtocol.Version)
                throw new InvalidDataException($"Unsupported Twitch integration manifest at {path}");
            return new IntegrationActionCatalog(json, manifest);
        }

        public bool TryGet(string actionId, out IntegrationActionDefinition action) => actions.TryGetValue(actionId == "command.bltbet" ? "command.predict" : actionId, out action);

        public string BuildLegacyArguments(IntegrationActionDefinition action, IReadOnlyDictionary<string, JsonElement> values)
        {
            if (string.Equals(action.Id, "command.retinue", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(action.Id, "command.eliteretinue", StringComparison.OrdinalIgnoreCase))
                return BuildRetinueArguments(action, values);
            if (string.Equals(action.Id, "command.transfer", StringComparison.OrdinalIgnoreCase))
                return BuildTransferArguments(action, values);
            if (string.Equals(action.Id, "command.upgrade", StringComparison.OrdinalIgnoreCase))
                return BuildUpgradeArguments(action, values);
            if (string.Equals(action.Id, "command.family", StringComparison.OrdinalIgnoreCase))
                return BuildFamilyArguments(action, values);
            var result = new List<string>();
            if (values.Keys.Any(key => action.Inputs.All(input => !string.Equals(input.Id, key, StringComparison.OrdinalIgnoreCase))))
                throw new ArgumentException("The request contains an unknown argument");
            foreach (var input in action.Inputs)
            {
                if (!IsVisible(input, values)) continue;
                if (!values.TryGetValue(input.Id, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                {
                    if (input.Required) throw new ArgumentException($"{input.Id} is required");
                    continue;
                }
                if (input.Type == "confirmation")
                {
                    if (input.Required && value.ValueKind != JsonValueKind.True) throw new ArgumentException($"{input.Id} must be confirmed");
                    if (value.ValueKind == JsonValueKind.True && string.Equals(input.ConfirmationPolicy, "legacy-token", StringComparison.OrdinalIgnoreCase))
                    {
                        var token = string.Equals(input.LegacyToken, "retire-yes", StringComparison.OrdinalIgnoreCase)
                            ? "{=xSbB2Zw5}yes".Translate()
                            : input.LegacyToken;
                        if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException($"{input.Id} has no confirmation token");
                        result.Add(token);
                    }
                    continue;
                }
                if (input.Type == "choice")
                {
                    if (value.ValueKind != JsonValueKind.String) throw new ArgumentException($"{input.Id} must be a choice");
                    var allowed = DynamicOptions(input.OptionsSource) ?? input.Options.Select(option => option.Value);
                    if (!allowed.Any(option => string.Equals(option, value.GetString(), StringComparison.OrdinalIgnoreCase)))
                        throw new ArgumentException($"{input.Id} is not a valid choice");
                }
                if (input.Type is "number" or "integer")
                {
                    if (!value.TryGetDouble(out var number)) throw new ArgumentException($"{input.Id} must be a number");
                    if (input.Type == "integer" && number != Math.Truncate(number)) throw new ArgumentException($"{input.Id} must be a whole number");
                    if (input.Minimum.HasValue && number < input.Minimum.Value) throw new ArgumentException($"{input.Id} is below the minimum");
                    if (input.Maximum.HasValue && number > input.Maximum.Value) throw new ArgumentException($"{input.Id} is above the maximum");
                }
                string text = value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
                if (string.IsNullOrWhiteSpace(text))
                {
                    if (input.Required) throw new ArgumentException($"{input.Id} is required");
                    continue;
                }
                if (text.Any(char.IsControl) || text.Contains('\n') || text.Contains('\r')) throw new ArgumentException($"{input.Id} contains invalid characters");
                result.Add(text.Trim());
            }
            return string.Join(" ", result);
        }

        private static IEnumerable<string> DynamicOptions(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) return null;
            var selectors = IntegrationSelectorProvider.Current();
            return source.ToLowerInvariant() switch
            {
                "cultures" => selectors.Cultures,
                "heroes" => selectors.Heroes,
                "clans" => selectors.Clans,
                "kingdoms" => selectors.Kingdoms,
                "settlements" => selectors.Settlements,
                "skills" => selectors.Skills,
                _ => throw new ArgumentException($"Unknown selector source: {source}")
            };
        }

        private static bool IsVisible(IntegrationActionInput input, IReadOnlyDictionary<string, JsonElement> values)
        {
            if (string.IsNullOrWhiteSpace(input.VisibleWhenInput)) return true;
            if (!values.TryGetValue(input.VisibleWhenInput, out var controlling) || controlling.ValueKind != JsonValueKind.String) return false;
            return input.VisibleWhenValues.Any(value => string.Equals(value, controlling.GetString(), StringComparison.OrdinalIgnoreCase));
        }

        private static string BuildRetinueArguments(IntegrationActionDefinition action, IReadOnlyDictionary<string, JsonElement> values)
        {
            if (values.Keys.Any(key => action.Inputs.All(input => !string.Equals(input.Id, key, StringComparison.OrdinalIgnoreCase))))
                throw new ArgumentException("The request contains an unknown argument");
            if (!values.TryGetValue("operation", out var operationValue) || operationValue.ValueKind != JsonValueKind.String)
                throw new ArgumentException("operation is required");
            var operation = operationValue.GetString();
            return operation switch
            {
                "upgrade-one" => "1",
                "upgrade-count" when values.TryGetValue("count", out var count) && count.TryGetInt32(out var quantity) && quantity > 0 => quantity.ToString(),
                "upgrade-count" => throw new ArgumentException("A positive troop quantity is required"),
                "upgrade-all" => "all",
                "clear-all" => "clear all",
                "clear-slot" when values.TryGetValue("slot", out var slot) && slot.TryGetInt32(out var index) && index > 0 => $"clear {index}",
                "clear-slot" => throw new ArgumentException("A positive slot number is required when dismissing one troop"),
                _ => throw new ArgumentException("operation is not a valid retinue action")
            };
        }

        private static string BuildTransferArguments(IntegrationActionDefinition action, IReadOnlyDictionary<string, JsonElement> values)
        {
            RejectUnknown(action, values);
            var mode = RequiredString(values, "mode");
            if (mode != "normal" && mode != "force") throw new ArgumentException("mode is not a valid transfer mode");
            var settlement = RequiredString(values, "settlement");
            var recipientType = RequiredString(values, "recipientType");
            if (recipientType != "clan" && recipientType != "hero") throw new ArgumentException("recipientType is not valid");
            values.TryGetValue("target", out var targetValue);
            var target = targetValue.ValueKind == JsonValueKind.String ? targetValue.GetString()?.Trim() : null;
            if (recipientType == "hero" && string.IsNullOrWhiteSpace(target)) throw new ArgumentException("target is required for hero transfers");
            return string.Join(" ", new[] { mode == "force" ? "force" : null, settlement, recipientType, target }.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        private static string BuildUpgradeArguments(IntegrationActionDefinition action, IReadOnlyDictionary<string, JsonElement> values)
        {
            RejectUnknown(action, values);
            var operation = RequiredString(values, "operation");
            var scope = RequiredString(values, "scope");
            values.TryGetValue("target", out var targetValue);
            values.TryGetValue("upgrade", out var upgradeValue);
            var target = targetValue.ValueKind == JsonValueKind.String ? targetValue.GetString()?.Trim() : null;
            var upgrade = upgradeValue.ValueKind == JsonValueKind.String ? upgradeValue.GetString()?.Trim() : null;
            if (operation == "list") return string.Join(" ", new[] { "list", scope });
            if (operation == "info") return string.Join(" ", new[] { "info", scope, target }.Where(value => !string.IsNullOrWhiteSpace(value)));
            if (operation == "remove") return string.Join(" ", new[] { "remove", scope, target, upgrade }.Where(value => !string.IsNullOrWhiteSpace(value)));
            if (operation is not ("apply" or "auto" or "bulk")) throw new ArgumentException("operation is not a valid upgrade operation");
            if (string.IsNullOrWhiteSpace(upgrade)) throw new ArgumentException("upgrade is required");
            return string.Join(" ", new[] { operation == "apply" ? null : operation, scope, target, upgrade }.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        private static string BuildFamilyArguments(IntegrationActionDefinition action, IReadOnlyDictionary<string, JsonElement> values)
        {
            RejectUnknown(action, values);
            values.TryGetValue("operation", out var operationValue);
            values.TryGetValue("target", out var targetValue);
            var operation = operationValue.ValueKind == JsonValueKind.String ? operationValue.GetString()?.Trim() : null;
            var target = targetValue.ValueKind == JsonValueKind.String ? targetValue.GetString()?.Trim() : null;
            if (string.IsNullOrWhiteSpace(operation)) return string.Empty;
            return operation == "member" ? target ?? string.Empty : string.Join(" ", new[] { operation, target }.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        private static void RejectUnknown(IntegrationActionDefinition action, IReadOnlyDictionary<string, JsonElement> values)
        {
            if (values.Keys.Any(key => action.Inputs.All(input => !string.Equals(input.Id, key, StringComparison.OrdinalIgnoreCase))))
                throw new ArgumentException("The request contains an unknown argument");
        }

        private static string RequiredString(IReadOnlyDictionary<string, JsonElement> values, string name)
        {
            if (!values.TryGetValue(name, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
                throw new ArgumentException($"{name} is required");
            return value.GetString().Trim();
        }
    }
}

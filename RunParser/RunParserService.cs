using System.Text;
using System.Text.Json;

namespace RunParser;

internal static class RunParserService
{
    public static ParseResult ParseFolder(string folder)
    {
        var files = Directory.EnumerateFiles(folder, "*.run", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var totals = new RunTotals();
        var errors = new List<string>();

        foreach (var file in files)
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(file));
                totals.AddRun(document.RootElement);
            }
            catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
            {
                errors.Add($"{Path.GetFileName(file)}: {exception.Message}");
            }
        }

        return new ParseResult(files.Length, files.Length - errors.Count, FormatReport(folder, totals, errors));
    }

    private static string FormatReport(string folder, RunTotals totals, IReadOnlyList<string> errors)
    {
        var report = new StringBuilder();
        report.AppendLine($"Run folder: {folder}");
        report.AppendLine($"Runs parsed: {totals.Runs}");
        report.AppendLine($"Wins: {totals.Wins} | Losses: {totals.Losses} | Win rate: {Percent(totals.Wins, totals.Runs)}");

        report.AppendLine();
        report.AppendLine("BY CHARACTER");
        report.AppendLine("Character                 Runs   Wins   Win rate");
        foreach (var (character, stats) in totals.Characters.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            report.AppendLine($"{character,-25} {stats.Runs,4} {stats.Wins,6} {Percent(stats.Wins, stats.Runs),10}");

        report.AppendLine();
        report.AppendLine("PICK RATES BY ITEM (picked / offered)");
        report.AppendLine("Character                 Type             Item                              Picked  Offered  Pick rate");
        foreach (var row in totals.Choices
                     .OrderBy(pair => pair.Key.Character, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(pair => pair.Key.Type, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(pair => pair.Key.Item, StringComparer.OrdinalIgnoreCase))
        {
            var (key, stats) = (row.Key, row.Value);
            report.AppendLine($"{key.Character,-25} {key.Type,-16} {key.Item,-34} {stats.Picked,6} {stats.Offered,8} {Percent(stats.Picked, stats.Offered),10}");
        }

        if (errors.Count > 0)
        {
            report.AppendLine();
            report.AppendLine($"FILES SKIPPED DUE TO ERRORS ({errors.Count})");
            foreach (var error in errors)
                report.AppendLine(error);
        }

        return report.ToString();
    }

    private static string Percent(int numerator, int denominator) =>
        denominator == 0 ? "n/a" : $"{numerator * 100.0 / denominator:F1}%";

    private static string? GetString(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? GetOptionId(JsonElement option)
    {
        if (GetString(option, "choice") is { } choice) return choice;
        if (GetString(option, "TextKey") is { } textKey) return textKey;
        return option.ValueKind == JsonValueKind.Object && option.TryGetProperty("card", out var card)
            ? GetString(card, "id")
            : null;
    }

    private static bool TryGetPicked(JsonElement option, out bool picked)
    {
        foreach (var propertyName in new[] { "was_picked", "was_chosen" })
        {
            if (option.ValueKind == JsonValueKind.Object && option.TryGetProperty(propertyName, out var value) &&
                (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False))
            {
                picked = value.GetBoolean();
                return true;
            }
        }

        picked = false;
        return false;
    }

    private sealed class RunTotals
    {
        public int Runs { get; private set; }
        public int Wins { get; private set; }
        public int Losses { get; private set; }
        public Dictionary<string, CharacterTotals> Characters { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<(string Character, string Type, string Item), ChoiceTotals> Choices { get; } = new();

        public void AddRun(JsonElement run)
        {
            Runs++;
            var won = run.TryGetProperty("win", out var winElement) && winElement.ValueKind == JsonValueKind.True;
            if (won) Wins++;
            else Losses++;

            if (!run.TryGetProperty("players", out var players) || players.ValueKind != JsonValueKind.Array)
                return;

            foreach (var player in players.EnumerateArray())
            {
                var character = GetString(player, "character") ?? "UNKNOWN";
                if (!Characters.TryGetValue(character, out var characterTotals))
                    Characters[character] = characterTotals = new CharacterTotals();
                characterTotals.Runs++;
                if (won) characterTotals.Wins++;

                var playerId = player.TryGetProperty("id", out var id) ? id : default;
                AddPlayerChoices(run, character, playerId);
            }
        }

        private void AddPlayerChoices(JsonElement run, string character, JsonElement playerId)
        {
            if (!run.TryGetProperty("map_point_history", out var history) || history.ValueKind != JsonValueKind.Array)
                return;

            foreach (var floor in history.EnumerateArray())
            {
                if (floor.ValueKind != JsonValueKind.Array) continue;
                foreach (var point in floor.EnumerateArray())
                {
                    if (!point.TryGetProperty("player_stats", out var playerStats) || playerStats.ValueKind != JsonValueKind.Array)
                        continue;
                    foreach (var stats in playerStats.EnumerateArray())
                    {
                        if (playerId.ValueKind != JsonValueKind.Undefined &&
                            stats.TryGetProperty("player_id", out var statsPlayerId) &&
                            statsPlayerId.ToString() != playerId.ToString())
                            continue;

                        foreach (var property in stats.EnumerateObject())
                        {
                            if (!property.Name.EndsWith("_choices", StringComparison.Ordinal) || property.Value.ValueKind != JsonValueKind.Array)
                                continue;

                            var type = property.Name[..^"_choices".Length];
                            foreach (var option in property.Value.EnumerateArray())
                            {
                                if (!TryGetPicked(option, out var picked) || GetOptionId(option) is not { } item) continue;

                                var key = (character, type, item);
                                if (!Choices.TryGetValue(key, out var choiceTotals))
                                    Choices[key] = choiceTotals = new ChoiceTotals();
                                choiceTotals.Offered++;
                                if (picked) choiceTotals.Picked++;
                            }
                        }
                    }
                }
            }
        }
    }

    private sealed class CharacterTotals
    {
        public int Runs { get; set; }
        public int Wins { get; set; }
    }

    private sealed class ChoiceTotals
    {
        public int Offered { get; set; }
        public int Picked { get; set; }
    }
}

internal sealed record ParseResult(int FileCount, int ParsedCount, string Report);

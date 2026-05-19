using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace SLAYER_Conquest;

public partial class SLAYER_Conquest : BasePlugin, IPluginConfig<SLAYER_ConquestConfig>
{
    private struct CenterMessageLine
    {
        public string Message;
        public float Duration;
        public float StartTime;

        public CenterMessageLine(string message, float duration, float startTime)
        {
            Message = message;
            Duration = duration;
            StartTime = startTime;
        }
    }

    private readonly Dictionary<CCSPlayerController, Dictionary<int, CenterMessageLine>> _centerMessageLinesByPlayer = new();

    /// <summary>
    /// Tick function to display the combined center message
    /// </summary>
    private void PrintCenterMessageTick()
    {
        if (_centerMessageLinesByPlayer.Count == 0) return;

        var validPlayers = activePlayers;
        if (validPlayers.Count == 0) return;

        var playerMessages = new List<string>();
        foreach (var player in validPlayers)
        {
            if (!IsPlayerValid(player)) continue;

            if (!_centerMessageLinesByPlayer.TryGetValue(player, out var playerLines) || playerLines.Count == 0)
            {
                continue;
            }

            List<int>? expiredLines = null;
            foreach (var line in playerLines)
            {
                float elapsedTime = Server.CurrentTime - line.Value.StartTime;
                float remainingTime = line.Value.Duration - elapsedTime;
                if (line.Value.Duration > 0f && remainingTime < 0f)
                {
                    expiredLines ??= new List<int>();
                    expiredLines.Add(line.Key);
                }
            }

            if (expiredLines != null)
            {
                foreach (var lineId in expiredLines)
                {
                    playerLines.Remove(lineId);
                }

                if (playerLines.Count == 0)
                {
                    _centerMessageLinesByPlayer.Remove(player);
                    continue;
                }
            }

            playerMessages.Clear();
            foreach (var line in playerLines.OrderBy(kvp => kvp.Key))
            {
                playerMessages.Add(line.Value.Message);
            }

            if (playerMessages.Count > 0)
            {
                string combinedMessage = string.Join("<br>", playerMessages);
                player.PrintToCenterHtml(combinedMessage);
            }
        }
    }

    private bool IsPlayerValid(CCSPlayerController? player)
    {
        return player != null && player.IsValid && player.Connected == PlayerConnectedState.PlayerConnected && player.TeamNum >= 1 && !player.IsBot && !player.IsHLTV;
    }

    private Dictionary<int, CenterMessageLine> GetOrCreatePlayerLines(CCSPlayerController player)
    {
        if (!_centerMessageLinesByPlayer.TryGetValue(player, out var playerLines))
        {
            playerLines = new Dictionary<int, CenterMessageLine>();
            _centerMessageLinesByPlayer[player] = playerLines;
        }

        return playerLines;
    }

    private List<CCSPlayerController> GetTargetPlayers(RecipientFilter? recipients)
    {
        var players = new List<CCSPlayerController>();
        if (recipients != null && recipients.Count > 0) // If specific recipients are provided, use them
        {
            foreach (var player in recipients)
            {
                if (IsPlayerValid(player))
                {
                    players.Add(player);
                }
            }

            return players;
        }

        // If no specific recipients, target all valid players
        foreach (var player in Utilities.GetPlayers())
        {
            if (IsPlayerValid(player))
            {
                players.Add(player);
            }
        }

        return players;
    }

    private int GetNextAvailableLineIdForPlayer(Dictionary<int, CenterMessageLine> playerLines)
    {
        if (playerLines.Count == 0) return 1;
        return playerLines.Keys.Max() + 1;
    }

    /// <summary>
    /// Add a line to the center message
    /// </summary>
    /// <param name="lineId">Unique identifier for the line (0 = auto-generate)</param>
    /// <param name="message">The message text for this line</param>
    /// <param name="recipients">Target players (null/empty = none)</param>
    /// <param name="duration">How long to display this line (in seconds)</param>
    public void AddCenterMessageLine(int lineId = 0, string message = "", RecipientFilter? recipients = null, float duration = 5f)
    {
        if (lineId < 0 || string.IsNullOrWhiteSpace(message)) return;

        var targetPlayers = GetTargetPlayers(recipients);
        if (targetPlayers.Count == 0) return;

        foreach (var player in targetPlayers)
        {
            var playerLines = GetOrCreatePlayerLines(player);

            int actualLineId = lineId > 0 ? lineId : GetNextAvailableLineIdForPlayer(playerLines);
            playerLines[actualLineId] = new CenterMessageLine(message, duration, Server.CurrentTime);
        }
    }

    /// <summary>
    /// Update an existing line with new content, recipients, or duration
    /// </summary>
    /// <param name="recipients">Target players (null/empty = all existing keys)</param>
    /// <param name="lineId">Unique identifier of the line to update</param>
    /// <param name="newMessage">New message text</param>
    /// <param name="duration">New duration (0 = keep existing)</param>
    /// <param name="resetTimer">If true, resets the timer to the new duration; if false, keeps remaining time</param>
    public void UpdateCenterMessageLine(RecipientFilter? recipients, int lineId, string newMessage, float duration = 0f, bool resetTimer = false)
    {
        if (lineId <= 0 || string.IsNullOrWhiteSpace(newMessage)) return;

        var targetPlayers = GetTargetPlayers(recipients);
        if (targetPlayers.Count == 0) return;

        foreach (var player in targetPlayers)
        {
            UpdateCenterMessageLineForPlayer(player, lineId, newMessage, duration, resetTimer);
        }
    }

    private void UpdateCenterMessageLineForPlayer(CCSPlayerController player, int lineId, string newMessage, float duration, bool resetTimer)
    {
        var playerLines = GetOrCreatePlayerLines(player);

        if (!playerLines.TryGetValue(lineId, out var existingLine))
        {
            playerLines[lineId] = new CenterMessageLine(newMessage, duration, Server.CurrentTime);
            return;
        }

        float elapsedTime = Server.CurrentTime - existingLine.StartTime;
        float remainingTime = existingLine.Duration - elapsedTime;
        if (existingLine.Duration == 0f) remainingTime = 0f;
        else if (remainingTime < 0f)
        {
            return;
        }

        float newDuration = resetTimer ? duration : remainingTime;
        float newStartTime = resetTimer ? Server.CurrentTime : existingLine.StartTime;
        playerLines[lineId] = new CenterMessageLine(newMessage, newDuration, newStartTime);
    }

    public void ExtendCenterMessageLine(RecipientFilter? recipients, int lineId, string newMessage, float duration = -1f)
    {
        if (lineId <= 0 || string.IsNullOrEmpty(newMessage)) return;

        var targetPlayers = GetTargetPlayers(recipients);
        if (targetPlayers.Count == 0) return;

        foreach (var player in targetPlayers)
        {
            if (!_centerMessageLinesByPlayer.TryGetValue(player, out var playerLines)) continue;
            if (!playerLines.TryGetValue(lineId, out var existingLine)) continue;

            float newDuration = duration > 0 ? duration : existingLine.Duration;
            float newStartTime = duration > 0 ? Server.CurrentTime : existingLine.StartTime;
            playerLines[lineId] = new CenterMessageLine(existingLine.Message + newMessage, newDuration, newStartTime);
        }
    }

    public void ExtendCenterMessageLineForPlayer(CCSPlayerController player, int lineId, string newMessage, float duration = -1f)
    {
        if (lineId <= 0 || string.IsNullOrEmpty(newMessage) || player == null) return;

        if (!_centerMessageLinesByPlayer.TryGetValue(player, out var playerLines)) return;
        if (!playerLines.TryGetValue(lineId, out var existingLine)) return;

        float newDuration = duration > 0 ? duration : existingLine.Duration;
        float newStartTime = duration > 0 ? Server.CurrentTime : existingLine.StartTime;
        playerLines[lineId] = new CenterMessageLine(existingLine.Message + newMessage, newDuration, newStartTime);
    }

    /// <summary>
    /// Remove a specific line from the center message
    /// </summary>
    /// <param name="lineId">Unique identifier of the line to remove</param>
    public void RemoveCenterMessageLine(int lineId)
    {
        if (lineId <= 0) return;

        var players = _centerMessageLinesByPlayer.Keys.ToList();
        foreach (var player in players)
        {
            if (!_centerMessageLinesByPlayer.TryGetValue(player, out var playerLines)) continue;

            if (playerLines.Remove(lineId) && playerLines.Count == 0)
            {
                _centerMessageLinesByPlayer.Remove(player);
            }
        }
    }

    public bool HasCenterMessageLine(CCSPlayerController player, int lineId)
    {
        if (lineId <= 0 || player == null) return false;
        return _centerMessageLinesByPlayer.TryGetValue(player, out var playerLines) && playerLines.ContainsKey(lineId);
    }

    /// <summary>
    /// Clear all center message lines
    /// </summary>
    public void ClearAllCenterMessageLines()
    {
        _centerMessageLinesByPlayer.Clear();
    }

    public void ClearCenterMessageLinesForPlayer(CCSPlayerController player)
    {
        if (player == null) return;
        _centerMessageLinesByPlayer.Remove(player);
    }
}
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace SLAYER_Conquest;

public partial class SLAYER_Conquest : BasePlugin, IPluginConfig<SLAYER_ConquestConfig>
{
    public Dictionary<int, (string, float, RecipientFilter, float)> CenterMessageLines = new Dictionary<int, (string, float, RecipientFilter, float)>();
    private bool _isSorting = false;

    /// <summary>
    /// Tick function to display the combined center message
    /// </summary>
    private void PrintCenterMessageTick()
    {
        if (CenterMessageLines == null || CenterMessageLines.Count == 0) return;

        // Get all valid players once
        var validPlayers = activePlayers;

        if (validPlayers.Count == 0) return;

        foreach (var line in CenterMessageLines)
        {
            // Calculate elapsed time
            float elapsedTime = Server.CurrentTime - line.Value.Item4;
            float remainingTime = line.Value.Item2 - elapsedTime;
            if (line.Value.Item2 > 0f && remainingTime < 0f) { RemoveCenterMessageLine(line.Key); return; } // Skip expired lines
        }

        // Sort lines once
        var sortedLines = CenterMessageLines.OrderBy(kvp => kvp.Key).ToList();

        var specificLines = sortedLines.Where(line => line.Value.Item3 != null && line.Value.Item3.Count > 0).ToList();

        foreach (var player in validPlayers.Where(p => p != null && p.IsValid))
        {
            var playerMessages = new List<string>();

            // Add specific messages for this player
            playerMessages.AddRange(specificLines.Where(line => line.Value.Item3.Contains(player)).Select(line => line.Value.Item1));

            // Send combined message if any exist
            if (playerMessages.Count > 0)
            {
                string combinedMessage = string.Join("<br>", playerMessages);
                player.PrintToCenterHtml(combinedMessage);
            }
        }
    }

    /// <summary>
    /// Add a line to the center message
    /// </summary>
    /// <param name="lineId">Unique identifier for the line</param>
    /// <param name="message">The message text for this line</param>
    /// <param name="recipients">Target players (null = all players)</param>
    /// <param name="duration">How long to display this line (in seconds)</param>
    public void AddCenterMessageLine(int lineId = 0, string message = "", RecipientFilter? recipients = null, float duration = 5f)
    {
        if (lineId < 0 || string.IsNullOrWhiteSpace(message)) return;

        // Get the actual line ID that will be used
        int actualLineId = lineId > 0 ? lineId : GetNextAvailableLineId();

        // Remove existing line if updating
        if (CenterMessageLines.ContainsKey(actualLineId))
        {
            RemoveCenterMessageLine(actualLineId);
        }


        //Server.PrintToChatAll($"Adding line: {actualLineId} | {Server.CurrentTime} | {message}");
        // Add the new line using the actual line ID
        CenterMessageLines[actualLineId] = (message, duration, recipients ?? new RecipientFilter(), Server.CurrentTime);
    }

    /// <summary>
    /// Remove a specific line from the center message
    /// </summary>
    /// <param name="lineId">Unique identifier of the line to remove</param>
    public void RemoveCenterMessageLine(int lineId)
    {
        if (lineId <= 0 || !CenterMessageLines.ContainsKey(lineId)) return;

        // Remove the line
        CenterMessageLines.Remove(lineId);
    }

    /// <summary>
    /// Update an existing line with new content, recipients, or duration
    /// </summary>
    /// <param name="lineId">Unique identifier of the line to update</param>
    /// <param name="newMessage">New message text</param>
    /// <param name="recipients">New recipients (null = keep existing)</param>
    /// <param name="duration">New duration (0 = keep existing)</param>
    /// <param name="resetTimer">If true, resets the timer to the new duration; if false, keeps remaining time</param>
    /// </summary>
    public void UpdateCenterMessageLine(int lineId, string newMessage, RecipientFilter? recipients = null, float duration = 0f, bool resetTimer = false)
    {
        if (lineId <= 0 || string.IsNullOrWhiteSpace(newMessage)) return;

        if (!CenterMessageLines.ContainsKey(lineId))
        {
            // If line 1 doesn't exist, just add it
            AddCenterMessageLine(lineId, newMessage, recipients, duration);
            return;
        }

        // Calculate elapsed time
        float elapsedTime = Server.CurrentTime - CenterMessageLines[lineId].Item4;
        float remainingTime = CenterMessageLines[lineId].Item2 - elapsedTime;
        if (CenterMessageLines[lineId].Item2 == 0) remainingTime = 0f; // Permanent lines
        else if (remainingTime < 0f) { return; } // Skip expired lines

        // updated line
        CenterMessageLines[lineId] = (newMessage, resetTimer ? duration : remainingTime, recipients ?? CenterMessageLines[lineId].Item3, resetTimer ? Server.CurrentTime : CenterMessageLines[lineId].Item4);
    }

    public void ExtendCenterMessageLine(int lineId, string newMessage, float duration = -1f)
    {
        if (lineId <= 0 || !CenterMessageLines.ContainsKey(lineId)) return;
        if (string.IsNullOrEmpty(newMessage)) return;

        var existingLine = CenterMessageLines[lineId];

        // Update the line with new timer and duration
        CenterMessageLines[lineId] = (existingLine.Item1 + newMessage, existingLine.Item2 + duration > 0 ? duration : 0f, existingLine.Item3, duration > 0 ? Server.CurrentTime : existingLine.Item4);
    }

    /// <summary>
    /// Insert a line at a specific index and shift existing lines down
    /// </summary>
    /// <param name="insertIndex">The index where to insert the new line</param>
    /// <param name="message">The message text for this line</param>
    /// <param name="recipients">Target players (null = all players)</param>
    /// <param name="duration">How long to display this line (in seconds)</param>
    public void InsertCenterMessageLineAtIndex(int insertIndex, string message, RecipientFilter? recipients = null, float duration = 5f)
    {
        if (insertIndex <= 0 || string.IsNullOrWhiteSpace(message)) return;

        // Get all existing lines sorted by ID
        var existingLines = CenterMessageLines.OrderBy(kvp => kvp.Key).ToList();

        // Clear the dictionary (but keep the line data)
        ClearAllCenterMessageLines();

        // Re-add lines with shifted IDs
        foreach (var line in existingLines)
        {
            int oldId = line.Key;
            int newId = oldId >= insertIndex ? oldId + 1 : oldId; // Shift lines at or after insertIndex

            // Calculate elapsed time
            float elapsedTime = Server.CurrentTime - line.Value.Item4;
            float remainingTime = line.Value.Item2 - elapsedTime;
            if (line.Value.Item2 == 0) remainingTime = 0f; // Permanent lines
            else if (remainingTime < 0f) continue; // Skip expired lines
            // Add the line with new ID
            AddCenterMessageLine(newId, line.Value.Item1, line.Value.Item3, remainingTime);
        }

        // Add the new line at the specified index
        AddCenterMessageLine(insertIndex, message, recipients, duration);
    }

    /// <summary>
    /// Move a line from one position to another
    /// </summary>
    /// <param name="fromLineId">Source line ID</param>
    /// <param name="toLineId">Target line ID</param>
    public void MoveCenterMessageLine(int fromLineId, int toLineId)
    {
        if (fromLineId <= 0 || toLineId <= 0 || !CenterMessageLines.ContainsKey(fromLineId)) return;
        if (fromLineId == toLineId) return; // No need to move

        // Get the line to move
        var lineToMove = CenterMessageLines[fromLineId];

        // Remove the line from current position
        RemoveCenterMessageLine(fromLineId);

        // Insert at new position (this will shift other lines)
        InsertCenterMessageLineAtIndex(toLineId, lineToMove.Item1, lineToMove.Item3, lineToMove.Item2);
    }
    /// <summary>
    /// Helper function to get the next available line ID
    /// </summary>
    /// <returns>The next available line ID (highest existing ID + 1)</returns>
    public int GetNextAvailableLineId()
    {
        if (CenterMessageLines.Count == 0) return 1;
        return CenterMessageLines.Keys.Max() + 1;
    }

    /// <summary>
    /// Compact line IDs to remove gaps (1, 2, 3, 4... instead of 1, 3, 7, 10...)
    /// </summary>
    public void SortCenterMessageLines()
    {
        if (_isSorting) return; // Prevent recursion

        _isSorting = true;

        try
        {
            // Get all existing lines sorted by ID
            var existingLines = CenterMessageLines.OrderBy(kvp => kvp.Key).ToList();
            if (existingLines.Count == 0) return;

            // Clear the dictionary
            ClearAllCenterMessageLines();

            // Re-add lines with sequential IDs starting from 1
            for (int i = 0; i < existingLines.Count; i++)
            {
                var line = existingLines[i].Value;

                // Calculate remaining time
                float elapsedTime = Server.CurrentTime - line.Item4;
                float remainingTime = line.Item2 - elapsedTime;

                if (line.Item2 == 0) remainingTime = 0f; // Permanent lines
                else if (remainingTime < 0f) continue; // Skip expired lines

                AddCenterMessageLine(i + 1, line.Item1, line.Item3, remainingTime);
            }
        }
        finally
        {
            _isSorting = false;
        }
    }

    /// <summary>
    /// Insert multiple lines at once, starting from a specific index
    /// </summary>
    /// <param name="startIndex">Starting index for insertion</param>
    /// <param name="messages">Array of messages to insert</param>
    /// <param name="recipients">Target players (null = all players)</param>
    /// <param name="duration">Duration for all inserted lines</param>
    public void InsertMultipleCenterMessageLines(int startIndex, string[] messages, RecipientFilter? recipients = null, float duration = 5f)
    {
        if (startIndex <= 0 || messages == null || messages.Length == 0) return;

        // Insert lines one by one (this will properly shift existing lines)
        for (int i = 0; i < messages.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(messages[i]))
            {
                InsertCenterMessageLineAtIndex(startIndex + i, messages[i], recipients, duration);
            }
        }
    }

    /// <summary>
    /// Get the display order of all lines (sorted by line ID)
    /// </summary>
    /// <returns>Dictionary with display order and line content</returns>
    public Dictionary<int, string> GetDisplayOrder()
    {
        return CenterMessageLines.OrderBy(kvp => kvp.Key).ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Item1);
    }

    /// <summary>
    /// Get the specific line message
    /// </summary>
    /// <param name="lineId">Line ID to retrieve</param>
    /// <returns>Line message content</returns>
    public string GetCenterMessageLine(int lineId)
    {
        if (CenterMessageLines.TryGetValue(lineId, out var line))
        {
            return line.Item1;
        }
        return "";
    }

    /// <summary>
    /// Clear all center message lines
    /// </summary>
    public void ClearAllCenterMessageLines()
    {

        // Clear the dictionary
        CenterMessageLines.Clear();
    }

    /// <summary>
    /// Get all current line IDs
    /// </summary>
    /// <returns>List of all active line IDs</returns>
    public List<int> GetActiveCenterMessageLines()
    {
        return CenterMessageLines.Keys.ToList();
    }

    /// <summary>
    /// Check if a specific line exists
    /// </summary>
    /// <param name="lineId">Line ID to check</param>
    /// <returns>True if line exists</returns>
    public bool HasCenterMessageLine(int lineId)
    {
        return CenterMessageLines.ContainsKey(lineId);
    }
    /// <summary>
    /// Extend the duration of an existing line
    /// </summary>
    /// <param name="lineId">Line ID to extend</param>
    /// <param name="additionalDuration">Additional time in seconds</param>
    public void ExtendCenterMessageLine(int lineId, float additionalDuration)
    {
        if (lineId <= 0 || !CenterMessageLines.ContainsKey(lineId)) return;
        if (additionalDuration <= 0) return;

        var existingLine = CenterMessageLines[lineId];


        // Update the line with new timer and duration
        CenterMessageLines[lineId] = (existingLine.Item1, existingLine.Item2 + additionalDuration, existingLine.Item3, existingLine.Item4);
    }

    /// <summary>
    /// Set a line to be permanent (remove auto-removal timer)
    /// </summary>
    /// <param name="lineId">Line ID to make permanent</param>
    public void MakeLinePermanent(int lineId)
    {
        if (lineId <= 0 || !CenterMessageLines.ContainsKey(lineId)) return;

        var existingLine = CenterMessageLines[lineId];

        // Update with no timer (permanent)
        CenterMessageLines[lineId] = (existingLine.Item1, 0f, existingLine.Item3, existingLine.Item4);
    }
    
    public void AddRecipientToLine(int lineId, CCSPlayerController player)
    {
        if (lineId <= 0 || !CenterMessageLines.ContainsKey(lineId) || player == null || !player.IsValid) return;

        var existingLine = CenterMessageLines[lineId];
        var recipients = existingLine.Item3 ?? new RecipientFilter();

        if (!recipients.Contains(player))
        {
            recipients.Add(player);
            CenterMessageLines[lineId] = (existingLine.Item1, existingLine.Item2, recipients, existingLine.Item4);
        }
    }
}
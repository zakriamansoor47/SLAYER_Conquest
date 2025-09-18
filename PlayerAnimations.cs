using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace SLAYER_CaptureTheFlag;


public partial class SLAYER_CaptureTheFlag : BasePlugin, IPluginConfig<SLAYER_CaptureTheFlagConfig>
{
    // Track last few used animations to avoid repetition
    private static readonly Queue<string> _recentVictoryAnimations = new Queue<string>();
    private static readonly Queue<string> _recentDefeatAnimations = new Queue<string>();
    private const int MAX_RECENT_ANIMATIONS = 3; // Remember last 3 animations

    /// <summary>
    /// Play the next animation in the list for the given pose entity
    /// </summary>
    /// <param name="poseEntity"></param>
    /// <param name="animations"></param>
    /// <param name="currentAnimation"></param>
    public void PlayNextAnimation(CDynamicProp poseEntity, List<string> animations, string currentAnimation)
    {
        if (poseEntity == null || !poseEntity.IsValid || animations == null || animations.Count == 0) return;

        // If current animation is start, remove it from list to avoid replaying
        if (currentAnimation.Contains("_start") && animations.Count > 0 && animations[0].Contains("_start"))
        {
            animations.RemoveAt(0);
        }
        
        // Find current position and play next
        int currentIndex = animations.IndexOf(currentAnimation);
        int nextIndex = currentIndex == -1 ? 0 : (currentIndex + 1) % animations.Count;
        
        if (animations.Count > 0)
        {
            poseEntity.AcceptInput("SetAnimation", value: animations[nextIndex]);
            poseEntity.IdleAnim = animations[nextIndex];
        }
    }
    public HookResult HookOnAnimationDone(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance poseEntity, CVariant value, float delay)
    {
        if(poseEntity == null || !poseEntity.IsValid) return HookResult.Continue;
        
        var entity = Utilities.GetEntityFromIndex<CDynamicProp>((int)poseEntity.Index);
        if (entity == null || !entity.IsValid) return HookResult.Continue;
        var animations = GetAnimationsListFromEntity(entity);
        if (animations == null || animations.Count == 0) return HookResult.Continue;
        PlayNextAnimation(entity, animations, entity.IdleAnim);

        return HookResult.Continue;
    }
    public List<string> GetAnimationsListFromEntity(CDynamicProp entity)
    {
        if (entity == null || !entity.IsValid) return new List<string>();

        foreach (var data in MatchStatus.PoseEntities.Values)
        {
            foreach (var poseInfo in data)
            {
                if (poseInfo.PoseEntity == entity)
                {
                    return poseInfo.Animations;
                }
            }
        }
        return new List<string>();
    }
    /// <summary>
    /// Get a random victory animation group that's different from the last one used
    /// </summary>
    /// <returns>List of victory animations in order (start first, then idle animations)</returns>
    public List<string> GetRandomVictoryAnimationGroup()
    {
        if (Config.VictoryAnimations == null || Config.VictoryAnimations.Count == 0)
            return new List<string>();

        var validAnimations = Config.VictoryAnimations
            .Where(anim => IsValidAnimationFormat(anim))
            .ToList();

        if (validAnimations.Count == 0) return new List<string>();

        var baseNames = validAnimations
            .Select(GetAnimationBaseName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Distinct()
            .ToList();

        if (baseNames.Count == 0) return new List<string>();

        // Get available animations (excluding recently used ones)
        var availableBaseNames = GetAvailableAnimations(baseNames, _recentVictoryAnimations);

        // Pick a random base name
        string selectedBaseName = availableBaseNames[_random.Next(availableBaseNames.Count)];
        
        // Track this animation as recently used
        TrackRecentAnimation(_recentVictoryAnimations, selectedBaseName);

        // Get all animations for this base name and sort them
        return validAnimations
            .Where(anim => GetAnimationBaseName(anim) == selectedBaseName)
            .OrderBy(GetAnimationOrder)
            .ToList();
    }

    /// <summary>
    /// Get a random defeat animation group that's different from the last one used
    /// </summary>
    /// <returns>List of defeat animations in order (start first, then idle animations)</returns>
    public List<string> GetRandomDefeatAnimationGroup()
    {
        if (Config.DefeatAnimations == null || Config.DefeatAnimations.Count == 0)
            return new List<string>();

        // Filter out unknown format animations first
        var validAnimations = Config.DefeatAnimations
            .Where(anim => IsValidAnimationFormat(anim))
            .ToList();

        if (validAnimations.Count == 0) return new List<string>();

        // Get all unique base names from valid animations
        var baseNames = validAnimations
            .Select(GetAnimationBaseName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Distinct()
            .ToList();
            
        if (baseNames.Count == 0) return new List<string>();

        // Get available animations (excluding recently used ones)
        var availableBaseNames = GetAvailableAnimations(baseNames, _recentDefeatAnimations);

        // Pick a random base name from available options
        string selectedBaseName = availableBaseNames[_random.Next(availableBaseNames.Count)];
        
        // Track this animation as recently used
        TrackRecentAnimation(_recentDefeatAnimations, selectedBaseName);
        
        // Get all animations for this base name and sort them
        return validAnimations
            .Where(anim => GetAnimationBaseName(anim) == selectedBaseName)
            .OrderBy(GetAnimationOrder)
            .ToList();
    }
    /// <summary>
    /// Get available animations excluding recently used ones
    /// </summary>
    private List<string> GetAvailableAnimations(List<string> allAnimations, Queue<string> recentAnimations)
    {
        // If we have enough different animations, exclude recent ones
        if (allAnimations.Count > recentAnimations.Count)
        {
            return allAnimations
                .Where(anim => !recentAnimations.Contains(anim))
                .ToList();
        }
        
        // If not enough different animations, use all available
        return allAnimations;
    }

    /// <summary>
    /// Track recently used animation
    /// </summary>
    private void TrackRecentAnimation(Queue<string> recentQueue, string animationName)
    {
        recentQueue.Enqueue(animationName);
        
        // Keep only the last MAX_RECENT_ANIMATIONS
        while (recentQueue.Count > MAX_RECENT_ANIMATIONS)
        {
            recentQueue.Dequeue();
        }
    }

    /// <summary>
    /// Group animations by their base name (prefix before _start or _idle)
    /// </summary>
    /// <param name="animations">List of animation names</param>
    /// <returns>Dictionary with base name as key and list of animations as value</returns>
    private Dictionary<string, List<string>> GroupAnimationsByBaseName(List<string> animations)
    {
        var groups = new Dictionary<string, List<string>>();

        foreach (var animation in animations)
        {
            string baseName = GetAnimationBaseName(animation);
            if (!string.IsNullOrEmpty(baseName))
            {
                if (!groups.ContainsKey(baseName))
                {
                    groups[baseName] = new List<string>();
                }
                groups[baseName].Add(animation);
            }
        }

        return groups;
    }

    /// <summary>
    /// Extract the base name from an animation (everything before _start or _idle)
    /// </summary>
    /// <param name="animationName">Full animation name</param>
    /// <returns>Base name of the animation</returns>
    private string GetAnimationBaseName(string animationName)
    {
        if (string.IsNullOrEmpty(animationName)) return string.Empty;

        // Find the position of "_start" or "_idle"
        int startIndex = animationName.IndexOf("_start");
        int idleIndex = animationName.IndexOf("_idle");

        int cutIndex = -1;
        if (startIndex >= 0 && idleIndex >= 0)
        {
            cutIndex = Math.Min(startIndex, idleIndex);
        }
        else if (startIndex >= 0)
        {
            cutIndex = startIndex;
        }
        else if (idleIndex >= 0)
        {
            cutIndex = idleIndex;
        }

        return cutIndex >= 0 ? animationName.Substring(0, cutIndex) : animationName;
    }

    /// <summary>
    /// Get the order priority for sorting animations (start = 0, idle01 = 1, idle02 = 2, etc.)
    /// </summary>
    /// <param name="animationName">Animation name</param>
    /// <returns>Order number for sorting</returns>
    private int GetAnimationOrder(string animationName)
    {
        if (string.IsNullOrEmpty(animationName)) return 999;

        // Start animations come first
        if (animationName.Contains("_start"))
        {
            return 0;
        }

        // Idle animations are ordered by their number
        if (animationName.Contains("_idle"))
        {
            // Extract number from idle (e.g., "idle01" -> 1, "idle02" -> 2)
            var idleMatch = System.Text.RegularExpressions.Regex.Match(animationName, @"_idle(\d+)");
            if (idleMatch.Success && int.TryParse(idleMatch.Groups[1].Value, out int idleNumber))
            {
                return idleNumber; // idle01 = 1, idle02 = 2, etc.
            }
        }

        return 999; // Unknown format goes to the end
    }
    /// <summary>
    /// Validation for animation format
    /// </summary>
    /// <param name="animationName">Animation name to validate</param>
    /// <returns>True if the animation follows the expected format</returns>
    private bool IsValidAnimationFormat(string animationName)
    {
        if (string.IsNullOrEmpty(animationName)) return false;
        
        // Must contain either "_start" or "_idle##"
        bool hasStart = animationName.Contains("_start");
        bool hasValidIdle = System.Text.RegularExpressions.Regex.IsMatch(animationName, @"_idle\d+");
        
        // Additional validation: ensure it has a base name before _start/_idle
        if (hasStart || hasValidIdle)
        {
            string baseName = GetAnimationBaseName(animationName);
            return !string.IsNullOrEmpty(baseName) && baseName.Length > 0;
        }
        
        return false;
    }
}
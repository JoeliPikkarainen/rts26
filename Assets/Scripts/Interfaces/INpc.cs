using UnityEngine;

/// <summary>
/// Behaviour states an NPC can operate in.
/// </summary>
public enum NpcBehaviour
{
    Idle,
    Follow,
    Gather,
    Attack,
    Defend,
    Wander
}

public enum GatherResourcePreference
{
    Closest,
    Tree,
    Rock
}

/// <summary>
/// Contract for recruitable NPC units. Attach to any NPC prefab alongside NpcController.
/// </summary>
public interface INpc
{
    string GetNpcId();
    string GetDisplayName();

    // --- Recruitment ---

    /// <summary>Recruit this NPC; assigns owner and switches to Follow.</summary>
    void Recruit(GameObject recruiter);
    /// <summary>Returns the GameObject that owns/recruited this NPC. Null = neutral.</summary>
    GameObject GetOwner();
    bool CanBeRecruited();
    bool IsNeutral();
    bool IsHostileTo(GameObject other);

    // --- Behaviour control ---

    void SetBehaviour(NpcBehaviour behaviour);
    NpcBehaviour GetCurrentBehaviour();

    // --- Task assignment ---

    /// <summary>Order the NPC to gather from a world object (e.g. a tree).</summary>
    void GatherFrom(GameObject gatherTarget);
    void SetGatherPreference(GatherResourcePreference preference);
    GatherResourcePreference GetGatherPreference();
    /// <summary>Order the NPC to attack a specific target.</summary>
    void AttackTarget(GameObject target);
}

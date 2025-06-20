using UnityEngine;

public class INPCStats : IStatSystem
{
    // You can add NPC-specific stats or logic here

    // Example: Add a stat for NPC level
    public int npcLevel = 1;

    // Override Awake if you want to add initialization logic
    protected override void Awake()
    {
        base.Awake();
        // Additional NPC-specific initialization here
    }

    // Optionally override Update if you want per-frame logic
    void Update()
    {
        // NPC-specific update logic here
    }
}

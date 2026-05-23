/// <summary>
/// All SFX identifiers. Add new entries here when new sounds are needed.
/// Each value maps to a SoundEntry in the SoundData ScriptableObject.
/// </summary>
public enum SoundType
{
    // ─── Player ───────────────────────────────────────────────────────────────
    KickSuccess,        // Player lands a valid kick on the pipe
    KickMiss,           // Player swipes but kick window closes with no contact
    PlayerDamage,       // Pipe hits player (non-lethal)
    PlayerDeath,        // Player dies — lethal pipe or last heart

    // ─── Pipe ─────────────────────────────────────────────────────────────────
    PipeHitPlayer,      // Pipe strikes the player
    PipeHitEnemy,       // Pipe strikes an enemy
    SecondPipeWarning,  // Second pipe activates — alert the player

    // ─── Enemy ────────────────────────────────────────────────────────────────
    EnemySpawn,         // Enemy enters the arena
    EnemyDamage,        // Enemy takes a hit from the pipe
    EnemyDeath,         // Enemy health reaches zero

    // ─── Game State ───────────────────────────────────────────────────────────
    GameOver,           // Game ends

    // ─── UI ───────────────────────────────────────────────────────────────────
    UIClick,            // Any button press
}

/// <summary>
/// All music track identifiers.
/// </summary>
public enum MusicType
{
    MainMenu,
    Gameplay,
}
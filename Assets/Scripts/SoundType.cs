/// <summary>
/// All SFX identifiers used by SoundManager.PlaySFX().
/// Add a new entry here and a matching SoundEntry in the SoundData ScriptableObject
/// when a new sound is needed. No other code changes are required.
/// </summary>
public enum SoundType
{
    // ─── Player ───────────────────────────────────────────────────────────────
    KickAttempt,        // Player or enemy initiates a kick (regardless of outcome)
    KickSuccess,        // Player lands a valid kick on the pipe
    KickMiss,           // Player swipes but the kick window closes with no contact
    PlayerDamage,       // Pipe hits player (non-lethal)
    PlayerDeath,        // Player dies — lethal pipe or last heart
    PlayerJump,         // Player jumps

    // ─── Pipe ─────────────────────────────────────────────────────────────────
    PipeHitPlayer,      // Pipe strikes the player
    PipeHitEnemy,       // Pipe strikes an enemy
    SecondPipeWarning,  // Second pipe activates — alerts the player

    // ─── Enemy ────────────────────────────────────────────────────────────────
    EnemySpawn,         // Enemy enters the arena
    EnemyDamage,        // Enemy takes a hit from the pipe
    EnemyDeath,         // Enemy health reaches zero

    // ─── Consumables ──────────────────────────────────────────────────────────
    ConsumablePickup,   // Player collects any consumable
    ConsumableSpawn,    // A consumable appears in the world
    ShieldBreak,        // Player's shield absorbs a hit and shatters

    // ─── Game State ───────────────────────────────────────────────────────────
    GameOver,           // Run ends

    // ─── UI ───────────────────────────────────────────────────────────────────
    UIClick,            // Any button or toggle interaction
}

/// <summary>
/// All music track identifiers used by SoundManager.PlayMusic().
/// Add a new entry here and a matching MusicEntry in SoundData for each new track.
/// </summary>
public enum MusicType
{
    MainMenu,
    Gameplay,
}
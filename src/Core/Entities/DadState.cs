namespace ZTown.Core.Entities;

/// <summary>Where Dad's at. He can be hurt (he's not memere); if he dies it's a failure like the player dying.</summary>
public enum DadState
{
    /// <summary>Out of it at his place when you find him.</summary>
    OutOfIt,
    /// <summary>Awake and talking, staying put.</summary>
    Awake,
    /// <summary>Coming with you.</summary>
    Following,
    /// <summary>Told to wait where he is.</summary>
    Waiting,
    /// <summary>At memere's, staying there.</summary>
    Home,
}

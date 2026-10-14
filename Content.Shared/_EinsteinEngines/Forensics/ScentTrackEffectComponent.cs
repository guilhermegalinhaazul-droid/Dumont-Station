namespace Content.Shared._EinsteinEngines.Forensics;

/// <summary>
/// Marks a scent trail marker as a local visual owned by one tracker.
/// It is intentionally not networked: scent trails must never become shared
/// world entities visible to observers who did not track the scent.
/// </summary>
[RegisterComponent]
public sealed partial class ScentTrackEffectComponent : Component
{
    public EntityUid Tracker;
    public string Scent = string.Empty;
}

using System.Globalization;
using System.Text;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Simulation.Baseball;

namespace StarPlayer.SimHarness;

/// <summary>
/// The counters every metric is computed from. Plate-appearance counters are written ONLY from the <see cref="PlateAppearanceCompleted"/> facts of confirmed commits; pitch counters ONLY
/// from the pitch-carrying events of confirmed commits (<see cref="PitchEvent"/>, <see cref="TakeEvent"/>, <see cref="InPlayEvent"/>). Nothing here reads the reducer's stat entries, so
/// <see cref="BoxScoreCrossCheck"/> compares two derivations that share no code beyond the committed events themselves.
/// </summary>
public sealed class MetricCounters
{
    // plate appearances (from the PlateAppearanceCompleted facts)
    public long PlateAppearances { get; set; }
    public long AtBats { get; set; }
    public long Hits { get; set; }
    public long Singles { get; set; }
    public long Doubles { get; set; }
    public long Triples { get; set; }
    public long HomeRuns { get; set; }
    public long Walks { get; set; }
    public long HitByPitch { get; set; }
    public long Strikeouts { get; set; }
    public long SacrificeFlies { get; set; }
    public long SacrificeBunts { get; set; }

    // actual pitches (from the events): one count per pitch however many records describe it
    public long Pitches { get; set; }
    public long LocatedPitches { get; set; }
    public long UnlocatedPitches { get; set; }
    public long ZonePitches { get; set; }
    public long HitByPitchPitches { get; set; }
    public long Swings { get; set; }
    public long SwingingStrikes { get; set; }
    public long Fouls { get; set; }
    public long InPlayPitches { get; set; }
    public long CalledBalls { get; set; }
    public long CalledStrikes { get; set; }
    public long Takes { get; set; }

    // the same facts read a second way, from the in-play events themselves (an extra internal consistency check)
    public long EventHomeRuns { get; set; }
    public long EventHits { get; set; }

    // housekeeping
    public long Events { get; set; }
    public long DuplicatesIgnored { get; set; }

    // every counter, in ordinal name order, so no text below depends on reflection order
    private static readonly System.Reflection.PropertyInfo[] All = typeof(MetricCounters).GetProperties().OrderBy(p => p.Name, StringComparer.Ordinal).ToArray();

    public void Add(MetricCounters other)
    {
        foreach (var property in All)
            property.SetValue(this, checked((long)property.GetValue(this)! + (long)property.GetValue(other)!));
    }

    /// <summary>A stable text of every counter in ordinal name order (used for hashing; no time, no culture).</summary>
    public string Canonical()
    {
        var text = new StringBuilder();
        foreach (var property in All)
            text.Append(property.Name).Append('=').Append(((long)property.GetValue(this)!).ToString(CultureInfo.InvariantCulture)).Append(';');
        return text.ToString();
    }
}

public enum ObserveOutcome { Counted, DuplicateIgnored, SequenceGap }

/// <summary>
/// Turns the confirmed commits of ONE game into <see cref="MetricCounters"/>. Only a <see cref="CommitRecord"/> returned by a successful <c>GameCursor.Publish</c> may be passed in: a staged
/// but unpublished commit, a rejected or stale one, a preview and an unconfirmed candidate have no <see cref="CommitRecord"/> in the caller's hands and so can never be counted. An event
/// sequence already seen is ignored (and counted as a duplicate), so the same pitch is never counted twice; a skipped sequence is reported, never repaired.
/// </summary>
public sealed class MetricsRecorder
{
    private int _lastSequence;

    public MetricCounters Counters { get; } = new();

    /// <summary>The only production entry: a <see cref="CommitRecord"/> exists only for a commit that <c>GameCursor.Publish</c> made official.</summary>
    public ObserveOutcome Observe(CommitRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return Observe(record.Event, record.EventSequence, record.Facts);
    }

    /// <summary>The counting itself, on plain events and facts, so a hand-written fixture can be counted without the engine (visible to the harness tests only).</summary>
    internal ObserveOutcome Observe(GameEvent gameEvent, int sequence, IEnumerable<GameFact> facts)
    {
        if (sequence <= _lastSequence)
        {
            Counters.DuplicatesIgnored++;
            return ObserveOutcome.DuplicateIgnored;
        }

        if (sequence != _lastSequence + 1)
            return ObserveOutcome.SequenceGap;
        _lastSequence = sequence;
        Counters.Events++;

        switch (gameEvent)
        {
            case PitchEvent pitch:
                Pitch(pitch.Spec, false);
                break;
            case TakeEvent take:
                Pitch(take.Spec, true);
                break;
            case InPlayEvent inPlay:
                InPlay(inPlay);
                break;
        }

        foreach (var fact in facts)
            if (fact is PlateAppearanceCompleted done)
                PlateAppearance(done.Result);
        return ObserveOutcome.Counted;
    }

    private void Pitch(PitchSpec spec, bool take)
    {
        var c = Counters;
        c.Pitches++;
        if (take)
            c.Takes++;
        Located(spec.Location);
        switch (spec.Outcome)
        {
            case PitchOutcome.Ball:
                c.CalledBalls++;
                break;
            case PitchOutcome.CalledStrike:
                c.CalledStrikes++;
                break;
            case PitchOutcome.SwingingStrike:
                c.Swings++;
                c.SwingingStrikes++;
                break;
            case PitchOutcome.Foul or PitchOutcome.FoulBunt:
                c.Swings++;
                c.Fouls++;
                break;
            case PitchOutcome.HitByPitch:
                c.HitByPitchPitches++;
                break;
        }
    }

    private void InPlay(InPlayEvent e)
    {
        var c = Counters;
        c.Pitches++;
        c.Swings++;
        c.InPlayPitches++;
        Located(e.Location);
        if (e.Result == InPlayResult.HomeRun)
            c.EventHomeRuns++;
        if (e.Result is InPlayResult.Single or InPlayResult.Double or InPlayResult.Triple or InPlayResult.HomeRun)
            c.EventHits++;
    }

    private void Located(PitchLocation? location)
    {
        if (location is null)
        {
            Counters.UnlocatedPitches++;
            return;
        }

        Counters.LocatedPitches++;
        // the ACTUAL location, never the target zone: a PitchLocation cannot hold a target
        if (PitchZoneModel.Resolve(location.Model).Value.Info(location.Zone).Value.Region == PitchZoneRegion.Inner)
            Counters.ZonePitches++;
    }

    private void PlateAppearance(BatterResult result)
    {
        var c = Counters;
        c.PlateAppearances++;
        // official at-bat: every plate appearance except a walk, hit by pitch, sacrifice fly or sacrifice hit
        if (result is not (BatterResult.Walk or BatterResult.HitByPitch or BatterResult.SacrificeFly or BatterResult.SacrificeBunt))
            c.AtBats++;
        switch (result)
        {
            case BatterResult.Single: c.Hits++; c.Singles++; break;
            case BatterResult.Double: c.Hits++; c.Doubles++; break;
            case BatterResult.Triple: c.Hits++; c.Triples++; break;
            case BatterResult.HomeRun: c.Hits++; c.HomeRuns++; break;
            case BatterResult.Walk: c.Walks++; break;
            case BatterResult.HitByPitch: c.HitByPitch++; break;
            case BatterResult.Strikeout or BatterResult.StrikeoutReachedBase: c.Strikeouts++; break;
            case BatterResult.SacrificeFly: c.SacrificeFlies++; break;
            case BatterResult.SacrificeBunt: c.SacrificeBunts++; break;
        }
    }
}

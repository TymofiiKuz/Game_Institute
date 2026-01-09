using UnityEngine;

[System.Serializable]
public class RegionModifierDefinition
{
    public string name;
    public float durationSeconds = 10f;
    public float tickIntervalSeconds = 1f;
    public int influencePerTick;
    public int stabilityPerTick;
    public int developmentPerTick;

    public RegionModifier CreateRuntimeModifier()
    {
        return new RegionModifier(this);
    }
}

[System.Serializable]
public class RegionModifier
{
    public string Name { get; private set; }
    public float DurationSeconds { get; private set; }
    public float TickIntervalSeconds { get; private set; }
    public int InfluencePerTick { get; private set; }
    public int StabilityPerTick { get; private set; }
    public int DevelopmentPerTick { get; private set; }

    private float remainingDuration;
    private float tickAccumulator;

    public float RemainingDuration => float.IsPositiveInfinity(remainingDuration) ? -1f : remainingDuration;

    public RegionModifier(RegionModifierDefinition definition)
    {
        if (definition == null)
        {
            Name = "Undefined";
            DurationSeconds = 0f;
            TickIntervalSeconds = 1f;
            InfluencePerTick = 0;
            StabilityPerTick = 0;
            DevelopmentPerTick = 0;
        }
        else
        {
            Name = string.IsNullOrEmpty(definition.name) ? "Modifier" : definition.name;
            DurationSeconds = Mathf.Max(0f, definition.durationSeconds);
            TickIntervalSeconds = Mathf.Max(0.1f, definition.tickIntervalSeconds <= 0f ? 1f : definition.tickIntervalSeconds);
            InfluencePerTick = definition.influencePerTick;
            StabilityPerTick = definition.stabilityPerTick;
            DevelopmentPerTick = definition.developmentPerTick;
        }

        Restart();
    }

    public void Restart()
    {
        remainingDuration = DurationSeconds > 0f ? DurationSeconds : Mathf.Infinity;
        tickAccumulator = 0f;
    }

    public bool Update(float deltaTime, Region region)
    {
        if (region == null)
            return true;

        if (remainingDuration <= 0f)
            return true;

        float interval = Mathf.Max(0.1f, TickIntervalSeconds);

        if (float.IsPositiveInfinity(remainingDuration))
        {
            tickAccumulator += deltaTime;
            ApplyTicks(region, interval);
            return false;
        }

        float usableTime = Mathf.Min(deltaTime, remainingDuration);
        tickAccumulator += usableTime;
        remainingDuration = Mathf.Max(0f, remainingDuration - deltaTime);

        ApplyTicks(region, interval);

        return remainingDuration <= 0f;
    }

    private void ApplyTicks(Region region, float interval)
    {
        while (tickAccumulator >= interval)
        {
            tickAccumulator -= interval;
            Apply(region);

            if (!float.IsPositiveInfinity(remainingDuration) && remainingDuration <= 0f)
                break;
        }
    }

    private void Apply(Region region)
    {
        if (InfluencePerTick != 0)
            region.ChangeInfluence(InfluencePerTick);
        if (StabilityPerTick != 0)
            region.ChangeStability(StabilityPerTick);
        if (DevelopmentPerTick != 0)
            region.ChangeDevelopment(DevelopmentPerTick);
    }
}


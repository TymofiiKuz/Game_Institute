using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Region
{
    public string Name;
    public int Influence;
    public int Stability;
    public int Development;
    private readonly List<RegionModifier> _activeModifiers = new List<RegionModifier>();
    private readonly List<Region> _neighbors = new List<Region>();
    public event Action<Region> StatsChanged;
    [NonSerialized] private Continent _continent;
    public Continent Continent
    {
        get => _continent;
        internal set => _continent = value;
    }

    public IReadOnlyList<RegionModifier> ActiveModifiers => _activeModifiers;
    public IReadOnlyList<Region> Neighbors => _neighbors;

    public Region(string name)
    {
        Name = name;
        Influence = UnityEngine.Random.Range(1, 11);
        Stability = UnityEngine.Random.Range(1, 11);
        Development = UnityEngine.Random.Range(1, 11);
    }

    public void AddModifier(RegionModifier modifier)
    {
        if (modifier == null) return;
        modifier.Restart();
        _activeModifiers.Add(modifier);
    }

    internal void ClearNeighbors()
    {
        _neighbors.Clear();
    }

    internal bool AddNeighbor(Region neighbor)
    {
        if (neighbor == null || neighbor == this)
            return false;

        if (_neighbors.Contains(neighbor))
            return false;

        _neighbors.Add(neighbor);
        return true;
    }

    public bool IsNeighbor(Region region)
    {
        if (region == null)
            return false;

        return _neighbors.Contains(region);
    }

    public void UpdateModifiers(float deltaTime)
    {
        if (_activeModifiers.Count == 0) return;
        for (int i = _activeModifiers.Count - 1; i >= 0; i--)
        {
            RegionModifier modifier = _activeModifiers[i];
            if (modifier.Update(deltaTime, this))
                _activeModifiers.RemoveAt(i);
        }
    }

    public void ChangeInfluence(int delta)
    {
        Influence = Mathf.Clamp(Influence + delta, 0, 20);
        NotifyStatsChanged();
    }

    public void ChangeStability(int delta)
    {
        Stability = Mathf.Clamp(Stability + delta, 0, 20);
        NotifyStatsChanged();
    }

    public void ChangeDevelopment(int delta)
    {
        Development = Mathf.Clamp(Development + delta, 0, 20);
        NotifyStatsChanged();
    }

    // Удобный текст для тултипа
    public string GetTooltip()
    {
        string continentName = Continent != null ? Continent.Name : "Unknown";
        return $"Continent: {continentName}\n" +
               $"Region: {Name}\n" +
               $"Influence: {Influence}\n" +
               $"Stability: {Stability}\n" +
               $"Development: {Development}\n" +
               $"Neighbors: {_neighbors.Count}";
    }

    public void NotifyStatsChanged()
    {
        StatsChanged?.Invoke(this);
    }
}

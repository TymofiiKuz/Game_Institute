using System;
using System.Collections.Generic;
using UnityEngine;

public class Continent
{
    public string Name;
    public Vector2 Center;
    [NonSerialized] public List<Region> Regions = new List<Region>();
    [NonSerialized] public List<RegionConnection> Connections = new List<RegionConnection>();

    public Continent(string name, Vector2 center)
    {
        Name = name;
        Center = center;
    }

    public void AddRegion(Region region)
    {
        if (region == null || Regions.Contains(region))
            return;

        Regions.Add(region);
        region.Continent = this;
    }

    public void AddConnection(Region a, Region b, float distance)
    {
        if (a == null || b == null || a == b)
            return;

        if (HasConnection(a, b))
            return;

        Connections.Add(new RegionConnection(a, b, distance));
    }

    public bool HasConnection(Region a, Region b)
    {
        for (int i = 0; i < Connections.Count; i++)
        {
            RegionConnection connection = Connections[i];
            if (connection == null)
                continue;

            if ((connection.A == a && connection.B == b) || (connection.A == b && connection.B == a))
                return true;
        }

        return false;
    }
}

public class RegionConnection
{
    public Region A;
    public Region B;
    public float Distance;

    public RegionConnection(Region a, Region b, float distance)
    {
        A = a;
        B = b;
        Distance = distance;
    }
}

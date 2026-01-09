using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public enum EventScope
{
    Local,
    Global,
    Personal
}

[System.Serializable]
public class EventOption
{
    public string text;
    public int sanityRequired;
    public int moneyRequired;
    public int artifactsRequired;
    public int influenceChange;
    public int stabilityChange;
    public int developmentChange;
    public List<RegionModifierDefinition> modifiers;
    public int minInfluenceRequired;
    public int minDevelopmentRequired;
    public int sanityChange;
    public int moneyChange;
    public int artifactsChange;
}

[System.Serializable]
public class FeaturedPerson
{
    public string name;
    public string position;
    public string gender;
}

[System.Serializable]
public class GameEvent
{
    public int id;
    public string description;
    public List<EventOption> options;
    public bool isBadEvent;
    public EventScope scope = EventScope.Local;
    public List<FeaturedPerson> featuredPeople = new List<FeaturedPerson>();
}

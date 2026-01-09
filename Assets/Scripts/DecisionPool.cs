using System.Collections.Generic;
using System.IO;
using UnityEngine;

[System.Serializable]
public class DecisionDefinition
{
    public string id;
    public string displayName;

    public int sanityCost;
    public int moneyCost;
    public int artifactsCost;

    public int sanityGain;
    public int moneyGain;
    public int artifactsGain;

    public int influenceDelta;
    public int stabilityDelta;
    public int developmentDelta;

    public float cooldownSeconds = 3f;
}

public class DecisionPool : MonoBehaviour
{
    public static DecisionPool Instance { get; private set; }

    [Tooltip("Predefined decision templates that ActionButtons can use.")]
    public List<DecisionDefinition> decisions = new List<DecisionDefinition>();
    public string decisionsFileName = "decisions.json";

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        LoadDecisions();
    }

    public DecisionDefinition GetDecisionById(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        for (int i = 0; i < decisions.Count; i++)
        {
            if (decisions[i] != null && decisions[i].id == id)
                return decisions[i];
        }
        return null;
    }

    public DecisionDefinition GetRandomDecision()
    {
        if (decisions == null || decisions.Count == 0)
            return null;

        int index = Random.Range(0, decisions.Count);
        return decisions[index];
    }

    void LoadDecisions()
    {
        string path = Path.Combine(Application.streamingAssetsPath, decisionsFileName);

        if (!File.Exists(path))
        {
            Debug.LogWarning($"DecisionPool: file not found at {path}");
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            DecisionCollection collection = JsonUtility.FromJson<DecisionCollection>(WrapJson(json));
            if (collection != null && collection.decisions != null)
            {
                decisions = collection.decisions;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("DecisionPool: failed to load decisions - " + ex.Message);
        }
    }

    string WrapJson(string rawArray)
    {
        return "{\"decisions\":" + rawArray + "}";
    }

    [System.Serializable]
    class DecisionCollection
    {
        public List<DecisionDefinition> decisions;
    }
}

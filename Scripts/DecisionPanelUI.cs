using UnityEngine;
using UnityEngine.UI;

public class DecisionPanelUI : MonoBehaviour
{
    public Transform contentParent;
    public GameObject decisionButtonPrefab;

    bool hasPopulated;

    void Start()
    {
        Populate();
    }

    void OnEnable()
    {
        hasPopulated = false;
        Populate();
    }

    void Update()
    {
        if (!hasPopulated)
            Populate();
    }

    void Populate()
    {
        if (contentParent == null || decisionButtonPrefab == null)
        {
            Debug.LogWarning("DecisionPanelUI: content parent or prefab not assigned");
            return;
        }

        foreach (Transform child in contentParent)
            Destroy(child.gameObject);

        if (DecisionPool.Instance == null)
        {
            Debug.Log("DecisionPanelUI: waiting for DecisionPool instance...");
            return;
        }

        if (DecisionPool.Instance.decisions == null || DecisionPool.Instance.decisions.Count == 0)
        {
            Debug.Log("DecisionPanelUI: DecisionPool has no decisions yet");
            return;
        }

        var decisions = DecisionPool.Instance.decisions;
        Debug.Log("DecisionPanelUI: populating " + decisions.Count + " decisions");
        for (int i = 0; i < decisions.Count; i++)
        {
            DecisionDefinition def = decisions[i];
            if (def == null)
                continue;

            GameObject buttonObj = Instantiate(decisionButtonPrefab, contentParent);
            ActionButton actionButton = buttonObj.GetComponent<ActionButton>();

            if (actionButton != null)
            {
                // Drive the UI and click handling via ActionButton so label and applied definition stay in sync.
                actionButton.assignFromPool = false;
                actionButton.ApplyDefinition(def);
            }
            else
            {
                Button button = buttonObj.GetComponent<Button>();
                Text label = buttonObj.GetComponentInChildren<Text>();
                if (label != null)
                    label.text = def.displayName;

                if (button != null)
                {
                    DecisionDefinition captured = def;
                    button.onClick.AddListener(() => OnDecisionSelected(captured));
                }
            }
        }

        hasPopulated = true;
    }

    void OnDecisionSelected(DecisionDefinition definition)
    {
        if (definition == null)
            return;

        if (DecisionSelectionManager.Instance != null)
        {
            DecisionSelectionManager.Instance.SelectDecision(definition);
        }
    }
}

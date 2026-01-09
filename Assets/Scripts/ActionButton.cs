using UnityEngine;
using UnityEngine.UI;

public class ActionButton : MonoBehaviour
{
    [Header("Visuals")]
    public string actionName = "Action";
    public Text labelText;

    [Header("Decision Pool")]
    public bool assignFromPool;
    public string decisionId;

    [Header("Costs")]
    public int sanityCost;
    public int moneyCost;
    public int artifactsCost;

    [Header("Rewards")]
    public int sanityGain;
    public int moneyGain;
    public int artifactsGain;

    [Header("Region Stat Changes")]
    public int influenceDelta;
    public int stabilityDelta;
    public int developmentDelta;

    [Header("Timing")]
    public float cooldownSeconds = 3f;

    Button btn;
    bool definitionAssigned;
    DecisionDefinition assignedDefinition;

    void Awake()
    {
        btn = GetComponent<Button>();
        if (labelText == null)
            labelText = GetComponentInChildren<Text>();
        definitionAssigned = !assignFromPool;
    }

    void OnEnable()
    {
        if (btn != null)
        {
            btn.onClick.RemoveListener(OnClick);
            btn.onClick.AddListener(OnClick);
        }
        TryAssignFromPool();
        UpdateLabel();
        UpdateInteractable();
    }

    void OnDisable()
    {
        if (btn != null)
            btn.onClick.RemoveListener(OnClick);
    }

    void Update()
    {
        if (assignFromPool && !definitionAssigned)
            TryAssignFromPool();

        UpdateInteractable();
    }

    void TryAssignFromPool()
    {
        if (!assignFromPool || DecisionPool.Instance == null)
            return;

        DecisionDefinition definition = null;

        if (!string.IsNullOrEmpty(decisionId))
            definition = DecisionPool.Instance.GetDecisionById(decisionId);

        if (definition == null)
            definition = DecisionPool.Instance.GetRandomDecision();

        if (definition != null)
            ApplyDefinition(definition);
    }

    public void ApplyDefinition(DecisionDefinition definition)
    {
        if (definition == null)
        {
            definitionAssigned = false;
            return;
        }

        assignedDefinition = definition;

        actionName = definition.displayName;
        sanityCost = definition.sanityCost;
        moneyCost = definition.moneyCost;
        artifactsCost = definition.artifactsCost;
        sanityGain = definition.sanityGain;
        moneyGain = definition.moneyGain;
        artifactsGain = definition.artifactsGain;
        influenceDelta = definition.influenceDelta;
        stabilityDelta = definition.stabilityDelta;
        developmentDelta = definition.developmentDelta;
        cooldownSeconds = definition.cooldownSeconds;
        decisionId = definition.id;
        definitionAssigned = true;

        UpdateLabel();

        // Привязываем тултип
        var tt = GetComponent<TooltipTrigger>();
        if (tt != null)
        {
            string cooldownText = definition.cooldownSeconds > 0f
                ? DecisionSelectionManager.AffectsRegion(definition)
                    ? $"Cooldown: {definition.cooldownSeconds}s (per region)"
                    : $"Cooldown: {definition.cooldownSeconds}s"
                : "Cooldown: none";
            tt.message =
                $"{definition.displayName}\n\n" +
                $"Sanity: {definition.sanityCost} / +{definition.sanityGain}\n" +
                $"Money: {definition.moneyCost} / +{definition.moneyGain}\n" +
                $"Artifacts: {definition.artifactsCost} / +{definition.artifactsGain}\n" +
                $"Influence Δ: {definition.influenceDelta}, Stability Δ: {definition.stabilityDelta}, Dev Δ: {definition.developmentDelta}\n" +
                cooldownText;
        }
    }

    void OnClick()
    {
        if (DecisionSelectionManager.Instance == null)
            return;

        DecisionDefinition definition = BuildCurrentDefinition();
        if (definition == null)
            return;

        DecisionSelectionManager.Instance.SelectDecision(definition, this);
    }

    DecisionDefinition BuildCurrentDefinition()
    {
        // Always build a fresh snapshot so the applied decision matches the visible button state.
        return new DecisionDefinition
        {
            id = !string.IsNullOrEmpty(decisionId) ? decisionId : actionName,
            displayName = actionName,
            sanityCost = sanityCost,
            moneyCost = moneyCost,
            artifactsCost = artifactsCost,
            sanityGain = sanityGain,
            moneyGain = moneyGain,
            artifactsGain = artifactsGain,
            influenceDelta = influenceDelta,
            stabilityDelta = stabilityDelta,
            developmentDelta = developmentDelta,
            cooldownSeconds = cooldownSeconds
        };
    }

    // мгновенное применение (без региона)
    public bool ExecuteAction()
    {
        var controller = LevelController.Instance;
        if (controller == null)
            return false;

        if (!HasResources(controller))
            return false;

        SpendResources(controller);
        ApplyRewards(controller);
        return true;
    }

    // применение к региону
    public bool ExecuteActionOnRegion(Region region)
    {
        var controller = LevelController.Instance;
        if (controller == null || region == null) return false;

        if (!HasResources(controller)) return false;

        SpendResources(controller);
        ApplyRewards(controller);

        if (influenceDelta != 0)   region.ChangeInfluence(influenceDelta);
        if (stabilityDelta != 0)   region.ChangeStability(stabilityDelta);
        if (developmentDelta != 0) region.ChangeDevelopment(developmentDelta);

        if (UIManager.Instance != null)
            UIManager.Instance.UpdateRegionInfo(region);

        return true;
    }

    bool HasResources(LevelController controller)
    {
        return controller.Sanity >= sanityCost &&
               controller.Money >= moneyCost &&
               controller.Artifacts >= artifactsCost;
    }

    void SpendResources(LevelController controller)
    {
        if (sanityCost != 0)
            controller.ChangeSanity(-sanityCost);
        if (moneyCost != 0)
            controller.ChangeMoney(-moneyCost);
        if (artifactsCost != 0)
            controller.ChangeArtifacts(-artifactsCost);
    }

    void ApplyRewards(LevelController controller)
    {
        if (sanityGain != 0)
            controller.ChangeSanity(sanityGain);
        if (moneyGain != 0)
            controller.ChangeMoney(moneyGain);
        if (artifactsGain != 0)
            controller.ChangeArtifacts(artifactsGain);
    }

    void UpdateLabel()
    {
        if (labelText != null)
            labelText.text = actionName;
    }

    void UpdateInteractable()
    {
        if (btn == null)
            return;

        bool hasDefinition = assignedDefinition != null;
        bool requiresRegion = RequiresRegion();
        Region selectedRegion = null;

        bool canUse = hasDefinition;

        if (requiresRegion)
        {
            selectedRegion = LevelController.Instance != null ? LevelController.Instance.SelectedRegion : null;
            canUse = canUse && selectedRegion != null;
        }

        var selectionManager = DecisionSelectionManager.Instance;
        if (canUse && selectionManager != null && assignedDefinition != null)
        {
            if (selectionManager.IsOnCooldown(assignedDefinition, selectedRegion))
                canUse = false;
        }

        if (canUse && LevelController.Instance != null)
            canUse = HasResources(LevelController.Instance);

        btn.interactable = canUse;
    }

    bool RequiresRegion()
    {
        if (assignedDefinition != null)
            return DecisionSelectionManager.AffectsRegion(assignedDefinition);

        return influenceDelta != 0 || stabilityDelta != 0 || developmentDelta != 0;
    }
}

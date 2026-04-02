using System;

public class GameStateManager
{
    public enum DemoStep
    {
        FindIngredientSmell = 0,
        OpenDoor = 1,
        Completed = 2,
    }

    private const string IngredientSmellKey = "Ingredient";
    private const string DoorKey = "Door";
    private const string DoorHandleKey = "Door Handle";

    public DemoStep CurrentStep { get; private set; } = DemoStep.FindIngredientSmell;

    public event Action<DemoStep> OnStepChanged;

    public void Init()
    {
        Clear();
    }

    public void Clear()
    {
        CurrentStep = DemoStep.FindIngredientSmell;
    }

    public bool IsDoorProgressBlocked(string interactionKey)
    {
        if (!IsDoorInteraction(interactionKey))
            return false;

        return CurrentStep == DemoStep.FindIngredientSmell;
    }

    public void NotifySmellFound(string smellKey)
    {
        if (CurrentStep != DemoStep.FindIngredientSmell)
            return;

        if (!string.Equals(smellKey, IngredientSmellKey, StringComparison.OrdinalIgnoreCase))
            return;

        SetStep(DemoStep.OpenDoor);
    }

    public void NotifyInteractionSuccess(string interactionKey)
    {
        if (CurrentStep != DemoStep.OpenDoor)
            return;

        if (!IsDoorInteraction(interactionKey))
            return;

        SetStep(DemoStep.Completed);
    }

    private static bool IsDoorInteraction(string interactionKey)
    {
        return string.Equals(interactionKey, DoorKey, StringComparison.OrdinalIgnoreCase)
            || string.Equals(interactionKey, DoorHandleKey, StringComparison.OrdinalIgnoreCase);
    }

    private void SetStep(DemoStep step)
    {
        if (CurrentStep == step)
            return;

        CurrentStep = step;
        OnStepChanged?.Invoke(CurrentStep);
    }
}

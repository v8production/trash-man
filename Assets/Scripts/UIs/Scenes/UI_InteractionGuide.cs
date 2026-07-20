using UnityEngine;

public class UI_InteractionGuide : UI_Base
{
    enum GameObjects
    {
        InteractionGuide,
    }

    private RectTransform _interactionGuideRectTransform;
    private bool _initialized;

    private void Awake()
    {
        Init();
    }

    public override void Init()
    {
        if (_initialized)
            return;

        _initialized = true;
        Bind<GameObject>(typeof(GameObjects));
        _interactionGuideRectTransform = GetObject((int)GameObjects.InteractionGuide).transform as RectTransform;
        _interactionGuideRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        _interactionGuideRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        Managers.UI.ShowCanvas(gameObject, false);
    }

    public void SetScreenCenter()
    {
        Init();
        _interactionGuideRectTransform.anchoredPosition = Vector2.zero;
    }

    public void Hide() => gameObject.SetActive(false);
    public void Show() => gameObject.SetActive(true);
}

using System;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_Controls : UI_Menu
{
    public event Action Closed;

    enum Images
    {
        Background,
    }

    enum Buttons
    {
        Back,
    }

    enum Texts
    {
        Back,
        Controls,
    }

    public override void Init()
    {
        base.Init();
        Bind<Image>(typeof(Images));
        Bind<Button>(typeof(Buttons));
        Bind<TextMeshProUGUI>(typeof(Texts));

        GetButton((int)Buttons.Back).gameObject.BindEvent(OnBackButtonClicked);
    }

    private void OnDestroy()
    {
        Closed = null;
    }

    private void OnBackButtonClicked(PointerEventData eventData)
    {
        Closed?.Invoke();
    }
}

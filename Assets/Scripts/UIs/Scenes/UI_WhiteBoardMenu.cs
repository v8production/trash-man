using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_WhiteBoardMenu : UI_Menu
{
    private const int CanvasOrder = 10;

    private enum GameObjects
    {
        Background,
    }

    private enum Buttons
    {
        Cancel,
        PenButton,
        RedButton,
        YellowButton,
        GreenButton,
        BlueButton,
        BlackButton,
        WhiteButton,
        EraserButton,
    }

    enum Sliders
    {
        PenBorder,
        EraserBorder,
    }

    enum Images
    {
        Cursor,
    }
    public event Action Closed;

    public override void Init()
    {

        base.Init();
        Managers.UI.ShowCanvas(gameObject, CanvasOrder);
        Bind<GameObject>(typeof(GameObjects));
        Bind<Button>(typeof(Buttons));
        Bind<Slider>(typeof(Sliders));
        Bind<Image>(typeof(Images));

        GetButton((int)Buttons.Cancel).gameObject.BindEvent(OnCancelClicked);
    }

    private void Update()
    {
    }

    private void OnDestroy()
    {
        Closed = null;
    }

    private void OnCancelClicked(PointerEventData eventData)
    {
        Closed?.Invoke();
    }
}

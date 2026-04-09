using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_Quit : UI_Scene
{

    enum Buttons
    {
        Quit,
    }

    enum Texts
    {
        Text,
    }

    public override void Init()
    {
        base.Init();
        Bind<Button>(typeof(Buttons));
        Bind<TextMeshProUGUI>(typeof(Texts));

        GetButton((int)Buttons.Quit).gameObject.BindEvent(OnButtonClicked);
    }

    public void OnButtonClicked(PointerEventData eventData)
    {
        Debug.Log("Join Click");
    }
}

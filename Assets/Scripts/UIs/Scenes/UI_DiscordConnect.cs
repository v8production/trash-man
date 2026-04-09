using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_DiscordConnect : UI_Scene
{

    enum Buttons
    {
        DiscordConnect,
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

        GetButton((int)Buttons.DiscordConnect).gameObject.BindEvent(OnButtonClicked);
    }

    public void OnButtonClicked(PointerEventData eventData)
    {
        Debug.Log("Discord Click");
    }
}

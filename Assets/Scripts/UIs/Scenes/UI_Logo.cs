using UnityEngine;
using UnityEngine.UI;

public class UI_Logo : UI_Scene
{

    enum Images
    {
        Logo,
    }
    public override void Init()
    {
        base.Init();
        Bind<Image>(typeof(Images));
    }
}
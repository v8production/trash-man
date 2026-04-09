using UnityEngine;
using UnityEngine.UI;

public class UI_Background : UI_Scene
{
    enum Images
    {
        Background,
    }

    public override void Init()
    {
        base.Init();
        Bind<Image>(typeof(Images));
    }
}

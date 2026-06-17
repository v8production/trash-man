using UnityEngine.UI;

public class UI_MainScreen : UI_Base
{
    private enum Images
    {
        Image,
    }

    private bool _isInitialized;

    public override void Init()
    {
        if (_isInitialized)
            return;

        Bind<Image>(typeof(Images));
        _isInitialized = true;
    }
}

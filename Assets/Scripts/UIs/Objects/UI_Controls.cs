using TMPro;

public class UI_Controls : UI_Base
{
    enum Texts
    {
        Controls,
    }

    public override void Init()
    {
        Bind<TextMeshProUGUI>(typeof(Texts));
    }
}

public class UI_Menu : UI_Scene
{
    private const int MenuCanvasOrder = 50;

    public override void Init()
    {
        Managers.UI.ShowCanvas(gameObject, MenuCanvasOrder);
    }
}

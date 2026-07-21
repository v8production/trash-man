using UnityEngine;
using UnityEngine.UI;

public class UI_Timer : UI_Scene
{
    enum GameObjects
    {
        Timer,
    }
    enum Images
    {
        Timer,
        Timeroutline,
    }

    private Image _timerImage;
    private RectTransform _timerRectTransform;
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
        Bind<Image>(typeof(Images));

        _timerImage = GetImage((int)Images.Timer);
        _timerRectTransform = GetObject((int)GameObjects.Timer).transform as RectTransform;
        _timerRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        _timerRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
    }

    public bool SetWorldPosition(Vector3 worldPosition)
    {
        Init();
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
            return false;

        Vector3 screenPosition = mainCamera.WorldToScreenPoint(worldPosition);
        if (screenPosition.z <= 0f
            || screenPosition.x < 0f
            || screenPosition.x > Screen.width
            || screenPosition.y < 0f
            || screenPosition.y > Screen.height)
        {
            Hide();
            return false;
        }

        Show();
        _timerRectTransform.position = new Vector3(screenPosition.x, screenPosition.y, 0f);
        return true;
    }

    public void SetFillAmount(float ratio)
    {
        Init();
        if (_timerImage == null)
            _timerImage = Util.FindChild<Image>(gameObject, nameof(Images.Timer), true);

        if (_timerImage != null)
            _timerImage.fillAmount = Mathf.Clamp01(ratio);
    }

    public void Hide() => gameObject.SetActive(false);
    public void Show() => gameObject.SetActive(true);
}

using UnityEngine;
using UnityEngine.UI;

public class UI_Loading : UI_Scene
{
    [SerializeField] private string _baseMessage = "Loading Lobby";

    private Image _dim;
    private Text _label;
    private float _nextTickTime;
    private int _dotCount;
    private string _message = string.Empty;

    public override void Init()
    {
        base.Init();
        Managers.UI.ShowCanvas(gameObject, 500);
        EnsureLayout();
        SetMessage(_baseMessage);
    }

    private void OnEnable()
    {
        _dotCount = 0;
        _nextTickTime = 0f;
    }

    private void Update()
    {
        if (_label == null)
            return;

        if (Time.unscaledTime < _nextTickTime)
            return;

        _nextTickTime = Time.unscaledTime + 0.35f;
        _dotCount = (_dotCount + 1) % 4;
        _label.text = _message + new string('.', _dotCount);
    }

    public void SetMessage(string message)
    {
        _message = string.IsNullOrWhiteSpace(message) ? _baseMessage : message.Trim();
        if (_label != null)
            _label.text = _message;
    }

    private void EnsureLayout()
    {
        if (_dim == null)
            _dim = EnsureDimImage();

        if (_label == null)
            _label = EnsureLabelText();
    }

    private Image EnsureDimImage()
    {
        Transform dimTransform = transform.Find("Dim");
        GameObject dimObject = dimTransform != null ? dimTransform.gameObject : new GameObject("Dim");
        dimObject.transform.SetParent(transform, false);

        RectTransform rect = dimObject.GetorAddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = dimObject.GetorAddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.72f);
        image.raycastTarget = true;
        return image;
    }

    private Text EnsureLabelText()
    {
        Transform labelTransform = transform.Find("Message");
        GameObject labelObject = labelTransform != null ? labelTransform.gameObject : new GameObject("Message");
        labelObject.transform.SetParent(transform, false);

        RectTransform rect = labelObject.GetorAddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(500f, 80f);
        rect.anchoredPosition = Vector2.zero;

        Text label = labelObject.GetorAddComponent<Text>();
        label.alignment = TextAnchor.MiddleCenter;
        label.fontSize = 34;
        label.color = Color.white;
        label.raycastTarget = false;
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        label.font = font;
        return label;
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_Loading : UI_Scene
{
    private const string BaseMessage = "Loading Lobby";

    private enum Images
    {
        Dim,
        LoadingGif,
    }

    private enum Texts
    {
        Message,
    }

    [SerializeField] private float _gifRotationSpeed = -220f;

    private Image _loadingGif;
    private TextMeshProUGUI _label;
    private float _nextTickTime;
    private int _dotCount;
    private string _message = BaseMessage;

    public override void Init()
    {
        base.Init();
        Managers.UI.ShowCanvas(gameObject, 500);

        Bind<Image>(typeof(Images));
        Bind<TextMeshProUGUI>(typeof(Texts));

        _loadingGif = GetImage((int)Images.LoadingGif);
        _label = GetText((int)Texts.Message);

        SetMessage(BaseMessage);
    }

    private void OnEnable()
    {
        _dotCount = 0;
        _nextTickTime = 0f;
    }

    private void Update()
    {
        if (_loadingGif != null)
            _loadingGif.rectTransform.Rotate(0f, 0f, _gifRotationSpeed * Time.unscaledDeltaTime);

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
        _message = string.IsNullOrWhiteSpace(message) ? BaseMessage : message.Trim();
        if (_label != null)
            _label.text = _message;
    }
}

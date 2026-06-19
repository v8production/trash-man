using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_Toast : UI_Scene
{
    private const float FadeDuration = 0.3f;
    private const float MinHoldDuration = 1.6f;
    private const float MaxHoldDuration = 5.5f;
    private const float PerCharacterDuration = 0.04f;
    private const float MaxBackgroundWidth = 1000f;
    private const float HorizontalPadding = 48f;
    private const float VerticalPadding = 20f;
    private const float MinBackgroundHeight = 50f;

    enum Texts
    {
        Text,
    }

    enum Images
    {
        Background,
    }

    private CanvasGroup _bubbleCanvasGroup;
    private bool _isInitialized;
    private float _elapsed;
    private float _holdDuration;
    private bool _isPlaying;

    public override void Init()
    {
        if (_isInitialized)
            return;

        Managers.UI.ShowCanvas(gameObject, 99);

        Bind<TextMeshProUGUI>(typeof(Texts));
        Bind<Image>(typeof(Images));

        _bubbleCanvasGroup = GetComponent<CanvasGroup>();
        if (_bubbleCanvasGroup == null)
        {
            Debug.LogError("[UI_Toast] Missing CanvasGroup. Add it to UI_Toast prefab.", this);
        }
        _isInitialized = true;
    }

    public void ShowBossMessage(string message, float holdDurationOverride = -1f)
    {
        if (!_isInitialized)
            Init();

        TextMeshProUGUI messageText = GetText((int)Texts.Text);
        if (messageText == null)
            return;

        messageText.text = message;
        ApplyMessageLayout(messageText);

        _holdDuration = holdDurationOverride > 0f
            ? holdDurationOverride
            : Mathf.Clamp(MinHoldDuration + (message.Length * PerCharacterDuration), MinHoldDuration, MaxHoldDuration);

        _elapsed = 0f;
        _isPlaying = true;
        gameObject.SetActive(true);

        if (_bubbleCanvasGroup != null)
            _bubbleCanvasGroup.alpha = 0f;
    }

    private void ApplyMessageLayout(TextMeshProUGUI messageText)
    {
        RectTransform textRect = messageText.rectTransform;
        RectTransform backgroundRect = GetImage((int)Images.Background).rectTransform;
        float maxTextWidth = MaxBackgroundWidth - HorizontalPadding;

        messageText.textWrappingMode = TextWrappingModes.NoWrap;
        Vector2 preferredSingleLineSize = messageText.GetPreferredValues(messageText.text);
        bool needsWrap = preferredSingleLineSize.x > maxTextWidth;
        float textWidth = needsWrap ? maxTextWidth : preferredSingleLineSize.x;

        if (needsWrap)
            messageText.textWrappingMode = TextWrappingModes.Normal;

        Vector2 preferredTextSize = needsWrap
            ? messageText.GetPreferredValues(messageText.text, textWidth, 0f)
            : preferredSingleLineSize;

        float textHeight = Mathf.Ceil(preferredTextSize.y);
        float backgroundWidth = Mathf.Min(MaxBackgroundWidth, Mathf.Ceil(textWidth + HorizontalPadding));
        float backgroundHeight = Mathf.Max(MinBackgroundHeight, Mathf.Ceil(textHeight + VerticalPadding));

        textRect.sizeDelta = new Vector2(textWidth, textHeight);
        backgroundRect.sizeDelta = new Vector2(backgroundWidth, backgroundHeight);

        LayoutRebuilder.ForceRebuildLayoutImmediate(textRect);
    }

    private void Update()
    {
        if (!_isPlaying)
            return;

        _elapsed += Time.unscaledDeltaTime;

        if (_bubbleCanvasGroup == null)
        {
            if (_elapsed >= _holdDuration)
            {
                _isPlaying = false;
                Managers.Resource.Destory(gameObject);
            }
            return;
        }

        float fadeInEnd = FadeDuration;
        float holdEnd = fadeInEnd + _holdDuration;
        float fadeOutEnd = holdEnd + FadeDuration;

        if (_elapsed <= fadeInEnd)
        {
            float t = fadeInEnd <= 0f ? 1f : Mathf.Clamp01(_elapsed / fadeInEnd);
            _bubbleCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
            return;
        }

        if (_elapsed <= holdEnd)
        {
            _bubbleCanvasGroup.alpha = 1f;
            return;
        }

        if (_elapsed <= fadeOutEnd)
        {
            float t = FadeDuration <= 0f ? 1f : Mathf.Clamp01((_elapsed - holdEnd) / FadeDuration);
            _bubbleCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
            return;
        }

        _isPlaying = false;
        Managers.Resource.Destory(gameObject);
    }
}

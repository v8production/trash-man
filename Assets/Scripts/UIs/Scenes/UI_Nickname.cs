using TMPro;
using UnityEngine;

public class UI_Nickname : UI_Base
{
    enum Texts
    {
        Nickname,
    }

    private const float NicknameHorizontalPadding = 12f;
    private const float NicknameVerticalPadding = 0.1f;

    private string _text;
    private Transform _target;
    private CharacterController _targetCharacterController;
    private RectTransform _parentRectTransform;
    private CanvasGroup _canvasGroup;
    private RectTransform _textRect;
    private TextMeshProUGUI _textComponent;
    private Color _defaultTextColor;
    private bool _hasDefaultTextColor;

    private void Awake()
    {
        CacheComponents();
    }

    public override void Init()
    {
        CacheComponents();
        Managers.UI.ShowCanvas(gameObject, false);
        Bind<TextMeshProUGUI>(typeof(Texts));
        _textComponent = GetText((int)Texts.Nickname);
        _textRect = _textComponent != null ? _textComponent.rectTransform : null;

        if (_textComponent != null)
        {
            CacheDefaultTextColor();
            _textComponent.textWrappingMode = TextWrappingModes.NoWrap;
            _textComponent.overflowMode = TextOverflowModes.Overflow;
            _textComponent.text = _text;
        }

        UpdateNicknameWidth();
    }

    private void LateUpdate()
    {
        UpdateScreenPosition();
    }

    public void SetTarget(Transform target)
    {
        CacheComponents();
        _target = target;
        _targetCharacterController = _target.GetComponent<CharacterController>();
        UpdateScreenPosition();
    }

    public void SetText(string text)
    {
        _text = text;
        if (_textComponent != null)
        {
            _textComponent.text = _text;
            UpdateNicknameWidth();
        }
    }

    public void SetTextColor(Color color, bool useOverride)
    {
        if (_textComponent == null)
            return;

        CacheDefaultTextColor();
        _textComponent.color = useOverride ? color : _defaultTextColor;
    }

    public void Hide() => gameObject.SetActive(false);
    public void Show() => gameObject.SetActive(true);

    private void CacheDefaultTextColor()
    {
        if (_hasDefaultTextColor || _textComponent == null)
            return;

        _defaultTextColor = _textComponent.color;
        _hasDefaultTextColor = true;
    }

    private void UpdateScreenPosition()
    {
        if (_target == null || Camera.main == null)
            return;

        CacheComponents();
        if (_textRect == null)
            return;

        Vector3 worldPosition = GetNicknameWorldPosition();
        Vector3 screenPosition = Camera.main.WorldToScreenPoint(worldPosition);
        bool isInFrontOfCamera = screenPosition.z > 0f;
        _canvasGroup.alpha = isInFrontOfCamera ? 1f : 0f;

        if (!isInFrontOfCamera)
            return;

        if (_parentRectTransform != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(_parentRectTransform, screenPosition, null, out Vector2 anchoredPosition))
        {
            _textRect.anchoredPosition = anchoredPosition;
            return;
        }

        _textRect.position = new Vector3(screenPosition.x, screenPosition.y, 0f);
    }

    private Vector3 GetNicknameWorldPosition()
    {
        float controllerTop = _targetCharacterController.center.y + _targetCharacterController.height * 0.5f;
        Vector3 localPosition = _targetCharacterController.center;
        localPosition.y = controllerTop + NicknameVerticalPadding;
        return _target.TransformPoint(localPosition);
    }

    private void CacheComponents()
    {
        if (_textComponent == null)
            _textComponent = GetComponentInChildren<TextMeshProUGUI>(true);

        if (_textRect == null && _textComponent != null)
            _textRect = _textComponent.rectTransform;

        _parentRectTransform = _textRect != null ? _textRect.parent as RectTransform : null;
        if (_textRect != null)
        {
            _textRect.anchorMin = new Vector2(0.5f, 0.5f);
            _textRect.anchorMax = new Vector2(0.5f, 0.5f);
        }

        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void UpdateNicknameWidth()
    {
        if (_textRect == null || _textComponent == null)
            return;

        _textComponent.ForceMeshUpdate();
        float preferredWidth = Mathf.Max(0f, _textComponent.preferredWidth);
        Vector2 size = _textRect.sizeDelta;
        size.x = preferredWidth + NicknameHorizontalPadding;
        _textRect.sizeDelta = size;
    }
}

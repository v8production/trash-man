using TMPro;
using UnityEngine;

public class UI_Nickname : UI_Base
{
    enum Texts
    {
        Nickname,
    }

    private const float NicknameHorizontalPadding = 12f;
    private const float NicknameVerticalPadding = 0.3f;

    private string _text;
    private Transform _parent;
    private CharacterController _parentCharacterController;
    private RectTransform _textRect;
    private TextMeshProUGUI _textComponent;

    public override void Init()
    {
        Bind<TextMeshProUGUI>(typeof(Texts));
        _textComponent = GetText((int)Texts.Nickname);
        _textRect = _textComponent != null ? _textComponent.rectTransform : null;
        CacheParentCharacterController();

        if (_textComponent != null)
        {
            _textComponent.textWrappingMode = TextWrappingModes.NoWrap;
            _textComponent.overflowMode = TextOverflowModes.Overflow;
            _textComponent.text = _text;
        }

        UpdateNicknameWidth();
    }

    void Update()
    {
        CacheParentCharacterController();
        transform.position = _parent.position + Vector3.up * GetNicknameHeightOffset();
        transform.rotation = Camera.main.transform.rotation;
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

    public void Hide() => gameObject.SetActive(false);
    public void Show() => gameObject.SetActive(true);

    private void CacheParentCharacterController()
    {
        if (_parent == transform.parent)
            return;

        _parent = transform.parent;
        _parentCharacterController = _parent.GetComponent<CharacterController>();
    }

    private float GetNicknameHeightOffset()
    {
        float parentScaleY = Mathf.Abs(_parent.lossyScale.y);
        float controllerTop = _parentCharacterController.center.y + _parentCharacterController.height * 0.5f;
        return controllerTop * parentScaleY + NicknameVerticalPadding;
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

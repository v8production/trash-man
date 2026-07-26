using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class ButtonClickSoundPlayer : MonoBehaviour
{
    private const string ClickSoundPath = "Sounds/SFXs/Common/Ui_button_click01";

    private Button _button;
    private bool _isBound;

    private void Awake()
    {
        Bind();
    }

    private void OnEnable()
    {
        Bind();
    }

    private void OnDestroy()
    {
        if (!_isBound || _button == null)
            return;

        _button.onClick.RemoveListener(PlayClickSound);
        _isBound = false;
    }

    private void Bind()
    {
        if (_isBound)
            return;

        _button = GetComponent<Button>();
        _button.onClick.AddListener(PlayClickSound);
        _isBound = true;
    }

    private static void PlayClickSound()
    {
        Managers.Sound.Play(ClickSoundPath);
    }
}

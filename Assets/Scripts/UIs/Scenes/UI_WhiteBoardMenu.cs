using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UI_WhiteBoardMenu : UI_Menu
{
    private const int CanvasOrder = 10;
    private const float BoardAspect = 16f / 9f;
    private const float BoardMargin = 10f;
    private const int MaxPayloadPoints = 180;

    private enum GameObjects
    {
        PanelBackground,
    }

    private enum Buttons
    {
        Cancel,
        PenButton,
        RedButton,
        YellowButton,
        GreenButton,
        BlueButton,
        BlackButton,
        WhiteButton,
        EraserButton,
    }

    private enum Sliders
    {
        PenBorder,
        EraserBorder,
    }

    private enum Images
    {
        Cursor,
    }

    public event Action Closed;

    private readonly List<Vector2Int> _strokePoints = new();
    private readonly Dictionary<Button, Color> _buttonBaseColors = new();
    private readonly Dictionary<Button, Outline> _buttonOutlines = new();

    private RectTransform _panelRect;
    private Image _panelImage;
    private RectTransform _cursorRect;
    private Image _cursorImage;
    private Slider _penBorderSlider;
    private Slider _eraserBorderSlider;
    private WhiteBoardTool _activeTool = WhiteBoardTool.Pen;
    private Color32 _activeColor = new(0, 0, 0, 255);
    private bool _isInitialized;
    private bool _isPointerOverPanel;
    private bool _isDrawing;
    private bool _isColorButtonHeld;
    private bool _isBorderSliderHeld;

    public override void Init()
    {
        if (_isInitialized)
            return;

        base.Init();
        Managers.UI.ShowCanvas(gameObject, CanvasOrder);
        Bind<GameObject>(typeof(GameObjects));
        Bind<Button>(typeof(Buttons));
        Bind<Slider>(typeof(Sliders));
        Bind<Image>(typeof(Images));

        _panelRect = GetObject((int)GameObjects.PanelBackground).GetComponent<RectTransform>();
        _panelImage = GetObject((int)GameObjects.PanelBackground).GetComponent<Image>();
        _cursorImage = GetImage((int)Images.Cursor);
        _cursorRect = _cursorImage.rectTransform;
        _penBorderSlider = Get<Slider>((int)Sliders.PenBorder);
        _eraserBorderSlider = Get<Slider>((int)Sliders.EraserBorder);

        ConfigureBoardPanel();
        ConfigureCursor();
        ConfigureSliders();
        BindButtons();
        BindPointerRelays();
        ApplyBoardSprite();
        RefreshSelectionVisuals();
        RefreshCursorVisual();

        WhiteBoardDrawingSurface.Changed += ApplyBoardSprite;
        _isInitialized = true;
    }

    private void OnEnable()
    {
        if (!_isInitialized)
            Init();

        ApplyBoardSprite();
        RefreshCursorVisual();
    }

    private void Update()
    {
        if (!_isInitialized)
            return;

        ConfigureBoardPanel();
        RefreshPointerOverPanel();
        UpdateCursorPosition();
        RefreshCursorVisibility();
    }

    private void OnDestroy()
    {
        WhiteBoardDrawingSurface.Changed -= ApplyBoardSprite;
        Closed = null;
    }

    private void ConfigureBoardPanel()
    {
        RectTransform parent = _panelRect.parent as RectTransform;
        if (parent == null)
            return;

        Rect parentRect = parent.rect;
        Vector2 anchorSize = new(
            parentRect.width * (_panelRect.anchorMax.x - _panelRect.anchorMin.x),
            parentRect.height * (_panelRect.anchorMax.y - _panelRect.anchorMin.y));

        if (anchorSize.x <= 0f || anchorSize.y <= 0f)
            return;

        float availableWidth = Mathf.Max(0f, anchorSize.x - BoardMargin);
        float availableHeight = Mathf.Max(0f, anchorSize.y - BoardMargin * 2f);
        float width = availableWidth;
        float height = width / BoardAspect;
        if (height > availableHeight)
        {
            height = availableHeight;
            width = height * BoardAspect;
        }

        _panelRect.sizeDelta = new Vector2(width - anchorSize.x, height - anchorSize.y);
        _panelRect.anchoredPosition = new Vector2((-anchorSize.x * 0.5f) + BoardMargin + (width * 0.5f), 0f);
    }

    private void ConfigureCursor()
    {
        _cursorImage.raycastTarget = false;
        _cursorImage.gameObject.SetActive(false);
    }

    private void ConfigureSliders()
    {
        ConfigureSlider(_penBorderSlider, 8f);
        ConfigureSlider(_eraserBorderSlider, 24f);
    }

    private void ConfigureSlider(Slider slider, float defaultValue)
    {
        slider.minValue = 1f;
        slider.maxValue = 50f;
        slider.wholeNumbers = true;
        slider.value = Mathf.Clamp(Mathf.Round(defaultValue), slider.minValue, slider.maxValue);
        slider.onValueChanged.AddListener(_ => RefreshCursorVisual());
    }

    private void BindButtons()
    {
        GetButton((int)Buttons.Cancel).gameObject.BindEvent(OnCancelClicked);
        BindToolButton(GetButton((int)Buttons.PenButton), WhiteBoardTool.Pen);
        BindToolButton(GetButton((int)Buttons.EraserButton), WhiteBoardTool.Eraser);
        BindColorButton(GetButton((int)Buttons.RedButton), new Color32(220, 40, 40, 255));
        BindColorButton(GetButton((int)Buttons.YellowButton), new Color32(245, 210, 45, 255));
        BindColorButton(GetButton((int)Buttons.GreenButton), new Color32(40, 180, 80, 255));
        BindColorButton(GetButton((int)Buttons.BlueButton), new Color32(45, 110, 230, 255));
        BindColorButton(GetButton((int)Buttons.BlackButton), new Color32(0, 0, 0, 255));
        BindColorButton(GetButton((int)Buttons.WhiteButton), new Color32(255, 255, 255, 255));
    }

    private void BindToolButton(Button button, WhiteBoardTool tool)
    {
        CacheButtonVisuals(button);
        button.onClick.AddListener(() =>
        {
            _activeTool = _activeTool == tool ? WhiteBoardTool.None : tool;
            RefreshSelectionVisuals();
            RefreshCursorVisual();
        });
    }

    private void BindColorButton(Button button, Color32 color)
    {
        CacheButtonVisuals(button);
        WhiteBoardPointerRelay relay = button.gameObject.GetorAddComponent<WhiteBoardPointerRelay>();
        relay.PointerDown += _ =>
        {
            _activeColor = color;
            _activeTool = WhiteBoardTool.Pen;
            _isColorButtonHeld = true;
            RefreshSelectionVisuals();
            RefreshCursorVisual();
        };
        relay.PointerUp += _ =>
        {
            _isColorButtonHeld = false;
            RefreshCursorVisibility();
        };
        button.onClick.AddListener(() =>
        {
            _activeColor = color;
            _activeTool = WhiteBoardTool.Pen;
            RefreshSelectionVisuals();
            RefreshCursorVisual();
        });
    }

    private void BindPointerRelays()
    {
        WhiteBoardPointerRelay panelRelay = _panelRect.gameObject.GetorAddComponent<WhiteBoardPointerRelay>();
        panelRelay.PointerEnter += _ => _isPointerOverPanel = true;
        panelRelay.PointerExit += _ =>
        {
            _isPointerOverPanel = false;
            RefreshCursorVisibility();
        };
        panelRelay.PointerDown += BeginStroke;
        panelRelay.Drag += ContinueStroke;
        panelRelay.PointerUp += EndStroke;

        BindSliderRelay(_penBorderSlider);
        BindSliderRelay(_eraserBorderSlider);
    }

    private void BindSliderRelay(Slider slider)
    {
        WhiteBoardPointerRelay relay = slider.gameObject.GetorAddComponent<WhiteBoardPointerRelay>();
        relay.PointerDown += _ =>
        {
            _isBorderSliderHeld = true;
            RefreshCursorVisual();
        };
        relay.PointerUp += _ =>
        {
            _isBorderSliderHeld = false;
            RefreshCursorVisibility();
        };
    }

    private void CacheButtonVisuals(Button button)
    {
        _buttonBaseColors[button] = button.targetGraphic != null ? button.targetGraphic.color : Color.white;
        Outline outline = button.gameObject.GetorAddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(3f, -3f);
        outline.enabled = false;
        _buttonOutlines[button] = outline;
    }

    private void BeginStroke(PointerEventData eventData)
    {
        if (_activeTool == WhiteBoardTool.None || !TryGetBoardPoint(eventData, out Vector2Int point))
            return;

        _isDrawing = true;
        _strokePoints.Clear();
        _strokePoints.Add(point);
        WhiteBoardDrawingSurface.ApplyStroke(_activeTool, GetActiveThickness(), _activeColor, _strokePoints);
    }

    private void ContinueStroke(PointerEventData eventData)
    {
        if (!_isDrawing || !TryGetBoardPoint(eventData, out Vector2Int point))
            return;

        Vector2Int lastPoint = _strokePoints[_strokePoints.Count - 1];
        if (lastPoint == point)
            return;

        _strokePoints.Add(point);
        WhiteBoardDrawingSurface.ApplyStroke(_activeTool, GetActiveThickness(), _activeColor, new[] { lastPoint, point });
    }

    private void EndStroke(PointerEventData eventData)
    {
        if (!_isDrawing)
            return;

        ContinueStroke(eventData);
        _isDrawing = false;

        List<Vector2Int> points = CreatePayloadPoints();
        if (points.Count == 0)
            return;

        string payload = WhiteBoardDrawingSurface.CreatePayload(_activeTool, GetActiveThickness(), _activeColor, points);
        LobbyNetworkPlayer localPlayer = LobbyNetworkPlayer.FindLocalOwnedPlayer();
        if (localPlayer != null)
            localPlayer.SubmitWhiteBoardStroke(payload);
    }

    private List<Vector2Int> CreatePayloadPoints()
    {
        if (_strokePoints.Count <= MaxPayloadPoints)
            return new List<Vector2Int>(_strokePoints);

        List<Vector2Int> result = new(MaxPayloadPoints);
        for (int i = 0; i < MaxPayloadPoints; i++)
        {
            int index = Mathf.RoundToInt(i * (_strokePoints.Count - 1) / (float)(MaxPayloadPoints - 1));
            result.Add(_strokePoints[index]);
        }
        return result;
    }

    private bool TryGetBoardPoint(PointerEventData eventData, out Vector2Int point)
    {
        point = default;
        Camera eventCamera = eventData.pressEventCamera != null ? eventData.pressEventCamera : eventData.enterEventCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_panelRect, eventData.position, eventCamera, out Vector2 localPoint))
            return false;

        Rect rect = _panelRect.rect;
        float normalizedX = (localPoint.x - rect.xMin) / rect.width;
        float normalizedY = (localPoint.y - rect.yMin) / rect.height;
        if (normalizedX < 0f || normalizedX > 1f || normalizedY < 0f || normalizedY > 1f)
            return false;

        point = WhiteBoardDrawingSurface.ClampPoint(new Vector2Int(
            Mathf.RoundToInt(normalizedX * (WhiteBoardDrawingSurface.Width - 1)),
            Mathf.RoundToInt(normalizedY * (WhiteBoardDrawingSurface.Height - 1))));
        return true;
    }

    private void ApplyBoardSprite()
    {
        if (_panelImage != null)
        {
            _panelImage.sprite = WhiteBoardDrawingSurface.Sprite;
            _panelImage.color = Color.white;
            _panelImage.preserveAspect = true;
        }
    }

    private void RefreshSelectionVisuals()
    {
        SetToolButtonSelected(GetButton((int)Buttons.PenButton), _activeTool == WhiteBoardTool.Pen);
        SetToolButtonSelected(GetButton((int)Buttons.EraserButton), _activeTool == WhiteBoardTool.Eraser);

        SetColorButtonSelected(GetButton((int)Buttons.RedButton), IsActiveColor(new Color32(220, 40, 40, 255)));
        SetColorButtonSelected(GetButton((int)Buttons.YellowButton), IsActiveColor(new Color32(245, 210, 45, 255)));
        SetColorButtonSelected(GetButton((int)Buttons.GreenButton), IsActiveColor(new Color32(40, 180, 80, 255)));
        SetColorButtonSelected(GetButton((int)Buttons.BlueButton), IsActiveColor(new Color32(45, 110, 230, 255)));
        SetColorButtonSelected(GetButton((int)Buttons.BlackButton), IsActiveColor(new Color32(0, 0, 0, 255)));
        SetColorButtonSelected(GetButton((int)Buttons.WhiteButton), IsActiveColor(new Color32(255, 255, 255, 255)));
    }

    private void SetToolButtonSelected(Button button, bool selected)
    {
        if (button.targetGraphic != null && _buttonBaseColors.TryGetValue(button, out Color baseColor))
            button.targetGraphic.color = selected ? new Color(baseColor.r * 0.65f, baseColor.g * 0.65f, baseColor.b * 0.65f, baseColor.a) : baseColor;

        if (_buttonOutlines.TryGetValue(button, out Outline outline))
            outline.enabled = selected;
    }

    private void SetColorButtonSelected(Button button, bool selected)
    {
        if (_buttonOutlines.TryGetValue(button, out Outline outline))
            outline.enabled = selected;
    }

    private bool IsActiveColor(Color32 color)
    {
        return _activeColor.r == color.r && _activeColor.g == color.g && _activeColor.b == color.b && _activeColor.a == color.a;
    }

    private void RefreshCursorVisual()
    {
        _cursorImage.color = _activeTool == WhiteBoardTool.Eraser ? Color.white : _activeColor;
        float size = GetActiveThickness();
        _cursorRect.sizeDelta = new Vector2(size, size);
        RefreshCursorVisibility();
    }

    private void RefreshCursorVisibility()
    {
        bool visible = _isPointerOverPanel || _isColorButtonHeld || _isBorderSliderHeld;
        _cursorImage.gameObject.SetActive(visible);
    }

    private void RefreshPointerOverPanel()
    {
        _isPointerOverPanel = TryGetMousePosition(out Vector2 mousePosition)
            && RectTransformUtility.RectangleContainsScreenPoint(_panelRect, mousePosition, GetEventCamera());
    }

    private void UpdateCursorPosition()
    {
        if (!_cursorImage.gameObject.activeSelf)
            return;

        if (!TryGetMousePosition(out Vector2 mousePosition))
            return;

        RectTransform cursorParent = _cursorRect.parent as RectTransform;
        if (cursorParent == null)
        {
            _cursorRect.position = mousePosition;
            return;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(cursorParent, mousePosition, GetEventCamera(), out Vector2 localPoint))
            _cursorRect.anchoredPosition = localPoint;
    }

    private static bool TryGetMousePosition(out Vector2 mousePosition)
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            mousePosition = default;
            return false;
        }

        mousePosition = mouse.position.ReadValue();
        return true;
    }

    private Camera GetEventCamera()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return canvas.worldCamera;
    }

    private int GetActiveThickness()
    {
        Slider slider = _activeTool == WhiteBoardTool.Eraser ? _eraserBorderSlider : _penBorderSlider;
        return Mathf.Clamp(Mathf.RoundToInt(slider.value), 1, 50);
    }

    private void OnCancelClicked(PointerEventData eventData)
    {
        Closed?.Invoke();
    }

}

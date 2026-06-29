using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UI_BoardMenu : UI_Menu
{
    private const int CanvasOrder = 10;
    private const float BoardAspect = 16f / 9f;
    private const float BoardMargin = 10f;
    private const int MaxPayloadPoints = 180;
    private static readonly Color32 UnassignedPenColor = new(0xE9, 0xE9, 0xE9, 255);

    private enum GameObjects
    {
        Board,
        PanelBackground,
    }

    private enum Buttons
    {
        Cancel,
        PenButton,
        EraserButton,
        ResetButton,
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

    private RectTransform _boardRect;
    private Image _boardImage;
    private RectTransform _panelBackgroundRect;
    private RectTransform _cursorRect;
    private Image _cursorImage;
    private Slider _penBorderSlider;
    private Slider _eraserBorderSlider;
    private BoardTool _activeTool = BoardTool.Pen;
    private Color32 _activeColor = UnassignedPenColor;
    private bool _isInitialized;
    private bool _isPointerOverPanel;
    private bool _isDrawing;
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

        _boardRect = GetObject((int)GameObjects.Board).GetComponent<RectTransform>();
        _boardImage = GetObject((int)GameObjects.Board).GetComponent<Image>();
        _panelBackgroundRect = GetObject((int)GameObjects.PanelBackground).GetComponent<RectTransform>();
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
        RefreshActivePenColor(forceVisualRefresh: false);
        RefreshSelectionVisuals();
        RefreshCursorVisual();

        BoardDrawingSurface.Changed += ApplyBoardSprite;
        _isInitialized = true;
    }

    private void OnEnable()
    {
        if (!_isInitialized)
            Init();

        ApplyBoardSprite();
        RefreshActivePenColor(forceVisualRefresh: false);
        RefreshCursorVisual();
    }

    private void Update()
    {
        if (!_isInitialized)
            return;

        ConfigureBoardPanel();
        if (!_isDrawing)
            RefreshActivePenColor(forceVisualRefresh: false);
        RefreshPointerOverPanel();
        UpdateCursorPosition();
        RefreshCursorVisibility();
    }

    private void OnDestroy()
    {
        BoardDrawingSurface.Changed -= ApplyBoardSprite;
        Closed = null;
    }

    private void ConfigureBoardPanel()
    {
        RectTransform parent = _boardRect.parent as RectTransform;
        if (parent == null)
            return;

        Rect parentRect = parent.rect;
        Vector2 anchorSize = new(
            parentRect.width * (_boardRect.anchorMax.x - _boardRect.anchorMin.x),
            parentRect.height * (_boardRect.anchorMax.y - _boardRect.anchorMin.y));

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

        Vector2 sizeDelta = new(width - anchorSize.x, height - anchorSize.y);
        Vector2 anchoredPosition = new((-anchorSize.x * 0.5f) + BoardMargin + (width * 0.5f), 0f);

        _boardRect.sizeDelta = sizeDelta;
        _boardRect.anchoredPosition = anchoredPosition;
        _panelBackgroundRect.sizeDelta = sizeDelta;
        _panelBackgroundRect.anchoredPosition = anchoredPosition;
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
        GetButton((int)Buttons.ResetButton).gameObject.BindEvent(OnResetClicked);
        BindToolButton(GetButton((int)Buttons.PenButton), BoardTool.Pen);
        BindToolButton(GetButton((int)Buttons.EraserButton), BoardTool.Eraser);
    }

    private void BindToolButton(Button button, BoardTool tool)
    {
        CacheButtonVisuals(button);
        button.onClick.AddListener(() =>
        {
            _activeTool = _activeTool == tool ? BoardTool.None : tool;
            RefreshSelectionVisuals();
            RefreshCursorVisual();
        });
    }

    private void BindPointerRelays()
    {
        BoardPointerRelay panelRelay = _boardRect.gameObject.GetorAddComponent<BoardPointerRelay>();
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
        BoardPointerRelay relay = slider.gameObject.GetorAddComponent<BoardPointerRelay>();
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
        if (_activeTool == BoardTool.None || !TryGetBoardPoint(eventData, out Vector2Int point))
            return;

        RefreshActivePenColor(forceVisualRefresh: false);
        _isDrawing = true;
        _strokePoints.Clear();
        _strokePoints.Add(point);
        BoardDrawingSurface.ApplyStroke(_activeTool, GetActiveThickness(), _activeColor, _strokePoints);
    }

    private void ContinueStroke(PointerEventData eventData)
    {
        if (!_isDrawing || !TryGetBoardPoint(eventData, out Vector2Int point))
            return;

        Vector2Int lastPoint = _strokePoints[_strokePoints.Count - 1];
        if (lastPoint == point)
            return;

        _strokePoints.Add(point);
        BoardDrawingSurface.ApplyStroke(_activeTool, GetActiveThickness(), _activeColor, new[] { lastPoint, point });
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

        string payload = BoardDrawingSurface.CreatePayload(_activeTool, GetActiveThickness(), _activeColor, points);
        LobbyNetworkPlayer localPlayer = LobbyNetworkPlayer.FindLocalOwnedPlayer();
        if (localPlayer != null)
            localPlayer.SubmitBoardStroke(payload);
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
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_boardRect, eventData.position, eventCamera, out Vector2 localPoint))
            return false;

        Rect rect = _boardRect.rect;
        float normalizedX = (localPoint.x - rect.xMin) / rect.width;
        float normalizedY = (localPoint.y - rect.yMin) / rect.height;
        if (normalizedX < 0f || normalizedX > 1f || normalizedY < 0f || normalizedY > 1f)
            return false;

        point = BoardDrawingSurface.ClampPoint(new Vector2Int(
            Mathf.RoundToInt(normalizedX * (BoardDrawingSurface.Width - 1)),
            Mathf.RoundToInt(normalizedY * (BoardDrawingSurface.Height - 1))));
        return true;
    }

    private void ApplyBoardSprite()
    {
        if (_boardImage != null)
        {
            _boardImage.sprite = BoardDrawingSurface.Sprite;
            _boardImage.color = Color.white;
            _boardImage.preserveAspect = true;
        }
    }

    private void RefreshSelectionVisuals()
    {
        SetToolButtonSelected(GetButton((int)Buttons.PenButton), _activeTool == BoardTool.Pen);
        SetToolButtonSelected(GetButton((int)Buttons.EraserButton), _activeTool == BoardTool.Eraser);
    }

    private void SetToolButtonSelected(Button button, bool selected)
    {
        if (button.targetGraphic != null && _buttonBaseColors.TryGetValue(button, out Color baseColor))
            button.targetGraphic.color = selected ? new Color(baseColor.r * 0.65f, baseColor.g * 0.65f, baseColor.b * 0.65f, baseColor.a) : baseColor;

        if (_buttonOutlines.TryGetValue(button, out Outline outline))
            outline.enabled = selected;
    }

    private void RefreshCursorVisual()
    {
        _cursorImage.color = _activeTool == BoardTool.Eraser ? Color.white : _activeColor;
        float size = GetActiveThickness();
        _cursorRect.sizeDelta = new Vector2(size, size);
        RefreshCursorVisibility();
    }

    private void RefreshActivePenColor(bool forceVisualRefresh)
    {
        Color32 nextColor = UnassignedPenColor;
        LobbyNetworkPlayer localPlayer = LobbyNetworkPlayer.FindLocalOwnedPlayer();
        if (localPlayer != null
            && localPlayer.TryGetSelectedRoleMask(out int roleMask)
            && LobbyNetworkPlayer.TryResolveRangerSuitColorFromRoleMask(roleMask, out Color32 roleColor))
        {
            nextColor = roleColor;
        }

        if (!forceVisualRefresh && IsSameColor(_activeColor, nextColor))
            return;

        _activeColor = nextColor;
        RefreshCursorVisual();
    }

    private static bool IsSameColor(Color32 a, Color32 b)
    {
        return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
    }

    private void RefreshCursorVisibility()
    {
        bool visible = _isPointerOverPanel || _isBorderSliderHeld;
        _cursorImage.gameObject.SetActive(visible);
    }

    private void RefreshPointerOverPanel()
    {
        _isPointerOverPanel = TryGetMousePosition(out Vector2 mousePosition)
            && RectTransformUtility.RectangleContainsScreenPoint(_boardRect, mousePosition, GetEventCamera());
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
        Slider slider = _activeTool == BoardTool.Eraser ? _eraserBorderSlider : _penBorderSlider;
        return Mathf.Clamp(Mathf.RoundToInt(slider.value), 1, 50);
    }

    private void OnCancelClicked(PointerEventData eventData)
    {
        Closed?.Invoke();
    }

    private void OnResetClicked(PointerEventData eventData)
    {
        _isDrawing = false;
        _strokePoints.Clear();
        BoardDrawingSurface.Clear();

        LobbyNetworkPlayer localPlayer = LobbyNetworkPlayer.FindLocalOwnedPlayer();
        if (localPlayer != null)
            localPlayer.SubmitBoardStroke(BoardDrawingSurface.CreateClearPayload());
    }

}

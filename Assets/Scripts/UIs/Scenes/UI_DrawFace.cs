using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_DrawFace : UI_Menu
{
    private enum DrawMode
    {
        Pen,
        Eraser,
    }

    private readonly List<List<PixelChange>> _strokeHistory = new();
    private readonly Dictionary<int, int> _currentStrokePixelIndices = new();
    private List<PixelChange> _currentStroke;
    private Texture2D _faceTexture;
    private DrawMode _drawMode = DrawMode.Pen;

    public event Action Closed;

    enum Images
    {
        Background,
    }

    enum RawImages
    {
        Panel,
    }

    enum Buttons
    {
        Back,
        Pen,
        Eraser,
        Revert,
        Reset,
        Save,
    }

    enum Texts
    {
        Back,
        Pen,
        Eraser,
        Revert,
        Reset,
        Save,
    }

    public override void Init()
    {
        base.Init();
        Bind<Image>(typeof(Images));
        Bind<RawImage>(typeof(RawImages));
        Bind<Button>(typeof(Buttons));
        Bind<TextMeshProUGUI>(typeof(Texts));

        GetButton((int)Buttons.Back).gameObject.BindEvent(OnBackButtonClicked);
        GetButton((int)Buttons.Pen).gameObject.BindEvent(OnPenButtonClicked);
        GetButton((int)Buttons.Eraser).gameObject.BindEvent(OnEraserButtonClicked);
        GetButton((int)Buttons.Revert).gameObject.BindEvent(OnRevertButtonClicked);
        GetButton((int)Buttons.Reset).gameObject.BindEvent(OnResetButtonClicked);
        GetButton((int)Buttons.Save).gameObject.BindEvent(OnSaveButtonClicked);

        InitializeDrawingPanel();
    }

    private void OnDestroy()
    {
        Closed = null;
    }

    private void OnDisable()
    {
        CommitCurrentStroke();
    }

    private void OnBackButtonClicked(PointerEventData eventData)
    {
        CommitCurrentStroke();
        Closed?.Invoke();
    }

    private void OnPenButtonClicked(PointerEventData eventData)
    {
        _drawMode = DrawMode.Pen;
    }

    private void OnEraserButtonClicked(PointerEventData eventData)
    {
        _drawMode = DrawMode.Eraser;
    }

    private void OnRevertButtonClicked(PointerEventData eventData)
    {
        CommitCurrentStroke();
        if (_strokeHistory.Count == 0)
            return;

        List<PixelChange> latestStroke = _strokeHistory[^1];
        _strokeHistory.RemoveAt(_strokeHistory.Count - 1);

        foreach (PixelChange change in latestStroke)
            _faceTexture.SetPixel(change.X, change.Y, change.PreviousColor);

        _faceTexture.Apply();
    }

    private void OnResetButtonClicked(PointerEventData eventData)
    {
        CommitCurrentStroke();
        ClearTexture();
        _strokeHistory.Clear();
    }

    private void OnSaveButtonClicked(PointerEventData eventData)
    {
        CommitCurrentStroke();
        RangerFaceTextureStore.SaveCustomTexture(_faceTexture);

        LobbyNetworkPlayer localPlayer = LobbyNetworkPlayer.FindLocalOwnedPlayer();
        if (localPlayer != null)
            localPlayer.SubmitLocalFaceTexture(_faceTexture);
        else
            RangerFaceTextureStore.ApplyToLoadedRangers();

        Managers.Toast.EnqueueMessage("Face image saved.", 2.0f);
    }

    private void InitializeDrawingPanel()
    {
        _faceTexture = RangerFaceTextureStore.CreateEditableTexture();

        RawImage panel = Get<RawImage>((int)RawImages.Panel);
        panel.texture = _faceTexture;
        panel.raycastTarget = true;
        BindPanelDrawingEvents(panel.gameObject);
    }

    private void BindPanelDrawingEvents(GameObject panelObject)
    {
        EventTrigger trigger = panelObject.GetorAddComponent<EventTrigger>();
        trigger.triggers.Clear();

        AddPanelEvent(trigger, EventTriggerType.PointerDown, BeginStroke);
        AddPanelEvent(trigger, EventTriggerType.Drag, ContinueStroke);
        AddPanelEvent(trigger, EventTriggerType.PointerUp, EndStroke);
    }

    private static void AddPanelEvent(EventTrigger trigger, EventTriggerType eventType, Action<PointerEventData> handler)
    {
        EventTrigger.Entry entry = new() { eventID = eventType };
        entry.callback.AddListener(eventData => handler((PointerEventData)eventData));
        trigger.triggers.Add(entry);
    }

    private void BeginStroke(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        _currentStroke = new List<PixelChange>();
        _currentStrokePixelIndices.Clear();
        PaintAt(eventData);
    }

    private void ContinueStroke(PointerEventData eventData)
    {
        if (_currentStroke == null)
            return;

        PaintAt(eventData);
    }

    private void EndStroke(PointerEventData eventData)
    {
        CommitCurrentStroke();
    }

    private void PaintAt(PointerEventData eventData)
    {
        RawImage panel = Get<RawImage>((int)RawImages.Panel);
        RectTransform panelRect = panel.rectTransform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(panelRect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
            return;

        Rect rect = panelRect.rect;
        float normalizedX = Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x);
        float normalizedY = Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y);

        if (normalizedX < 0f || normalizedX > 1f || normalizedY < 0f || normalizedY > 1f)
            return;

        int x = Mathf.Clamp(Mathf.FloorToInt(normalizedX * RangerFaceTextureStore.TextureWidth), 0, RangerFaceTextureStore.TextureWidth - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(normalizedY * RangerFaceTextureStore.TextureHeight), 0, RangerFaceTextureStore.TextureHeight - 1);
        Color targetColor = _drawMode == DrawMode.Pen ? Color.white : Color.clear;

        if (_faceTexture.GetPixel(x, y) == targetColor)
            return;

        int pixelIndex = y * RangerFaceTextureStore.TextureWidth + x;
        if (!_currentStrokePixelIndices.ContainsKey(pixelIndex))
        {
            _currentStrokePixelIndices.Add(pixelIndex, _currentStroke.Count);
            _currentStroke.Add(new PixelChange(x, y, _faceTexture.GetPixel(x, y)));
        }

        _faceTexture.SetPixel(x, y, targetColor);
        _faceTexture.Apply();
    }

    private void CommitCurrentStroke()
    {
        if (_currentStroke != null && _currentStroke.Count > 0)
            _strokeHistory.Add(_currentStroke);

        _currentStroke = null;
        _currentStrokePixelIndices.Clear();
    }

    private void ClearTexture()
    {
        Color[] pixels = new Color[RangerFaceTextureStore.TextureWidth * RangerFaceTextureStore.TextureHeight];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.clear;

        _faceTexture.SetPixels(pixels);
        _faceTexture.Apply();
    }

    private readonly struct PixelChange
    {
        public readonly int X;
        public readonly int Y;
        public readonly Color PreviousColor;

        public PixelChange(int x, int y, Color previousColor)
        {
            X = x;
            Y = y;
            PreviousColor = previousColor;
        }
    }
}

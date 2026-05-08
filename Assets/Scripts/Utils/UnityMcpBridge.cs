// Unity MCP bridge is editor-only tooling.
// Run only in Unity "client" context: Play Mode, not batchmode/headless/server.
#if UNITY_EDITOR && !UNITY_SERVER

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class UnityMcpBridge
{
    private const int Port = 17777;
    private const string MenuPath = "Tools/OpenCode/Unity MCP Bridge/Restart";

    private static TcpListener listener;
    private static Thread listenerThread;
    private static readonly Queue<BridgeRequest> pendingRequests = new();
    private static readonly List<InputSequence> inputSequences = new();
    private static readonly object pendingLock = new();
    private static bool shouldRun;

    static UnityMcpBridge()
    {
        EditorApplication.delayCall += EvaluateRunState;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        AssemblyReloadEvents.beforeAssemblyReload += Stop;

        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (listener == null)
            return;
        PumpRequests();
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        EvaluateRunState();
    }

    private static void EvaluateRunState()
    {
        // Only run in an interactive Unity client context (not batch mode).
        if (Application.isBatchMode)
        {
            Stop();
            return;
        }

        Start();
    }

    [MenuItem(MenuPath)]
    private static void RestartFromMenu()
    {
        Stop();
        EvaluateRunState();
    }

    private static void Start()
    {
        if (Application.isBatchMode)
            return;

        if (listener != null)
            return;

        try
        {
            shouldRun = true;
            listener = new TcpListener(IPAddress.Loopback, Port);
            listener.Start();
            listenerThread = new Thread(ListenLoop)
            {
                IsBackground = true,
                Name = "Unity MCP Bridge",
            };
            listenerThread.Start();
            Debug.Log($"[UnityMcpBridge] Listening on 127.0.0.1:{Port}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UnityMcpBridge] Failed to start: {ex.Message}");
            Stop();
        }
    }

    private static void Stop()
    {
        shouldRun = false;
        try
        {
            listener?.Stop();
        }
        catch
        {
            // Ignore shutdown errors.
        }

        listener = null;
        listenerThread = null;
    }

    private static void ListenLoop()
    {
        while (shouldRun)
        {
            try
            {
                TcpClient client = listener.AcceptTcpClient();
                ThreadPool.QueueUserWorkItem(_ => HandleClient(client));
            }
            catch
            {
                if (shouldRun)
                    Thread.Sleep(50);
            }
        }
    }

    private static void HandleClient(TcpClient client)
    {
        using (client)
        using (NetworkStream stream = client.GetStream())
        using (StreamReader reader = new(stream, Encoding.UTF8, false, 4096, true))
        using (StreamWriter writer = new(stream, new UTF8Encoding(false), 4096, true) { AutoFlush = true })
        {
            string line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
                return;

            BridgeRequest request = new(line);
            lock (pendingLock)
            {
                pendingRequests.Enqueue(request);
            }

            request.WaitHandle.WaitOne(TimeSpan.FromSeconds(15));
            writer.WriteLine(request.ResponseJson ?? JsonUtility.ToJson(BridgeResponse.Error("timeout")));
        }
    }

    private static void PumpRequests()
    {
        PumpInputSequences();

        while (true)
        {
            BridgeRequest request;
            lock (pendingLock)
            {
                if (pendingRequests.Count == 0)
                    return;

                request = pendingRequests.Dequeue();
            }

            try
            {
                BridgeCommand command = JsonUtility.FromJson<BridgeCommand>(request.Json);
                if (command != null && command.tool == "apply_input_buffer")
                {
                    StartInputSequence(request, command);
                    continue;
                }

                request.ResponseJson = HandleRequest(command);
            }
            catch (Exception ex)
            {
                request.ResponseJson = JsonUtility.ToJson(BridgeResponse.Error(ex.ToString()));
            }
            finally
            {
                request.WaitHandle.Set();
            }
        }
    }

    private static string HandleRequest(BridgeCommand command)
    {
        if (command == null || string.IsNullOrWhiteSpace(command.tool))
            return JsonUtility.ToJson(BridgeResponse.Error("Missing tool"));

        switch (command.tool)
        {
            case "ping":
                return JsonUtility.ToJson(BridgeResponse.Ok(CaptureState("pong")));
            case "start_play_mode":
                return JsonUtility.ToJson(StartPlayMode(command));
            case "stop_play_mode":
                return JsonUtility.ToJson(StopPlayMode());
            case "inspect_game_state":
                return JsonUtility.ToJson(BridgeResponse.Ok(CaptureState("inspect")));
            case "capture_visual_observation":
                return JsonUtility.ToJson(CaptureVisualObservation(command));
            default:
                return JsonUtility.ToJson(BridgeResponse.Error($"Unknown tool: {command.tool}"));
        }
    }

    private static BridgeResponse StartPlayMode(BridgeCommand command)
    {
        if (!string.IsNullOrWhiteSpace(command.scene))
        {
            string scenePath = ResolveScenePath(command.scene);
            if (string.IsNullOrWhiteSpace(scenePath))
                return BridgeResponse.Error($"Scene not found: {command.scene}");

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(scenePath);
        }

        if (!EditorApplication.isPlaying)
            EditorApplication.EnterPlaymode();

        return BridgeResponse.Ok(CaptureState("start_play_mode_requested"));
    }

    private static BridgeResponse StopPlayMode()
    {
        if (EditorApplication.isPlaying)
            EditorApplication.ExitPlaymode();

        return BridgeResponse.Ok(CaptureState("stop_play_mode_requested"));
    }

    private static void StartInputSequence(BridgeRequest request, BridgeCommand command)
    {
        if (!EditorApplication.isPlaying)
        {
            request.ResponseJson = JsonUtility.ToJson(BridgeResponse.Error("Unity is not in Play Mode"));
            request.WaitHandle.Set();
            return;
        }

        InputFrame[] frames = command.frames ?? Array.Empty<InputFrame>();
        inputSequences.Add(new InputSequence(request, frames));
    }

    private static void PumpInputSequences()
    {
        if (inputSequences.Count == 0)
            return;

        double now = EditorApplication.timeSinceStartup;
        for (int i = inputSequences.Count - 1; i >= 0; i--)
        {
            InputSequence sequence = inputSequences[i];
            if (sequence.IsComplete)
            {
                inputSequences.RemoveAt(i);
                continue;
            }

            if (!sequence.HasStarted || now >= sequence.NextStepTime)
                AdvanceInputSequence(sequence, now);
        }
    }

    private static void AdvanceInputSequence(InputSequence sequence, double now)
    {
        if (sequence.Index >= sequence.Frames.Length)
        {
            QueueKeyboardFrame(Array.Empty<string>());
            QueueMouseFrame(0f, 0f, false);
            InputSystem.Update();

            sequence.Request.ResponseJson = JsonUtility.ToJson(BridgeResponse.Ok(CaptureState($"applied {sequence.Frames.Length} frames / {sequence.TotalDurationMs}ms")));
            sequence.Request.WaitHandle.Set();
            sequence.IsComplete = true;
            return;
        }

        InputFrame frame = sequence.Frames[sequence.Index];
        QueueKeyboardFrame(frame.keys ?? Array.Empty<string>());
        QueueMouseFrame(frame.mouse_delta_x, frame.mouse_delta_y, frame.right_mouse);
        InputSystem.Update();

        sequence.HasStarted = true;
        sequence.Index++;
        sequence.TotalDurationMs += Mathf.Max(0, frame.duration_ms);
        sequence.NextStepTime = now + Mathf.Max(0, frame.duration_ms) / 1000.0;
    }

    private static BridgeResponse CaptureVisualObservation(BridgeCommand command)
    {
        string directory = Path.Combine(Application.dataPath, "..", "Temp");
        Directory.CreateDirectory(directory);
        string path = Path.GetFullPath(Path.Combine(directory, $"unity-mcp-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.png"));
        ScreenCapture.CaptureScreenshot(path);
        GameStateSnapshot state = CaptureState("screenshot_requested");
        state.screenshot_path = path;
        return BridgeResponse.Ok(state);
    }

    private static void QueueKeyboardFrame(string[] keys)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            keyboard = InputSystem.AddDevice<Keyboard>();

        KeyboardState state = new();
        for (int i = 0; i < keys.Length; i++)
        {
            if (TryParseKey(keys[i], out Key key))
                state.Press(key);
        }

        InputSystem.QueueStateEvent(keyboard, state);
    }

    private static void QueueMouseFrame(float deltaX, float deltaY, bool rightMouse)
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
            mouse = InputSystem.AddDevice<Mouse>();

        MouseState state = new()
        {
            delta = new Vector2(deltaX, deltaY),
            position = mouse.position.ReadValue() + new Vector2(deltaX, deltaY),
        };

        if (rightMouse)
            state = state.WithButton(MouseButton.Right);

        InputSystem.QueueStateEvent(mouse, state);
    }

    private static bool TryParseKey(string value, out Key key)
    {
        key = Key.None;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string normalized = value.Trim();
        if (normalized.StartsWith("Key", StringComparison.OrdinalIgnoreCase))
            normalized = normalized.Substring(3);

        return Enum.TryParse(normalized, ignoreCase: true, out key);
    }

    private static GameStateSnapshot CaptureState(string status)
    {
        GameStateSnapshot snapshot = new()
        {
            status = status,
            is_playing = EditorApplication.isPlaying,
            active_scene = SceneManager.GetActiveScene().name,
            time = EditorApplication.isPlaying ? Time.time : 0f,
            camera_yaw_degrees = ResolveCameraYaw(),
            nearby = string.Empty,
            flags = string.Join(",", CollectFlags()),
        };

        TitanRigRuntime titanRuntime = UnityEngine.Object.FindAnyObjectByType<TitanRigRuntime>();
        Transform titanRoot = titanRuntime != null ? titanRuntime.MovementRoot : null;
        if (titanRoot != null)
        {
            snapshot.titan_position = FormatVector(titanRoot.position);
            snapshot.titan_rotation = FormatVector(titanRoot.rotation.eulerAngles);
        }

        Transform cameraTransform = Camera.main != null ? Camera.main.transform : null;
        if (cameraTransform != null)
            snapshot.camera_position = FormatVector(cameraTransform.position);

        return snapshot;
    }

    private static IEnumerable<string> CollectFlags()
    {
        if (Unity.Netcode.NetworkManager.Singleton != null)
        {
            Unity.Netcode.NetworkManager networkManager = Unity.Netcode.NetworkManager.Singleton;
            if (networkManager.IsListening) yield return "network_listening";
            if (networkManager.IsServer) yield return "server";
            if (networkManager.IsClient) yield return "client";
            if (networkManager.IsHost) yield return "host";
        }

        if (UnityEngine.Object.FindAnyObjectByType<TitanController>() != null) yield return "titan_present";
        if (UnityEngine.Object.FindAnyObjectByType<TitanRoleNetworkDriver>() != null) yield return "titan_driver_present";
    }

    private static float ResolveCameraYaw()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return 0f;

        return camera.transform.eulerAngles.y;
    }

    private static string ResolveScenePath(string scene)
    {
        if (scene.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) && File.Exists(scene))
            return scene;

        string[] guids = AssetDatabase.FindAssets($"{scene} t:Scene");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (Path.GetFileNameWithoutExtension(path).Equals(scene, StringComparison.OrdinalIgnoreCase))
                return path;
        }

        return null;
    }

    private static string FormatVector(Vector3 value)
    {
        return $"{value.x:0.###},{value.y:0.###},{value.z:0.###}";
    }

    private sealed class BridgeRequest
    {
        public readonly string Json;
        public readonly ManualResetEvent WaitHandle = new(false);
        public string ResponseJson;

        public BridgeRequest(string json)
        {
            Json = json;
        }
    }

#pragma warning disable 0649
    [Serializable]
    private sealed class BridgeCommand
    {
        public string tool;
        public string scene;
        public InputFrame[] frames;
    }

    [Serializable]
    private sealed class InputFrame
    {
        public string[] keys;
        public int duration_ms;
        public float mouse_delta_x;
        public float mouse_delta_y;
        public bool right_mouse;
    }
#pragma warning restore 0649

    private sealed class InputSequence
    {
        public readonly BridgeRequest Request;
        public readonly InputFrame[] Frames;
        public int Index;
        public int TotalDurationMs;
        public double NextStepTime;
        public bool HasStarted;
        public bool IsComplete;

        public InputSequence(BridgeRequest request, InputFrame[] frames)
        {
            Request = request;
            Frames = frames;
        }
    }

    [Serializable]
    private sealed class BridgeResponse
    {
        public bool ok;
        public string error;
        public GameStateSnapshot state;

        public static BridgeResponse Ok(GameStateSnapshot state)
        {
            return new BridgeResponse { ok = true, state = state };
        }

        public static BridgeResponse Error(string error)
        {
            return new BridgeResponse { ok = false, error = error };
        }
    }

    [Serializable]
    private sealed class GameStateSnapshot
    {
        public string status;
        public bool is_playing;
        public string active_scene;
        public float time;
        public float camera_yaw_degrees;
        public string titan_position;
        public string titan_rotation;
        public string camera_position;
        public string nearby;
        public string flags;
        public string screenshot_path;
    }
}

#endif

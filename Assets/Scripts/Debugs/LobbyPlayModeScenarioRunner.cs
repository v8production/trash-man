using System;
using System.Collections;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class LobbyPlayModeScenarioRunner : MonoBehaviour
{
    private enum StartupRole
    {
        Auto,
        Host,
        Client,
        Disabled,
    }

    [SerializeField] private bool _runOnStart = true;
    [SerializeField] private bool _editorOnly = true;
    [SerializeField] private StartupRole _startupRole = StartupRole.Auto;
    [SerializeField] private string _hostAddress = "127.0.0.1";
    [SerializeField] private ushort _port = 18000;
    [SerializeField] private float _initialDelaySeconds = 0.35f;
    [SerializeField] private float _retryIntervalSeconds = 0.3f;
    [SerializeField] private int _maxRetryCount = 25;
    [SerializeField] private bool _verboseLog = true;

    private bool _started;

    private void Start()
    {
        if (!_runOnStart || _started)
            return;

        _started = true;
        StartCoroutine(RunScenarioCoroutine());
    }

    private IEnumerator RunScenarioCoroutine()
    {
        if (_editorOnly && !Application.isEditor)
            yield break;

        if (_initialDelaySeconds > 0f)
            yield return new WaitForSecondsRealtime(_initialDelaySeconds);

        StartupRole role = ResolveStartupRole(out string reason);
        if (role == StartupRole.Disabled)
            yield break;

        Log($"Resolved role={role}. reason={reason}");

        int retryCount = Mathf.Max(1, _maxRetryCount);
        for (int i = 0; i < retryCount; i++)
        {
            if (!LobbyNetworkRuntime.EnsureSetup(out NetworkManager networkManager, out UnityTransport transport) || networkManager == null || transport == null)
            {
                yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, _retryIntervalSeconds));
                continue;
            }

            if (IsRoleAlreadyRunning(networkManager, role))
                yield break;

            if (networkManager.IsListening)
            {
                networkManager.Shutdown();
                yield return null;
            }

            string host = string.IsNullOrWhiteSpace(_hostAddress) ? "127.0.0.1" : _hostAddress.Trim();
            transport.SetConnectionData(host, _port);

            bool started = role == StartupRole.Host ? networkManager.StartHost() : networkManager.StartClient();
            Log($"Start requested. role={role}, started={started}, host={host}, port={_port}");
            if (started)
                yield break;

            yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, _retryIntervalSeconds));
        }

        Debug.LogWarning("[PlayModeScenario] Failed to start within retry limit.");
    }

    private static bool IsRoleAlreadyRunning(NetworkManager manager, StartupRole role)
    {
        if (!manager.IsListening)
            return false;

        if (role == StartupRole.Host)
            return manager.IsHost;

        if (role == StartupRole.Client)
            return manager.IsClient && !manager.IsServer;

        return false;
    }

    private StartupRole ResolveStartupRole(out string reason)
    {
        if (_startupRole == StartupRole.Host || _startupRole == StartupRole.Client || _startupRole == StartupRole.Disabled)
        {
            reason = "Inspector override";
            return _startupRole;
        }

        if (TryResolveRoleFromCurrentPlayerApi(out StartupRole roleFromApi, out reason))
            return roleFromApi;

        if (TryResolveRoleFromArgs(out StartupRole roleFromArgs, out reason))
            return roleFromArgs;

        reason = "Fallback Host";
        return StartupRole.Host;
    }

    private static bool TryResolveRoleFromCurrentPlayerApi(out StartupRole role, out string reason)
    {
        role = StartupRole.Auto;
        reason = string.Empty;

        Type currentPlayerType = Type.GetType("Unity.Multiplayer.Playmode.CurrentPlayer, Unity.Multiplayer.Playmode") ??
                                 Type.GetType("Unity.Multiplayer.Playmode.CurrentPlayer, Unity.Multiplayer.Playmode.Runtime");
        if (currentPlayerType == null)
            return false;

        if (TryGetStaticBool(currentPlayerType, "IsHost", out bool isHost) && isHost)
        {
            role = StartupRole.Host;
            reason = "CurrentPlayer.IsHost";
            return true;
        }

        if (TryGetStaticBool(currentPlayerType, "IsClient", out bool isClient) && isClient)
        {
            role = StartupRole.Client;
            reason = "CurrentPlayer.IsClient";
            return true;
        }

        if (TryGetStaticInt(currentPlayerType, "PlayerIndex", out int index) || TryGetStaticInt(currentPlayerType, "Index", out index))
        {
            role = index <= 0 ? StartupRole.Host : StartupRole.Client;
            reason = $"CurrentPlayer index={index}";
            return true;
        }

        object tags = GetStaticValue(currentPlayerType, "ReadOnlyTags") ?? GetStaticValue(currentPlayerType, "Tags");
        if (TryResolveRoleFromTags(tags, out role))
        {
            reason = "CurrentPlayer tags";
            return true;
        }

        return false;
    }

    private static bool TryResolveRoleFromArgs(out StartupRole role, out string reason)
    {
        role = StartupRole.Auto;
        reason = string.Empty;

        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (string.IsNullOrWhiteSpace(arg))
                continue;

            if (arg.IndexOf("host", StringComparison.OrdinalIgnoreCase) >= 0 || arg.IndexOf("server", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                role = StartupRole.Host;
                reason = $"arg={arg}";
                return true;
            }

            if (arg.IndexOf("client", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                role = StartupRole.Client;
                reason = $"arg={arg}";
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveRoleFromTags(object tagObject, out StartupRole role)
    {
        role = StartupRole.Auto;
        if (tagObject == null)
            return false;

        IEnumerable values = tagObject as IEnumerable;
        if (values == null)
            return false;

        foreach (object value in values)
        {
            string token = value != null ? value.ToString() : string.Empty;
            if (string.IsNullOrWhiteSpace(token))
                continue;

            if (token.IndexOf("host", StringComparison.OrdinalIgnoreCase) >= 0 || token.IndexOf("server", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                role = StartupRole.Host;
                return true;
            }

            if (token.IndexOf("client", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                role = StartupRole.Client;
                return true;
            }
        }

        return false;
    }

    private static object GetStaticValue(Type type, string memberName)
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        PropertyInfo property = type.GetProperty(memberName, Flags);
        if (property != null)
            return property.GetValue(null);

        MethodInfo method = type.GetMethod(memberName, Flags, Type.DefaultBinder, Type.EmptyTypes, null);
        if (method != null)
            return method.Invoke(null, null);

        return null;
    }

    private static bool TryGetStaticBool(Type type, string memberName, out bool value)
    {
        value = false;
        object raw = GetStaticValue(type, memberName);
        if (raw is bool boolValue)
        {
            value = boolValue;
            return true;
        }

        return false;
    }

    private static bool TryGetStaticInt(Type type, string memberName, out int value)
    {
        value = 0;
        object raw = GetStaticValue(type, memberName);
        if (raw is int intValue)
        {
            value = intValue;
            return true;
        }

        return raw != null && int.TryParse(raw.ToString(), out value);
    }

    private void Log(string message)
    {
        if (_verboseLog)
            Debug.Log($"[PlayModeScenario] {message}");
    }
}

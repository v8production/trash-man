using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CoopUtpGameBootstrap
{
    private static bool _bootRequested;
    private static GameObject _sessionPrefab;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnAfterSceneLoad()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!string.Equals(activeScene.name, Util.GetEnumName(Define.Scene.Game), System.StringComparison.Ordinal))
        {
            return;
        }

        if (_bootRequested)
        {
            return;
        }

        _bootRequested = true;
        EnsureRuntimeBootstrapRunner();
    }

    private static void EnsureRuntimeBootstrapRunner()
    {
        GameObject runnerObject = new GameObject("@CoopUtpBootstrapRunner");
        Object.DontDestroyOnLoad(runnerObject);
        runnerObject.AddComponent<CoopUtpBootstrapRunner>();
    }

    private static GameObject EnsureSessionPrefab()
    {
        if (_sessionPrefab != null)
        {
            return _sessionPrefab;
        }

        _sessionPrefab = new GameObject("CoopNetworkSessionPrefab");
        _sessionPrefab.SetActive(false);
        _sessionPrefab.AddComponent<NetworkObject>();
        _sessionPrefab.AddComponent<CoopNetworkSession>();
        Object.DontDestroyOnLoad(_sessionPrefab);

        return _sessionPrefab;
    }

    private static NetworkManager EnsureNetworkManager()
    {
        NetworkManager manager = Object.FindAnyObjectByType<NetworkManager>();
        if (manager != null)
        {
            return manager;
        }

        GameObject managerObject = new GameObject("@CoopNetworkManager");
        Object.DontDestroyOnLoad(managerObject);

        manager = managerObject.AddComponent<NetworkManager>();
        managerObject.AddComponent<UnityTransport>();

        return manager;
    }

    private static void ConfigureTransport(NetworkManager manager)
    {
        UnityTransport transport = manager.GetComponent<UnityTransport>();
        if (transport == null)
        {
            transport = manager.gameObject.AddComponent<UnityTransport>();
        }

        transport.SetConnectionData(CoopNetLaunchContext.HostAddress, CoopNetLaunchContext.HostPort);
    }

    private static void EnsureNetworkPrefabRegistered(NetworkManager manager)
    {
        GameObject prefab = EnsureSessionPrefab();
        manager.AddNetworkPrefab(prefab);
    }

    private static void EnsureTitanRuntimeControllers()
    {
        TitanCoopBootstrap.AttachControllerIfMissing();

        TitanRigRuntime rig = Object.FindAnyObjectByType<TitanRigRuntime>();
        if (rig == null)
        {
            return;
        }

        if (rig.GetComponent<TitanNetworkRuntimeController>() == null)
        {
            rig.gameObject.AddComponent<TitanNetworkRuntimeController>();
        }
    }

    private static void BootstrapNetwork()
    {
        NetworkManager manager = EnsureNetworkManager();
        if (manager.IsListening)
        {
            return;
        }

        ConfigureTransport(manager);
        EnsureNetworkPrefabRegistered(manager);
        EnsureTitanRuntimeControllers();

        CoopNetStartMode startMode = CoopNetLaunchContext.ResolveStartMode();

        if (startMode == CoopNetStartMode.Host)
        {
            manager.OnServerStarted += SpawnSessionOnServerStart;
            manager.StartHost();
            Debug.Log("[CoopUtpGameBootstrap] Started as host.");
            return;
        }

        manager.StartClient();
        Debug.Log("[CoopUtpGameBootstrap] Started as client.");
    }

    private static void SpawnSessionOnServerStart()
    {
        NetworkManager manager = Object.FindAnyObjectByType<NetworkManager>();
        if (manager == null)
        {
            return;
        }

        manager.OnServerStarted -= SpawnSessionOnServerStart;

        if (Object.FindAnyObjectByType<CoopNetworkSession>() != null)
        {
            return;
        }

        GameObject prefab = EnsureSessionPrefab();
        GameObject instance = Object.Instantiate(prefab);
        instance.SetActive(true);

        NetworkObject networkObject = instance.GetComponent<NetworkObject>();
        networkObject.Spawn();
    }

    private sealed class CoopUtpBootstrapRunner : MonoBehaviour
    {
        private bool _bootDone;

        private void Update()
        {
            if (_bootDone)
            {
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!string.Equals(activeScene.name, Util.GetEnumName(Define.Scene.Game), System.StringComparison.Ordinal))
            {
                return;
            }

            _bootDone = true;
            BootstrapNetwork();
            Destroy(gameObject);
        }
    }
}

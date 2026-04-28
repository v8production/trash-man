using System.Reflection;
using Netcode.Transports;
using Unity.Netcode;
using UnityEngine;

public static class LobbyNetworkRuntime
{
    private const string RuntimeRootName = "@NetworkManager";
    private const string NetworkObjectPrefabPath = "Prefabs/@NetworkObject";

    private static readonly uint LobbyPlayerPrefabHash = ComputeStableHash32("TrashMan.LobbyNetworkPlayerPrefab.v1");

    private static GameObject s_runtimePlayerPrefab;
    private static EntityId s_registeredNetworkManagerInstanceId;

    public static void ShutdownRuntime()
    {
        try
        {
            NetworkManager networkManager = Object.FindAnyObjectByType<NetworkManager>();
            if (networkManager != null)
            {
                if (networkManager.IsListening)
                    networkManager.Shutdown();

                Object.Destroy(networkManager.gameObject);
            }
        }
        catch
        {
            // Intentionally ignore shutdown teardown exceptions.
        }

        if (s_runtimePlayerPrefab != null)
            Object.Destroy(s_runtimePlayerPrefab);

        s_runtimePlayerPrefab = null;
        s_registeredNetworkManagerInstanceId = EntityId.None;
    }

    public static bool EnsureSetup()
    {
        return EnsureSetup(out _, out _);
    }

    public static bool EnsureSetup(
        out NetworkManager networkManager,
        out SteamNetworkingSocketsTransport transport)
    {
        networkManager = Object.FindAnyObjectByType<NetworkManager>();
        transport = networkManager != null
            ? networkManager.GetComponent<SteamNetworkingSocketsTransport>()
            : null;

        if (networkManager != null)
        {
            networkManager.gameObject.name = RuntimeRootName;
            Object.DontDestroyOnLoad(networkManager.gameObject);
        }

        if (networkManager == null)
        {
            GameObject runtimeRoot = GameObject.Find(RuntimeRootName);
            if (runtimeRoot == null)
                runtimeRoot = new GameObject { name = RuntimeRootName };

            Object.DontDestroyOnLoad(runtimeRoot);

            networkManager = runtimeRoot.GetComponent<NetworkManager>();
            if (networkManager == null)
                networkManager = runtimeRoot.AddComponent<NetworkManager>();
        }

        if (transport == null)
        {
            transport = networkManager.GetComponent<SteamNetworkingSocketsTransport>();
            if (transport == null)
                transport = networkManager.gameObject.AddComponent<SteamNetworkingSocketsTransport>();
        }

        GameObject playerPrefab = EnsurePlayerPrefab();
        if (playerPrefab == null)
            return false;

        if (networkManager.NetworkConfig == null)
            networkManager.NetworkConfig = new NetworkConfig();

        if (networkManager.NetworkConfig.Prefabs == null)
            networkManager.NetworkConfig.Prefabs = new NetworkPrefabs();

        networkManager.NetworkConfig.NetworkTransport = transport;
        networkManager.NetworkConfig.EnableSceneManagement = false;
        networkManager.NetworkConfig.ConnectionApproval = false;
        networkManager.NetworkConfig.ForceSamePrefabs = false;
        networkManager.NetworkConfig.PlayerPrefab = playerPrefab;

        EnsureNetworkPrefabRegistered(networkManager, playerPrefab);
        return true;
    }

    private static GameObject EnsurePlayerPrefab()
    {
        if (s_runtimePlayerPrefab != null)
            return s_runtimePlayerPrefab;

        GameObject networkObjectPrefab = Managers.Resource.Load<GameObject>(NetworkObjectPrefabPath);
        if (networkObjectPrefab == null)
        {
            Debug.LogError($"[Lobby] Failed to load runtime player prefab at {NetworkObjectPrefabPath}.");
            return null;
        }

        s_runtimePlayerPrefab = Object.Instantiate(networkObjectPrefab);
        s_runtimePlayerPrefab.name = "@NetworkObject";
        s_runtimePlayerPrefab.transform.position = Vector3.zero;
        s_runtimePlayerPrefab.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;

        NetworkObject networkObject = s_runtimePlayerPrefab.GetorAddComponent<NetworkObject>();
        EnsureGlobalObjectIdHash(networkObject, LobbyPlayerPrefabHash);

        // The visible Ranger is a separate lobby-local object.
        // The network player object must carry transform replication so other clients can see movement.
        s_runtimePlayerPrefab.GetorAddComponent<OwnerNetworkTransform>();

        s_runtimePlayerPrefab.GetorAddComponent<LobbyNetworkPlayer>();

        return s_runtimePlayerPrefab;
    }

    private static void EnsureGlobalObjectIdHash(NetworkObject networkObject, uint hash)
    {
        FieldInfo hashField = typeof(NetworkObject).GetField("GlobalObjectIdHash", BindingFlags.Instance | BindingFlags.NonPublic);
        if (hashField == null)
            return;

        // Always overwrite so we don't carry a stale/duplicate serialized value from prefab mode.
        // This prefab is created at runtime and must use a deterministic hash.
        hashField.SetValue(networkObject, hash);
    }

    private static void EnsureNetworkPrefabRegistered(NetworkManager networkManager, GameObject prefab)
    {
        if (networkManager == null || prefab == null)
            return;

        EntityId instanceId = networkManager.GetEntityId();
        if (instanceId.IsValid() && s_registeredNetworkManagerInstanceId == instanceId)
            return;

        NetworkPrefabs prefabs = networkManager.NetworkConfig != null ? networkManager.NetworkConfig.Prefabs : null;
        if (prefabs != null && IsPrefabAlreadyRegistered(prefabs, prefab))
        {
            if (instanceId.IsValid())
                s_registeredNetworkManagerInstanceId = instanceId;
            return;
        }

        networkManager.AddNetworkPrefab(prefab);

        if (instanceId.IsValid())
            s_registeredNetworkManagerInstanceId = instanceId;
    }

    private static bool IsPrefabAlreadyRegistered(NetworkPrefabs prefabs, GameObject prefab)
    {
        if (prefabs == null || prefab == null)
            return false;

        // NetworkPrefabs API/fields vary across NGO versions; use reflection to stay resilient.
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        object listObj = null;
        PropertyInfo prop = prefabs.GetType().GetProperty("Prefabs", Flags);
        if (prop != null)
            listObj = prop.GetValue(prefabs);

        if (listObj == null)
        {
            FieldInfo field = prefabs.GetType().GetField("m_Prefabs", Flags);
            if (field != null)
                listObj = field.GetValue(prefabs);
        }

        if (listObj is not System.Collections.IEnumerable enumerable)
            return false;

        foreach (object entry in enumerable)
        {
            if (entry == null)
                continue;

            object entryPrefab = null;
            FieldInfo prefabField = entry.GetType().GetField("Prefab", Flags);
            if (prefabField != null)
                entryPrefab = prefabField.GetValue(entry);
            else
            {
                PropertyInfo prefabProp = entry.GetType().GetProperty("Prefab", Flags);
                if (prefabProp != null)
                    entryPrefab = prefabProp.GetValue(entry);
            }

            if (ReferenceEquals(entryPrefab, prefab))
                return true;
        }

        return false;
    }

    private static uint ComputeStableHash32(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;

        // FNV-1a 32-bit (deterministic, fast, no allocations).
        const uint OffsetBasis = 2166136261u;
        const uint Prime = 16777619u;

        uint hash = OffsetBasis;
        for (int i = 0; i < value.Length; i++)
        {
            hash ^= value[i];
            hash *= Prime;
        }

        // NGO treats 0 as "unset" for some validation paths; avoid it.
        return hash != 0 ? hash : 1u;
    }
}

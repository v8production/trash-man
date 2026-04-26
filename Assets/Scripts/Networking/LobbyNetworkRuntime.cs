using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public static class LobbyNetworkRuntime
{
    private const string RuntimeRootName = "@NetworkManager";
    private const string NetworkObjectPrefabPath = "Prefabs/@NetworkObject";
    // Deterministic per-project hash for the runtime-created lobby player prefab.
    // Must be stable across host/client, and must not collide with any authored NetworkObject prefab hashes.
    private static readonly uint LobbyPlayerPrefabHash = ComputeStableHash32("TrashMan.LobbyNetworkPlayerPrefab.v1");

    private static GameObject s_runtimePlayerPrefab;
    private static bool s_inputDebugBootLogged;
    private static int s_registeredNetworkManagerInstanceId;

    public static bool EnsureSetup()
    {
        return EnsureSetup(out _, out _);
    }

    public static bool EnsureSetup(out NetworkManager networkManager, out UnityTransport transport)
    {
        networkManager = Object.FindAnyObjectByType<NetworkManager>();
        transport = networkManager != null ? networkManager.GetComponent<UnityTransport>() : null;

        if (!s_inputDebugBootLogged)
        {
            s_inputDebugBootLogged = true;
            Debug.Log($"{InputDebug.Prefix} Boot Debug.isDebugBuild={Debug.isDebugBuild} Enabled={InputDebug.Enabled}");
        }

        // Keep the NetworkManager under a stable, cross-scene runtime root name.
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
            transport = networkManager.GetComponent<UnityTransport>();
            if (transport == null)
                transport = networkManager.gameObject.AddComponent<UnityTransport>();
        }

        GameObject playerPrefab = EnsurePlayerPrefab();
        if (playerPrefab == null)
            return false;

        if (networkManager.NetworkConfig == null)
            networkManager.NetworkConfig = new NetworkConfig();

        if (networkManager.NetworkConfig.Prefabs == null)
            networkManager.NetworkConfig.Prefabs = new NetworkPrefabs();

        networkManager.NetworkConfig.NetworkTransport = transport;
        // The lobby is entered via plain Unity scene loads before NGO connects host/client.
        // Keeping NGO scene management enabled here can block client synchronization/player spawn
        // until a disconnect/promotion cycle occurs. GameScene already has a manual ClientRpc fallback,
        // so the lobby runtime should stay out of NGO scene synchronization.
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

        int instanceId = networkManager.GetInstanceID();
        if (instanceId != 0 && s_registeredNetworkManagerInstanceId == instanceId)
            return;

        NetworkPrefabs prefabs = networkManager.NetworkConfig != null ? networkManager.NetworkConfig.Prefabs : null;
        if (prefabs != null && IsPrefabAlreadyRegistered(prefabs, prefab))
        {
            if (instanceId != 0)
                s_registeredNetworkManagerInstanceId = instanceId;
            return;
        }

        networkManager.AddNetworkPrefab(prefab);

        if (instanceId != 0)
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

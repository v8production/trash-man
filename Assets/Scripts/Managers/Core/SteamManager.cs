using Steamworks;
using UnityEngine;

public class SteamManager
{
    private const uint AppId = 480; // 개발 중 Spacewar. 출시 전 실제 AppID로 교체.
    private bool _initialized;

    public bool IsInitialized => _initialized;
    public CSteamID LocalSteamId => _initialized ? SteamUser.GetSteamID() : CSteamID.Nil;

    public void Init()
    {
        if (_initialized)
            return;

        try
        {
            if (!SteamAPI.Init())
            {
                Debug.LogWarning("[Steam] SteamAPI.Init failed. Is Steam client running?");
                return;
            }

            SteamNetworkingUtils.InitRelayNetworkAccess();

            _initialized = true;
            Debug.Log($"[Steam] Initialized. LocalSteamId={LocalSteamId.m_SteamID}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Steam] Init failed: {e}");
            _initialized = false;
        }
    }

    public void OnUpdate()
    {
        if (!_initialized)
            return;

        SteamAPI.RunCallbacks();
    }

    public void Clear()
    {
        if (!_initialized)
            return;

        SteamAPI.Shutdown();
        _initialized = false;
    }
}
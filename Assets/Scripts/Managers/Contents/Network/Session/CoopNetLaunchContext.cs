using UnityEngine;

public enum CoopNetStartMode
{
    Auto = 0,
    Host = 1,
    Client = 2,
}

public static class CoopNetLaunchContext
{
    public static CoopNetStartMode StartMode = CoopNetStartMode.Auto;
    public static string HostAddress = "127.0.0.1";
    public static ushort HostPort = 7777;

    public static string LocalDiscordUserId = string.Empty;

    public static bool HasLobbyRoleAssignment;
    public static Define.TitanRole LobbyAssignedRole = Define.TitanRole.Body;

    public static void Configure(CoopNetStartMode mode, string hostAddress, ushort hostPort)
    {
        StartMode = mode;
        HostAddress = string.IsNullOrWhiteSpace(hostAddress) ? "127.0.0.1" : hostAddress;
        HostPort = hostPort == 0 ? (ushort)7777 : hostPort;
    }

    public static void SetLobbyRole(Define.TitanRole role)
    {
        HasLobbyRoleAssignment = true;
        LobbyAssignedRole = role;
    }

    public static CoopNetStartMode ResolveStartMode()
    {
        if (StartMode != CoopNetStartMode.Auto)
        {
            return StartMode;
        }

        if (Managers.Coop.SessionState == CoopSessionState.Active)
        {
            if (!string.IsNullOrWhiteSpace(LocalDiscordUserId)
                && Managers.Coop.Members.TryGetValue(LocalDiscordUserId, out CoopMember localMember))
            {
                return localMember.IsHost ? CoopNetStartMode.Host : CoopNetStartMode.Client;
            }
        }

        if (Application.isEditor)
        {
            return CoopNetStartMode.Host;
        }

        return CoopNetStartMode.Client;
    }
}

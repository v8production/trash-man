using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class TitanRoleManager
{
    private readonly Dictionary<Define.TitanRole, LobbyNetworkPlayer> _playersByRole = new();
    private readonly Dictionary<ulong, Define.TitanRole> _rolesByClientId = new();

    public void Init()
    {
        Clear();
    }

    public void Clear()
    {
        _playersByRole.Clear();
        _rolesByClientId.Clear();
    }

    public bool RefreshRoleMap(bool requireAllRoles, out string error)
    {
        error = string.Empty;
        _playersByRole.Clear();
        _rolesByClientId.Clear();

        LobbyNetworkPlayer[] players = UnityEngine.Object.FindObjectsByType<LobbyNetworkPlayer>();
        if (players == null || players.Length == 0)
        {
            error = "No lobby players were found.";
            return false;
        }

        for (int i = 0; i < players.Length; i++)
        {
            LobbyNetworkPlayer player = players[i];
            if (player == null || !player.IsSpawned)
                continue;

            if (!player.TryGetSelectedRole(out Define.TitanRole role))
            {
                error = $"Player {player.OwnerClientId} has no selected role.";
                return false;
            }

            if (_playersByRole.TryGetValue(role, out LobbyNetworkPlayer existing) && existing != null)
            {
                error = $"Duplicate role selected: {role}";
                return false;
            }

            _playersByRole[role] = player;
            _rolesByClientId[player.OwnerClientId] = role;
        }

        if (requireAllRoles)
        {
            for (int roleValue = (int)Define.TitanRole.Body; roleValue <= (int)Define.TitanRole.RightLeg; roleValue++)
            {
                Define.TitanRole role = (Define.TitanRole)roleValue;
                if (!_playersByRole.ContainsKey(role))
                {
                    error = $"Missing role owner: {role}";
                    return false;
                }
            }
        }

        return _playersByRole.Count > 0;
    }

    public bool TryGetLocalRole(out Define.TitanRole role)
    {
        role = Define.TitanRole.Body;
        if (!RefreshRoleMap(false, out _))
            return false;

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null)
            return false;

        return _rolesByClientId.TryGetValue(networkManager.LocalClientId, out role);
    }

    public bool TryGetRoleInput(Define.TitanRole role, out TitanAggregatedInput input)
    {
        input = default;
        if (!RefreshRoleMap(false, out _))
            return false;

        if (!_playersByRole.TryGetValue(role, out LobbyNetworkPlayer player) || player == null)
            return false;

        input = player.CurrentRoleInput.ToAggregatedInput();
        return true;
    }
}

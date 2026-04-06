using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public struct TitanRoleInputNetState : INetworkSerializable
{
    public int Role;
    public float AimX;
    public float AimY;
    public float PrimaryAxis;
    public float SecondaryAxis;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Role);
        serializer.SerializeValue(ref AimX);
        serializer.SerializeValue(ref AimY);
        serializer.SerializeValue(ref PrimaryAxis);
        serializer.SerializeValue(ref SecondaryAxis);
    }
}

public struct TitanPoseNetState : INetworkSerializable
{
    public Vector3 RootPosition;
    public Quaternion RootRotation;

    public Quaternion LeftShoulder;
    public Quaternion LeftElbow;
    public Quaternion RightShoulder;
    public Quaternion RightElbow;
    public Quaternion LeftHip;
    public Quaternion LeftKnee;
    public Quaternion RightHip;
    public Quaternion RightKnee;
    public Quaternion Spine;

    public bool Valid;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref RootPosition);
        serializer.SerializeValue(ref RootRotation);
        serializer.SerializeValue(ref LeftShoulder);
        serializer.SerializeValue(ref LeftElbow);
        serializer.SerializeValue(ref RightShoulder);
        serializer.SerializeValue(ref RightElbow);
        serializer.SerializeValue(ref LeftHip);
        serializer.SerializeValue(ref LeftKnee);
        serializer.SerializeValue(ref RightHip);
        serializer.SerializeValue(ref RightKnee);
        serializer.SerializeValue(ref Spine);
        serializer.SerializeValue(ref Valid);
    }
}

public sealed class CoopNetworkSession : NetworkBehaviour
{
    private static readonly Define.TitanRole[] OrderedRoles =
    {
        Define.TitanRole.Body,
        Define.TitanRole.LeftArm,
        Define.TitanRole.RightArm,
        Define.TitanRole.LeftLeg,
        Define.TitanRole.RightLeg,
    };

    private readonly Dictionary<Define.TitanRole, TitanRoleInputNetState> _hostInputStates = new();

    private readonly Dictionary<ulong, Define.TitanRole> _serverRoleByClientId = new();
    private readonly Dictionary<Define.TitanRole, TitanRoleRuntimeInput> _runtimeInputStates = new();

    private readonly NetworkVariable<TitanPoseNetState> _authoritativePose =
        new(writePerm: NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> _connectedPlayerCount =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private TitanNetworkRuntimeController _runtimeController;
    private bool _loggedMissingRuntimeController;

    public Define.TitanRole LocalRole { get; private set; }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            _hostInputStates.Clear();
            RebuildRoleAssignments();
        }

        LocalRole = 0;

        _authoritativePose.OnValueChanged += OnAuthoritativePoseChanged;
        ResolveRuntimeController();
    }

    public override void OnNetworkDespawn()
    {
        _authoritativePose.OnValueChanged -= OnAuthoritativePoseChanged;

        if (NetworkManager != null && IsServer)
        {
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            _hostInputStates.Clear();
        }

        LocalRole = 0;

        base.OnNetworkDespawn();
    }

    private void Update()
    {
        if (!IsClient || LocalRole == 0)
        {
            return;
        }

        if (!ResolveRuntimeController())
        {
            return;
        }

        TitanRoleRuntimeInput runtimeInput = _runtimeController.CaptureLocalInput(LocalRole);
        TitanRoleInputNetState localInput = ToNetInput(runtimeInput);

        if (IsServer)
        {
            _hostInputStates[LocalRole] = localInput;
        }
        else
        {
            SubmitRoleInputServerRpc(localInput);
        }
    }

    private void FixedUpdate()
    {
        if (!IsServer || !ResolveRuntimeController())
        {
            return;
        }

        _runtimeInputStates.Clear();
        foreach (KeyValuePair<Define.TitanRole, TitanRoleInputNetState> pair in _hostInputStates)
        {
            _runtimeInputStates[pair.Key] = ToRuntimeInput(pair.Value);
        }

        if (!_runtimeController.SimulateAuthoritativeStep(_runtimeInputStates, Time.fixedDeltaTime, out TitanRuntimePoseState runtimePose))
        {
            return;
        }

        _authoritativePose.Value = ToNetPose(runtimePose);
    }

    private bool ResolveRuntimeController()
    {
        if (_runtimeController != null && _runtimeController.EnsureReady())
        {
            return true;
        }

        _runtimeController = FindAnyObjectByType<TitanNetworkRuntimeController>();
        if (_runtimeController == null)
        {
            if (!_loggedMissingRuntimeController)
            {
                _loggedMissingRuntimeController = true;
                Debug.LogWarning("[CoopNetworkSession] TitanNetworkRuntimeController is missing. Ensure bootstrap attaches runtime controllers before session starts.");
            }

            return false;
        }

        _loggedMissingRuntimeController = false;
        return _runtimeController.EnsureReady();
    }

    private void OnClientConnected(ulong _)
    {
        RebuildRoleAssignments();
    }

    private void OnClientDisconnected(ulong _)
    {
        RebuildRoleAssignments();
    }

    private void RebuildRoleAssignments()
    {
        if (!IsServer || NetworkManager == null)
        {
            return;
        }

        _serverRoleByClientId.Clear();

        List<ulong> connectedIds = NetworkManager.ConnectedClientsIds.OrderBy(id => id).ToList();
        _connectedPlayerCount.Value = connectedIds.Count;

        for (int i = 0; i < connectedIds.Count; i++)
        {
            ulong clientId = connectedIds[i];
            Define.TitanRole role = i < OrderedRoles.Length ? OrderedRoles[i] : 0;

            if (CoopNetLaunchContext.HasLobbyRoleAssignment && clientId == NetworkManager.ServerClientId)
            {
                role = CoopNetLaunchContext.LobbyAssignedRole;
            }

            _serverRoleByClientId[clientId] = role;

            ClientRpcParams target = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { clientId },
                },
            };

            AssignRoleClientRpc((int)role, target);
        }

        HashSet<Define.TitanRole> activeRoles = new HashSet<Define.TitanRole>(_serverRoleByClientId.Values);
        List<Define.TitanRole> staleRoles = _hostInputStates.Keys.Where(role => !activeRoles.Contains(role)).ToList();
        for (int i = 0; i < staleRoles.Count; i++)
        {
            _hostInputStates.Remove(staleRoles[i]);
        }
    }

    [ClientRpc]
    private void AssignRoleClientRpc(int role, ClientRpcParams rpcParams = default)
    {
        LocalRole = (Define.TitanRole)role;
        Debug.Log($"[CoopNetworkSession] Local role assigned: {LocalRole}");
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitRoleInputServerRpc(TitanRoleInputNetState input, ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        if (!_serverRoleByClientId.TryGetValue(senderClientId, out Define.TitanRole assignedRole))
        {
            return;
        }

        if ((Define.TitanRole)input.Role != assignedRole)
        {
            return;
        }

        _hostInputStates[assignedRole] = input;
    }

    private void OnAuthoritativePoseChanged(TitanPoseNetState _, TitanPoseNetState nextValue)
    {
        if (IsServer)
        {
            return;
        }

        ApplyAuthoritativePose(nextValue);
    }

    private void ApplyAuthoritativePose(TitanPoseNetState state)
    {
        if (!state.Valid || !ResolveRuntimeController())
        {
            return;
        }

        _runtimeController.ApplyRuntimePose(ToRuntimePose(state));
    }

    private static TitanRoleRuntimeInput ToRuntimeInput(TitanRoleInputNetState input)
    {
        return new TitanRoleRuntimeInput
        {
            Role = (Define.TitanRole)input.Role,
            AimX = input.AimX,
            AimY = input.AimY,
            PrimaryAxis = input.PrimaryAxis,
            SecondaryAxis = input.SecondaryAxis,
        };
    }

    private static TitanRoleInputNetState ToNetInput(TitanRoleRuntimeInput input)
    {
        return new TitanRoleInputNetState
        {
            Role = (int)input.Role,
            AimX = input.AimX,
            AimY = input.AimY,
            PrimaryAxis = input.PrimaryAxis,
            SecondaryAxis = input.SecondaryAxis,
        };
    }

    private static TitanRuntimePoseState ToRuntimePose(TitanPoseNetState pose)
    {
        return new TitanRuntimePoseState
        {
            RootPosition = pose.RootPosition,
            RootRotation = pose.RootRotation,
            LeftShoulder = pose.LeftShoulder,
            LeftElbow = pose.LeftElbow,
            RightShoulder = pose.RightShoulder,
            RightElbow = pose.RightElbow,
            LeftHip = pose.LeftHip,
            LeftKnee = pose.LeftKnee,
            RightHip = pose.RightHip,
            RightKnee = pose.RightKnee,
            Spine = pose.Spine,
            Valid = pose.Valid,
        };
    }

    private static TitanPoseNetState ToNetPose(TitanRuntimePoseState pose)
    {
        return new TitanPoseNetState
        {
            RootPosition = pose.RootPosition,
            RootRotation = pose.RootRotation,
            LeftShoulder = pose.LeftShoulder,
            LeftElbow = pose.LeftElbow,
            RightShoulder = pose.RightShoulder,
            RightElbow = pose.RightElbow,
            LeftHip = pose.LeftHip,
            LeftKnee = pose.LeftKnee,
            RightHip = pose.RightHip,
            RightKnee = pose.RightKnee,
            Spine = pose.Spine,
            Valid = pose.Valid,
        };
    }
}

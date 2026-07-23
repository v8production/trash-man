using System;
using System.Collections.Generic;
using TMPro;

public class UI_Roles : UI_Base
{
    private const string EmptyNicknameText = "-";

    private bool _isInitialized;
    private readonly Dictionary<Define.TitanRole, List<string>> _namesByRole = new();

    enum Texts
    {
        TorsoNickname,
        LeftArmNickname,
        RightArmNickname,
        LeftLegNickname,
        RightLegNickname,
    }

    public override void Init()
    {
        if (_isInitialized)
            return;

        Bind<TextMeshProUGUI>(typeof(Texts));
        LobbyNetworkPlayer.GameRoleMappingChanged -= CaptureCurrentRoleMapping;
        LobbyNetworkPlayer.GameRoleMappingChanged += CaptureCurrentRoleMapping;

        _isInitialized = true;
    }

    private void OnEnable()
    {
        Init();
        ApplyStoredRoleNicknames();
    }

    private void OnDestroy()
    {
        LobbyNetworkPlayer.GameRoleMappingChanged -= CaptureCurrentRoleMapping;
    }

    public void CaptureCurrentRoleMapping()
    {
        Init();

        _namesByRole.Clear();
        LobbyNetworkPlayer[] players = LobbyNetworkPlayer.FindAllSpawnedPlayers();

        for (int i = 0; i < players.Length; i++)
        {
            LobbyNetworkPlayer player = players[i];
            if (player == null)
                continue;

            string displayName = player.DisplayName;
            if (string.IsNullOrWhiteSpace(displayName))
                continue;

            int roleMask = player.SelectedTitanRoleMaskValue;
            if (roleMask == 0)
                continue;

            AddRoleNameIfSelected(_namesByRole, Define.TitanRole.Torso, roleMask, displayName);
            AddRoleNameIfSelected(_namesByRole, Define.TitanRole.LeftArm, roleMask, displayName);
            AddRoleNameIfSelected(_namesByRole, Define.TitanRole.RightArm, roleMask, displayName);
            AddRoleNameIfSelected(_namesByRole, Define.TitanRole.LeftLeg, roleMask, displayName);
            AddRoleNameIfSelected(_namesByRole, Define.TitanRole.RightLeg, roleMask, displayName);
        }

        ApplyStoredRoleNicknames();
    }

    private void ApplyStoredRoleNicknames()
    {
        ApplyRoleNicknameText(Define.TitanRole.Torso, Texts.TorsoNickname);
        ApplyRoleNicknameText(Define.TitanRole.LeftArm, Texts.LeftArmNickname);
        ApplyRoleNicknameText(Define.TitanRole.RightArm, Texts.RightArmNickname);
        ApplyRoleNicknameText(Define.TitanRole.LeftLeg, Texts.LeftLegNickname);
        ApplyRoleNicknameText(Define.TitanRole.RightLeg, Texts.RightLegNickname);
    }

    private void ApplyRoleNicknameText(Define.TitanRole role, Texts targetText)
    {
        if (!_namesByRole.TryGetValue(role, out List<string> names) || names == null || names.Count == 0)
        {
            SetNicknameText(targetText, EmptyNicknameText);
            return;
        }

        names.Sort(StringComparer.OrdinalIgnoreCase);
        SetNicknameText(targetText, string.Join("\n", names));
    }

    private static void AddRoleNameIfSelected(Dictionary<Define.TitanRole, List<string>> namesByRole, Define.TitanRole role, int roleMask, string displayName)
    {
        int roleValue = (int)role;
        int bit = 1 << (roleValue - (int)Define.TitanRole.Torso);
        if ((roleMask & bit) == 0)
            return;

        if (!namesByRole.TryGetValue(role, out List<string> list))
        {
            list = new List<string>();
            namesByRole[role] = list;
        }

        list.Add(displayName);
    }

    private void SetNicknameText(Texts textId, string value)
    {
        TextMeshProUGUI text = GetText((int)textId);
        if (text == null)
            return;

        text.text = value ?? string.Empty;
    }
}

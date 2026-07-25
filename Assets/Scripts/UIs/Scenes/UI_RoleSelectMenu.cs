using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_RoleSelectMenu : UI_Scene
{
    private const int CanvasOrder = 10;
    private const float NicknameRefreshIntervalSeconds = 0.25f;
    private const float RoleFaceSpritePixelsPerUnit = 32f;

    private bool _isInitialized;
    private float _nextNicknameRefreshTime;
    private readonly Dictionary<Define.TitanRole, Image> _roleImages = new();
    private readonly Dictionary<Define.TitanRole, Sprite> _roleFaceSprites = new();
    private readonly Dictionary<Define.TitanRole, Texture2D> _roleFaceSpriteTextures = new();

    private enum GameObjects
    {
        Background,
        TitanLayout,
    }

    private enum Buttons
    {
        Cancel,
        Torso,
        LeftArm,
        RightArm,
        LeftLeg,
        RightLeg,
    }

    enum Texts
    {
        TorsoNickname,
        LeftArmNickname,
        RightArmNickname,
        LeftLegNickname,
        RightLegNickname,
    }

    public event Action<Define.TitanRole> RoleSelected;
    public event Action Closed;

    public override void Init()
    {
        if (_isInitialized)
            return;

        base.Init();
        Managers.UI.ShowCanvas(gameObject, CanvasOrder);
        Bind<GameObject>(typeof(GameObjects));
        Bind<Button>(typeof(Buttons));
        Bind<TextMeshProUGUI>(typeof(Texts));
        BindTitanRoleImages();

        GetButton((int)Buttons.Cancel).gameObject.BindEvent(OnCancelClicked);
        GetButton((int)Buttons.Torso).gameObject.BindEvent(_ => NotifyRoleSelected(Define.TitanRole.Torso));
        GetButton((int)Buttons.LeftArm).gameObject.BindEvent(_ => NotifyRoleSelected(Define.TitanRole.LeftArm));
        GetButton((int)Buttons.RightArm).gameObject.BindEvent(_ => NotifyRoleSelected(Define.TitanRole.RightArm));
        GetButton((int)Buttons.LeftLeg).gameObject.BindEvent(_ => NotifyRoleSelected(Define.TitanRole.LeftLeg));
        GetButton((int)Buttons.RightLeg).gameObject.BindEvent(_ => NotifyRoleSelected(Define.TitanRole.RightLeg));

        _isInitialized = true;
    }

    private void OnEnable()
    {
        LobbyNetworkPlayer.LobbyRolePresentationChanged -= RefreshRoleNicknames;
        LobbyNetworkPlayer.LobbyRolePresentationChanged += RefreshRoleNicknames;
        _nextNicknameRefreshTime = 0f;
        RefreshRoleNicknames();
    }

    private void OnDisable()
    {
        LobbyNetworkPlayer.LobbyRolePresentationChanged -= RefreshRoleNicknames;
    }

    private void Update()
    {
        if (!_isInitialized || !gameObject.activeInHierarchy)
            return;

        if (Time.unscaledTime < _nextNicknameRefreshTime)
            return;

        _nextNicknameRefreshTime = Time.unscaledTime + NicknameRefreshIntervalSeconds;
        RefreshRoleNicknames();
    }

    private void OnDestroy()
    {
        LobbyNetworkPlayer.LobbyRolePresentationChanged -= RefreshRoleNicknames;
        ClearRoleFaceSprites();
        RoleSelected = null;
        Closed = null;
    }

    private void OnCancelClicked(PointerEventData eventData)
    {
        Closed?.Invoke();
    }

    private void NotifyRoleSelected(Define.TitanRole role)
    {
        RoleSelected?.Invoke(role);
    }

    public void RefreshRoleNicknames()
    {
        LobbyNetworkPlayer[] players = FindObjectsByType<LobbyNetworkPlayer>();
        Dictionary<Define.TitanRole, List<string>> namesByRole = new();
        Dictionary<Define.TitanRole, LobbyNetworkPlayer> playersByRole = new();
        int localRoleMask = 0;
        int occupiedByOtherMask = 0;

        if (players != null && players.Length > 0)
        {
            for (int i = 0; i < players.Length; i++)
            {
                LobbyNetworkPlayer player = players[i];
                if (player == null)
                    continue;

                int roleMask = 0;
                // Prefer the lobby registry because it is updated immediately for the local player
                // (LobbyScene.TryToggleLocalRole -> RegisterUserPartSelection), while the network variable
                // update can arrive slightly later.
                if (player.TryGetLobbyUserId(out string lobbyUserId)
                    && LobbyScene.TryGetRegisteredUserSelectedRoleMask(lobbyUserId, out int registeredMask))
                {
                    roleMask = registeredMask;
                }
                else
                {
                    roleMask = player.SelectedTitanRoleMaskValue;
                }

                if (roleMask == 0)
                    continue;

                if (player.IsOwner)
                    localRoleMask |= roleMask;
                else
                    occupiedByOtherMask |= roleMask;

                AddRolePlayerIfSelected(playersByRole, Define.TitanRole.Torso, roleMask, player);
                AddRolePlayerIfSelected(playersByRole, Define.TitanRole.LeftArm, roleMask, player);
                AddRolePlayerIfSelected(playersByRole, Define.TitanRole.RightArm, roleMask, player);
                AddRolePlayerIfSelected(playersByRole, Define.TitanRole.LeftLeg, roleMask, player);
                AddRolePlayerIfSelected(playersByRole, Define.TitanRole.RightLeg, roleMask, player);

                string displayName = player.DisplayName;
                if (string.IsNullOrWhiteSpace(displayName))
                    continue;

                AddRoleNameIfSelected(namesByRole, Define.TitanRole.Torso, roleMask, displayName);
                AddRoleNameIfSelected(namesByRole, Define.TitanRole.LeftArm, roleMask, displayName);
                AddRoleNameIfSelected(namesByRole, Define.TitanRole.RightArm, roleMask, displayName);
                AddRoleNameIfSelected(namesByRole, Define.TitanRole.LeftLeg, roleMask, displayName);
                AddRoleNameIfSelected(namesByRole, Define.TitanRole.RightLeg, roleMask, displayName);
            }
        }

        ApplyRoleNicknameText(namesByRole, Define.TitanRole.Torso, Texts.TorsoNickname);
        ApplyRoleNicknameText(namesByRole, Define.TitanRole.LeftArm, Texts.LeftArmNickname);
        ApplyRoleNicknameText(namesByRole, Define.TitanRole.RightArm, Texts.RightArmNickname);
        ApplyRoleNicknameText(namesByRole, Define.TitanRole.LeftLeg, Texts.LeftLegNickname);
        ApplyRoleNicknameText(namesByRole, Define.TitanRole.RightLeg, Texts.RightLegNickname);
        ApplyRoleButtonInteractable(Define.TitanRole.Torso, Buttons.Torso, localRoleMask, occupiedByOtherMask);
        ApplyRoleButtonInteractable(Define.TitanRole.LeftArm, Buttons.LeftArm, localRoleMask, occupiedByOtherMask);
        ApplyRoleButtonInteractable(Define.TitanRole.RightArm, Buttons.RightArm, localRoleMask, occupiedByOtherMask);
        ApplyRoleButtonInteractable(Define.TitanRole.LeftLeg, Buttons.LeftLeg, localRoleMask, occupiedByOtherMask);
        ApplyRoleButtonInteractable(Define.TitanRole.RightLeg, Buttons.RightLeg, localRoleMask, occupiedByOtherMask);
        ApplyRoleImage(playersByRole, Define.TitanRole.Torso);
        ApplyRoleImage(playersByRole, Define.TitanRole.LeftArm);
        ApplyRoleImage(playersByRole, Define.TitanRole.RightArm);
        ApplyRoleImage(playersByRole, Define.TitanRole.LeftLeg);
        ApplyRoleImage(playersByRole, Define.TitanRole.RightLeg);
    }

    private void BindTitanRoleImages()
    {
        _roleImages.Clear();
        GameObject titanLayout = GetObject((int)GameObjects.TitanLayout);
        if (titanLayout == null)
            return;

        BindTitanRoleImage(titanLayout, Define.TitanRole.Torso);
        BindTitanRoleImage(titanLayout, Define.TitanRole.LeftArm);
        BindTitanRoleImage(titanLayout, Define.TitanRole.RightArm);
        BindTitanRoleImage(titanLayout, Define.TitanRole.LeftLeg);
        BindTitanRoleImage(titanLayout, Define.TitanRole.RightLeg);
    }

    private void BindTitanRoleImage(GameObject titanLayout, Define.TitanRole role)
    {
        Image image = Util.FindChild<Image>(titanLayout, role.ToString());
        if (image == null)
            return;

        image.preserveAspect = true;
        HideRoleImage(image);
        _roleImages[role] = image;
    }

    private void ApplyRoleNicknameText(Dictionary<Define.TitanRole, List<string>> namesByRole, Define.TitanRole role, Texts targetText)
    {
        if (!namesByRole.TryGetValue(role, out List<string> names) || names == null || names.Count == 0)
        {
            SetNicknameText(targetText, string.Empty);
            return;
        }

        names.Sort(StringComparer.OrdinalIgnoreCase);
        SetNicknameText(targetText, string.Join("\n", names));
    }

    private void ApplyRoleButtonInteractable(Define.TitanRole role, Buttons targetButton, int localRoleMask, int occupiedByOtherMask)
    {
        Button button = GetButton((int)targetButton);
        if (button == null)
            return;

        int bit = 1 << (((int)role) - (int)Define.TitanRole.Torso);
        bool isSelectedByLocalPlayer = (localRoleMask & bit) != 0;
        bool isSelectedByOtherPlayer = (occupiedByOtherMask & bit) != 0;
        button.interactable = isSelectedByLocalPlayer || !isSelectedByOtherPlayer;
    }

    private void ApplyRoleImage(Dictionary<Define.TitanRole, LobbyNetworkPlayer> playersByRole, Define.TitanRole role)
    {
        if (!_roleImages.TryGetValue(role, out Image image) || image == null)
            return;

        if (!playersByRole.TryGetValue(role, out LobbyNetworkPlayer player) || player == null)
        {
            HideRoleImage(image);
            return;
        }

        Texture2D faceTexture = player.TryGetRangerFaceTexture(out Texture2D customFaceTexture)
            ? customFaceTexture
            : RangerFaceTextureStore.LoadDefaultFaceTexture();
        if (faceTexture == null)
        {
            HideRoleImage(image);
            return;
        }

        image.gameObject.SetActive(true);
        image.sprite = GetOrCreateRoleFaceSprite(role, faceTexture);
        image.preserveAspect = true;
        image.color = Color.white;
    }

    private Sprite GetOrCreateRoleFaceSprite(Define.TitanRole role, Texture2D faceTexture)
    {
        if (_roleFaceSpriteTextures.TryGetValue(role, out Texture2D cachedTexture)
            && cachedTexture == faceTexture
            && _roleFaceSprites.TryGetValue(role, out Sprite cachedSprite)
            && cachedSprite != null)
        {
            return cachedSprite;
        }

        ClearRoleFaceSprite(role);
        Rect faceRect = new(0f, RangerFaceTextureStore.TextureHeight, RangerFaceTextureStore.TextureWidth, RangerFaceTextureStore.TextureHeight);
        Sprite sprite = Sprite.Create(faceTexture, faceRect, new Vector2(0.5f, 0.5f), RoleFaceSpritePixelsPerUnit);
        _roleFaceSprites[role] = sprite;
        _roleFaceSpriteTextures[role] = faceTexture;
        return sprite;
    }

    private static void HideRoleImage(Image image)
    {
        image.sprite = null;
        image.color = new Color(1f, 1f, 1f, 0f);
        image.gameObject.SetActive(false);
    }

    private static void AddRolePlayerIfSelected(Dictionary<Define.TitanRole, LobbyNetworkPlayer> playersByRole, Define.TitanRole role, int roleMask, LobbyNetworkPlayer player)
    {
        int roleValue = (int)role;
        int bit = 1 << (roleValue - (int)Define.TitanRole.Torso);
        if ((roleMask & bit) == 0 || playersByRole.ContainsKey(role))
            return;

        playersByRole[role] = player;
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

    private void ClearRoleFaceSprites()
    {
        Define.TitanRole[] roles = new Define.TitanRole[_roleFaceSprites.Keys.Count];
        _roleFaceSprites.Keys.CopyTo(roles, 0);
        for (int i = 0; i < roles.Length; i++)
        {
            Define.TitanRole role = roles[i];
            ClearRoleFaceSprite(role);
        }

        _roleFaceSprites.Clear();
        _roleFaceSpriteTextures.Clear();
    }

    private void ClearRoleFaceSprite(Define.TitanRole role)
    {
        if (_roleFaceSprites.TryGetValue(role, out Sprite sprite) && sprite != null)
            Destroy(sprite);

        _roleFaceSprites.Remove(role);
        _roleFaceSpriteTextures.Remove(role);
    }
}

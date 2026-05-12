using UnityEngine;

public class Util
{
    private const int LobbyJoinCodeLength = 6;

    public static string NormalizeLobbyJoinCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string trimmed = value.Trim();
        if (trimmed.Length != LobbyJoinCodeLength)
            return string.Empty;

        bool hasLetter = false;
        bool hasDigit = false;

        char[] normalized = new char[LobbyJoinCodeLength];
        for (int i = 0; i < LobbyJoinCodeLength; i++)
        {
            char c = char.ToUpperInvariant(trimmed[i]);
            bool isDigit = c >= '0' && c <= '9';
            bool isLetter = c >= 'A' && c <= 'Z';
            if (!isDigit && !isLetter)
                return string.Empty;

            hasDigit |= isDigit;
            hasLetter |= isLetter;
            normalized[i] = c;
        }

        if (!hasDigit || !hasLetter)
            return string.Empty;

        return new string(normalized);
    }

    public static string CreateLobbyJoinCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        // Requirement: 6 chars, must include both letters and digits.
        while (true)
        {
            bool hasLetter = false;
            bool hasDigit = false;

            char[] code = new char[LobbyJoinCodeLength];
            for (int i = 0; i < LobbyJoinCodeLength; i++)
            {
                char c = chars[Random.Range(0, chars.Length)];
                code[i] = c;
                hasDigit |= c >= '0' && c <= '9';
                hasLetter |= c >= 'A' && c <= 'Z';
            }

            if (hasDigit && hasLetter)
                return new string(code);
        }
    }

    public static T GetorAddComponent<T>(GameObject go) where T : Component
    {
        return go.GetComponent<T>() ?? go.AddComponent<T>();
    }

    public static GameObject FindChild(GameObject go, string name = null, bool recursive = false)
    {
        return FindChild<Transform>(go, name, recursive)?.gameObject;
    }

    public static T FindChild<T>(GameObject go, string name = null, bool recursive = false) where T : UnityEngine.Object
    {
        if (go == null)
            return null;
        if (recursive)
        {
            foreach (T component in go.GetComponentsInChildren<T>(true))
            {
                if (string.IsNullOrEmpty(name) || component.name == name)
                    return component;
            }
        }
        else
        {
            for (int i = 0; i < go.transform.childCount; i++)
            {
                Transform transform = go.transform.GetChild(i);
                if (string.IsNullOrEmpty(name) || transform.name == name)
                {
                    T component = transform.GetComponent<T>();
                    if (component != null)
                        return component;
                }
            }
        }

        return null;
    }

    public static string RemoveUnityCloneSuffix(string name)
    {
        const string suffix = "(Clone)";
        return name.EndsWith(suffix)
            ? name[..^suffix.Length]
            : name;
    }

    public static string GetEnumName<T>(T element)
    {
        return System.Enum.GetName(typeof(T), element);
    }

    public static bool IsValid(GameObject go)
    {
        return go != null && go.activeSelf;
    }

}

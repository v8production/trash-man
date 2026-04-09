using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class Util
{
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
            foreach (T component in go.GetComponentsInChildren<T>())
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

    public static string GetEnv(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        string processValue = Environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrWhiteSpace(processValue))
            return processValue.Trim();

        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrWhiteSpace(projectRoot))
            return string.Empty;

        foreach (string envFile in GetEnvFileCandidates())
        {
            string filePath = Path.Combine(projectRoot, envFile);
            if (!File.Exists(filePath))
                continue;

            if (TryReadEnvValue(filePath, key, out string value))
                return value;
        }

        return string.Empty;
    }

    private static IEnumerable<string> GetEnvFileCandidates()
    {
        string stage = GetRuntimeStage();

        if (!string.IsNullOrWhiteSpace(stage))
            yield return $".env.{stage}.local";

        yield return ".env.local";

        if (!string.IsNullOrWhiteSpace(stage))
            yield return $".env.{stage}";

        yield return ".env";
    }

    private static string GetRuntimeStage()
    {
        string stage = Environment.GetEnvironmentVariable("APP_ENV");
        if (string.IsNullOrWhiteSpace(stage))
            stage = Environment.GetEnvironmentVariable("UNITY_ENV");
        if (string.IsNullOrWhiteSpace(stage))
            stage = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

        if (string.IsNullOrWhiteSpace(stage))
            stage = Application.isEditor ? "local" : "prod";

        return stage.Trim().ToLowerInvariant();
    }

    private static bool TryReadEnvValue(string filePath, string key, out string value)
    {
        value = string.Empty;

        string[] lines = File.ReadAllLines(filePath);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                continue;

            int separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
                continue;

            string parsedKey = line.Substring(0, separatorIndex).Trim();
            if (!string.Equals(parsedKey, key, StringComparison.OrdinalIgnoreCase))
                continue;

            string parsedValue = line.Substring(separatorIndex + 1).Trim();
            value = parsedValue.Trim('"').Trim();
            return !string.IsNullOrWhiteSpace(value);
        }

        return false;
    }
}

using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class DataManager
{
    const string UserSettingsFileName = "user_settings.json";

    public Dictionary<string, List<Vector3>> HumanRouteDict { get; protected set; } = new();
    Data.UserSettingsData _userSettings;
    public Data.UserSettingsData UserSettings
    {
        get
        {
            _userSettings ??= Load<Data.UserSettingsData>(UserSettingsFileName);
            return _userSettings;
        }
    }

    public void Init()
    {
        // Data.HumanRouteData humanRouteData = LoadJson<Data.HumanRouteData, string, List<Vector3>>("HumanRouteData");
        // if (humanRouteData != null)
        //     HumanRouteDict = humanRouteData.MakeDict();

        _userSettings = Load<Data.UserSettingsData>(UserSettingsFileName);
    }

    public void Save<T>(T data, string fileName = UserSettingsFileName)
    {
        string json = JsonUtility.ToJson(data, true);
        string path = GetSavePath(fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, json);
    }

    public T Load<T>(string fileName = UserSettingsFileName) where T : new()
    {
        string path = GetSavePath(fileName);
        if (!File.Exists(path))
        {
            T newData = new();
            Save(newData, fileName);
            return newData;
        }

        string json = File.ReadAllText(path);
        return JsonUtility.FromJson<T>(json);
    }

    public void SaveUserSettings()
    {
        Save(UserSettings, UserSettingsFileName);
    }

    private Loader LoadJson<Loader, Key, Value>(string path) where Loader : ILoader<Key, Value>
    {
        TextAsset textAsset = Managers.Resource.Load<TextAsset>($"Datas/{path}");
        if (textAsset == null)
            return default;
        return JsonUtility.FromJson<Loader>(textAsset.text);
    }

    private string GetSavePath(string fileName)
    {
        return Path.Combine(Application.persistentDataPath, fileName);
    }

    public void Clear() { }
}

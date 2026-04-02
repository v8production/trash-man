using System;
using System.Collections.Generic;
using UnityEngine;

public interface ILoader<Key, Value>
{
    Dictionary<Key, Value> MakeDict();
}

namespace Data
{
    [Serializable]
    public class Stat
    {
        public int maxHp;
        public int currentHp;
        public int gaize;
    }

    [Serializable]
    public class HumanRoute
    {
        public string name;
        public List<Vector3> positions = new();
    }

    [Serializable]
    public class HumanRouteData : ILoader<string, List<Vector3>>
    {
        public List<HumanRoute> routes = new();

        public Dictionary<string, List<Vector3>> MakeDict()
        {
            Dictionary<string, List<Vector3>> dict = new();
            foreach (HumanRoute route in routes)
            {
                if (string.IsNullOrWhiteSpace(route.name) || route.positions == null)
                    continue;

                dict[route.name] = route.positions;
            }
            return dict;
        }
    }
}

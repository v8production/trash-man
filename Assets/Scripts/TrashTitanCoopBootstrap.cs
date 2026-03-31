using UnityEngine;

namespace TrashMan
{
    public static class TrashTitanCoopBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void AttachControllerIfMissing()
        {
            GameObject target = GameObject.Find("Trash_titan");

            if (target == null)
            {
                Animator[] animators = Object.FindObjectsByType<Animator>(FindObjectsSortMode.None);
                for (int i = 0; i < animators.Length; i++)
                {
                    Animator current = animators[i];
                    if (current == null)
                    {
                        continue;
                    }

                    string lowerName = current.gameObject.name.ToLowerInvariant();
                    if (!lowerName.Contains("trash") && !lowerName.Contains("titan"))
                    {
                        continue;
                    }

                    target = current.gameObject;
                    break;
                }
            }

            if (target == null)
            {
                return;
            }

            if (target.GetComponent<TrashTitanRig>() == null)
            {
                target.AddComponent<TrashTitanRig>();
            }

            if (target.GetComponent<TrashTitanBodyRoleController>() == null)
            {
                target.AddComponent<TrashTitanBodyRoleController>();
            }

            if (target.GetComponent<TrashTitanLeftArmRoleController>() == null)
            {
                target.AddComponent<TrashTitanLeftArmRoleController>();
            }

            if (target.GetComponent<TrashTitanRightArmRoleController>() == null)
            {
                target.AddComponent<TrashTitanRightArmRoleController>();
            }

            if (target.GetComponent<TrashTitanLeftLegRoleController>() == null)
            {
                target.AddComponent<TrashTitanLeftLegRoleController>();
            }

            if (target.GetComponent<TrashTitanRightLegRoleController>() == null)
            {
                target.AddComponent<TrashTitanRightLegRoleController>();
            }

            TrashTitanLocalRoleSwitchTester tester = target.GetComponent<TrashTitanLocalRoleSwitchTester>();
            if (tester == null)
            {
                tester = target.AddComponent<TrashTitanLocalRoleSwitchTester>();
            }

            tester.SetHostAuthority(true);
        }
    }
}

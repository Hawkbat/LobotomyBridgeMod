using Harmony;
using System;

namespace BridgeMod
{
    public class Harmony_Patch
    {
        public Harmony_Patch() {
            HarmonyInstance harmony = HarmonyInstance.Create($"Lobotomy.{nameof(BridgeMod)}");
            Postfix(harmony, typeof(GlobalGameManager), "Awake", nameof(GlobalGameManager_Awake));
        }

        static void Prefix(HarmonyInstance harmony, Type srcType, string srcMethodName, string dstMethodName)
        {
            var src = srcType.GetMethod(srcMethodName, AccessTools.all);
            var dst = new HarmonyMethod(typeof(Harmony_Patch).GetMethod(dstMethodName));
            harmony.Patch(src, dst, null);
        }

        static void Postfix(HarmonyInstance harmony, Type srcType, string srcMethodName, string dstMethodName)
        {
            var src = srcType.GetMethod(srcMethodName, AccessTools.all);
            var dst = new HarmonyMethod(typeof(Harmony_Patch).GetMethod(dstMethodName));
            harmony.Patch(src, null, dst);
        }

        public static void GlobalGameManager_Awake()
        {
            BridgeMod.GetInstance();
        }
    }
}

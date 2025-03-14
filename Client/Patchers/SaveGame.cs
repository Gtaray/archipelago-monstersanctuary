using Archipelago.MonsterSanctuary.Client.Behaviors;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Archipelago.MonsterSanctuary.Client.Persistence;
using MonoMod.Cil;
using static UnityEngine.GridBrushBase;
using System.Reflection;

namespace Archipelago.MonsterSanctuary.Client
{
    public partial class Patcher
    {
        [HarmonyPatch(typeof(SaveGameManager), "CreateBinaryFormatter")]
        private static class SaveGameManager_CreateBinaryFormatter
        {
            private static void Postfix(ref BinaryFormatter __result)
            {
                ApDataSerializationSurrogate apSurrogate = new ApDataSerializationSurrogate(); ;
                ReferenceableSerializationSurrogate surrogate = new ReferenceableSerializationSurrogate();
                surrogate.WorldData = GameController.Instance.WorldData;

                var selector = (SurrogateSelector)__result.SurrogateSelector;
                selector.AddSurrogate(typeof(ApDataFile), new StreamingContext(StreamingContextStates.All), apSurrogate);
                selector.AddSurrogate(typeof(ArchipelagoItem), new StreamingContext(StreamingContextStates.All), surrogate);

                __result.SurrogateSelector = selector;
            }
        }

        //private static class SaveGameManager_SaveGame
        //{
        //    static FieldInfo f_someField = AccessTools.Field(typeof(SomeType), nameof(SomeType.someField));
        //    static MethodInfo m_MyExtraMethod = SymbolExtensions.GetMethodInfo(() => Tools.MyExtraMethod());

        //    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        //    {
        //        var found = false;
        //        foreach (var instruction in instructions)
        //        {
        //            if (instruction.StoresField(f_someField))
        //            {
        //                yield return new CodeInstruction(OpCodes.Call, m_MyExtraMethod);
        //                found = true;
        //            }
        //            yield return instruction;
        //        }
        //        if (found is false)
        //            ReportError("Cannot find <Stdfld someField> in OriginalType.OriginalMethod");
        //    }
        //}
    }
}

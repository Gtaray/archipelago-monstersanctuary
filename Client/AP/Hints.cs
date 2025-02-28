using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.MonsterSanctuary.Client.AP
{
    public class Hints
    {
        public class Hint
        {
            /// <summary>
            /// The text that should be displayed for this hint
            /// </summary>
            public string Text { get; set; }

            /// <summary>
            /// If true, all remaining DialogActions will be skipped
            /// </summary>
            public bool IgnoreRemainingText { get; set; }
        }

        /// <summary>
        /// A dictionary that maps a DialogueAction ID to a hint
        /// </summary>
        public static Dictionary<int, Hint> HintCache = new();

        /// <summary>
        /// Adds a hint to the hint cache
        /// </summary>
        /// <param name="id"></param>
        /// <param name="text"></param>
        /// <param name="ignoreRemainingText"></param>
        public static void AddHint(int id, string text, bool ignoreRemainingText)
        {
            HintCache[id] = new Hint() { Text = text, IgnoreRemainingText = ignoreRemainingText };
        }

        /// <summary>
        /// Gets a hint from the hint cache
        /// </summary>
        /// <param name="id">DialogAction ID</param>
        /// <returns></returns>
        public static string GetHintText(int id)
        {
            if (!HintCache.ContainsKey(id))
                return null;

            return HintCache[id].Text;
        }

        /// <summary>
        /// Gets whether a hint should end the dialog early, or if dialog should continue
        /// </summary>
        /// <param name="id">DialogAction ID</param>
        /// <returns></returns>
        public static bool ShouldEndHintDialog(int id)
        {
            if (!HintCache.ContainsKey(id))
                return false;

            return HintCache[id].IgnoreRemainingText;
        }

        /// <summary>
        /// Empties out data that is supplied by Archipelago. Used primarily to refresh state when connecting to AP
        /// </summary>
        public static void ClearApData()
        {
            HintCache.Clear();
        }
    }
}

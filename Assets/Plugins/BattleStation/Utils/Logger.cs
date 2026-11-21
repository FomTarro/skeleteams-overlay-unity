using UnityEngine;

namespace Skeletom.BattleStation.Utils {
    public class Logger
    {
        private readonly string _context;
        public Logger(string context)
        {
            _context = context;
        }

        public void Log(string msg)
        {
            Debug.Log($"[{_context}] - {msg}");
        }

        public void LogWarning(string msg)
        {
            Debug.LogWarning($"[{_context}] - {msg}");
        }


        public void LogError(string msg)
        {
            Debug.LogError($"[{_context}] - {msg}");
        }
    }
}

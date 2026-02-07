using System;
using System.Collections.Generic;

namespace Skeletom.BattleStation.Integrations
{
    public class DependencyManager
    {
        private readonly ISet<string> pendingTasks;
        private readonly Queue<string> taskQueue;
        private readonly Action onAllComplete;
        private readonly Action<string, int> onOneComplete;
        private bool allowChecks;
        public DependencyManager(Action onAllComplete, Action<string, int> onOneComplete = null)
        {
            pendingTasks = new HashSet<string>();
            taskQueue = new Queue<string>();
            this.onAllComplete = onAllComplete;
            this.onOneComplete = onOneComplete;
            allowChecks = false;
        }

        public void AddDependency(string key)
        {
            pendingTasks.Add(key);
        }

        public void ResolveDependency(string key)
        {
            if (!allowChecks)
            {
                taskQueue.Enqueue(key);
            }
            else
            {
                if (pendingTasks.Contains(key))
                {
                    pendingTasks.Remove(key);
                    onOneComplete?.Invoke(key, pendingTasks.Count);
                    if (pendingTasks.Count <= 0)
                    {
                        onAllComplete?.Invoke();
                    }
                }
            }
        }

        public void Enable(bool toggle)
        {
            allowChecks = toggle;
            if (allowChecks && taskQueue.Count > 0)
            {
                do
                {
                    if (taskQueue.TryDequeue(out string key))
                    {
                        ResolveDependency(key);
                    }
                } while (taskQueue.Count > 0);
            }
        }
    }
}

using System;
using System.Collections.Generic;

namespace Skeletom.BattleStation.Integrations
{
    /// <summary>
    /// Manager which can track arbitrary task status, 
    /// executing provided callbacks when one and when all tasks are completed.
    /// 
    /// Useful for awaiting several asynchronous operations.
    /// </summary>
    public class DependencyManager
    {
        private readonly ISet<string> _pendingTaskSet;
        private readonly Queue<string> _resolvedTaskQueue;
        private readonly Action _onAllComplete;
        private readonly Action<string, int> _onOneComplete;
        private bool _allowChecks;
        /// <summary>
        /// Creates a Dependency Manager.
        /// </summary>
        /// <param name="onAllComplete">The callback executed when all tasks are completed.</param>
        /// <param name="onOneComplete">The callback executed when one task is completed. 
        /// Receives the task key and remaining task count as arguments.</param>
        public DependencyManager(Action onAllComplete, Action<string, int> onOneComplete = null)
        {
            _pendingTaskSet = new HashSet<string>();
            _resolvedTaskQueue = new Queue<string>();
            _onAllComplete = onAllComplete;
            _onOneComplete = onOneComplete;
            _allowChecks = false;
        }

        /// <summary>
        /// Adds a dependent task to the manager, identified by a unique key.
        /// </summary>
        /// <param name="key">The key of the task to track.</param>
        public void AddDependency(string key)
        {
            _pendingTaskSet.Add(key);
        }

        /// <summary>
        /// Marks a dependent task as resolved, identified by its unique key.
        /// If the manager is enabled, invokes the onOneComplete callback provided in the constructor,
        /// and if all dependences are resolved, invokes the onAllComplete callback.
        /// </summary>
        /// <param name="key">The key of the task to resolve.</param>
        public void ResolveDependency(string key)
        {
            if (!_allowChecks)
            {
                _resolvedTaskQueue.Enqueue(key);
            }
            else
            {
                if (_pendingTaskSet.Contains(key))
                {
                    _pendingTaskSet.Remove(key);
                    _onOneComplete?.Invoke(key, _pendingTaskSet.Count);
                    if (_pendingTaskSet.Count <= 0)
                    {
                        _onAllComplete?.Invoke();
                    }
                }
            }
        }

        /// <summary>
        /// Enables or disables the manager. 
        /// Be sure to add all dependent tasks to the manager before enabling it.
        /// </summary>
        /// <param name="toggle">The status to set the manager to.</param>
        public void Enable(bool toggle)
        {
            _allowChecks = toggle;
            if (_allowChecks && _resolvedTaskQueue.Count > 0)
            {
                // Process backlog of resolved tasks 
                do
                {
                    if (_resolvedTaskQueue.TryDequeue(out string key))
                    {
                        ResolveDependency(key);
                    }
                } while (_resolvedTaskQueue.Count > 0);
            }
        }
    }
}

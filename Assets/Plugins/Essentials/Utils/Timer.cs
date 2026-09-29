using System;

namespace Skeletom.Essentials.Utils
{
    public class Timer
    {
        private readonly float _baseInterval = 1f;
        private float _nextInterval = 1f;
        private float _currentTime = 0f;
        private readonly float _variance = 0f;
        private readonly Action _onInterval;
        public Timer(float interval, float variance, Action onInterval)
        {
            this._baseInterval = interval;
            this._variance = variance;
            this._onInterval = onInterval;
            CalculateNextInterval();
        }

        public void Tick(float delta)
        {
            this._currentTime = this._currentTime + delta;
            if (this._currentTime >= this._nextInterval)
            {
                this._onInterval.Invoke();
                CalculateNextInterval();
                this._currentTime = 0f;
            }
        }

        private void CalculateNextInterval()
        {
            this._nextInterval = this._baseInterval + UnityEngine.Random.Range(-this._variance, this._variance);
        }
    }
}
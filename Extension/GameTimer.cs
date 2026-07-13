using System;
using System.Collections.Generic;
using UnityEngine;

namespace qtLib.Extension
{
    public class GameTimer : MonoBehaviour
    {
        #region ----- Definition -----

        public enum ETimerType
        {
            Time,
            UnscaledTime,
            RealtimeSinceStartup,
        }

        #endregion

        #region ----- Event -----

        public static event Action<bool> onApplicationPause;
        public static event Action<bool> onApplicationFocus;

        #endregion
        
        #region ----- Variables -----

        private static List<Timer> _timer = new ();
        private static List<Timer> _fixedTimer = new ();
        private static Timer _temp;
        
        #endregion
        
        #region ----- Unity Event -----

        private void Update()
        {
            float currentTime = 0;
            for (var i = 0; i < _timer.Count; i++)
            {
                _temp = _timer[i];
                if (_temp.isPause)
                {
                    continue;
                }

                currentTime = GetTime(_temp.TimerType);
                if (_temp.isDown)
                {
                    if (currentTime >= _temp.time)
                    {
                        _temp.completeCallback?.Invoke();
                        if (_timer.Remove(_temp))
                        {
                            i--;
                        }
                    }
                }
                else
                {
                    _temp.updateCallback?.Invoke(currentTime - _temp.time);
                }
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            onApplicationPause?.Invoke(pauseStatus);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            onApplicationFocus?.Invoke(hasFocus);
        }

        #endregion

        #region ----- Public Functions -----

        public static long RegisterTimer(float time, bool isDown, ETimerType timerType, Action onComplete)
        {
            return RegisterTimer(time, isDown, timerType, null, onComplete);
        }

        public static long RegisterTimer(float time, bool isDown, ETimerType timerType, Action<float> onUpdate, Action onComplete)
        {
            long timerID = DateTime.Now.Ticks;
            _timer.Add(new Timer(timerID, GetTime(timerType) + time, isDown, timerType, onUpdate, onComplete));
            return timerID;
        }

        public static void PauseTimer(long timerID, bool isPause)
        {
            for (var i = 0; i < _timer.Count; i++)
            {
                if (_timer[i].ID == timerID)
                {
                    _timer[i].isPause = isPause;
                }
            }
        }
        
        public static void UnRegisterTimer(long timerID)
        {
            for (var i = 0; i < _timer.Count; i++)
            {
                if (_timer[i].ID == timerID)
                {
                    _timer.RemoveAt(i);
                    return;
                }
            }
        }
        
        #endregion

        #region ----- Private Functions -----

        private static float GetTime(ETimerType timerType)
        {
            switch (timerType)
            {
                case ETimerType.Time:
                {
                    return Time.time;
                }
                case ETimerType.UnscaledTime:
                {
                    return Time.unscaledTime;
                }
                case ETimerType.RealtimeSinceStartup:
                {
                    return Time.realtimeSinceStartup;
                }
                default:
                {
                    throw new ArgumentOutOfRangeException(nameof(timerType), timerType, null);
                }
            }
        }

        #endregion

        private class Timer
        {
            public readonly long ID;
            public bool isPause;
            public readonly float time;
            public readonly ETimerType TimerType = ETimerType.Time;
            public readonly bool isDown = true;
            public readonly Action<float> updateCallback;
            public readonly Action completeCallback;

            public Timer(long id, float time, bool isDown, ETimerType timerType, Action<float> updateCallback, Action completeCallback)
            {
                ID = id;
                isPause = false;
                this.time = time;
                this.isDown = isDown;
                this.TimerType = timerType;
                this.updateCallback = updateCallback;
                this.completeCallback = completeCallback;
            }
        }
    }
}
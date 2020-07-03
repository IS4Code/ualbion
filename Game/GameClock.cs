﻿using System;
using System.Collections.Generic;
using UAlbion.Core;
using UAlbion.Core.Events;
using UAlbion.Game.Events;
using UAlbion.Game.State;

namespace UAlbion.Game
{
    public class GameClock : ServiceComponent<IClock>, IClock
    {
        const float TickDurationSeconds = 1 / 60.0f;
        const float GameSecondsPerSecond = 60;
        const int TicksPerCacheCycle = 3600; // Cycle the cache every minute

        readonly IList<(string, float)> _activeTimers = new List<(string, float)>();
        float _elapsedTimeThisGameFrame;
        int _ticksRemaining;
        int _stoppedFrames;
        float _stoppedMs;
        Action _pendingContinuation;

        public GameClock()
        {
            On<StartClockEvent>(e =>
            {
                GameTrace.Log.ClockStart(_stoppedFrames, _stoppedMs);
                _stoppedFrames = 0;
                _stoppedMs = 0;
                IsRunning = true;
            });
            On<StopClockEvent>(e =>
            {
                GameTrace.Log.ClockStop();
                IsRunning = false;
            });
            On<EngineUpdateEvent>(OnEngineUpdate);
            On<StartTimerEvent>(StartTimer);

            OnAsync<UpdateEvent>((e, c) =>
            {
                if (IsRunning || _pendingContinuation != null)
                    return false;

                GameTrace.Log.ClockUpdating(e.Cycles);
                _pendingContinuation = c;
                _ticksRemaining = e.Cycles * 3;
                IsRunning = true;
                return true;
            });
        }

        public float ElapsedTime { get; private set; }
        public bool IsRunning { get; private set; }

        void StartTimer(StartTimerEvent e) => _activeTimers.Add((e.Id, ElapsedTime + e.IntervalMilliseconds / 1000));

        void OnEngineUpdate(EngineUpdateEvent e)
        {
            ElapsedTime += e.DeltaSeconds;

            for (int i = 0; i < _activeTimers.Count; i++)
            {
                if (!(_activeTimers[i].Item2 <= ElapsedTime))
                    continue;

                Raise(new TimerElapsedEvent(_activeTimers[i].Item1));
                _activeTimers.RemoveAt(i);
                i--;
            }

            if (IsRunning)
            {
                var state = Resolve<IGameState>();
                if (state != null)
                {
                    var lastGameTime = state.Time;
                    var newGameTime = lastGameTime.AddSeconds(e.DeltaSeconds * GameSecondsPerSecond);
                    ((IComponent) state).Receive(new SetTimeEvent(newGameTime), this);

                    if (newGameTime.Hour != lastGameTime.Hour)
                        Raise(new HourElapsedEvent());

                    if (newGameTime.Date != lastGameTime.Date)
                        Raise(new DayElapsedEvent());
                }

                _elapsedTimeThisGameFrame += e.DeltaSeconds;

                // If the game was paused for a while don't try and catch up
                if (_elapsedTimeThisGameFrame > 4 * TickDurationSeconds)
                    _elapsedTimeThisGameFrame = 4 * TickDurationSeconds;

                while (_elapsedTimeThisGameFrame >= TickDurationSeconds && IsRunning)
                {
                    _elapsedTimeThisGameFrame -= TickDurationSeconds;
                    RaiseTick();

                    if ((state?.TickCount ?? 0) % TicksPerCacheCycle == TicksPerCacheCycle - 1)
                        Raise(new CycleCacheEvent());
                }
            }
            else
            {
                _stoppedFrames++;
                _stoppedMs += 1000.0f * e.DeltaSeconds;
            }

            Raise(new PostUpdateEvent());
        }

        void RaiseTick()
        {
            Raise(new FastClockEvent(1));
            if (_ticksRemaining <= 0)
                return;

            _ticksRemaining --;
            if (_ticksRemaining > 0)
                return;

            IsRunning = false;
            GameTrace.Log.ClockUpdateComplete();
            var continuation = _pendingContinuation;
            _pendingContinuation = null;
            continuation?.Invoke();
        }
    }
}

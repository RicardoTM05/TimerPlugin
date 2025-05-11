
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Rainmeter;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Globalization;

namespace TimerPlugin
{
    class Measure
    {
        // Exposed Variables
        private double duration = 0;
        private double interval = 0;
        private int repeat = -1;
        private int update = 1000;
        private bool resetOnStop = true;
        private bool isCountdown = false;
        private bool debug = false;
        private bool warnings = false;
        private string targetTime;
        private string durationUnits = "milliseconds";
        private string IntervalUnits = "milliseconds";
        private string formatLocale;
        private string formatString = "%t";

        private string onTickAction;
        private string onStartAction;
        private string onStopAction;
        private string onResumeAction;
        private string onPauseAction;
        private string onResetAction;
        private string onDismissAction;
        private string onRepeatAction;

        // Internal Variables
        internal API _api;

        internal IntPtr skin;
        internal string measureName;
        internal string skinName;
        internal CancellationTokenSource _cts;
        internal readonly Stopwatch _stopwatch = new Stopwatch();
        internal Task _timerTask;
        internal TaskCompletionSource<object> _resumeTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private string _lastFormatString;
        private string _lastOnTickAction;
        private string _lastOnStartAction;
        private string _lastOnStopAction;
        private string _lastOnResumeAction;
        private string _lastOnPauseAction;
        private string _lastOnResetAction;
        private string _lastOnDismissAction;
        private string _lastOnRepeatAction;

        internal string _updateBang;

        internal Action _formatString;
        private Action _onTickAction;
        private Action _onStartAction;
        private Action _onStopAction;
        private Action _onResumeAction;
        private Action _onPauseAction;
        private Action _onResetAction;
        private Action _onDismissAction;
        private Action _onRepeatAction;

        private CultureInfo _customCulture;
        private double lcm = 0;
        private double pauseTime = 0;
        private long totalTicks = 0;
        private long durationMs = 0;
        private long intervalMs = 0;
        internal int updateDivider;
        private int _tickCount = 0;
        private int repeatCount = 0;
        private string stopTime;
        private string _lastFormatLocale;
        private string timerMode = "None";
        internal string timeString;
        private bool _suppressCatchUp = false;
        private bool hasDuration = false;
        private bool doUpdate = true;
        private bool doInterval = false;

        internal enum TimerState
        {
            Stopped = 0,
            Running = 1,
            Paused = 2
        }

        internal TimerState state = TimerState.Stopped;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SendNotifyMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private static readonly CultureInfo[] FallbackCultures = new[]
        {
            new CultureInfo("en-US"), // English (United States)
            new CultureInfo("en-GB"), // English (United Kingdom)
            new CultureInfo("fr-FR"), // French (France)
            new CultureInfo("de-DE"), // German (Germany)
            new CultureInfo("es-ES"), // Spanish (Spain)
            new CultureInfo("es-MX"), // Spanish (Mexico)
            new CultureInfo("it-IT"), // Italian (Italy)
            new CultureInfo("pt-PT"), // Portuguese (Portugal)
            new CultureInfo("pt-BR"), // Portuguese (Brazil)
            new CultureInfo("ru-RU"), // Russian (Russia)
            new CultureInfo("ja-JP"), // Japanese (Japan)
            new CultureInfo("zh-CN"), // Chinese (Simplified, China)
            new CultureInfo("zh-TW"), // Chinese (Traditional, Taiwan)
            new CultureInfo("ko-KR"), // Korean (Korea)
            new CultureInfo("nl-NL"), // Dutch (Netherlands)
            new CultureInfo("sv-SE"), // Swedish (Sweden)
            new CultureInfo("pl-PL"), // Polish (Poland)
            new CultureInfo("tr-TR"), // Turkish (Turkey)
            new CultureInfo("ar-SA"), // Arabic (Saudi Arabia)
            new CultureInfo("cs-CZ"), // Czech (Czech Republic)
            new CultureInfo("fi-FI"), // Finnish (Finland)
            new CultureInfo("da-DK"), // Danish (Denmark)
            new CultureInfo("he-IL"), // Hebrew (Israel)
            new CultureInfo("hu-HU"), // Hungarian (Hungary)
            new CultureInfo("no-NO"), // Norwegian (Norway)
            new CultureInfo("th-TH"), // Thai (Thailand)
        };

        private const int WM_APP = 0x8000;
        private const int WM_RAINMETER_EXECUTE = WM_APP + 2;
        private const string RAINMETER_CLASS_NAME = "DummyRainWClass";
        private const string RAINMETER_WINDOW_NAME = "Rainmeter control window";
        private IntPtr _hwndRainmeter;

        private void EnsureRainmeterHwnd()
        {
            if (_hwndRainmeter == IntPtr.Zero)
                _hwndRainmeter = FindWindow(RAINMETER_CLASS_NAME, RAINMETER_WINDOW_NAME);
        }
        internal class Action
        {
            private readonly Measure _parent;
            private readonly List<IFormatSegment> _segments;

            internal Action(Measure parent, string input)
            {
                _parent = parent;
                _segments = _parent.CompileString(input);
            }

            internal string Render()
            {
                return _parent.RenderString(_segments);
            }
        }
        private interface IFormatSegment
        {
            string GetValue();
        }
        private class StaticSegment : IFormatSegment
        {
            private readonly string _value;
            internal StaticSegment(string value) => _value = value;
            public string GetValue() => _value;
        }
        private class DynamicSegment : IFormatSegment
        {
            private readonly Func<string> _getter;
            internal DynamicSegment(Func<string> getter) => _getter = getter;
            public string GetValue() => _getter();
        }
        private static readonly Regex FormatRegex = new(
        @"%(k|tf|tms|ts|tm|th|td|T|t|H|M|S|D|f{1,7}|F{1,7})(?:\{(\d+)\})?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
        );
        private void CompileActions()
        {
            if (formatString != _lastFormatString)
            {
                _formatString = new Action(this, formatString);
                _lastFormatString = formatString;
            }
            if (formatLocale != _lastFormatLocale)
            {
                if (!string.IsNullOrWhiteSpace(formatLocale))
                {
                    try
                    {
                        _customCulture = new CultureInfo(formatLocale);
                    }
                    catch (CultureNotFoundException)
                    {
                            Log("Error", $"Timer.dll: Unknown culture: {formatLocale}");
                        _customCulture = null;
                    }
                }
                else
                {
                    _customCulture = null;
                }
                _lastFormatLocale = formatLocale;
            }
            if (onTickAction != _lastOnTickAction)
            {
                _onTickAction = new Action(this, onTickAction);
                _lastOnTickAction = onTickAction;
            }
            if (onStartAction != _lastOnStartAction)
            {
                _onStartAction = new Action(this, onStartAction);
                _lastOnStartAction = onStartAction;
            }
            if (onStopAction != _lastOnStopAction)
            {
                _onStopAction = new Action(this, onStopAction);
                _lastOnStopAction = onStopAction;
            }
            if (onResumeAction != _lastOnResumeAction)
            {
                _onResumeAction = new Action(this, onResumeAction);
                _lastOnResumeAction = onResumeAction;
            }
            if (onPauseAction != _lastOnPauseAction)
            {
                _onPauseAction = new Action(this, onPauseAction);
                _lastOnPauseAction = onPauseAction;
            }
            if (onResetAction != _lastOnResetAction)
            {
                _onResetAction = new Action(this, onResetAction);
                _lastOnResetAction = onResetAction;
            }
            if (onDismissAction != _lastOnDismissAction)
            {
                _onDismissAction = new Action(this, onDismissAction);
                _lastOnDismissAction = onDismissAction;
            }
            if (onRepeatAction != _lastOnRepeatAction)
            {
                _onRepeatAction = new Action(this, onRepeatAction);
                _lastOnRepeatAction = onRepeatAction;
            }
        }

        private void Log(string Type, string message)
        {
            if (Type == "Error")
            {
                _api.Log(API.LogType.Error, message);
            }
            else if (Type == "Debug" && debug == true)
            {
                _api.Log(API.LogType.Debug, message);
            }
            else if (Type == "Warning" && warnings == true)
            {
                _api.Log(API.LogType.Warning, message);
            }
            else if (Type == "Notice")
            {
                _api.Log(API.LogType.Notice, message);
            }
            return;
        }
        internal void Reload(API api)
        {
            _api = api;

            _updateBang = $"!UpdateMeasure \"{measureName}\"";

            // Force UpdateDivider=-1 to avoid rainmeter from updating the measure.
            updateDivider = _api.ReadInt("UpdateDivider", 1);
            if (updateDivider != -1)
            {
                _api.Execute($"!SetOption \"{measureName}\" \"UpdateDivider\" \"-1\"");
            }

            // Options
            update = _api.ReadInt("UpdateTimer", 1000);
            formatString = _api.ReadString("Format", "%t");
            durationUnits = _api.ReadString("DurationUnits", "milliseconds");
            duration = _api.ReadDouble("Duration", -1);
            formatLocale = _api.ReadString("FormatLocale", "");
            targetTime = _api.ReadString("TargetTime", "");
            IntervalUnits = _api.ReadString("IntervalUnits", "milliseconds");
            interval = _api.ReadDouble("Interval", -1);
            isCountdown = _api.ReadInt("Countdown", -1) > 0;
            resetOnStop = _api.ReadInt("ResetOnStop", -1) > 0;
            repeat = _api.ReadInt("Repeat", -1);

            debug = _api.ReadInt("Debug", 0) > 0;
            warnings = _api.ReadInt("Warnings", 1) > 0;

            // Actions
            onTickAction = _api.ReadString("OnTickAction", "", false);
            onStartAction = _api.ReadString("OnStartAction", "", false);
            onStopAction = _api.ReadString("OnStopAction", "", false);
            onResumeAction = _api.ReadString("OnResumeAction", "", false);
            onPauseAction = _api.ReadString("OnPauseAction", "", false);
            onResetAction = _api.ReadString("OnResetAction", "", false);
            onDismissAction = _api.ReadString("OnDismissAction", "", false);
            onRepeatAction = _api.ReadString("OnRepeatAction", "", false);

            CompileActions();

            // Initial Configutation
            if (string.IsNullOrWhiteSpace(targetTime))
                durationMs = ToMilliseconds(duration, durationUnits);
            intervalMs = ToMilliseconds(interval, IntervalUnits);

            doUpdate = update > 0;
            doInterval = intervalMs > 0;
            hasDuration = durationMs > 0;

            CheckTimerMode();
        }
        #region Timer
        private async Task StartTimer()
        {
            try
            {
                var oldCts = _cts;
                var oldTask = _timerTask;

                oldCts?.Cancel();

                if (oldTask != null)
                {
                    try
                    {
                        await oldTask.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException ex)
                    {
                        Log("Error", $"Timer.dll: {ex}");
                    }
                }

                oldCts?.Dispose();

                _cts = new CancellationTokenSource();
                _resumeTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
                _resumeTcs.TrySetResult(null);

                _timerTask = Task.Run(() => Timer(_cts.Token, _stopwatch), _cts.Token);
            }
            catch (Exception ex)
            {
                Log("Error", $"Timer.dll: Error when starting the timer: {ex}");
            }
        }
        private async Task Timer(CancellationToken token, Stopwatch stopwatch)
        {
            if (stopwatch == null)
                throw new InvalidOperationException("TimerPlugin: stopwatch must be initialized before running the timer loop.");
            
            double nextUpdateMs = update;
            double nextIntervalMs = intervalMs;
            double nextSharedMs = lcm;

            if (timerMode == "None")
            {
                return;
            }
            else if (timerMode == "Mixed")
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        if (state == TimerState.Paused)
                            await _resumeTcs.Task.ConfigureAwait(false);

                        token.ThrowIfCancellationRequested();

                        double now = stopwatch.Elapsed.TotalMilliseconds;

                        if (!_suppressCatchUp)
                        {
                            double missedIntervals = (now - nextIntervalMs) / intervalMs;
                            double missedUpdates = (now - nextUpdateMs) / update;
                            if (missedIntervals > 1 || missedUpdates > 1)
                            {
                                _suppressCatchUp = true;
                            }
                        }

                        if (_suppressCatchUp)
                        {
                            nextIntervalMs = Math.Ceiling(now / intervalMs) * intervalMs;
                            nextUpdateMs = Math.Ceiling(now / update) * update;
                            if (!double.IsInfinity(lcm) && lcm > 0)
                                nextSharedMs = Math.Ceiling(now / lcm) * lcm;
                            _suppressCatchUp = false;
                        }

                        double nextTarget = Math.Min(nextUpdateMs, nextIntervalMs);
                        if (hasDuration)
                            nextTarget = Math.Min(nextTarget, durationMs);
                        double delayMs = Math.Max(1.0, nextTarget - now);
                        await Task.Delay(TimeSpan.FromMilliseconds(delayMs), token).ConfigureAwait(false);

                        now = stopwatch.Elapsed.TotalMilliseconds;
                        if (hasDuration && now >= durationMs)
                        {
                            if (_tickCount >= totalTicks) break;
                            else if (now >= durationMs + intervalMs)
                            {
                                break;
                            }
                        }
                        now = stopwatch.Elapsed.TotalMilliseconds;

                        if (doUpdate && doInterval)
                        {
                            while (!double.IsInfinity(lcm) && now >= nextSharedMs)
                            {
                                Tick(); Update(); Execute();
                                nextIntervalMs += intervalMs;
                                nextUpdateMs += update;
                                nextSharedMs += lcm;
                            }
                            while (now >= nextIntervalMs)
                            {
                                Tick(); Execute();
                                nextIntervalMs += intervalMs;
                            }
                            while (now >= nextUpdateMs)
                            {
                                Update();
                                nextUpdateMs += update;
                            }
                        }
                        else break;
                    }
                }
                catch (OperationCanceledException)
                { }
                catch (Exception)
                { }
                finally
                {
                    if (!token.IsCancellationRequested)
                    {
                        if (repeat == 0 || (repeat > 0 && repeatCount < repeat))
                        {
                            DoRepeat();
                        }
                        else
                        {
                            DoStop();
                        }
                    }
                }
            }
            else if (timerMode == "Update")
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        if (state == TimerState.Paused)
                            await _resumeTcs.Task.ConfigureAwait(false);

                        token.ThrowIfCancellationRequested();

                        double now = stopwatch.Elapsed.TotalMilliseconds;

                        if (!_suppressCatchUp)
                        {
                            double missedUpdates = (now - nextUpdateMs) / update;
                            if (missedUpdates > 1)
                            {
                                _suppressCatchUp = true;
                            }
                        }

                        if (_suppressCatchUp)
                        {
                            nextUpdateMs = Math.Ceiling(now / update) * update;
                            _suppressCatchUp = false;
                        }
                        double nextTarget = Math.Min(nextUpdateMs, durationMs);
                        double delayMs = Math.Max(1.0, nextTarget - now);
                        await Task.Delay(TimeSpan.FromMilliseconds(delayMs), token).ConfigureAwait(false);
                        now = stopwatch.Elapsed.TotalMilliseconds;
                        if (hasDuration && now >= durationMs)
                        {
                            break;
                        }
                        now = stopwatch.Elapsed.TotalMilliseconds;
                        if (doUpdate && !doInterval)
                        {
                            while (now >= nextUpdateMs)
                            {
                                Update();
                                nextUpdateMs += update;
                            }
                        }
                        else break;
                    }
                }
                catch (OperationCanceledException)
                { }
                catch (Exception)
                { }
                finally
                {
                    if (!token.IsCancellationRequested)
                    {
                        if (repeat == 0 || (repeat > 0 && repeatCount < repeat))
                        {
                            DoRepeat();
                        }
                        else
                        {
                            DoStop();
                        }
                    }
                }
            }
            else if (timerMode == "Interval")
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        if (state == TimerState.Paused)
                            await _resumeTcs.Task.ConfigureAwait(false);

                        token.ThrowIfCancellationRequested();

                        double now = stopwatch.Elapsed.TotalMilliseconds;

                        if (!_suppressCatchUp)
                        {
                            double missedIntervals = (now - nextIntervalMs) / intervalMs;
                            if (missedIntervals > 1)
                            {
                                _suppressCatchUp = true;
                            }
                        }

                        if (_suppressCatchUp)
                        {
                            nextIntervalMs = Math.Ceiling(now / intervalMs) * intervalMs;
                            _suppressCatchUp = false;
                        }
                        double nextTarget = Math.Min(nextIntervalMs, durationMs);
                        double delayMs = Math.Max(1.0, nextTarget - now);
                        await Task.Delay(TimeSpan.FromMilliseconds(delayMs), token).ConfigureAwait(false);
                        now = stopwatch.Elapsed.TotalMilliseconds;
                        if (hasDuration && now >= durationMs)
                        {
                            if (_tickCount >= totalTicks) break;
                        }
                        now = stopwatch.Elapsed.TotalMilliseconds;
                        if (doInterval && !doUpdate)
                        {
                            while (now >= nextIntervalMs)
                            {
                                Tick(); Execute();
                                nextIntervalMs += intervalMs;
                            }
                        }
                        else break;
                    }
                }
                catch (OperationCanceledException)
                { }
                catch (Exception)
                { }

                finally
                {
                    if (!token.IsCancellationRequested)
                    {
                        if (repeat == 0 || (repeat > 0 && repeatCount < repeat))
                        {
                            DoRepeat();
                        }
                        else
                        {
                            DoStop();
                        }
                    }
                }
            }
        }

        private void Tick()
        {
            if (hasDuration && _tickCount >= totalTicks)
                return;
            _tickCount++;
        }

        private void CheckTimerMode()
        {
                if (doUpdate && doInterval)
                {
                    timerMode = "Mixed";
                    lcm = LCM(intervalMs, update);
                    if (hasDuration)
                        totalTicks = durationMs / intervalMs;
                }
                else if (doUpdate && !doInterval)
                    timerMode = "Update";
                else if (doInterval && !doUpdate)
                {
                    timerMode = "Interval";
                    if (hasDuration)
                        totalTicks = durationMs / intervalMs;
                }
                else
                    timerMode = "None";
        }
        private void Update() => SendBang(_updateBang);
        private void Execute() => ExecuteCompiled(_onTickAction);
        private static long GCD(long a, long b)
        { while (b != 0) { var t = b; b = a % b; a = t; } return a; }
        private static long LCM(long a, long b)
        {
            try
            {
                checked
                {
                    return (a / GCD(a, b)) * b;
                }
            }
            catch (OverflowException)
            {
                return long.MaxValue;
            }
        }
        internal void SendBang(string bang)
        {
            EnsureRainmeterHwnd();
            if (_hwndRainmeter == IntPtr.Zero || string.IsNullOrEmpty(bang))
                return;

            IntPtr uniPtr = Marshal.StringToHGlobalUni(bang);

            SendNotifyMessage(_hwndRainmeter,WM_RAINMETER_EXECUTE,skin,uniPtr);

            _ = Task.Delay(500).ContinueWith(_ => { Marshal.FreeHGlobal(uniPtr); }, TaskScheduler.Default);
        }
        #endregion

        #region Commanding
        internal void CommandMeasure(string args)
        {
            bool isRunning = state != TimerState.Stopped;
            bool isPaused = state == TimerState.Paused;

            if (hasDuration && intervalMs > durationMs)
            {
                Log("Error", $"Timer.dll: The interval ({intervalMs}) can't be greater than the duration ({durationMs}).");
                return;
            }

            string cmd = args.Trim().ToLowerInvariant();

            switch (cmd)
            {
                case "start":
                    if (!CanStart()) break;
                    DoStart();
                    break;

                case "pause":
                    if (!CanPause()) break;
                    DoPause();
                    break;

                case "resume":
                    if (state == TimerState.Stopped)
                    {
                        DoStart();
                    }
                    else if (!CanResume())
                    {
                        break;
                    }
                    else
                    {
                        DoResume();
                    }
                    break;

                case "stop":
                    if (!CanStop()) break;
                    DoStop();
                    break;

                case "reset":
                    DoReset(false);
                    break;

                case "dismiss":
                    if (!CanStop()) break;
                    DoDismiss();
                    break;

                case "toggle":
                    if (isRunning) CommandMeasure("stop");
                    else CommandMeasure("start");
                    break;

                case "toggleresume":
                    if (!isRunning) CommandMeasure("start");
                    else if (isPaused) CommandMeasure("resume");
                    else CommandMeasure("pause");
                    break;

                default:
                    Log("Warning", $"Timer.dll: Unknown command: {args}");
                    break;
            }
        }
        private void DoStart(bool execute = true)
        {
            if (!string.IsNullOrWhiteSpace(targetTime))
            {
                durationMs = DateToMs(targetTime);
                durationUnits = "ms";
                if (durationMs < 0)
                {
                    Log("Error", $"Timer.dll: Invalid date: {targetTime}. It's in the past.");
                }
                _suppressCatchUp = true;
            }
            else
            {
                durationMs = ToMilliseconds(duration, durationUnits);
            }

            state = TimerState.Running;
            if (_cts != null)
            {
                _cts.Cancel();
            }
            pauseTime = 0;
            _tickCount = 0;
            _stopwatch.Restart();

            Log("Debug", $"Timer.dll: Started.");
            if (execute && !string.IsNullOrWhiteSpace(onStartAction))
            {
                Update();
                ExecuteCompiled(_onStartAction);
            }
            else if (execute)
                Update();

            _ = StartTimer();
        }
        private void DoPause()
        {
            _resumeTcs?.TrySetResult(null);
            _resumeTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            _stopwatch.Stop();
            pauseTime = GetCurrentTime();
            state = TimerState.Paused;

            Log("Debug", $"Timer.dll: Paused");

            Update();
            if (!string.IsNullOrWhiteSpace(onPauseAction))
                ExecuteCompiled(_onPauseAction);
        }
        private void DoResume()
        {
            _suppressCatchUp = true;
            pauseTime = 0;
            if (!string.IsNullOrWhiteSpace(targetTime))
            {
                long msUntilTarget = DateToMs(targetTime);
                durationMs = msUntilTarget;
                durationUnits = "ms";

                state = TimerState.Running;
                _stopwatch.Reset();

                _stopwatch.Start();
                _resumeTcs.TrySetResult(null);

                Log("Debug", $"Timer.dll: Resumed.");

                Update();
                if (!string.IsNullOrWhiteSpace(onResumeAction))
                    ExecuteCompiled(_onResumeAction);

                return;
            }
            else
            {
                state = TimerState.Running;
                _stopwatch.Start();
                _resumeTcs.TrySetResult(null);
                Log("Debug", $"Timer.dll: Resumed.");
            }

            Update();
            if (!string.IsNullOrWhiteSpace(onResumeAction))
                ExecuteCompiled(_onResumeAction);
        }
        private void DoStop()
        {
            _resumeTcs?.TrySetResult(null);
            _cts?.Cancel();
            if (_stopwatch.IsRunning) _stopwatch.Stop(); stopTime = GetAdjustedTimeString(@"dd\:hh\:mm\:ss\.fffffff");

            Log("Debug", $"Timer.dll: Stopped at {stopTime}.");


            pauseTime = 0;
            repeatCount = 0;
            state = 0;

            if (resetOnStop)
            {
                _tickCount = 0;
                _stopwatch.Reset();
            }

            Update();
            if (!string.IsNullOrWhiteSpace(onStopAction))
                ExecuteCompiled(_onStopAction);
        }
        private void DoDismiss()
        {
            _resumeTcs?.TrySetResult(null);
            _cts?.Cancel();

            if (_stopwatch.IsRunning)
                _stopwatch.Stop(); stopTime = GetAdjustedTimeString(@"dd\:hh\:mm\:ss\.fffffff");

            Log("Debug", $"Timer.dll: Dismissed at {stopTime}.");


            Action actionStr = _onDismissAction;

            state = 0;

            if (resetOnStop)
            {
                _tickCount = 0;
                _stopwatch.Reset();
            }

            Update();

            if (!string.IsNullOrWhiteSpace(onDismissAction))
                ExecuteCompiled(actionStr);
        }
        private void DoReset(bool isRepeat)
        {
            _resumeTcs?.TrySetResult(null);
            _cts?.Cancel();
            _stopwatch?.Stop(); stopTime = GetAdjustedTimeString(@"dd\:hh\:mm\:ss\.fffffff");

            Action actionStr;

            if (!isRepeat)
            {
                actionStr = _onResetAction;
            }
            else
            {
                actionStr = _onRepeatAction;
            }

            if (state == TimerState.Running)
            {
                _tickCount = 0;
                _stopwatch.Reset();

                Log("Debug", $"Timer.dll: Reset at {stopTime}.");

                Update();

                if (isRepeat)
                {
                    if (!string.IsNullOrWhiteSpace(onRepeatAction))
                        ExecuteCompiled(actionStr);
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(onResetAction))
                        ExecuteCompiled(actionStr);
                }

                DoStart(false);

                return;
            }
            else if ((state == TimerState.Paused) || state == TimerState.Stopped && (_tickCount > 0 || GetCurrentTime() > 0))
            {
                state = 0;
                _tickCount = 0;
                _stopwatch.Reset();

                Log("Debug", $"Timer.dll: Reset at {stopTime}.");

                Update();

                if (isRepeat)
                {
                    if (!string.IsNullOrWhiteSpace(onRepeatAction))
                        ExecuteCompiled(actionStr);
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(onResetAction))
                        ExecuteCompiled(actionStr);
                }
            }
        }
        private void DoRepeat()
        {
            repeatCount++;
            DoReset(true);
        }
        #endregion

        #region Precondition helpers 
        private bool CanStart()
        {
            if (state != TimerState.Stopped)
            {
                Log("Warning", "Timer.dll: Already running.");
                return false;
            }
            return true;
        }
        private bool CanPause()
        {
            if (state == TimerState.Stopped)
            {
                Log("Warning", "Timer.dll: Not running.");
                return false;
            }
            if (state == TimerState.Paused)
            {
                Log("Warning", "Timer.dll: Already paused.");
                return false;
            }
            return true;
        }
        private bool CanResume()
        {
            if (state != TimerState.Paused)
            {
                Log("Warning", "Timer.dll: Not paused.");
                return false;
            }
            return true;
        }
        private bool CanStop()
        {
            if (state == TimerState.Stopped)
            {
                Log("Warning", "Timer.dll: Not running.");
                return false;
            }
            return true;
        }
        #endregion

        #region String Compilation
        private List<IFormatSegment> CompileString(string input)
        {
            var segments = new List<IFormatSegment>();
            int lastIndex = 0;

            foreach (Match match in FormatRegex.Matches(input))
            {
                if (match.Index > lastIndex)
                {
                    segments.Add(new StaticSegment(input.Substring(lastIndex, match.Index - lastIndex)));
                }

                string token = match.Groups[1].Value;
                string lowerToken = token.ToLowerInvariant();
                string decimalText = match.Groups[2].Value;

                if (!int.TryParse(decimalText, out int decimals))
                {
                    decimals = 0;
                }

                segments.Add(token switch
                {
                    "k" => new DynamicSegment(() => AddLeadingZeros(GetCurrentTick(), decimals)),

                    "tms" => new DynamicSegment(() => FormatTimeValue("tms", decimals)),
                    "ts" => new DynamicSegment(() => FormatTimeValue("ts", decimals)),
                    "tm" => new DynamicSegment(() => FormatTimeValue("tm", decimals)),
                    "th" => new DynamicSegment(() => FormatTimeValue("th", decimals)),
                    "td" => new DynamicSegment(() => FormatTimeValue("td", decimals)),

                    "tf" => new DynamicSegment(() => GetAdjustedTimeString(@"dd\:hh\:mm\:ss\.fffffff")),
                    "T" => new DynamicSegment(() => GetAdjustedTimeString(@"hh\:mm\:ss\.ff")),
                    "t" => new DynamicSegment(() => GetAdjustedTimeString(@"hh\:mm\:ss")),

                    "S" => new DynamicSegment(() => FormatTimeValue("S", 0, true)),
                    "M" => new DynamicSegment(() => FormatTimeValue("M", 0, true)),
                    "H" => new DynamicSegment(() => FormatTimeValue("H", 0, true)),
                    "D" => new DynamicSegment(() => FormatTimeValue("D", 0, true)),

                    "s" => new DynamicSegment(() => FormatTimeValue("s")),
                    "m" => new DynamicSegment(() => FormatTimeValue("m")),
                    "h" => new DynamicSegment(() => FormatTimeValue("h")),
                    "d" => new DynamicSegment(() => FormatTimeValue("d")),

                    var f when f.StartsWith("f") || f.StartsWith("F") => new DynamicSegment(() => GetAdjustedTimeString(f)),

                    _ => new StaticSegment(match.Value)
                });

                lastIndex = match.Index + match.Length;
            }

            if (lastIndex < input.Length)
            {
                segments.Add(new StaticSegment(input.Substring(lastIndex)));
            }

            return segments;
        }
        private string RenderString(List<IFormatSegment> segments)
        {
            var sb = new StringBuilder();
            foreach (var segment in segments)
            {
                sb.Append(segment.GetValue());
            }
            return sb.ToString();
        }
        #endregion

        #region Formatting
        private string FormatTimeValue(string unit, int decimals = 0, bool pad = false)
        {
            TimeSpan span = GetElapsedTimeSpan();

            double rawValue = unit switch
            {
                "ts" or "s" or "S" => span.TotalSeconds,
                "tm" or "m" or "M" => span.TotalMinutes,
                "th" or "h" or "H" => span.TotalHours,
                "td" or "d" or "D" => span.TotalDays,
                "tms" => span.TotalMilliseconds,
                _ => span.TotalMilliseconds,
            };

            if (hasDuration && isCountdown && (unit == "ts" || unit == "tms") && decimals == 0)
            {
                rawValue = Math.Ceiling(rawValue);
            }

            if (unit.StartsWith("t"))
            {   
                    return rawValue.ToString($"F{decimals}");
            }
            else 
            {
                int intVal = (int)rawValue;

                if (unit == "s" || unit == "S") intVal %= 60;
                else if (unit == "m" || unit == "M") intVal %= 60;
                else if (unit == "h" || unit == "H") intVal %= 24;

                return pad ? intVal.ToString("D2") : intVal.ToString();
            }
        }
        private string AddLeadingZeros(int number, int totalLength)
        {
            return number.ToString().PadLeft(totalLength, '0');
        }
        #endregion

        #region Time Extraction
        private string GetAdjustedTimeString(string format)
        {
            TimeSpan span = GetElapsedTimeSpan();

            if (Regex.IsMatch(format, @"^[fF]+$"))
            {
                int precision = format.Length;
                double totalSeconds = span.TotalSeconds;
                double fraction = totalSeconds - Math.Floor(totalSeconds);
                int raw = (int)(fraction * Math.Pow(10, precision));
                string s = raw.ToString().PadLeft(precision, '0');

                if (char.IsUpper(format[0]))
                {
                    s = s.TrimEnd('0');
                }

                return s;
            }

            if (format.IndexOf('F') >= 0)
            {
                string fFmt = Regex.Replace(format, @"F+", m => new string('f', m.Length));

                string baseText = span.ToString(fFmt);

                int dot = baseText.LastIndexOf('.');
                if (dot >= 0)
                {
                    string before = baseText.Substring(0, dot);
                    string frac = baseText.Substring(dot + 1).TrimEnd('0');
                    return frac.Length > 0 ? $"{before}.{frac}" : before;
                }

                return baseText;
            }

            Match match = Regex.Match(format, @"f+");

            return span.ToString(format);
        }
        private double GetCurrentTime()
        {
            return (_stopwatch.ElapsedTicks / (double)Stopwatch.Frequency) * 1000.0;
        }
        private int GetCurrentTick()
        {
            return (int)(isCountdown && totalTicks > 0
                ? Math.Max(0, totalTicks - _tickCount)
                : _tickCount);
        }
        private TimeSpan GetElapsedTimeSpan()
        {
            if (_stopwatch == null)
                return TimeSpan.Zero;

            double tickScale = (double)TimeSpan.TicksPerSecond / Stopwatch.Frequency;
            long currentTicks = (long)(_stopwatch.ElapsedTicks * tickScale);

            if (state == TimerState.Paused)
                currentTicks = (long)(pauseTime * TimeSpan.TicksPerMillisecond);

            if (isCountdown && hasDuration)
            {
                double totalTicks = (durationMs * TimeSpan.TicksPerMillisecond);
                double remaining = Math.Max(0.0, totalTicks - currentTicks);
                return new TimeSpan((long)remaining);
            }

            return new TimeSpan(currentTicks);
        }
        #endregion

        #region Execution
        private void ExecuteCompiled(Action template)
        {
            if (template != null)
                SendBang(template.Render());
        }
        #endregion

        #region Time Conversion
        private long ToMilliseconds(double number, string format)
        {
            return format.ToLowerInvariant() switch
            {
                "1" or "ms" or "mil" or "millisecond" or "milliseconds" => (long)number,
                "2" or "s" or "sec" or "second" or "seconds" => SecondsToMs(number),
                "3" or "m" or "min" or "minute" or "minutes" => MinutesToMs(number),
                "4" or "h" or "hour" or "hours" => HoursToMs(number),
                "5" or "d" or "day" or "days" => DaysToMs(number),
                _ => (long)number,
            };
        }
        private long DaysToMs(double days) => (long)(days * 24 * 60 * 60 * 1000);
        private long HoursToMs(double hours) => (long)(hours * 60 * 60 * 1000);
        private long MinutesToMs(double minutes) => (long)(minutes * 60 * 1000);
        private long SecondsToMs(double seconds) => (long)(seconds * 1000);
        private long DateToMs(string input)
        {

            if (DateTime.TryParse(input, out DateTime target))
            {
                return AdjustAndReturnMs(target);
            }

            if (_customCulture != null)
            {
                if (DateTime.TryParse(input, _customCulture, DateTimeStyles.None, out target))
                    return AdjustAndReturnMs(target);
            }
            else
            {
                foreach (var culture in FallbackCultures)
                {
                    if (DateTime.TryParse(input, culture, DateTimeStyles.None, out target))
                        return AdjustAndReturnMs(target);
                }
            }

            Log("Error", $"Timer.dll: Invalid date format: {input}.");
            return ToMilliseconds(duration, durationUnits); 
        }
        private long AdjustAndReturnMs(DateTime target)
        {
            DateTime now = DateTime.Now;

            if (target.Date == now.Date && target.TimeOfDay <= now.TimeOfDay)
            {
                target = target.AddDays(1);
            }

            TimeSpan diff = target - now;
            return diff.TotalMilliseconds > 0 ? (long)diff.TotalMilliseconds : -1;
        }
        #endregion
    }
    public class Plugin
    {
        [DllExport]
        public static void Initialize(ref IntPtr data, IntPtr rm)
        {
            Measure measure = new();
            Rainmeter.API api = (Rainmeter.API)rm;
            measure.skin = api.GetSkin();
            measure.skinName = api.GetSkinName();
            measure.measureName = api.GetMeasureName();
            measure.updateDivider = api.ReadInt("UpdateDivider", 1);

            measure._updateBang = $"!UpdateMeasure \"{measure.measureName}\"";

            if (measure.updateDivider != -1)
            {
                measure.SendBang($"!SetOption \"{measure.measureName}\" \"UpdateDivider\" \"-1\"");
            }
            data = GCHandle.ToIntPtr(GCHandle.Alloc(measure));
        }

        [DllExport]
        public static void Reload(IntPtr data, IntPtr rm)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            Rainmeter.API api = (Rainmeter.API)rm;
            measure.Reload(api);
        }

        [DllExport]
        public static double Update(IntPtr data)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            measure.timeString = measure._formatString.Render();
            return (double)measure.state;
        }

        [DllExport]
        public static void Finalize(IntPtr data)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;

            try
            {
                measure._resumeTcs?.TrySetResult(null);
                measure._cts?.Cancel();
                measure._cts?.Dispose();
            }
            catch { }

            measure._cts = null;

            if (measure._stopwatch.IsRunning)
                measure._stopwatch.Stop();
            measure._stopwatch.Reset();

            measure._timerTask = null;

            GCHandle.FromIntPtr(data).Free();
        }

        [DllExport]
        public static IntPtr GetString(IntPtr data)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;

            return StringBuffer.Update(measure.timeString);
        }

        [DllExport]
        public static void ExecuteBang(IntPtr data,
            [MarshalAs(UnmanagedType.LPWStr)] string args)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            measure.CommandMeasure(args);
        }

        [DllExport]
        public static IntPtr TimeStamp(IntPtr data, int argc,
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr, SizeParamIndex = 1)] string[] argv)
        {
            return GetElapsedTime(data, argv);
        }

        [DllExport]
        public static IntPtr TS(IntPtr data, int argc,
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr, SizeParamIndex = 1)] string[] argv)
        {
            return GetElapsedTime(data, argv);
        }

        private static IntPtr GetElapsedTime(IntPtr data, string[] argv)
        {
            string customFormat = (argv == null || argv.Length == 0) ? "%t" : argv[0];
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            Measure.Action _customFormat = string.IsNullOrEmpty(customFormat) ? null : new Measure.Action(measure, customFormat);

            string timeStampText = _customFormat.Render();

            return StringBuffer.Update(timeStampText);
        }
    }
}

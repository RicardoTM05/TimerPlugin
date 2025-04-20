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
        internal API _api;

        internal IntPtr skin;
        internal string measureName;
        internal string skinName;
        internal CancellationTokenSource _cts;
        internal ManualResetEventSlim _pauseEvent = new(true);
        internal Stopwatch _stopwatch;
        internal Task _timerTask;
        internal int updateDivider;
        internal double duration;
        internal string targetTime;
        internal long durationMs;
        internal double interval;
        internal long intervalMs;
        internal string onTickAction;
        internal string onStartAction;
        internal string onStopAction;
        internal string onResumeAction;
        internal string onPauseAction;
        internal string onResetAction;
        internal string onDismissAction;
        internal bool resetOnStop;
        internal int _tickCount = 0;
        // State: 0 = stopped, 1 = running, 2 = paused.
        internal int state = 0;
        internal bool isCountdown;
        internal int update;
        internal string durationUnits;
        internal string IntervalUnits;
        internal string formatString;
        internal string formatLocale;
        internal string timeString;
        private bool _suppressCatchUp = false;

        internal void Reload(API api)
        {
            _api = api;

            // Force UpdateDivider=-1 to avoid rainmeter from updating the measure.
            updateDivider = api.ReadIntFromSection(measureName, "UpdateDivider", 1);
            if (updateDivider != -1)
            {
                api.Execute($"!SetOption \"{measureName}\" \"UpdateDivider\" \"-1\"");
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
            resetOnStop = _api.ReadInt("ResetOnStop", 1) > 0;

            // Actions
            onTickAction = _api.ReadString("OnTickAction", "", false);
            onStartAction = _api.ReadString("OnStartAction", "", false);
            onStopAction = _api.ReadString("OnStopAction", "", false);
            onResumeAction = _api.ReadString("OnResumeAction", "", false);
            onPauseAction = _api.ReadString("OnPauseAction", "", false);
            onResetAction = _api.ReadString("OnResetAction", "", false);
            onDismissAction = _api.ReadString("OnDismissAction", "", false);

            // TargetTime check
            if (string.IsNullOrWhiteSpace(targetTime))
                durationMs = ToMilliseconds(duration, durationUnits);
            intervalMs = ToMilliseconds(interval, IntervalUnits);
        }

        internal async Task RunTimer( long durationMs, long intervalMs, ManualResetEventSlim pauseEvent, CancellationToken token, Stopwatch stopwatch)
        {
            double AutoPeriod = update > 0 ? update : 0;
            bool doAutoUpdate = AutoPeriod > 0;
            bool doInterval = intervalMs > 0;

            double nextAutoMs = AutoPeriod;
            double nextIntervalMs = doInterval ? intervalMs : double.PositiveInfinity;

            stopwatch.Start();

            TimeSpan delayTimeSpan = TimeSpan.Zero;

            while (!token.IsCancellationRequested)
            {
                pauseEvent.Wait(token);

                var now = stopwatch.Elapsed.TotalMilliseconds;

                double nextTarget = Math.Min(nextAutoMs, nextIntervalMs);
                if (durationMs > 0)
                    nextTarget = Math.Min(nextTarget, durationMs);

                var delayMs = Math.Max(1.0, nextTarget - now);
                delayTimeSpan = TimeSpan.FromMilliseconds(delayMs);
                await Task.Delay(delayTimeSpan, token);

                
                if (doInterval && _suppressCatchUp)
                {
                    nextIntervalMs = Math.Ceiling(now / intervalMs) * intervalMs;
                    _suppressCatchUp = false;
                }
                now = stopwatch.Elapsed.TotalMilliseconds;

                if (durationMs > 0 && now >= durationMs)
                {
                    if (doInterval && nextIntervalMs - intervalMs < durationMs)
                        DispatchTick();
                    break;
                }

                while (doInterval && (now = _stopwatch.Elapsed.TotalMilliseconds) >= nextIntervalMs)
                {
                    nextIntervalMs += intervalMs;
                    DispatchTick2();
                }

                while (doAutoUpdate && (now = _stopwatch.Elapsed.TotalMilliseconds) >= nextAutoMs)
                {
                    nextAutoMs += AutoPeriod;
                    _api.Execute($"!UpdateMeasure \"{measureName}\"");
                }
            }

            if (!token.IsCancellationRequested)
                DoStop();

            void DispatchTick()
            {
                _tickCount++;
                if (update > 0 && stopwatch.IsRunning)
                    _api.Execute($"!UpdateMeasure \"{measureName}\"");
                if (!string.IsNullOrWhiteSpace(onTickAction))
                {
                    _api.Execute(GetElapsedTime(onTickAction));
                }
            }
            void DispatchTick2()
            {
                _tickCount++;
                if (!string.IsNullOrWhiteSpace(onTickAction))
                {
                    _api.Execute(GetElapsedTime(onTickAction));
                }
            }
        }

        #region Commanding
        internal void CommandMeasure(string args)
        {
            bool isRunning = _timerTask != null
                             && !_timerTask.IsCompleted
                             && !_cts.IsCancellationRequested;
            bool isPaused = !_pauseEvent.IsSet;

            if (durationMs > 0 && intervalMs > durationMs)
            {
                _api.Log(API.LogType.Error,
                    $"Timer.dll: The interval ({intervalMs}) can't be greater than the duration ({durationMs}).");
                return;
            }

            string cmd = args.Trim().ToLowerInvariant();

            switch (cmd)
            {
                case "start":
                    if (!CanStart(isRunning)) break;
                    DoStart();
                    break;

                case "pause":
                    if (!CanPause(isRunning, isPaused)) break;
                    DoPause();
                    break;

                case "resume":
                    if (!isRunning)
                    {
                        DoStart();
                    }
                    else if (!CanResume(isPaused))
                    {
                        break;
                    }
                    else
                    {
                        DoResume();
                    }
                    break;

                case "stop":
                    if (!CanStop(isRunning)) break;
                    DoStop();
                    break;

                case "reset":
                    DoReset(isRunning, isPaused);
                    break;

                case "dismiss":
                    if (!CanStop(isRunning)) break;
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
                    _api.Log(API.LogType.Warning, $"Timer.dll: Unknown command: {args}");
                    break;
            }
        }
        private void DoStart(bool execute = true)
        {
            if (!string.IsNullOrWhiteSpace(targetTime))
            {
                durationMs = DateToMs(targetTime, formatLocale);
                durationUnits = "ms";
                if (durationMs < 0)
                {
                    _api.Log(API.LogType.Error,
                        $"Timer.dll: Invalid date: {targetTime}. It's in the past.");
                }
                _suppressCatchUp = true;
            }
            else
            {
                durationMs = ToMilliseconds(duration, durationUnits);
            }

            state = 1;
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            _pauseEvent.Set();
            _tickCount = 0;
            _stopwatch = Stopwatch.StartNew();

            _api.Log(API.LogType.Debug, "Timer.dll: Started.");
            if (execute && !string.IsNullOrWhiteSpace(onStartAction))
            {
                _api.Execute($"!UpdateMeasure \"{measureName}\"");
                _api.Execute(GetElapsedTime(onStartAction));
            }
            else if (execute)
                _api.Execute($"!UpdateMeasure \"{measureName}\"");

            _timerTask = RunTimer(
                    durationMs,
                    intervalMs,
                    _pauseEvent,
                    _cts.Token,
                    _stopwatch
                );
        }
        private void DoPause()
        {
            _stopwatch.Stop();
            _pauseEvent.Reset();
            state = 2;

            _api.Log(API.LogType.Debug,
                $"Timer.dll: Paused");

            _api.Execute($"!UpdateMeasure \"{measureName}\"");
            if (!string.IsNullOrWhiteSpace(onPauseAction))
                _api.Execute(GetElapsedTime(onPauseAction));
        }
        private void DoResume()
        {
            if (!string.IsNullOrWhiteSpace(targetTime))
            {
                _cts?.Cancel();

                long elapsed = _stopwatch.ElapsedMilliseconds;
                long msUntilTarget = DateToMs(targetTime,formatLocale);

                durationMs = (elapsed + Math.Max(0, msUntilTarget));
                durationUnits = "ms";
                
                _cts = new CancellationTokenSource();
                _pauseEvent.Set();
                _stopwatch.Start();
                state = 1;

                _suppressCatchUp = true;

                _timerTask = RunTimer(
                    durationMs,
                    intervalMs,
                    _pauseEvent,
                    _cts.Token,
                    _stopwatch
                );
            }
            else
            {

                _pauseEvent.Set();
                _stopwatch.Start();
                state = 1;
            }
            _api.Log(API.LogType.Debug,
               $"Timer.dll: Resumed.");

            _api.Execute($"!UpdateMeasure \"{measureName}\"");
            if (!string.IsNullOrWhiteSpace(onResumeAction))
                _api.Execute(GetElapsedTime(onResumeAction));
           
        }
        private void DoStop()
        {
            _cts?.Cancel();
            _pauseEvent.Set();
            if (_stopwatch.IsRunning) _stopwatch.Stop();

            _api.Log(API.LogType.Debug, $"Timer.dll: Stopped.");

            state = 0;

            if (resetOnStop)
            {
                _tickCount = 0;
                _stopwatch.Reset();
            }

            _api.Execute($"!UpdateMeasure \"{measureName}\"");
            if (!string.IsNullOrWhiteSpace(onStopAction))
                _api.Execute(GetElapsedTime(onStopAction));
        }
        private void DoDismiss()
        {
            _cts?.Cancel();
            _pauseEvent.Set();
            if (_stopwatch.IsRunning) _stopwatch.Stop();

            _api.Log(API.LogType.Debug,
                $"Timer.dll: Dismissed.");

            string actionStr = onDismissAction;

            state = 0;

            if (resetOnStop)
            {
                _tickCount = 0;
                _stopwatch.Reset();
            }

            _api.Execute($"!UpdateMeasure \"{measureName}\"");
            if (!string.IsNullOrWhiteSpace(onDismissAction))
                _api.Execute(GetElapsedTime(actionStr));
        }
        private void DoReset(bool isRunning, bool isPaused)
        {
            if (isRunning && !isPaused)
            {
                _cts?.Cancel();
                _pauseEvent.Set();
                _stopwatch?.Stop();

                string actionStr = onResetAction;

                _tickCount = 0;
                _stopwatch.Reset();

                _api.Execute($"!UpdateMeasure \"{measureName}\"");
                if (!string.IsNullOrWhiteSpace(onResetAction))
                    _api.Execute(GetElapsedTime(actionStr));

                DoStart(false);

                return;
            }
            else if ((isRunning && isPaused) || !isRunning && (_tickCount > 0 || _stopwatch?.Elapsed.TotalMilliseconds > 0))
            {
                _cts?.Cancel();
                _pauseEvent.Set();
                _stopwatch?.Stop();

                if (!string.IsNullOrWhiteSpace(onResetAction))
                    _api.Execute(GetElapsedTime(onResetAction));
                _api.Log(API.LogType.Debug,
                $"Timer.dll: Reset.");

                state = 0;
                _tickCount = 0;
                _stopwatch.Reset();

                _api.Execute($"!UpdateMeasure \"{measureName}\"");
            }
        }
        #endregion

        #region Precondition helpers 
        private bool CanStart(bool isRunning)
        {
            if (isRunning)
            {
                _api.Log(API.LogType.Warning, "Timer.dll: Already running.");
                return false;
            }
            return true;
        }
        private bool CanPause(bool isRunning, bool isPaused)
        {
            if (!isRunning)
            {
                _api.Log(API.LogType.Warning, "Timer.dll: Not running.");
                return false;
            }
            if (isPaused)
            {
                _api.Log(API.LogType.Warning, "Timer.dll: Already paused.");
                return false;
            }
            return true;
        }
        private bool CanResume(bool isPaused)
        {
            if (!isPaused)
            {
                _api.Log(API.LogType.Warning, "Timer.dll: Already running.");
                return false;
            }
            return true;
        }
        private bool CanStop(bool isRunning)
        {
            if (!isRunning)
            {
                _api.Log(API.LogType.Warning, "Timer.dll: Not running.");
                return false;
            }
            return true;
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
        internal long DaysToMs(double days)
        {
            return (long)(days * 24 * 60 * 60 * 1000);
        }
        private long HoursToMs(double hours)
        {
            return (long)(hours * 60 * 60 * 1000);
        }
        private long MinutesToMs(double minutes)
        {
            return (long)(minutes * 60 * 1000);
        }
        private long SecondsToMs(double seconds)
        {
            return (long)(seconds * 1000);
        }
        private long DateToMs(string input, string formatLocale)
        {
            DateTime now = DateTime.Now;

            if (DateTime.TryParse(input, out DateTime target))
            {
                return AdjustAndReturnMs(now, target);
            }

            string[] fallbackCultures = {
                "en-US", // English (United States)
                "en-GB", // English (United Kingdom)
                "fr-FR", // French (France)
                "de-DE", // German (Germany)
                "es-ES", // Spanish (Spain)
                "es-MX", // Spanish (Mexico)
                "it-IT", // Italian (Italy)
                "pt-PT", // Portuguese (Portugal)
                "pt-BR", // Portuguese (Brazil)
                "ru-RU", // Russian (Russia)
                "ja-JP", // Japanese (Japan)
                "zh-CN", // Chinese (Simplified, China)
                "zh-TW", // Chinese (Traditional, Taiwan)
                "ko-KR", // Korean (Korea)
                "nl-NL", // Dutch (Netherlands)
                "sv-SE", // Swedish (Sweden)
                "pl-PL", // Polish (Poland)
                "tr-TR", // Turkish (Turkey)
                "ar-SA", // Arabic (Saudi Arabia)
                "cs-CZ", // Czech (Czech Republic)
                "fi-FI", // Finnish (Finland)
                "da-DK", // Danish (Denmark)
                "he-IL", // Hebrew (Israel)
                "hu-HU", // Hungarian (Hungary)
                "no-NO", // Norwegian (Norway)
                "th-TH"  // Thai (Thailand)
            };

            if (!string.IsNullOrWhiteSpace(formatLocale))
            {
                CultureInfo culture = new(formatLocale);
                if (DateTime.TryParse(input, culture, DateTimeStyles.None, out target))
                {
                    return AdjustAndReturnMs(now, target);
                }
            }
            else
                foreach (string cultureName in fallbackCultures)
                {
                    CultureInfo culture = new(cultureName);
                    if (DateTime.TryParse(input, culture, DateTimeStyles.None, out target))
                    {
                        return AdjustAndReturnMs(now, target);
                    }
                }

            _api.Log(API.LogType.Error, $"Timer.dll: Invalid date format: {input}.");
            return 0;
        }
        private long AdjustAndReturnMs(DateTime now, DateTime target)
        {
            if (target.Date == now.Date && target.TimeOfDay <= now.TimeOfDay)
            {
                target = target.AddDays(1);
            }

            TimeSpan diff = target - now;
            return diff.TotalMilliseconds > 0 ? (long)diff.TotalMilliseconds : -1;
        }
        internal double MsToDays(long milliseconds)
        {
            return milliseconds / (1000.0 * 60 * 60 * 24);
        }
        private double MsToHours(long milliseconds)
        {
            return milliseconds / (1000.0 * 60 * 60);
        }
        private double MsToMinutes(long milliseconds)
        {
            return milliseconds / (1000.0 * 60);
        }
        private double MsToSeconds(long milliseconds)
        {
            return (milliseconds / 1000.0);
        }
        #endregion

        #region Elapsed Time
        private static readonly Dictionary<string, string> PlaceholderMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "%tms", "[[<MILLISECONDS>]]" },
            { "%ts",  "[[<SECONDS>]]" },
            { "%tm",  "[[<MINUTES>]]" },
            { "%th",  "[[<HOURS>]]" },
            { "%td",  "[[<DAYS>]]" },
            { "%k",   "[[<TICKCOUNT>]]" },
            { "'",    "[[<QUOTE>]]" }
        };
        private static readonly List<(string Token, string Replacement)> SortedFormatTokens = new()
        {
            ("%FFFFFFF", "FFFFFFF"),
            ("%FFFFFF",  "FFFFFF"),
            ("%FFFFF",   "FFFFF"),
            ("%FFFF",    "FFFF"),
            ("%FFF",     "FFF"),
            ("%FF",      "FF"),
            ("%fffffff", "fffffff"),
            ("%ffffff",  "ffffff"),
            ("%fffff",   "fffff"),
            ("%ffff",    "ffff"),
            ("%fff",     "fff"),
            ("%ff",      "ff"),
            ("%T",       "hh\\:mm\\:ss\\.ff"),
            ("%t",       "hh\\:mm\\:ss"),
            ("%H",       "hh"),
            ("%M",       "mm"),
            ("%S",       "ss"),
            ("%D",       "dd"),
            ("%g",       "g"),
            ("%G",       "G"),
            ("%c",       "c"),
            ("%F",       "%F"),
            ("%f",       "%f"),
            ("%h",       "%h"),
            ("%m",       "%m"),
            ("%s",       "%s"),
            ("%d",       "%d")
        };
        private static readonly char[] FractionalTokens = { 'f', 'F', 'g', 'G', 'c' };
        internal string GetElapsedTime(string argv)
        {
            string customFormat = string.IsNullOrEmpty(argv) ? "%t" : argv;

            customFormat = customFormat.Replace("'", "[[<QUOTE>]]");

            foreach (var kvp in PlaceholderMap)
            {
                if (kvp.Key == "'")
                    continue;
                var unitName = kvp.Value.Trim('[', ']');
                customFormat = Regex.Replace(
                    customFormat,
                    $@"{Regex.Escape(kvp.Key)}(?:\{{(\d+)\}})?",
                    m =>
                    {
                        var dec = m.Groups[1].Success ? m.Groups[1].Value : "0";
                        return $"[[{unitName}:{dec}]]";
                    },
                    RegexOptions.IgnoreCase
                );
            }

            foreach (var kvp in PlaceholderMap)
            {
                if (customFormat.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    customFormat = Regex.Replace(customFormat, Regex.Escape(kvp.Key), kvp.Value, RegexOptions.IgnoreCase);
                }
            }

            const string defaultFormat = "%t";
            string timeSpanFormat = ConvertFormat(customFormat);
            string formatToUse = timeSpanFormat;

            try
            {
                _ = TimeSpan.Zero.ToString(formatToUse);
            }
            catch (FormatException)
            {
                formatToUse = defaultFormat;
                timeSpanFormat = ConvertFormat(defaultFormat);
            }

            TimeSpan timeToDisplay;

            if (_stopwatch == null)
            {
                timeToDisplay = (isCountdown && durationMs > 0 && string.IsNullOrWhiteSpace(targetTime))
                    ? TimeSpan.FromMilliseconds(durationMs)
                    : TimeSpan.Zero;
            }
            else if (isCountdown && durationMs > 0)
            {
                if (!string.IsNullOrWhiteSpace(targetTime) && _stopwatch.Elapsed.TotalMilliseconds == 0)
                {
                    timeToDisplay = TimeSpan.Zero;
                }
                else
                {
                    double rem = Math.Max(0, durationMs - _stopwatch.Elapsed.TotalMilliseconds);
                    bool wantsFraction = timeSpanFormat.IndexOfAny(FractionalTokens) >= 0;

                    timeToDisplay = wantsFraction
                        ? TimeSpan.FromMilliseconds(rem)
                        : TimeSpan.FromSeconds(Math.Ceiling(rem / 1000.0));
                } 
            }
            else
            {
                timeToDisplay = _stopwatch.Elapsed;
            }

            string result = timeToDisplay.ToString(formatToUse);

            double rawMs = isCountdown && durationMs > 0
                ? Math.Max(0, durationMs - (_stopwatch?.Elapsed.TotalMilliseconds ?? 0.0))
                : _stopwatch?.Elapsed.TotalMilliseconds ?? 0.0;

            result = Regex.Replace(
                result,
                @"\[\[<(MILLISECONDS|TICKCOUNT|SECONDS|MINUTES|HOURS|DAYS)>:(\d+)\]\]",
                match =>
                {
                    var unit = match.Groups[1].Value;
                    var decimals = int.Parse(match.Groups[2].Value);
                    double val = unit switch
                    {
                        "MILLISECONDS" => rawMs,
                        "TICKCOUNT" => _tickCount,
                        "SECONDS" => MsToSeconds((long)rawMs),
                        "MINUTES" => MsToMinutes((long)rawMs),
                        "HOURS" => MsToHours((long)rawMs),
                        "DAYS" => MsToDays((long)rawMs),
                        _ => 0
                    };
                    return FormatNumber(val, decimals);
                }
            );

            result = result.Replace("[[>QUOTE>]]", "'");

            return result;
        }
        public static string FormatNumber(double number, int decimals)
        {
            if (decimals == 0)
                return Math.Floor(number).ToString();
            else
                return Math.Round(number, decimals).ToString($"F{decimals}");
        }
        internal string ConvertFormat(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            static string EscapeLiteral(string str)
            {
                return $"'{str.Replace("\\", "\\\\").Replace("'", "''")}'";
            }

            var output = new StringBuilder();
            int i = 0;

            while (i < input.Length)
            {
                bool matched = false;

                foreach (var (token, replacement) in SortedFormatTokens)
                {
                    if (i + token.Length <= input.Length && input.Substring(i, token.Length) == token)
                    {
                        output.Append(replacement);
                        i += token.Length;
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                {
                    int start = i;
                    while (i < input.Length)
                    {
                        bool isTokenStart = false;
                        foreach (var (token, _) in SortedFormatTokens)
                        {
                            if (i + token.Length <= input.Length && input.Substring(i, token.Length) == token)
                            {
                                isTokenStart = true;
                                break;
                            }
                        }

                        if (isTokenStart)
                            break;

                        i++;
                    }

                    string literal = input.Substring(start, i - start);
                    output.Append(EscapeLiteral(literal));
                }
            }

            return output.ToString();
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
            // Read UpdateDivider
            measure.updateDivider = api.ReadInt("UpdateDivider", 1);
            // Force to -1 if not already -1
            if (measure.updateDivider != -1)
            {
                api.Execute($"!SetOption \"{measure.measureName}\" \"UpdateDivider\" \"-1\"");
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
            measure.timeString = measure.GetElapsedTime(measure.formatString);
            return measure.state;
        }

        [DllExport]
        public static void Finalize(IntPtr data)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;

            try
            {
                measure._cts?.Cancel();
                measure._pauseEvent?.Set();

                if (measure._timerTask != null && !measure._timerTask.IsCompleted)
                {
                    try { measure._timerTask.Wait(TimeSpan.FromSeconds(1)); }
                    catch (AggregateException) { }
                }
            }
            catch { }

            measure._cts?.Dispose();
            measure._cts = null;
            measure._pauseEvent?.Dispose();
            measure._pauseEvent = null;

            if (measure._stopwatch != null)
            {
                if (measure._stopwatch.IsRunning)
                    measure._stopwatch.Stop();
                measure._stopwatch.Reset();
                measure._stopwatch = null;
            }

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
            return GetElapsedTime(data,argv);
        }

        private static IntPtr GetElapsedTime(IntPtr data, string[] argv)
        {
            string customFormat = (argv == null || argv.Length == 0) ? "%t" : argv[0];
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;

            string timeStampText = measure.GetElapsedTime(customFormat);

            return StringBuffer.Update(timeStampText);
        }

    }
}

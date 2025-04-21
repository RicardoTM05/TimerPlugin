# Timer Plugin

Timer Plugin provides timing functionality (typically accurate to within a few milliseconds, subject to the limits of the underlying OS timer and task scheduler) and supports both count‑up and countdown modes. Total duration and interval can each be specified in any time unit (from milliseconds to days), with support for exact time and date. Can execute bangs on start, tick, pause, resume, stop, dismiss, and reset events. It offers automatic measure updates, time formatting codes (including total seconds, total milliseconds and tick counts). It's sole purpose is to offer an easy way to create timer skins.

## Preview


![A](https://github.com/user-attachments/assets/caffac63-d8b8-4714-8573-f9579c8a4c0d)


&#x20;*This GIF was created entirely in Rainmeter using *[*Finalshot*](https://github.com/NSTechBytes/FinalShot)* + *[*Timer*](https://github.com/RicardoTM05/TimerPlugin/tree/master)* plugins and *[*gifski*](https://github.com/ImageOptim/gifski).*

## ✨ Features

- **Multiple Output Units**: Elapsed/remaining in milliseconds, seconds, minutes, hours, days, and tick count.
- **Custom Formatting**: TimeMeasure‑style format codes, including total seconds, milliseconds, and tick counts.
- **Full Control**: Start, Stop, Pause, Resume, Reset, Dismiss, Toggle commands.
- **Set Target Time**: Countdown to a specific date/time or duration.
- **Event Bangs**: Execute Rainmeter bangs on Start, Tick, Pause, Resume, Stop, Dismiss, Reset.
- **Intervals**: Fire bangs at regular ticks.

## 📥 Download & Installation

Download the latest `.rmskin` package from releases:

[*Releases*](https://github.com/RicardoTM05/TimerPlugin/releases)

The package includes the plugin and 5 example skins to get you started.

## 👏 Huge Thanks

- **Yincognito** for testing and feedback.
- **Everyone** who dares to test and share experiences! 🙏

## 📝 Changelog

- Renamed `Update` → `UpdateTimer`
- Removed fractional time codes (`%tfd`, `%tfh`, `%tfm`, `%tfs`)
- Added decimal‑places operator `{N}` for total‑time codes (`%td`, `%th`, `%tm`, `%ts`)
  - e.g. `%td{3}` → `3.125` days

* First public release.

---

## 📖 Documentation

For a basic countdown timer:

```ini
[MeasureTimer]
Measure=Plugin
Plugin=Timer
DurationUnits=Seconds
Duration=35
UpdateTimer=1000
OnStopAction=[!Log "Time's up!"]
```

- Returns `hh:mm:ss` by default. Stops after 35 s and logs `Time's up!`.
- Set `Duration=-1` to run indefinitely.
- Control via bangs:
  - Start: `LeftMouseUpAction=[!CommandMeasure MeasureTimer "Start"]`
  - Stop:  `LeftMouseUpAction=[!CommandMeasure MeasureTimer "Stop"]`
  - Toggle: `... "Toggle"]`

The timer runs on its own thread—no need to adjust Rainmeter's `UpdateDivider`.

| Value Type | Meaning                                                                                                          |
| ---------- | ---------------------------------------------------------------------------------------------------------------- |
| **Number** | `0` = Stopped, `1` = Running, `2` = Paused                                                                       |
| **String** | Formatted time (default `hh:mm:ss`). Use `Format=` codes (see below) or enable `Countdown=1` for remaining time. |

Examples:

```ini
Format=Total Elapsed sec: %ts       ; -> "Total Elapsed sec: 2675"
Format=Hour:%H Min:%M Sec:%S        ; -> "Hour:01 Min:25 Sec:08"
Format=%k                           ; tick count, e.g. "125"
```

Options (defaults shown):

```ini
[MeasureTimer]
Measure=Plugin
Plugin=Timer
; ms between updates (<=0 disables)
UpdateTimer=1000
; default output format     
Format=%t               
DurationUnits=milliseconds
Duration=-1
; overrides Duration when set:         
TargetTime=""          
FormatLocale=""
IntervalUnits=milliseconds
Interval=-1             
Countdown=-1            
ResetOnStop=1
OnStartAction=[]
OnStopAction=[]
OnResumeAction=[]
OnPauseAction=[]
OnResetAction=[]
OnDismissAction=[]
OnTickAction=[]
```

- **UpdateTimer**: ms between updates (timer always runs). `-1` hides display but timer continues.
- **Duration/TargetTime**: Set stop point by duration or date/time string.
- **FormatLocale**: e.g. `es-MX` to parse localized dates.
- **Interval**: fires `OnTickAction` every N units.
- **Countdown**: show remaining rather than elapsed time.

| Code                               | Description                                                                         |
| ---------------------------------- | ----------------------------------------------------------------------------------- |
| `%D`                               | Days (zero‑padded)                                                                  |
| `%H`                               | Hours (zero‑padded)                                                                 |
| `%M`                               | Minutes (zero‑padded)                                                               |
| `%S`                               | Seconds (zero‑padded)                                                               |
| `%F…%FFFFFFF`                      | Fractional seconds (tenths → 10⁻⁷s), trailing zeros trimmed                         |
| `%d`                               | Days (no padding)                                                                   |
| `%h`                               | Hours (no padding)                                                                  |
| `%m`                               | Minutes (no padding)                                                                |
| `%s`                               | Seconds (no padding)                                                                |
| `%f…%fffffff`                      | Fractional seconds (up to 7 digits)                                                 |
| `%T`                               | Shortcut for `hh:mm:ss.ff`                                                          |
| `%t`                               | Shortcut for `hh:mm:ss`                                                             |
| `%td`, `%th`, `%tm`, `%ts`, `%tms` | Total elapsed days, hours, minutes, seconds, milliseconds (supports `{N}` decimals) |
| `%k`                               | Tick count                                                                          |

### Actions

- **OnStartAction**: bangs on start
- **OnStopAction**: bangs on stop
- **OnPauseAction**: bangs on pause
- **OnResumeAction**: bangs on resume
- **OnResetAction**: bangs on reset
- **OnDismissAction**: bangs on dismiss
- **OnTickAction**: bangs each interval

### Commands

Use `[!CommandMeasure "MeasureName" "Command"]`:

| Command        | Description                                                |
| -------------- | ---------------------------------------------------------- |
| `Start`        | Start timer & fire OnStartAction                           |
| `Stop`         | Stop timer & fire OnStopAction                             |
| `Toggle`       | Toggle start/stop                                          |
| `Pause`        | Pause timer & fire OnPauseAction                           |
| `Resume`       | Resume (or Start if stopped) & fire OnResumeAction         |
| `ToggleResume` | Toggle resume/pause                                        |
| `Reset`        | Reset to 0 (or Duration if countdown) & fire OnResetAction |
| `Dismiss`      | Stop & fire OnDismissAction                                |



### Section Variables

**TimeStamp** or **TS**

- Returns formatted elapsed (or remaining) time, e.g. `[&Measure:TimeStamp()]` or `[&Measure:TS("%th{4}")]`.

> *Note: Set **`DynamicVariables=1`** on the meter/measure using these.*

# ⚙️ Options in depth

---

### **UpdateTimer**
**Default**: `1000`

Defines the update interval of the measure in milliseconds.

**Values:**
- `<= 0` Disabled  
- `> 0` Enabled  

The plugin measure will not update until started, once started it will update once every 1000ms (1 second) by default.

When `UpdateTimer=-1`, the measure will not update but the timer will run and work normally. The only difference is that the measure will not display the elapsed/remaining time.

The lowest possible value for `UpdateTimer` is `1`, however, setting it this low doesn't offer better precision. For most tasks leaving it at 1000 is just fine. If a low update is required, setting it at `16` is the lowest recommended.

The timer itself runs on a different thread, this means that it doesn't depend on Rainmeter's Update to work. This Update value is only the rate at which the plugin's measure is updated to report the timer's elapsed/remaining time to Rainmeter by updating its Number and String values.

The measure will also update automatically when executing any command that changes its state (Start, Stop, Pause, Resume, Reset, Dismiss) even if `UpdateTimer=-1` is set. The update will occur before executing the actions. For example, if `[!CommandMeasure Timer Stop]`, the timer will first stop, then it will update the measure and finally will execute the `OnStopAction`. The same order applies to all other commands.

In short, if you don't need to display the elapsed/remaining time, then simply leave `UpdateTimer=-1`, the timer will still run normally.  
If you need to display the time or use the string value for anything else, set `UpdateTimer=1000` (or lower if needed).  
If you only need to display the time on each tick, set `UpdateTimer` and `Interval` to the same value.

If your skin will only display the time given by the timer and nothing else that needs regular updating, it's recommended to set `Update=-1` on `[Rainmeter]` to avoid the regular skin updates from interfering with the plugin’s measure update. This won’t stop the plugin from ticking at the right time because again, the timer is in a separate thread and it will continue regardless of Rainmeter’s update cycle. The `OnTickAction` is still executed at the right moment.

---

### **Format**
**Default**: `%t`

Defines the format string returned by the measure. It works pretty much like the Format option of the Time measure.

**Values:**
- `"Any string with or without %Codes"`

It can use any Format Code from the Format Codes list to return the formatted elapsed time on every measure update.

Example:  
`Format= %H-%M-%S` will return the elapsed/remaining time as `07-25-32`.

Check the Format Codes section to see all available format codes.

---

### **DurationUnits**
**Default**: `Milliseconds`

Defines the units the `Duration` option will take.

**Values:**
- `1`, `ms`, `mil`, `millisecond`, `milliseconds`
- `2`, `s`, `sec`, `second`, `seconds`  
- `3`, `m`, `min`, `minute`, `minutes`  
- `4`, `h`, `hour`, `hours`  
- `5`, `d`, `day`, `days`

Although you can set e.g. `DurationUnits=2`, the option itself can't take math directly, as it is in reality a string option, not a number option.

---

### **Duration**
**Default**: `-1`

Defines the duration of the timer in the units set by the `DurationUnits` option.

**Values:**
- `<= 0`: Disabled  
- `> 0`: Timer duration

Once the timer reaches the set duration it will stop automatically and will execute the `OnStopAction`.

When `Duration=-1` (or any value <= 0) the timer will run until manually stopped.

If `ResetOnStop` is enabled, the timer will return to 00:00:00 when it stops.

All units take fractional numbers except milliseconds. Any fraction in ms will be floored to the nearest integer.

---

### **TargetTime**
**Default**: `""`

Defines the duration of the timer with a formatted time string.

**Values:**
- `2025/04/26 06:48:17`: The timer will stop on April 26, 2025 at 06:48:17 AM.

When a `TargetTime` string is given, the `Duration` value is ignored.

Some valid time string formats include:

**Full date and time:**
- 2025-04-19 16:30  
- April 19, 2025 4:30 PM  
- 4/19/2025 16:30  
- 19/04/2025 16:30  

**Date only:**
- 2025-04-19  
- April 19, 2025  
- 4/19/2025 or 19/04/2025 or 04/19/2025  

**Time only:**
- 4:30 PM  
- 4:30 AM  
- 16:30  
- 04:30 (24-hour)  

When a time-only value is given, it will assume today's date. If the time has already passed, it is then assumed to be tomorrow.  
If a date in the past that is greater than one day is given, it will fail and start with `duration=-1`. An error will be logged.  
If an invalid format is given, then it will start with `duration=-1`. An error will be logged.

To use a specific date format, use the `FormatLocale` option.

---

## FormatLocale
**Default**: `""`

An optional value that defines the "language - locale" that the formatted date/time string defined in `TargetTime` is in.

Example:
- `es-MX`

For a list of all valid locales visit: [Microsoft LCID List](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-lcid/a9eac961-e77d-41a6-90a5-ce1a8b0cdb9c)

If this option is not defined, the plugin will compare the given `TargetTime` string to a set list of common locales.

Examples:
```
FormatLocale=lo-LA
TargetTime=25 ເມສາ 2025

FormatLocale=cy-GB
TargetTime=25 Ebrill 2025
```

---

## IntervalUnits
**Default**: `Milliseconds`

Defines the units the `Interval` option will take.

Values:
- `1`, `ms`, `mil`, `millisecond`, `milliseconds`
- `2`, `s`, `sec`, `second`, `seconds`
- `3`, `m`, `min`, `minute`, `minutes`
- `4`, `h`, `hour`, `hours`
- `5`, `d`, `day`, `days`

Although you can set e.g. `IntervalUnits=3`, the option itself can't take math directly, as it is in reality a string option, not a number option.

---

## Interval
**Default**: `-1`

Defines the interval in the units set by the `IntervalUnits` option at which the timer will execute the `OnTickAction`.

Values:
- `<= 0`: Disabled
- `> 0`: Tick interval

Example:
If `IntervalUnits=Hours` and `Interval=1`, the timer will execute the `OnTickAction` once an hour.

When `Interval <= 0`, the `OnTickAction` will never be executed.

Every time the timer reaches the interval, it counts a "tick" up. The total ticks count can be returned by using the Format Code `%k` on the `Format` option, the `OnTickAction`, or on any other action.

All units take fractional numbers except milliseconds; any fraction in ms will be floored to the nearest integer.

Note: It is important to understand that the timer has a delay of around ±16ms. This means that if you set an interval every 1000ms, those intervals won't tick at exactly 1000ms. More like 1003ms, 1018ms, 998ms, etc.

---

## Countdown
**Default**: `-1`

Sets the timer to return the remaining time instead of the elapsed time.

Values:
- `-1`: Disabled
- `1`: Enabled

If enabled, and `Duration` or `TargetTime` are set, the strings returned by the Format Codes will start from the `Duration` and will stop at zero.

---

## ResetOnStop
**Default**: `1`

If disabled, the string value of the measure won't be reset to zero when the timer stops.

Values:
- `-1`: Disabled
- `1`: Enabled

---

## Actions
The following actions can use the `Format Codes` to export the timer's elapsed time.

### OnStartAction
Executes given bangs when the timer starts.

Example:
```
OnStartAction=[!Log "The timer has started at %t."]
```
Logs: `The timer has started at 00:25:00.` (when `countdown=1`)

### OnStopAction
Executes given bangs when the timer stops.

Example:
```
OnStopAction=[!Log "The timer has stopped on tick %k."]
```
Logs: `The timer has stopped on tick 325.`

### OnResumeAction
Executes given bangs when the timer resumes.

Example:
```
OnResumeAction=[!Log "The timer has been resumed at minute %M."]
```
Logs: `The timer has been resumed at minute 35.`

### OnPauseAction
Executes given bangs when the timer pauses.

Example:
```
OnPauseAction=[!Log "The timer was paused at %T."]
```
Logs: `The timer was paused at 00:25:37.125.`

### OnDismissAction
Executes given bangs when the timer is dismissed.

Example:
```
OnDismissAction=[!Log "Timer dismissed at %H:%M."]
```
Logs: `The timer was paused at 05:25.`

### OnResetAction
Executes given bangs when the timer is reset.

Example:
```
OnResetAction=[!Log "Reset after %h hours and %m minutes."]
```
Logs: `Reset after 5 hours and 3 minutes.`

### OnTickAction
Executes given bangs when the timer reaches the set interval (ticks).

Example:
```
OnTickAction=[!Log "Tick %k."]
```
Logs: `Tick 27.`

---

## 🛠️ Commands
The following commands are used with the `!CommandMeasure MeasureName "Command"` bang.

### Start
Starts and executes the `OnStartAction`.

Example:
```
[!CommandMeasure MeasureName "Start"]
```

### Stop
Stops and executes the `OnStopAction`.

Example:
```
[!CommandMeasure MeasureName "Stop"]
```

### Toggle
Toggles the timer and executes the `OnStartAction` or `OnStopAction`.

Example:
```
[!CommandMeasure MeasureName "Toggle"]
```

### Resume
- If not running, first it will `Start` and then will execute `OnStartAction`.
- If paused, it will `Resume` and execute `OnResumeAction`.

Example:
```
[!CommandMeasure MeasureName "Resume"]
```

### Pause
Pauses and executes `OnPauseAction`.

Example:
```
[!CommandMeasure MeasureName "Pause"]
```

### ToggleResume
- If not running, it will `Start` and execute `OnStartAction`.
- If running, it will `Pause` and execute `OnPauseAction`.
- If paused, it will `Resume` and execute `OnResumeAction`.

Example:
```
[!CommandMeasure MeasureName "ToggleResume"]
```

### Reset
- If running, it will restart from 0 and execute `OnStartAction`.
- If paused, it will `Stop` and execute `OnResetAction`.
- If not running and not at 0, it will reset to 0 and execute `OnResetAction`.

When `Countdown=1`, it will reset to the `Duration`.

Example:
```
[!CommandMeasure MeasureName "Reset"]
```

### Dismiss
Stops the timer and executes `OnDismissAction`.

Example:
```
[!CommandMeasure MeasureName "Dismiss"]
```

---

## 🪄 Section Variables

### TimeStamp or TS
- Argument: `"String"`
- Default: `%t`

Returns the elapsed time formatted using Format Codes.

Examples:
```
[&Measure:TimeStamp()] or [&Measure:TS()] => 12:25:17
[&Measure:TimeStamp("%ts")] => 2600
[&Measure:TimeStamp("Hour: %h, Minute: %m")] => Hour: 5, Minute: 25
```

If `Countdown=1` is set on the measure, it will return the remaining time.

Note: `DynamicVariables=1` is required to be set on the Measure/Meter where the section variable is used.

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

Download the latest `.rmskin` package:

[TimerPlugin\_1.1.rmskin](TimerPlugin_1.1.rmskin)

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

# Debugging the cBot in Rider on macOS

cTrader's docs only cover Visual Studio on Windows with `System.Diagnostics.Debugger.Launch()`.
That call does nothing on macOS. Instead, start the bot in cTrader first, then attach Rider to the
process it runs in.

## How it works

cTrader runs every cBot instance in its own `algohost.netcore` process, on the shared .NET
runtime in `/usr/local/share/dotnet`. Each process shows its role in the `--title=` argument:

| `--title=`  | What it is                          |
| ----------- | ----------------------------------- |
| `VWAPTrade` | your bot (live instance or backtest) |
| `Build`     | cTrader's compiler, not your bot     |

## Steps (confirmed working)

1. **Build in Debug.** Make sure cTrader loads a Debug `.algo`, not a Release one. In a Release
   build, variables show as unavailable and stepping jumps around.
2. **Start the bot or the backtest in cTrader.**
3. **In Rider, choose Run → Attach to Process…** and pick the `algohost.netcore` entry whose
   command line has `--title=VWAPTrade`. To find its PID:

   ```bash
   pgrep -fl 'title=VWAPTrade'
   ```

4. **Set breakpoints** in `OnBar`, `SignalDetector`, and so on.

## If something goes wrong (not tried yet)

**A breakpoint in `OnStart` never hits.** A backtest starts a new process and runs `OnStart`
before you can attach. The bot already has a `debug调试` parameter, but its `LaunchDebug()` in
`VWAPTrade.cs` calls `Debugger.Launch()`, which Spotware documents only for Visual Studio on
Windows. To catch `OnStart` on macOS, change `LaunchDebug()` to wait for Rider to attach
(needs `using System.Threading;`):

```csharp
private void LaunchDebug() {
    if (!IsDebug)
        return;

    // Debugger.Launch() is the Windows route; on macOS, wait for Rider to attach instead.
    var deadline = DateTime.UtcNow.AddSeconds(60);
    while (!Debugger.IsAttached && DateTime.UtcNow < deadline)
        Thread.Sleep(200);
}
```

Turn on `debug调试`, start the backtest, and attach within 60 seconds.

**Breakpoints stay hollow ("no symbols loaded").** cTrader loads the bot from the `.algo`
package rather than a DLL on disk, so Rider may not find the `.pdb`. Embed the symbols in the
DLL for Debug builds, in `VWAPTrade/VWAPTrade.csproj`:

```xml
<DebugType Condition="'$(Configuration)' == 'Debug'">embedded</DebugType>
```

**Attach fails.** cTrader's docs say debugging needs `AccessRights.FullAccess`. The bot already
uses that (see the `[Robot]` attribute in `VWAPTrade.cs`), and attaching works with it. If the
attribute is ever lowered to `AccessRights.None`, attaching hasn't been tried with that setting,
so check this first.

## Faster option for rule logic

The gates, sizing and risk rules are pure classes (`VwapStack`, `DepartureTracker`,
`OrderPlanner`, `RiskBudget`, …) covered by xUnit tests in `tests/`. In Rider, right-click a
test and choose **Debug**. No cTrader is needed. To reproduce one specific bar, copy its values
from the trade CSV into a test. Attach to cTrader only for code that touches the platform
(`Bars`, positions, order placement).

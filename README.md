# lidless for Windows

## Download

**[Download the installer](https://github.com/soulflowxd/lidless/releases/latest/download/lidless-Setup.exe)** · **[Download the portable app](https://github.com/soulflowxd/lidless/releases/latest/download/lidless.exe)**

Run the installer for a Start menu shortcut, or save and open the portable app. **Right-click the tray icon** (you may need the **^** arrow) for the glass-styled quick menu, including **Quit**. Lidless runs entirely from the tray and has no separate window.

A Windows tray app inspired by [Lidless](https://github.com/Blueturboguy07/Lidless). It temporarily changes the **active Windows power plan's lid-close action** to **Do nothing**, so your laptop keeps running when you close the lid. The tray menu has a keep-awake toggle, a 15-minute to 4-hour auto-off timer, a low-battery guard, and an option to launch at login. It restores the original AC and battery lid actions when you turn it off or quit. A separate watchdog process restores them if the tray process exits unexpectedly.

## Auto when agent runs

Turn on **Auto when agent runs** in the tray menu to keep the laptop awake during an active local Codex or Claude Code turn, then restore normal sleep when the turn ends. It reads local session records every 15 seconds and ignores apps that are merely open. A stalled session expires after 10 minutes without activity. The battery guard still takes priority. Manual keep-awake and its auto-off timer remain available when auto mode is off.

Claude Code sessions in the desktop app are included when they write to the local `~/.claude/projects` journal. Remote/cloud agents and ordinary Claude chat sessions do not provide a reliable local running signal, so auto mode cannot track them. You can use the manual toggle for those.

## Build locally

Install the .NET 8 SDK and run:

```powershell
dotnet publish src/lidless/lidless.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist
```

The app is `dist/lidless.exe`.

## Important behavior

- Windows must still be configured to allow this power change. A device policy or firmware setting can override it.
- While active, lidless also requests that Windows not enter idle sleep; it does not force the display to stay lit. Closing the lid should leave local work running under the active power plan, though device policy or firmware may override it.
- Closing the lid does not lock Windows. Press **Win+L** if you want to lock it.
- Keep the laptop ventilated. The battery guard disables lidless at 20% when unplugged. Windows does not provide a consistent thermal sensor API, so this version does not offer the macOS heat guard.
- If you change power plans while lidless is on, the new plan is left alone and the original plan's saved lid action is restored when lidless turns off.

Original project copyright © 2026 Nghia Luong. This Windows port is MIT licensed; see [LICENSE](LICENSE).

# lidless for Windows

## Download

**[Download the installer](https://github.com/soulflowxd/lidless/releases/latest/download/lidless-Setup.exe)** · **[Download the portable app](https://github.com/soulflowxd/lidless/releases/latest/download/lidless.exe)**

Run the installer for a Start menu shortcut, or save and open the portable app. **Right-click the tray icon** (you may need the **^** arrow) for the quick menu, including **Quit**. Left-click it to open the settings window.

A Windows tray app inspired by [Lidless](https://github.com/Blueturboguy07/Lidless). It temporarily changes the **active Windows power plan's lid-close action** to **Do nothing**, so your laptop keeps running when you close the lid. The tray menu has a keep-awake toggle, a 15-minute to 4-hour auto-off timer, a low-battery guard, and an option to launch at login. It restores the original AC and battery lid actions when you turn it off or quit. A separate watchdog process restores them if the tray process exits unexpectedly.

## Build locally

Install the .NET 8 SDK and run:

```powershell
dotnet publish src/lidless/lidless.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist
```

The app is `dist/lidless.exe`.

## Important behavior

- Windows must still be configured to allow this power change. A device policy or firmware setting can override it.
- Closing the lid does not lock Windows. Press **Win+L** if you want to lock it.
- Keep the laptop ventilated. The battery guard disables lidless at 20% when unplugged. Windows does not provide a consistent thermal sensor API, so this version does not offer the macOS heat guard.
- If you change power plans while lidless is on, the new plan is left alone and the original plan's saved lid action is restored when lidless turns off.

Original project copyright © 2026 Nghia Luong. This Windows port is MIT licensed; see [LICENSE](LICENSE).

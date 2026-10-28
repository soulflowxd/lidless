# Contributor guide

Read [CLAUDE.md](CLAUDE.md) too. It points back here so Codex and Claude share these instructions.

- Keep lidless a tray-only Windows app. Right-click opens the compact menu; Quit must restore the saved lid actions.
- Manual Keep awake and Auto agent detection are independent requests. The timer only ends the manual request. Battery and thermal guards pause either request.
- Do not claim a temperature is known if the CPU sensor is unavailable. Do not claim remote or ordinary chat activity can be detected from local session files.
- Build on Windows with `.NET 8`: `dotnet publish src/lidless/lidless.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o dist`.
- The installer is built with Inno Setup from `installer/lidless.iss`. CI produces both portable and installer files.
- New original contributions from v0.4.0 use PolyForm Noncommercial 1.0.0. Older MIT releases retain their prior grant. Keep third-party MPL-2.0 notices with releases. Do not copy the inspiration project's icon.

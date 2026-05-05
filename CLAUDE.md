# Clicky Win

WPF .NET 9 Windows port of Clicky — an AI-powered screen + voice assistant that sits in the system tray, captures screenshots and audio on demand, and sends them to a backend worker for AI processing.

## Stack

- Language: C# 13
- Framework: WPF on .NET 9
- Target: `net9.0-windows10.0.19041.0`
- DI: `Microsoft.Extensions.DependencyInjection`
- Logging: Serilog (file sink, rolling daily)

## Commands

```bash
# Build all projects
dotnet build

# Run all tests
dotnet test

# Run the app
dotnet run --project src/Clicky.App
```

## Solution Structure

Six projects in `src/`:

| Project | Purpose |
|---|---|
| `Clicky.App` | WPF executable, entry point, DI wiring, tray icon host |
| `Clicky.Core` | Shared models, interfaces, constants — no external deps |
| `Clicky.Capture` | Screen capture (Vortice D3D11), audio capture (NAudio), input hooks (SharpHook) |
| `Clicky.Overlay` | WPF overlay window (transparent, click-through, always-on-top) |
| `Clicky.Services` | Business logic: worker API client, session management, AI pipeline |
| `Clicky.Tests` | xUnit tests, NSubstitute for mocks |

## Key NuGet Packages

| Package | Purpose |
|---|---|
| `H.NotifyIcon.Wpf` | System tray icon with context menu for WPF |
| `SharpHook` | Global keyboard/mouse hook (cross-platform libuiohook wrapper) |
| `NAudio` | Windows audio capture (WASAPI loopback + mic) |
| `Vortice.Direct3D11` | High-performance screen capture via Desktop Duplication API |
| `Microsoft.Windows.CsWin32` | Source-gen P/Invoke for Win32 APIs (UIPI, window styles, etc.) |
| `Serilog` + sinks | Structured file logging to `%LOCALAPPDATA%\Clicky\logs\` |

## Architecture Notes

- **No `StartupUri`** on `App.xaml` — the tray icon is the sole UI entry point; no main window on startup.
- **DI container** built in `App.OnStartup`; all services registered in `ServiceRegistration.cs`.
- **Overlay window** uses `WS_EX_TRANSPARENT | WS_EX_LAYERED` so mouse events pass through to underlying windows.
- **Screen capture** uses Desktop Duplication API via `Vortice.Direct3D11`; captures at native resolution then scales for display.
- **Audio capture** uses WASAPI loopback for system audio and a separate WASAPI client for mic input.
- **Global hooks** via SharpHook run on a background thread; marshal UI updates to the dispatcher.

## Known Weirdnesses

- **UIPI (User Interface Privilege Isolation)**: WPF apps run at medium IL. Elevated windows (run-as-admin) block `SendMessage`/`PostMessage` from the hook. If you need to send input to elevated windows, the app must also run elevated — which requires a manifest change and breaks some WPF features.
- **Virtual screen coordinates**: On multi-monitor setups the virtual screen origin is top-left of the leftmost/topmost monitor. Coordinates can be **negative** if a monitor is to the left or above the primary. Always use `SystemParameters.VirtualScreenLeft/Top` as the origin.
- **PerMonitorV2 DPI**: The manifest sets `PerMonitorV2`. WPF handles scaling automatically, but `RenderTargetBitmap` and any Win32 coordinate operations must work in physical pixels, not device-independent units. Convert with `VisualTreeHelper.GetDpi()`.
- **WS_EX_TRANSPARENT for click-through overlay**: Setting `WS_EX_TRANSPARENT` makes the window invisible to hit-testing. Combined with `WS_EX_LAYERED`, the overlay renders but all clicks pass through. Remove `WS_EX_TRANSPARENT` temporarily when you need the overlay to receive input.
- **`AllowsTransparency=true` performance**: WPF software rendering kicks in with `AllowsTransparency`. For the overlay, prefer `WindowChrome` + clip region or use a separate transparent `HwndHost` to avoid the perf hit.

## Push-to-Talk Hotkey

Default: **Ctrl+Win+Space**

Rationale: avoids AltGr collision on Spanish (ES) keyboards where AltGr+Space produces a non-breaking space. Win key combinations are generally safe across layouts.

## Coordinate System

- Origin: virtual screen top-left (can be negative on multi-monitor)
- Y axis: increases **downward**
- DPI: per-monitor; always query the DPI of the target monitor for a given point, not the primary monitor DPI
- Use `Screen.FromPoint` or `MonitorFromPoint` (via CsWin32) to get the monitor for a coordinate, then query its DPI scale

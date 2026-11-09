# Architecture

## Decision

Use C#/.NET 10. Cutsy.Core targets net10.0. Cutsy.Windows targets net10.0-windows with Windows Forms for settings/tray and a native layered HWND for mascot rendering. Windows 11 x64 is the supported v0.1 runtime. Pin an installed stable .NET 10 SDK in C01 and package a self-contained win-x64 release.

Qt and Godot can render transparent windows, but do not eliminate OS-specific foreign-window observation and positioning. The smaller Windows host is selected for v0.1. KDE Wayland requires a separate compositor integration feasibility pass.

## Boundaries and flow

1. Windows host supplies an immutable DesktopSnapshot (monitors, work areas, surface IDs, exposed segments, quiet-state flags) and pointer/user events.
2. Core consumes elapsed monotonic time, snapshot, input, validated recipes, and an injected seeded random source.
3. Core returns a RenderPose (sprite ID, world contact point, scale, facing, optional clip) and audio/host command requests.
4. Host resolves assets, renders, plays audio, and persists preferences.

Core uses plain records and geometry types, not HWND, System.Drawing, Forms, filesystem paths, playback objects, or wall-clock calls. Keep scheduling deterministic. Surface IDs are opaque; the host maps them to native handles. Capabilities gate recipes requiring window edges, absolute placement, or mouse interaction.

Separate loader/validator from execution. Validate into an immutable pack before swapping at a safe state boundary. Preview mode uses the same engine with controlled snapshots, clock, seed, and forced behavior selection.

## Windows host

Use WS_EX_LAYERED, WS_EX_TOOLWINDOW and WS_EX_NOACTIVATE, per-pixel alpha, and UpdateLayeredWindow with premultiplied BGRA. Keep this HWND small around the rendered sprite instead of covering the entire desktop. Cache decoded/scaled surfaces; release GDI/native resources deterministically.

Do not apply whole-window WS_EX_TRANSPARENT in ordinary mode: the body must remain clickable. Build a body interaction region from an explicit mask. Verify pass-through into an unrelated process, rather than assuming HTTRANSPARENT alone handles cross-process input. Where needed, update whole-window pass-through outside the mask using a bounded cursor-position check; validate its race/latency in C03. WM_MOUSEACTIVATE must preserve foreground focus.

Use mouse capture only during drag. Convert the four-logical-pixel drag threshold through the active monitor scale. Maintain a single physical-pixel world coordinate space; convert logical size at the host boundary. Enable per-monitor V2 DPI and handle display/DPI changes including negative monitor origins.

Enumerate windows initially, then SetWinEventHook with WINEVENT_OUTOFCONTEXT and WINEVENT_SKIPOWNPROCESS; use a message loop and bounded reconciliation every 2 seconds. Query DWM visible bounds and cloaked state. Compute exposed edge segments with z-order occlusion. Clip peeks explicitly; do not assume always-on-top creates behind-window occlusion.

Check foreground fullscreen bounds plus SHQueryUserNotificationState every 500 ms and on foreground events. Allow small frame-bound tolerance, distinguish maximized work-area windows from monitor-filling fullscreen, and handle API failures. Manual quiet mode remains available.

## Rendering and audio

Use a fixed-step 30 Hz movement simulation with clamped elapsed time after stalls; no large catch-up jump on resume. Sprite frame changes have their own durations. Idle rendering is event-driven unless a subtle transform is active. No frame allocations/PNG decoding on every tick.

Windows audio uses one reusable output service with gain, cancellation, and device recovery. C08 may use a pinned maintained playback library if built-in APIs do not support these requirements; record its license. Pack audio remains ordinary PCM WAV. Original effects are generated at build time, not by an online service at runtime.

## Packaging and portability

Default pack is an embedded resource; external overrides are resolved by the host. Portable flag selects adjacent data; otherwise use per-user local application data. Put persistence and migration behind the host boundary. Publish self-contained single-file without trimming until the full UI path has been verified; native runtime extraction is acceptable and documented.

Future hosts reuse core and packs, implementing snapshot/input/render/audio adapters. Do not claim KDE XWayland solves native Wayland window visibility. Investigate KWin script/extension plus a local IPC adapter for geometry and overlay placement; permissions and compositor support are deferred. macOS window observation/overlay/permissions are a separate adapter.

## Primary references

- [Layered windows and hit testing](https://learn.microsoft.com/windows/win32/winmsg/window-features)
- [Extended window styles](https://learn.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles)
- [Out-of-process window events](https://learn.microsoft.com/en-us/windows/desktop/api/winuser/nf-winuser-setwineventhook)
- [Notification state and fullscreen limitations](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shqueryusernotificationstate)
- [Per-monitor DPI](https://learn.microsoft.com/en-us/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows)
- [.NET single-file publishing](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview)
- [KWin scripting API](https://develop.kde.org/docs/plasma/kwin/api/)

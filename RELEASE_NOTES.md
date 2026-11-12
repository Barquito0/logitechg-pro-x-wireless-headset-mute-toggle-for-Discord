## v1.0.1: prevent volume changes from toggling Discord mute

Fixed a false trigger where a keyboard volume wheel or Windows volume slider caused
the headset to emit the same empty USB report as its mute button. The bridge now
subscribes to Windows playback-volume notifications and suppresses those reports,
including notifications that arrive just after the headset report. The listener is
event-driven and adds no periodic idle polling.

Calibration now checks headset and keyboard/Windows volume controls for 12 seconds.
Existing calibration, shortcut selection, and startup preferences are retained.
The app waits for the volume guard when a playback device is unavailable and refreshes
subscriptions when audio endpoints change. Six regression checks were added.

Download **HeadsetButtonBridge-Setup-1.0.1.exe** and install over the existing version.

## Features

Turn the Logitech PRO X Wireless mute button into a configurable keyboard shortcut.

- Small per-user Windows installer; no administrator rights or separate runtime download.
- Four-step calibration: receiver detection, three button samples, volume-wheel check, and shortcut selection.
- F13-F24, F1-F12, Pause, and Scroll Lock, with optional Ctrl/Alt/Shift modifiers.
- Start at Windows sign-in by default after setup, with a visible opt-out.
- Event-driven HID listener, one-shot confirmation timers, tray pause/exit, and USB reconnect handling.
- No microphone capture, network requests, telemetry, or automatic updater.

Supported: Windows 10/11 x64 with .NET Framework 4.8, original Logitech PRO X Wireless USB receiver **046D:0ABA**. Other headset models are not supported by this release.

On first install, finish calibration. In Discord, bind the chosen shortcut to **Toggle Mute**. The portable executable also needs the `.exe.config` file beside it.

Known limits: this is a toggle, not physical mute-state synchronization. Allow about one second after volume changes before pressing mute. The event filter is based on this model's generic empty consumer-control report; calibration reduces accidental triggers but cannot prove that every firmware behavior is distinct. The installer is unsigned. SHA-256 checksums are included.

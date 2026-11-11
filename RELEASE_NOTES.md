Turn the Logitech PRO X Wireless mute button into a configurable keyboard shortcut.

- Small per-user Windows installer; no administrator rights or separate runtime download.
- Four-step calibration: receiver detection, three button samples, volume-wheel check, and shortcut selection.
- F13-F24, F1-F12, Pause, and Scroll Lock, with optional Ctrl/Alt/Shift modifiers.
- Start at Windows sign-in by default after setup, with a visible opt-out.
- Event-driven HID listener, one-shot confirmation timers, tray pause/exit, and USB reconnect handling.
- No microphone capture, network requests, telemetry, or automatic updater.

Supported: Windows 10/11 x64 with .NET Framework 4.8, original Logitech PRO X Wireless USB receiver **046D:0ABA**. Other headset models are not supported by this release.

Download **HeadsetButtonBridge-Setup-1.0.0.exe**, install, and finish calibration. In Discord, bind the chosen shortcut to **Toggle Mute**. The portable executable also needs the `.exe.config` file beside it.

Known limits: this is a toggle, not physical mute-state synchronization. Allow about one second after volume changes before pressing mute. The event filter is based on this model's generic empty consumer-control report; calibration reduces accidental triggers but cannot prove that every firmware behavior is distinct. The installer is unsigned. SHA-256 checksums are included.

# Headset Button Bridge

Make the **Logitech PRO X Wireless mute button** toggle Discord mute, deafen, or another app shortcut—even if you use a separate microphone.

[Download the Windows installer](https://github.com/Barquito0/logitechg-pro-x-wireless-headset-mute-toggle-for-Discord/releases/latest/download/HeadsetButtonBridge-Setup-1.0.0.exe) · [Releases](https://github.com/Barquito0/logitechg-pro-x-wireless-headset-mute-toggle-for-Discord/releases) · [Report an issue](https://github.com/Barquito0/logitechg-pro-x-wireless-headset-mute-toggle-for-Discord/issues)

## Install

1. Download and run **HeadsetButtonBridge-Setup-1.0.0.exe**. It installs for your Windows account without administrator rights.
2. Connect the receiver and turn on the headset.
3. Follow the four-step wizard: detect the headset, record three mute presses, check the volume wheel and verify mute, then choose a shortcut.
4. Keep **Start quietly when I sign in to Windows** checked to run automatically after a restart and sign-in.
5. In Discord's desktop app, open **Settings → System → Custom Keybinds → Add a Keybind**, choose **Toggle Mute**, click **Record Keybind**, and press the headset button. Stop recording and close settings before testing. Older Discord versions put Keybinds directly under Settings.

**F24 is the default and recommended shortcut.** Windows supports it even if your keyboard has no F24 key. The wizard also offers F1–F23, Pause, Scroll Lock, and optional Ctrl/Alt/Shift modifiers. To record a shortcut in another app, keep that app in the foreground. Setup includes a three-second delayed shortcut sender if needed.

The tray icon provides **Pause**, **Calibrate / change shortcut**, **Start with Windows**, and **Exit**. Double-click the icon to open setup. Only one copy runs per Windows session.

## Supported hardware

- Original **Logitech PRO X Wireless** USB receiver, **VID 046D / PID 0ABA**.
- Windows 10/11 **x64**, with **.NET Framework 4.8** already installed.
- USB control messages must be readable by Windows, and calibration must pass.

This release does not claim support for PRO X 2, wired PRO X, Bluetooth headsets, or every Logitech headset. Unsupported receivers are not opened. If calibration cannot distinguish the button from other controls, the app refuses to activate that mapping.

## How it works

```text
Headset mute button → USB control report → calibrated filter → keyboard shortcut → Discord
```

The app listens to one headset HID control interface using a blocking Windows read. It does not capture microphone audio. The prototype headset emits an empty consumer-control report on mute presses; volume actions send a different report followed by a similar empty release. Calibration learns the report and checks the volume filter before enabling the mapping.

Idle operation has **no repeating polling timer**. A one-shot timer runs briefly when a possible press arrives. Receiver connection changes and resume events trigger reconnection. Input queues, the calibration display, and error logs are bounded. There is no background web service, bundled browser, telemetry, network connection, update checker, or extra driver.

All running programs use some memory and processing time. This app aims for negligible idle CPU use; resource measurements and their limits are in [validation notes](docs/validation.md).

## Limits to understand

- **This toggles the target app; it does not synchronize physical mute state.** The headset button still mutes its own microphone. When using a separate mic, Discord's mute icon tells you whether Discord is transmitting it. Clicking mute in Discord separately can change the relationship between the two states.
- **Wait about one second after adjusting volume before pressing mute.** Other control activity suppresses matching reports for 750 ms. A candidate is confirmed after about 150 ms; very rapid repeated presses are ignored.
- The empty report is not a unique mute-state bit. Calibration and the volume check reduce false triggers, but unusual firmware, power, or radio behavior could emit the same report. This is an experimental convenience mapping, not a privacy guarantee.
- Shortcut keys can reach the foreground app and any other app bound to the same shortcut. F24 helps avoid conflicts. Hold-to-talk actions are not supported by a brief synthetic press.
- If Ctrl, Alt, or Shift is already held down, the shortcut is skipped rather than sending an unexpected chord or releasing your held keys.
- Apps running as administrator may reject input from a normally running bridge. Use matching privilege levels.
- The installer is **unsigned**. Windows may show an unknown-publisher or reputation warning. Release assets include SHA-256 checksums for verification.

## Startup, settings, and removal

The installer uses `%LOCALAPPDATA%\Programs\Headset Button Bridge`. Calibration and bounded error logs live in `%LOCALAPPDATA%\HeadsetButtonBridge`. The app does not need your project folder after installation.

Startup is a per-user entry named `HeadsetButtonBridge` in `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. It launches with `--background` after sign-in. A user-session app can deliver shortcuts to Discord; a traditional Windows service is isolated from the interactive desktop.

Turn startup off from the tray menu. Uninstall through **Windows Settings → Apps → Installed apps → Headset Button Bridge**. Uninstall stops the app and removes its startup entry and program files. Calibration and local error logs are retained so reinstalling can reuse them; delete the app's own local-data folder if you also want those removed.

Command-line options: `--setup` opens calibration/settings; `--background` starts quietly when already calibrated; `--stop` exits the running copy; `--uninstall` also removes the startup entry.

## Build and test

From a Windows PowerShell window in this repository:

```powershell
.\Build.ps1 -Test
.\Build.ps1 -Test -Installer
```

The app uses Windows' .NET Framework C# compiler and built-in libraries; there are no NuGet dependencies and no separate .NET SDK requirement. Building the installer also requires **Inno Setup 6**. Outputs go to `dist/`.

Tests cover captured hardware reports, button confirmation, volume release filtering, duplicate suppression, startup warmup, pause cancellation, calibration consistency, shortcut selection, and invalid settings. GitHub Actions builds and tests on Windows; version tags produce release assets. See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

[MIT](LICENSE). This is an independent project, not affiliated with Logitech or Discord.

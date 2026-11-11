# Contributing

Build and test with `powershell -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1 -Test`.
Add `-Installer` when Inno Setup 6 is available. The app uses the built-in .NET
Framework compiler and has no package dependencies.

For a headset compatibility report, include the headset model, Windows version,
USB VID/PID, and control reports copied from the calibration window. Do not upload
microphone recordings, device serial numbers, your complete device inventory, or
your full local application-data directory.

Before adding another model, capture repeated mute presses and negative examples
(volume, power, reconnect, and other buttons). Add behavioral tests and confirm that
the proposed pattern is distinguishable. Never claim a generic empty report is a
unique hardware mute state. Keep runtime work event-driven and buffers bounded.

Changes to startup or uninstall behavior should be verified on a test installation.
Update the version in `src/Program.cs`, `packaging/installer.iss`, and the workflow's
artifact filenames together before creating a new version tag.

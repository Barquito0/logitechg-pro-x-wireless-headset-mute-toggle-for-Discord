# Validation for v1.0.0

Tested November 11, 2026 on one Windows x64 PC with the original Logitech PRO X
Wireless USB receiver (`046D:0ABA`) and a separate Yeti selected in Discord.

## Automated checks

`Build.ps1 -Test -Installer` compiles the app, runs 22 behavioral checks, and builds
the Inno Setup installer. Checks include the captured real hardware report sequence
(two mute presses followed by volume-up/down activity), confirmation delay, duplicate
suppression, startup warmup, cancellation, ambiguous calibration rejection, invalid
configuration rejection, and shortcut labels.

## Local installation and hardware checks

- Installed into an isolated test directory and verified the app and config deployed.
- Ran the uninstaller and verified the installed app executable was removed.
- Installed to the normal per-user application directory, with no administrator prompt.
- Completed the actual four-step wizard using the physical headset: three matching
  button samples, a volume-wheel check, and a final button verification.
- Confirmed the persisted calibration, F24 selection, and Windows startup entry.
- User confirmed two physical presses toggled Discord and volume adjustment did not.
- Restarted the helper using the saved `--background` startup command and verified a
  second launch did not create another process.
- The `--stop` command exited the running app before replacing it with the final build.

## Idle resource measurement

Measured a fresh background launch after calibration, after a short startup settling
period, with no button presses during the sample. PowerShell read Windows process
counters before and after a 20.01-second interval:

| Counter | Observed |
| --- | ---: |
| Process CPU time added | 0.0 ms (at the counter's resolution) |
| Working set | 38.5 MiB |
| Private committed memory | 23.4 MiB |
| Handles | 312 |
| Threads | 9 |
| App executable | 47,104 bytes |
| Installer | approximately 2.0 MiB |

This is a short observation on one PC, not a promise of zero CPU use or a universal
memory bound. Calibration loads more UI code and temporarily uses more memory.
The runtime blocks in a Windows HID read when idle; the short confirmation and
reconnect timers only run in response to an event. No repeating idle poll is used.

## Limits of this validation

No full Windows reboot was performed during testing; the exact background startup
command and registry entry were verified. USB reconnect and sleep-resume handlers
are implemented but have not been exercised across different controllers or Windows
versions. Only the original receiver and this PC were tested. Other firmware behavior
can still produce an indistinguishable empty report. The binaries are unsigned.

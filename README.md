# DesktopIdle

DesktopIdle is a lightweight executable that automatically clears your desktop after you have been idle for a set duration of time (configurable).
This executable emulates the native behaviour of Window's Show Desktop feature, but with additional features to act as a pseudo-screen saver. 

## What it does

DesktopIdle can:

- activates after a configurable time, in seconds.
- minimise open windows and show the desktop
- temporarily enable taskbar auto-hide while minimised windows are active
- move the mouse away from the taskbar before ambient mode starts
- hide the cursor while ambient mode is active
- ignore tiny accidental mouse movement, such as a light table jog (100px screen threshold)
- restore your windows when you return
- let you exclude specific apps from triggering ambient mode
- let you exclude specific app windows from being minimised
- optionally start with Windows

## Requirements

DesktopIdle requires:

- Windows 10 or Windows 11
- .NET 8 (or above) Desktop Runtime for Windows x64

## Installation

1. Download `DesktopIdle.exe` from the latest release.
2. Run `DesktopIdle.exe`.
3. Open the tray icon and choose **Settings...**.
4. Set your preferred idle time.
5. Add / remove app exclusions from the list. Some basic game exclusions have been set up by default.
6. Add / remove window minimisation exclusions from the list. These are apps that will not be minimised. Some generic exclusions have been set up by default.
7. Use **Enable start with Windows** if you want it to run automatically when you log in.

## Licence

DesktopIdle is released under the MIT License.

You are free to use, modify, distribute, and include this project in other software, provided that the original copyright and licence notice are included.

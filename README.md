# NVIDIA Color Profiles

A WPF desktop app to manage color profiles for NVIDIA GPUs. Create, save, and switch between color settings (brightness, contrast, gamma, digital vibrance, color temperature) with hotkeys and system tray support.

![Screenshot](NvidiaColorProfileManager/screenshots/NVIDIAColorProfiles_01.png?v=2)

## Disclaimer

This project is not affiliated with, endorsed by, or connected to NVIDIA Corporation in any way. "NVIDIA" is a registered trademark of NVIDIA Corporation.

## Why?

Ideal when NVIDIA Freestyle filters aren't available (unsupported games, driver issues) or when you want per-game color adjustments without installing ReShade. Since it talks directly to the GPU driver via NVAPI, changes apply globally and work in any application.

## Features

- Create and manage multiple color profiles
- Apply profiles per display or all monitors
- Live preview while editing settings
- Keyboard shortcuts to switch profiles instantly
- System tray with quick profile switching
- Start with Windows option
- NVAPI + Windows GDI fallback for gamma control

## Download

Get the latest version from [Releases](https://github.com/ProfeLvL99/NvidiaColorProfiles/releases).

Download `NVIDIA-Color-Profiles.zip`, extract it, and run `NVIDIA Color Profiles.exe`.

## Requirements

- Windows 10/11
- NVIDIA GPU with latest drivers
- .NET 9 Desktop Runtime (included in the download)

## Build from source

```bash
git clone https://github.com/ProfeLvL99/NvidiaColorProfiles.git
cd NvidiaColorProfiles
dotnet run --project NvidiaColorProfileManager
```

## Support

If you find this useful, consider [buying me a coffee](https://ko-fi.com/profelvl99).

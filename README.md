# NVIDIA Color Profiles

A WPF desktop app to manage color profiles for NVIDIA GPUs. Create, save, and switch between color settings (brightness, contrast, gamma, digital vibrance, color temperature) with hotkeys and system tray support.

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

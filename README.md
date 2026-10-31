<div align="center">

# 🐱 Oneko for Windows

**A tiny pixel cat that chases your mouse around the screen.**
Click-through, customizable, and she pixelates when she walks over a terminal.

[**⬇ Download**](../../releases/latest) · [Website](https://lav1n.is-a.dev) · [More creations ↗](https://lav1n.is-a.dev)

</div>

---

## ✨ Features

- **The real Oneko:** 8-direction running, the "!" alert pose, scratching, yawning and napping with Zs.
- **Click-through:** she never blocks a button or link. Click straight through her.
- **Customizable:** cat size, opacity, run speed, animation speed, follow distance, sleepiness, sprinting, and 7 cat colors.
- **Terminal Version:** when she walks over cmd, PowerShell, Windows Terminal, Git Bash or WSL, she turns into chunky pixels (with an optional green phosphor tint).
- **Beautiful settings window:** a modern, animated UI with tabs, sliders and a live cat preview.
- **Tiny and portable:** one small `.exe`. No installer, no admin rights.

## 📥 Install

1. Download **`Oneko.exe`** from the [latest release](../../releases/latest).
2. Double-click it. If SmartScreen warns about an unknown publisher, choose **More info → Run anyway** (the app isn't code-signed).
3. Find the cat icon in the system tray (near the clock) to open settings, pause her, or quit.

Requires Windows 10 or 11 (uses the .NET Framework 4 that is already built in).

## 🎛️ Settings

| Tab | What you can change |
|---|---|
| **Cat** | Size, opacity, animation speed, cat color |
| **Behavior** | Run speed, follow distance, sleepiness, sprint, wall scratching |
| **Terminal** | Terminal Version on/off, pixelation strength, green tint |
| **General** | Keep on top, start with Windows, open settings on launch |

Settings save automatically to `%APPDATA%\Oneko\settings3.ini`. Closing the window keeps the cat running in the tray, and launching Oneko again brings the window back.

## 🛠️ Build from source

No SDK needed. Run `build.bat` (it uses the C# compiler that ships with Windows):

```bat
build.bat
```

The source is a single file: [`src/Oneko.cs`](src/Oneko.cs). The sprite sheet is embedded into the exe.

## 🌐 Website

The landing page lives in [`docs/`](docs/). To host it, enable **GitHub Pages → Deploy from a branch → `/docs`** in the repo settings.

## 🙏 Credits

Made by **[Lav1nRules](https://lav1n.is-a.dev)**. The cat and its sprites come from the classic *Oneko* desktop-cat idea and sprite set. Credit to its original creators.

## 📄 License

Code is released under the [MIT License](LICENSE).

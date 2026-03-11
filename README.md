# Coop Launcher

A modern, high-performance game launcher for Windows, designed specifically to leverage Steam's **Remote Play Together** benefits for non-Steam games and emulators.

![Coop Launcher Interface](https://github.com/MBaliver/cooplauncher/blob/main/coop.png?raw=true)

## 🚀 Features

-   **Automatic Steam Discovery**: Scans your entire Steam library instantly.
-   **High-Quality Grid Banners**: Fetches beautiful landscape covers from Steam and SteamGridDB.
-   **Remote Play Together Magic**: Easily "replaces" a donor game (like RetroArch) to gain full Steam overlay and Remote Play Together support for any app.
-   **Manual Executable Overrides**: Point any game to a custom EXE or mod loader via a simple right-click menu.
-   **Self-Contained & Portable**: No complex installation required.

## 🛠️ How to Use

1.  **Select a Donor Game**: Open Coop Launcher and click the **⚙ Donor** button. Pick a game in your Steam library that you are willing to use as a "host" (e.g., RetroArch is a common choice).
2.  **Install**: Click "Install Launcher Here". This will copy Coop Launcher into that game's folder.
3.  **Launch from Steam**: Close the standalone launcher and **launch your chosen donor game from Steam**.
4.  **Play**: Coop Launcher will open. Select any game from your list, and Steam will treat it as if you are playing the donor game, providing full Remote Play Together invites!

## ⚙️ Manual Selection

If a game's executable isn't found automatically:
-   **Right-click** any game in the list.
-   Select **"Change Executable..."** to manually browse for the file.
-   Your choice is saved globally across all instances.

## 🏗️ Building from Source

Requirements:
-   .NET 8.0 SDK

```bash
# Clone and build
dotnet build -c Release

# Publish as a single-file executable
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## ⚖️ License

Distributed under the MIT License. See `LICENSE` for more information.

---
*Created for the community of local multiplayer enthusiasts.*



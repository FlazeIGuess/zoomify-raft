# Zoomify

A smooth, customizable zoom mod for Raft. Hold a key to zoom in, scroll while zooming to adjust the zoom level on the fly, and enjoy a buttery-smooth transition between zoom states — just like OptiFine for Minecraft.

---

## Features

- Hold a configurable key to zoom in
- Scroll while zooming to adjust the zoom level (from 1x up to 50x)
- Smooth zoom in/out transitions with configurable speed
- Zoom indicator displayed above the hotbar while zooming
- Relative zoom: works correctly regardless of your in-game FOV setting
- Mouse sensitivity scales automatically with zoom level
- Hotbar scroll is blocked while zooming so you don't accidentally switch items
- Fully configurable via [ExtraSettingsAPI](https://www.raftmodding.com/mods/extrasettingsapi) (optional)

---

## Installation

1. Install [RaftModLoader](https://www.raftmodding.com/loader)
2. *(Optional but recommended)* Install [ExtraSettingsAPI](https://www.raftmodding.com/mods/extrasettingsapi) for in-game settings
3. Download `Zoomify.rmod` from the [Releases](https://github.com/FlazeIGuess/zoomify-raft/releases) page
4. Place `Zoomify.rmod` in your `%appdata%/RaftModLoader/mods/` folder
5. Launch Raft — Zoomify loads automatically

---

## Configuration

All settings are available in the in-game mod settings menu (requires ExtraSettingsAPI).

| Setting | Default | Description |
|---|---|---|
| **Zoom Key** | `C` | Key to hold for zooming |
| **Starting Zoom (x)** | `5` | Zoom factor applied when you first press the zoom key |
| **Scroll Step (x)** | `1` | How much each scroll tick adjusts the zoom factor |
| **Smooth Zoom** | On | Enables smooth lerp transitions between zoom levels |
| **Transition Speed** | `10` | Speed of the smooth transition (1 = very slow, 30 = instant) |

The scroll step automatically scales for high zoom levels: above 10x the step is doubled, above 20x it is quintupled, so you can reach high magnifications without excessive scrolling.

---

## Building from Source

Requirements: Visual Studio or MSBuild, .NET Framework 3.5

```bash
git clone https://github.com/FlazeIGuess/zoomify-raft.git
cd zoomify-raft
.\build.bat
```

The build script compiles the project and packages `Zoomify.rmod` automatically.

---

## Contributing

Pull requests and bug reports are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md) if one exists, or open an issue on GitHub.

---

## License

See [LICENSE](../LICENSE) or the repository root for license information.

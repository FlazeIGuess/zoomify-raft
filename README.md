# Zoomify Mod for Raft

A smooth, configurable zoom mod for Raft. Hold a key to zoom in, scroll to adjust the magnification on the fly, and enjoy buttery-smooth transitions - just like OptiFine zoom for Minecraft.

## Features

- Hold a configurable key to zoom in
- Scroll while zooming to adjust the zoom level (1x up to 50x)
- Smooth zoom transitions with configurable speed
- Zoom indicator above the hotbar showing the current magnification
- Relative zoom - works correctly at any in-game FOV setting
- Mouse sensitivity scales automatically with zoom level
- Hotbar scroll blocked while zooming (no accidental item switches)
- Configurable via in-game settings (requires Extra Settings API)

## Installation

1. Install [RaftModLoader](https://www.raftmodding.com/loader)
2. Download the latest `Zoomify.rmod` file from the [releases page](https://github.com/FlazeIGuess/zoomify-raft/releases)
3. Place the `.rmod` file in your RaftModLoader mods folder
4. Launch Raft through RaftModLoader

## Configuration (Optional)

For in-game configuration, install the [Extra Settings API](https://www.raftmodding.com/mods/extrasettingsapi) mod. This allows you to customize:

### Zoom Settings
- Zoom key (default: C)
- Starting zoom factor (1x-50x, default: 5x)
- Scroll step per tick (0.5x-5.0x, default: 1x)

### Smoothing Settings
- Enable/disable smooth zoom transitions
- Transition speed (1 = very slow, 30 = nearly instant, default: 10)

Access settings via: Main Menu > Settings > Mods tab

The scroll step automatically scales at high zoom levels: above 10x the step is doubled, above 20x it is quintupled.

The mod works perfectly fine without Extra Settings API using default values.

## Building from Source

### Prerequisites

- Visual Studio 2019 or later
- .NET Framework 3.5
- Raft game installed
- RaftModLoader installed

### Build Steps

1. Clone the repository:
   ```bash
   git clone https://github.com/FlazeIGuess/zoomify-raft.git
   cd zoomify-raft
   ```

2. Update the reference paths in `Zoomify/Zoomify.csproj` to match your Raft installation directory

3. Build the solution:
   ```bash
   .\build.bat
   ```

   Or open `Zoomify.sln` in Visual Studio and build from there

## Contributing

Contributions are welcome! Please read [CONTRIBUTING.md](CONTRIBUTING.md) for details on our code of conduct and the process for submitting pull requests.

## License

This project is licensed under the GNU Affero General Public License v3.0 - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Built with [RaftModLoader](https://www.raftmodding.com/)
- Uses [Harmony](https://github.com/pardeike/Harmony) for runtime patching
- Created by Flaze

## Support

- Report bugs on the [Issues page](https://github.com/FlazeIGuess/zoomify-raft/issues)
- Join the [Raft Modding Discord](https://www.raftmodding.com/discord)

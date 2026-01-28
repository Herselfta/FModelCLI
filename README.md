# FModelCLI

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-GPL--3.0-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%20x64-lightgrey)](https://github.com/Herselfta/FModelCLI)

A lightweight command-line wrapper for [FModel](https://github.com/4sval/FModel)'s core extraction capabilities, designed for automated asset extraction from Unreal Engine games.

## 🎯 Features

- **🚀 Single-file executable** - No .NET runtime installation required
- **⚡ Fast extraction** - Direct access to CUE4Parse core without GUI overhead
- **🔐 AES decryption** - Support for encrypted PAK files with multiple keys
- **🎮 Multi-game support** - Works with Wuthering Waves and other UE4/UE5 games
- **🔍 Smart filtering** - Extract only what you need with keyword filters
- **📦 Self-contained** - All dependencies bundled in one executable

## 🧠 Why FModelCLI?

While `CUE4Parse` is the powerful low-level library that does the heavy lifting, and `FModel` is the industry-standard GUI, **FModelCLI** exists to bridge the gap between "power user" automation and "game-specific" compatibility.

### CUE4Parse vs. FModel Kernel
- **CUE4Parse**: A raw, high-performance C# library for parsing Unreal assets. It is generic and strictly follows UE standards.
- **FModel**: Built on `CUE4Parse`, but adds a "Compatibility Layer". It contains specific patches, custom enums (like `EGame.GAME_WutheringWaves`), and serialization fixes for games that deviate from standard Unreal behavior.

### The Rationale
1. **Headless Automation**: GUI tools are impossible to integrate into automated toolchains (like **Ludiglot**). FModelCLI provides the robust FModel "kernel" in a format that scripts can use.
2. **The "GUID Gap" Solution**: Many modern games provide AES keys via community tables without the corresponding Internal GUIDs. Raw CUE4Parse implementations often fail to mount these packages because they require a 1:1 GUID-to-Key mapping. FModelCLI implements a **Brute-Force Mounting** logic that attempts every provided key against every encrypted package, ensuring a 100% mount rate for games like *Wuthering Waves*.
3. **Upstream Synergies**: By using FModel as the upstream instead of raw CUE4Parse, this tool inherits community-driven fixes for new game versions (like UE5 IO Store / Zen Loader support) the moment they are added to FModel.

## 📋 Prerequisites

- **Windows x64** (for pre-built binaries)
- **Git** (for building from source)
- **.NET 8.0 SDK** (only for building from source)

## 🚀 Quick Start

### Using Pre-built Binary

1. Download `FModelCLI.exe` from [Releases](https://github.com/Herselfta/FModelCLI/releases)
2. Run the extraction:

```powershell
FModelCLI.exe "C:\Game\Paks" "0xYourAESKey" "C:\Output" "ConfigDB"
```

### Building from Source

```powershell
# Clone the repository with submodules
git clone --recursive https://github.com/Herselfta/FModelCLI.git
cd FModelCLI

# Run the build script
.\sync_upstream.ps1

# The executable will be in dist/FModelCLI.exe
```

## 📖 Usage

```
FModelCLI.exe <GameDir> <AESKey> <OutputDir> [Filter]
FModelCLI.exe <GameDir> <AESKey> --list [Filter]
```

### Parameters

| Parameter | Required | Description | Example |
|-----------|----------|-------------|---------|
| `GameDir` | ✅ | Path to game's root directory or PAK directory | `E:\WutheringWaves\WutheringWaves Game` |
| `AESKey`  | ✅ | AES decryption key(s), semicolon separated | `0x1234...;0xABCD...` |
| `OutputDir`| ✅* | Where to save extracted files (N/A in list mode) | `E:\Extracted` |
| `--list`  | ❌ | Toggle **list mode**: print file paths without extracting | `--list` |
| `Filter`  | ❌ | Optional keyword to filter files (case-insensitive) | `ConfigDB`, `Audio`, `zh` |

*\* OutputDir is required for extraction but should be replaced by `--list` for list mode.*

### Examples

**Extract all ConfigDB files:**
```powershell
FModelCLI.exe "E:\Game\Paks" "0xABCD1234..." "E:\Output" "ConfigDB"
```

**Extract TextMap for localization:**
```powershell
FModelCLI.exe "E:\Game\Paks" "0xABCD1234..." "E:\Output" "TextMap"
```

**Extract everything (no filter):**
```powershell
FModelCLI.exe "E:\Game\Paks" "0xABCD1234..." "E:\Output"
```

**List all files in Chinese language pack:**
```powershell
FModelCLI.exe "E:\Game" "0xKEY1;0xKEY2" --list "/zh/"
```

**Debug all audio files:**
```powershell
FModelCLI.exe "E:\Game" "0xKEY1;0xKEY2" --list "WwiseAudio_Generated"
```

**Multiple AES keys:**
```powershell
FModelCLI.exe "E:\Game" "0xKEY1;0xKEY2;0xKEY3" "E:\Output" "Audio"
```

## 🏗️ Project Structure

```
FModelCLI/
├── FModelCLI/              # Main CLI project
│   ├── Program.cs          # Entry point and extraction logic
│   └── FModelCLI.csproj    # Project configuration
├── upstream/               # Git submodule
│   └── FModel/             # FModel source (includes CUE4Parse)
├── dist/                   # Build output (gitignored)
│   └── FModelCLI.exe       # Final executable
├── sync_upstream.ps1       # Automated build script
├── .gitignore              # Git ignore rules
├── LICENSE                 # GPL-3.0 License
└── README.md               # This file
```

## 🔧 Development

### Build Script Workflow

The `sync_upstream.ps1` script automates the entire build process:

1. **Clone/Update upstream** - Fetches latest FModel source
2. **Initialize submodules** - Ensures CUE4Parse is available
3. **Restore dependencies** - Downloads NuGet packages
4. **Publish release** - Creates self-contained single-file executable

### Modifying for Other Games

To support a different game, edit `Program.cs` line 59:

```csharp
// Change the game enum
var version = new VersionContainer(EGame.GAME_YourGame);
```

Available games are defined in `CUE4Parse.UE4.Versions.EGame`.

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

### Development Setup

1. Fork the repository
2. Clone with submodules: `git clone --recursive https://github.com/Herselfta/FModelCLI.git`
3. Make your changes in `FModelCLI/Program.cs`
4. Test with `.\sync_upstream.ps1`
5. Submit a PR

## 📝 License

This project is licensed under the **GNU General Public License v3.0** - see the [LICENSE](LICENSE) file for details. And licenses of third-party libraries used are listed [here](https://github.com/Herselfta/FModelCLI/blob/main/NOTICE).

### Why GPL-3.0?

FModelCLI uses [FModel](https://github.com/4sval/FModel) as a dependency, which is licensed under GPL-3.0. Due to the copyleft nature of GPL-3.0, any derivative work (including this CLI wrapper) **must** also be distributed under GPL-3.0.

**What this means for you:**
- ✅ You can freely use, modify, and distribute this software
- ✅ You can use it for commercial purposes
- ⚠️ If you distribute modified versions, you **must** also release them under GPL-3.0
- ⚠️ You **must** provide source code to anyone you distribute binaries to
- ⚠️ You **cannot** incorporate this code into proprietary software

### Dependency Licenses

- **FModel**: GPL-3.0 (https://github.com/4sval/FModel)
- **CUE4Parse**: Apache-2.0 (https://github.com/FabianFG/CUE4Parse)
- **Newtonsoft.Json**: MIT (https://github.com/JamesNK/Newtonsoft.Json)

## 🙏 Acknowledgments

- [FModel](https://github.com/4sval/FModel) - The original GUI application
- [CUE4Parse](https://github.com/FabianFG/CUE4Parse) - Core UE4/UE5 parsing library
- All contributors to the FModel ecosystem

## ⚠️ Disclaimer

This tool is for educational and research purposes only. Please respect game developers' intellectual property and terms of service. Only use this tool on games you own and for personal, non-commercial purposes.

## 📞 Support

- **Issues**: [GitHub Issues](https://github.com/Herselfta/FModelCLI/issues)
- **Discussions**: [GitHub Discussions](https://github.com/Herselfta/FModelCLI/discussions)

---

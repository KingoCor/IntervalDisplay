# Installation

1. Install [BepInEx](https://github.com/BepInEx/BepInEx/releases) 5.x.x.
2. Download the mod from the [releases](https://github.com/KingoCor/IntervalDisplay/releases) page and place the `.dll` file into the `BepInEx/plugins` folder.

**Optional:** Download [ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager) to configure the mod in-game, or edit the configuration file manually (`.cfg`).

# Building

1. Place `Assembly-CSharp.dll`, `Unity.UI.dll`, `Unity.TextMeshPro.dll` and `Mirror.dll` in the root folder of the project.
2. Initialize the project:
 ```bash
 dotnet restore
 ```
3. Build:
```bash
 dotnet build
 ```

The mod was originally created and hosted by the BepInEx team https://github.com/BepInEx/BepInEx.ConfigurationManager

It is distributed under the [GPLv3](https://www.gnu.org/licenses/gpl-3.0.html) and this version maintains that license.

That fork have additions from other similar mods
* Coloring and localization are based on aedenthorn's https://github.com/aedenthorn/BepInEx.ConfigurationManager
* Color drawer is based on Azumatt's https://github.com/AzumattDev/BepInEx.ConfigurationManager (dev branch)
* Everything taken was improved and refined

## How to use
Press hotkey button in game (default `F1`) to open mod window and change configuration of mods.

## Features improved
* Localization - every button and label can be localized
* Elements recolored - you can set color of background, widget and enabled toggle
* Fonts colored - set distinct colors for default and changed values
* Color drawer extended further
* Window is draggable, resizable and remembers its size and position
* Open and close window by hitting one hotkey. Close window with Escape.
* Dropdown menu style refined
* Lots of minor refinements and improvements
* Readonly entries (locked from server) could be colored, disabled or completely hidden
* Window can be scaled to better fit 4K screens
* default view is Split View where plugins and categories are showed as a tree in left column
* File Editor for configuration files
* Setting Edit Window for more detailed setting configuration

## Valheim specific
The game does not take input while the window is open (only player input by default).

The game will be paused (if it can be paused) while the window is open (disabled by default).

### Hidden settings
Create file `shudnal.ConfigurationManager.hiddensettings.json` and place next to plugin dll or in \BepInEx\config folder.

The file could contain array of strings with special format. `pluginGUID=Section name=Settings name` equal sign separated strings containing mod GUID, section name and config name. 

If such settings is found in settings list it will be hidden completely.

In given file entries 1 and 2 are here for format example. 3rd string will hide setting "Pause game" in section "Valheim" of that Configuration manager mod.
```
[
	"pluginGUID=Section name=Settings name",
	"authorname.exampleGUID=Section name=Settings name 2",
	"shudnal.ConfigurationManager=Valheim=Pause game"
]
```

To get mod GUID ingame you can enable "Debug mode" toggle in mods header. Now mod GUID will be presented in mod tooltip on hover.

Said file could also be placed on server to push that hidden settings to clients. Combined with preconfigured modpack configs you can prevent users from changing values of client-sided mods easily. Yet they can still edit files manually.

## Compatibility
The mod is incompatible with original configuration manager and will not be loaded in that case.

## Installation (manual)
extract ConfigurationManager.dll folder to your BepInEx\Plugins\ folder.

## Mirrors
[Nexus](https://www.nexusmods.com/valheim/mods/2746)

## Donation
[Buy Me a Coffee](https://buymeacoffee.com/shudnal)

## Discord
[Join server](https://discord.gg/e3UtQB8GFK)
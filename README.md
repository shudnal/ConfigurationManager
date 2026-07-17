The mod was originally created and hosted by the BepInEx team https://github.com/BepInEx/BepInEx.ConfigurationManager

It is distributed under the [GPLv3](https://www.gnu.org/licenses/gpl-3.0.html) and this version maintains that license.

That fork have additions from other similar mods
* Coloring and localization are based on aedenthorn's https://github.com/aedenthorn/BepInEx.ConfigurationManager
* Color drawer is based on Azumatt's https://github.com/AzumattDev/BepInEx.ConfigurationManager (dev branch)
* Everything taken was improved and refined in various places

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
* Dynamic `ReadOnly` and `Browsable` attributes are refreshed while the window remains open and rechecked immediately before a setting write
* Synchronization state buttons with hover details for Jotunn, ServerSync and Conditional Config Sync settings; `S` means server-controlled and `C` means client-controlled. Conditional settings use the normal font color while their mod default is active and blue after server policy changes the ownership. Authorized administrators can toggle CCS Conditional ownership directly.
* Optional compact config list layout applies uniform reduced vertical spacing to every setting row, is enabled by default and is switchable from the window header
* default view is Split View where plugins and categories are showed as a tree in left column
* File Editor for configuration files
* Setting Edit Window for more detailed setting configuration

## Valheim specific
The game does not take input while the window is open (only player input by default).

The game will be paused (if it can be paused) while the window is open (disabled by default).

### Configuration Manager settings and server policy
All settings belonging to Configuration Manager itself are registered with Conditional Config Sync as policy-controlled settings. They remain client-controlled by default, preserving each player's local UI, input, window, text, color, and file-editor preferences.

When Configuration Manager is also installed on the server, administrators can use the CCS synchronization policy to force individual settings or complete sections to server-controlled ownership. `Lock Configuration` is always server-controlled. Configuration Manager remains optional on remote peers (`ModRequired = false`), so clients without the mod can still connect.

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

Conditional Config Sync 1.0.2 or newer is required.

## Installation (manual)
Install Conditional Config Sync 1.0.2 or newer, then place ConfigurationManager.dll in your BepInEx\Plugins\ folder.

## Mirrors
[Nexus](https://www.nexusmods.com/valheim/mods/2746)

[Thunderstore](https://thunderstore.io/c/valheim/p/shudnal/ConfigurationManager/)

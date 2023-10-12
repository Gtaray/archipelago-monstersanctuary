# Monster Sanctuary Archipelago Client
This is a client mod for Monster Sanctuary that makes it compatible with the Archipelago Multiworld Randomizer.

This mod features randomization for all items and monsters through Archipelago.

NOTE: This mod is currently in its alpha stage. Features are likely to be broken and there will be bugs. The fastest way to report bugs is to message me (Saagael) directly on Discord, though anyone is welcome to add issues here on github. If you can, include a screenshot of the problem as well as any error messages in the console.

## Installation
- Must be using a PC version of Monster Sanctuary that is on the latest update
- [Download the latest version of this mod from this link.](https://github.com/Gtaray/archipelago-monstersanctuary/releases/latest/download/Monster.Sanctuary.Mod.zip)
- Extra and copy the zip file into the root folder where Monster Sanctuary is located. By default it should be something like:
`C:\Program Files (x86)\Steam\steamapps\common\Monster Sanctuary`
- Launch the game and close it. This will finalise the installation for BepInEx.
- Launch the game again and you should see a new bit of UI in the top-left of the screen showing `Archipelago v0.4.2 Status: Not Connected`, as well as text fields to enter connection info.
- To uninstall the mod, either remove/delete the `Archipelago.MonsterSanctuary` folder from the plugins folder, or rename the winhttp.dll file in the game's root directory (this will disable all mods from running)

## Generating a Multiworld
- In order to setup a multiworld you must first install the latest version of [Archipelago](https://github.com/ArchipelagoMW/Archipelago/releases/tag/0.4.3)
	- When running the Archipelago setup exe you'll want to at least install the Generator and Text Client
- After installing, run `Archipelago Launcher` and click on `Browse Files`. This will open the local file directory for your Archipelago installation.
- Download the [yaml template file](https://github.com/Gtaray/archipelago-monstersanctuary/releases/latest/download/MonsterSanctuary.yaml) Place `MonsterSanctuary.yaml` inside the `Players` folder
	- The yaml file needs to be edited before you can generate a game. Open the file and fill in your player name and choose the settings you want.
	- You will also need to get the .yaml files for everyone who is joining your multiworld and place them inside
- [Download the latest version of the .apworld file from this link.](https://github.com/Gtaray/archipelago-monstersanctuary/releases/latest/download/monster_sanctuary.apworld)
- Place the `monster_sanctuary.apworld` file inside of `lib/worlds`. 
- Once all of the .yaml files have been configured and placed into Players, click `Generate` in the Archipelago Launcher.
- A .zip file will be created in the `output` folder containing information for your multiworld. In order to hose your game, go to [https://archipelago.gg/uploads](https://archipelago.gg/uploads) and upload the .zip file, then click `Create New Room`.

## Connecting to a Room
Once you start the game with the client mod, you'll see connection info in the top right. Enter the link to your room, the room's password, and your player name. Then click Connect.

This connection info is saved and will re-populate when you run the game in the future.

## Randomizer Options
- `randomize_champions`: 
	- Randomization rules for champions
	- 0: Champions are not randomized and remain vanilla
	- 1: The list of champion encounters is the same, but it is shuffled around
	- 2: Champions are entirely randomized
- `monster_shift_rule`:
	- When do shifted monsters start appearing in the wild?
	- 0: Never
	- 1: After Sun Palace
	- 2: At any time
- `champions_in_wild`:
	- Can champion monsters be encountered in the wild?
	- 0: No
	- 1: Yes
- `evolutions_in_wild`:
	- Can evolved monsters be encountered in the wild?
	- 0: No
	- 1: Yes
- `monsters_always_drop_egg`:
	- Determines whether monster encounters will always drop an egg
	- 0: No, normal rules for egg drop chances apply
	- 1: Yes, all monsters in an encounter will always drop an egg
- `drop_chance_craftingmaterial`:
	- Controls the relative frequency that a random non-progression item is a crafting material
	- Range from 0 to 100
- `drop_chance_consumable`:
	- Controls the relative frequency that a random non-progression item is a consumable
	- Range from 0 to 100
- `drop_chance_food`:
	- Controls the relative frequency that a random non-progression item is a food item
	- Range from 0 to 100
- `drop_chance_catalyst`:
	- Controls the relative frequency that a random non-progression item is a catalyst
	- Range from 0 to 100
- `drop_chance_weapon`:
	- Controls the relative frequency that a random non-progression item is a weapon
	- Range from 0 to 100
- `drop_chance_accessory`:
	- Controls the relative frequency that a random non-progression item is an accessory
	- Range from 0 to 100
- `drop_chance_currency`:
	- Controls the relative frequency that a random non-progression item is gold
	- Range from 0 to 100
- `include_chaos_relics`:
	- Are chaos relics included in the random item pool?
	- 0: No
	- 1: Yes
- `exp_multiplier`:
	- Multiplier for experienced gained.
	- Range from 1 to 5
- `goal`:
	- Victory condition for the game
	- 0: Defeat Mard Lord
	- 1: Defeat all champions

### Note on Drop Chance Settings
The seven options to control relative drop frequency for different items works such that the higher the value is compared to the other drop item types, the more frequently it will occur.

For example, if the `drop_chance_weapon` value is twice the value of all other item types, then weapons show up, on average, twice as often as the everything else. If a drop chance is set to 0, it will never occur.

A more complicated example: If `drop_chance_catalyst` is set to 10, and `drop_chance_food` is set to 20, and everything else is set to 40, then catalysts will appear 1/4 as frequently as non-food items and half as frequently as food. While food will appear twice as frequently as catalysts and half as frequently as everything else.
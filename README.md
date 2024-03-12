# Monster Sanctuary Archipelago Client
This is a client mod for Monster Sanctuary that makes it compatible with the Archipelago Multiworld Randomizer.

This mod features randomization for all items and monsters through Archipelago.

NOTE: This mod is currently in its alpha stage. Features are likely to be broken and there will be bugs. The fastest way to report bugs is to message me (Saagael) directly on Discord, though anyone is welcome to add issues here on github. If you can, include a screenshot of the problem as well as any error messages in the console.

## Installation
- Must be using a PC version of Monster Sanctuary that is on the latest update
- [Download the latest version of this mod from this link.](https://github.com/Gtaray/archipelago-monstersanctuary/releases/latest/download/Monster_Sanctuary_Mod.zip)
- Extra and copy the zip file into the root folder where Monster Sanctuary is located. By default it should be something like:
`C:\Program Files (x86)\Steam\steamapps\common\Monster Sanctuary`
	- Windows likes to place an intermediary folder when extracting zip files. Make sure that the `BepInEx` folder, `doorstop_config.ini`, and `winhttp.dll` are all located directly in the game's install folder.
- Launch the game and close it. This will finalise the installation for BepInEx.
- Launch the game again and you should see a new bit of UI in the top-left of the screen showing `Archipelago v0.4.4 Status: Not Connected`, as well as text fields to enter connection info.
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
- `randomize_monsters`: 
	- Randomization rules for monsters
	- 0: Monsters are not randomized
	- 1: Monsters are entirely randomized, with every monster replaced with any other monster
	- 2: Monsters of the same specie are all randomized to another monster specie (this is how the built-in randomizer works)
 	- 3: Within a given encounter, all monsters of the same specie are randomized to another specie. Each encounter is randomized entirely separately.
- `improved_mobility_limit`
	- Restrict placement of monsters with improved mobility abilities (improved flying, improved swimming, dual mobility, and lofty mount)
 	- 0: Do not restrict placement of these monsters
  	- 1: Monsters with these abilities will not show up in the Mountain Path, Blue Caves, Stronghold Dungeon, Snowy Peaks, Sun Palace, or Ancient Woods.
- `remove_locked_doors`
	- Removes locked doors
 	- 0: Do not remove any locked doors
  	- 1: Remove extraneous locked doors such that each area only has one place where keys are used
  	- 2: Remove all locked doors
- `local_area_keys`
	- Controls where area keys can be placed
 	- 0: Area keys can show up anywhere in any world (following normal logic rules)
  	- 1: Area keys will only show up in the Monster Sanctuary player's world, and they will only appear in their own area
- `add_gift_eggs_to_pool`
	- Adds eggs you would normally receive from NPCs to the item pool so their location is randomized. Gift monsters are: Koi, Skorch, Shockhopper, and Bard
 	- 0: Gift monsters are received in their normal location (though the monster you receive may be different depending on monster randomization rules)
  	- 1: Gift monster eggs are added to the item pool and can appear anywhere (following normal logic rules)
- `monster_shift_rule`:
	- When do shifted monsters start appearing in the wild?
	- 0: Never
	- 1: After Sun Palace
	- 2: At any time
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
 - `skip_intro`:
	- Skip the cut scenes, dialog, and tutorial fight at the beginning of the game.
 	- 0: No
  	- 1: Yes
- `skip_plot`
	- Disable all flags and checks that require progressing the game's story. This allows entering areas before you would normally be able to
 	- 0: No
  	- 1: Yes
- `goal`:
	- Victory condition for the game
	- 0: Defeat Mad Lord
	- 1: Defeat all champions

### Note on Drop Chance Settings
The seven options to control relative drop frequency for different items works such that the higher the value is compared to the other drop item types, the more frequently it will occur.

For example, if the `drop_chance_weapon` value is twice the value of all other item types, then weapons show up, on average, twice as often as the everything else. If a drop chance is set to 0, it will never occur.

A more complicated example: If `drop_chance_catalyst` is set to 10, and `drop_chance_food` is set to 20, and everything else is set to 40, then catalysts will appear 1/4 as frequently as non-food items and half as frequently as food. While food will appear twice as frequently as catalysts and half as frequently as everything else.

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
- `starting_familiar`:
	- Select your starting familiar. If not set, your starting familiar will be randomly selected from the four spectral familiars
	- 0: Spectral Wolf
	- 1: Spectral Eagle
	- 2: Spectral Toad
	- 3: Spectral Lion
- `randomize_monsters`:
	- Randomization rules for monsters
	- 0: Monsters are not randomized
	- 1: Monsters are entirely randomized, with every monster replaced with any other monster
	- 2: Monsters of the same specie are all randomized to another monster specie (this is how the built-in randomizer works)
 	- 3: Within a given encounter, all monsters of the same specie are randomized to another specie. Each encounter is randomized entirely separately.
- `monster_shift_rule`:
	- When do shifted monsters start appearing in the wild?
	- 0: Never
	- 1: After Sun Palace
	- 2: At any time
- `improved_mobility_limit`:
	- Restrict placement of monsters with improved mobility abilities (improved flying, improved swimming, dual mobility, and lofty mount)
 	- 0: Do not restrict placement of these monsters
  	- 1: Monsters with these abilities will not show up in the Mountain Path, Blue Caves, Stronghold Dungeon, Snowy Peaks, Sun Palace, or Ancient Woods.
- `lock_explore_abilities`:
	- If enabled, explore abilities cannot be used until a corresponding item has been collected. The items required to use explore abilities depend on the selected option. Default is Off
	- 0: Off
	- 1: Type. Monsters are grouped into 16 difference categories based on monster type, and there are 16 unique items that unlock abilities for all monsters of that particular type.
	- 2: Ability. Each explore ability must be unlocked separately with its own item.
	- 3: Species: Each monster species will require a unique item to unlock its explore ability, with the exception of evolutions whose ability is the same as its base form.

- `eggsanity`:
	- Add locations for all 111 monsters that are checked when you hatch or evolve that monster.
	- 0: Off
	- 1: On
- `shopsanity`:
	- If enabled, all shop inventories are randomized
	- 0: Off
	- 1: On
- `shopsanity_prices`:
	- Only applies if Shopsanity is enabled. Randomizes the prices of shop items
	- 0: Normal. Shop prices remain unchanged
	- 1: Weighted. Prices are randomized, with the cost of progression items weighted towards the upper end of the range
	- 2: Any. Shop prices are entirely randomized
- `shops_ignore_rank`:
	- If enabled, all shops will offer their full inventory regardless of player rank
	- 0: Off
	- 1: On
- `monster_army`:
	- If enabled, then all monster army rewards are added to the location pool and the rewards are randomized. Since these checks are technically in sphere 0 (and would require a massive grind), the logic adds progressively higher keeper ranks as an artificial gate for item placement in these locations.
	- 0: Disabled
	- 1: Enabled

- `local_area_keys`:
	- Controls where area keys can be placed
 	- 0: Area keys can show up anywhere in any world (following normal logic rules)
  	- 1: Area keys will only show up in the Monster Sanctuary player's world, and they will only appear in their own area
- `remove_locked_doors`:
	- Removes locked doors
 	- 0: Do not remove any locked doors
  	- 1: Remove extraneous locked doors such that each area only has one place where keys are used
  	- 2: Remove all locked doors
- `add_gift_eggs_to_pool`:
	- Adds eggs you would normally receive from NPCs to the item pool so their location is randomized. Gift monsters are: Koi, Skorch, Shockhopper, and Bard
 	- 0: Gift monsters are received in their normal location (though the monster you receive may be different depending on monster randomization rules)
  	- 1: Gift monster eggs are added to the item pool and can appear anywhere (following normal logic rules)

- `starting_gold`:
	- Override the player's starting gold. The amount is counted in increments of 100. Default is 100g
	- Range from 0 to 1000
- `add_smoke_bombs`:
	- If enabled, the player will start with 50 smoke bombs
	- 0: Disabled
	- 1: Enabled
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
- `drop_chance_trap`:
	- Controls the relative frequency that a random non-progression item is a trap
	- Range from 0 to 100
- `replace_filler_with_level_badges`:
	- If enabled, replaces a percentage value of filler items with Level 42 Badges, meant to reduce the grind required to complete a game. The value is a percentage range from 0 (no filler items are replaced with badges) to 100 (all filler items are replaced with badges)
	- Range from 0 to 100
- `include_chaos_relics`:
	- Are chaos relics included in the random item pool?
	- 0: No
	- 1: Yes

- `skip_plot`:
	- Disable all flags and checks that require progressing the game's story. This allows entering areas before you would normally be able to
 	- 0: No
  	- 1: Yes
- `open_blue_caves`:
	- If enabled, the Blue Caves to Mountain Path shortcut will start opened
	- 0: Disabled
	- 1: Enabled
- `open_stronghold_dungeon`:
	- Opens shortcuts and entrances within and to the Stronghold Dungeon
	- 0: Disabled
	- 1: Entrances. Opens up entrances to the Dungeon from the Blue Caves and Ancient Woods
	- 2: Shortcuts. Opens up interior gates and shortcuts within the Dungeon
	- 3: Fully open. Opens both Entrances and Shortcuts
- `open_ancient_woods`:
	- If enabled, opens up alternate routes to access the Brutus and Goblin King fights. 
	- NOTE: These shortcuts allow you to bypass the need for Ancient Woods Keys. It is recommended to only use this setting if locked doors are turned off.
	- 0: Disabled
	- 1: Enabled
- `open_snowy_peaks`:
	- If enabled, opens up shortcuts within Snowy Peaks 
	- 0: Disabled
	- 1: Enabled
- `open_sun_palace`:
	- Opens shortcuts and entrances within and so Sun Palace
	- 0: Disabled
	- 1: Entrances. Opens the gate between Snowy Peaks and Sun Palace
	- 2: Raise Pillar. Raises the central pillar, lowers the water level, and opens the east and west shortcuts
	- 3: Fully open. Opens both Entrances and raises the pillar
- `open_horizon_beach`:
	- Opens shortcuts and entrances within and to Horizon Beach
	- 0: Disabled
	- 1: Entrances. Opens the elemental door locks between Ancient Woods and Horizon Beach, as well as the Magma Chamber to Horizon Beach shortcut
	- 2: Shortcuts. Opens the shortcut in central Horizon Beach
	- 3: Fully open. Opens both Entrances and Shortcuts
- `open_magma_chamber`:
	- Opens shortcuts and entrances within and to the Magma Chamber
	- 0: Disabled
	- 1: Entrances. Opens the rotating gates between Ancient Woods and Magma Chamber, and the breakable wall between Forgotten World and Magma Chamber
	- 2: Lower Lava. Removes the runestone shard from the item pool, lowers the lava, and opens all interior shortcut gates
	- 3: Fully open. Opens both Entrances and Lowers Lava
- `open_blob_burg`:
	- Opens up Blob Burg
	- 0: Disabled
	- 1: Entrances. Removes the blob key from the item pool and makes Blob Burg start opened up with no requirements
	- 2: Open Walls. Opens up all areas within Blob Burg, removing the need to incremementally open via chests.
	- 3: Fully open. Opens both Entrances and all Walls
- `open_forgotten_world`:
	- Opens shortcuts and entrances within and to Forgotten World
	- 0: Disabled
	- 1: Entrances. Opens alternative entrances to Forgotten World from Horizon Beach and Magma Chamber
	- 2: Shortcuts. Opens one-way shortcuts in the Forgotten World
	- 3: Fully open. Opens both Entrances and Shortcuts
- `open_mystical_workshop`:
	- If enabled, opens up the northern shortcut within Mystical Workshop
	- NOTE: This shortcut allows you to bypass the need for Mystical Workshop Keys. It is recommended to only use this setting if locked doors are turned off
	- 0: Disabled
	- 1: Enabled
- `open_underworld`:
	- Opens shortcuts and entrances within and to the Underworld
	- 0: Disabled
	- 1: Entrances. Removes sanctuary tokens from the item pool and opens up the Underworld door in Blue Caves, as well as the shortcut between Sun Palace and the Underworld
	- 2: Shortcuts. Opens all shortcuts and enables all grapple points
	- 3: Fully open. Opens both Entrances and Shortcuts
- `open_abandoned_tower`:
	- Opens shortcuts and entrances within and to the Abandoned Tower
	- 0: Disabled
	- 1: Entrances. Removes the key of power from the item pool and opens both doors between Mystical Workshop and Abandoned Tower
	- 2: Shortcuts. Opens the shortcuts within Abandoned Tower
	- 3: Fully open. Opens both Entrances and Shortcuts

- `exp_multiplier`:
	- Multiplier for experienced gained.
	- Range from 1 to 5
- `hints`:
	- Adds hints for common checks, items, and monsters. These hints are not persisted on the Archipelago server, and are only displayed as text within game dialog.
	- 0: Disabled
	- 1: Enabled
- `goal`:
	- Victory condition for the game
	- 0: Defeat Mad Lord
	- 1: Defeat all 27 champions
	- 2: Complete the monster journal by hatching or evolving all 111 monsters
	- 3: Reunite Mozzie and Velvet Melody by collecting the fragments of Mozzie's soul and returning them to Velvet Melody, then defeat Velvet Melody in a duel.
- `mozzie_soul_fragments`:
	- Only applies if the goal is to Reunite Mozzie and Velvet Melody.
	- How many Mozzies are required to complete the game.
	- Range from 3 to 15, with a default of 7.

### Note on Drop Chance Settings
The eight options to control relative drop frequency for different items works such that the higher the value is compared to the other drop item types, the more frequently it will occur.

For example, if the `drop_chance_weapon` value is twice the value of all other item types, then weapons show up, on average, twice as often as the everything else. If a drop chance is set to 0, it will never occur.

A more complicated example: If `drop_chance_catalyst` is set to 10, and `drop_chance_food` is set to 20, and everything else is set to 40, then catalysts will appear 1/4 as frequently as non-food items and half as frequently as food. While food will appear twice as frequently as catalysts and half as frequently as everything else.

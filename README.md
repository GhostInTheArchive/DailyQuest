# DailyQuest

DailyQuest is a **server-side** V Rising mod that gives each player **three rotating daily kill quests**.

## Latest Update
- Default quest configuration for new installations.
  - Added a default configuration with 116 quests for new installations.
  - If you have already installed DailyQuest and configured `quest_config.json`, you can continue using it as usual.
  - If you have already installed DailyQuest and want to use the default configuration, delete `quest_config.json` and `quest_player.json`, then restart the server.
  - You can edit `quest_config.json` after it has been generated from the default configuration.
- Discord webhook notifications.
  - DailyQuest now supports Discord webhook notifications when players claim quest rewards.
  - You can configure the webhook in `webhook_config.json`.

## Features
- Assigns **3 daily quests** to each player every day:
  - **Quest 1 (Easy)**
  - **Quest 2 (Medium)**
  - **Quest 3 (Hard)**
- Tracks kill progress automatically
- Stores daily quest progress per player
- Uses a simple JSON configuration file
- Includes an admin command to reload the quest configuration
- Lets players claim rewards with chat commands
- Supports **optional gear repair on claim**
  - Quest 1 reward: repairs **amulet** in the character's equipment slots (excluding Soul Shards)
  - Quest 2 reward: repairs **armor** in the character's equipment slots
  - Quest 3 reward: repairs **weapons** in the character's equipment slots
- Supports **optional Discord notifications on claim**

## Requirements
1. [BepInEx 1.733.2](https://thunderstore.io/c/v-rising/p/BepInEx/BepInExPack_V_Rising/) 
2. [VampireCommandFramework 0.10.4](https://thunderstore.io/c/v-rising/p/deca/VampireCommandFramework/)

## Installation
1. Install the required dependencies.
2. Place `DailyQuest.dll` into your server's BepInEx plugins folder.
3. Start the server once to generate the config files.
4. Edit the config file.
5. Restart the server or reload the quest configuration.

## How It Works
- Each player is assigned one Easy, one Medium, and one Hard quest for the day.
- Progress is tracked automatically when the player kills matching configured targets.
- Players can check progress at any time with `.quest daily`.
- Completed rewards can be claimed with `.quest reward`.
- Quest assignments refresh daily.

## Commands

### Player Commands
- `.quest daily` 
   - Show your current daily quests and progress.
   - Shortcut: *.quest d*

- `.quest reward` 
  - Claim all completed daily quest rewards.
  - Shortcut: *.quest rw*

### Admin Commands
- `.quest info <player>` 
  - Show daily quest status for a specific player.
  - Shortcut: *.quest i <player>*

- `.quest reload`
  - Reload quest_config.json
  - Shortcut: *.quest rl*

- `.questhook on`
  - Enable DailyQuest webhook sending.
  - Shortcut: *.qh on*

- `.questhook off`
  - Disable DailyQuest webhook sending.
  - Shortcut: *.qh off*

- `.questhook reload`
  - Reload webhook_config.json
  - Shortcut: *.qh rl*

- `.questhook test`
  - Send a test message to Discord.
  - Shortcut: *.qh t*

## Config Files

After the first server start, the following files will be created:
- `BepInEx/config/DailyQuest/quest_config.json`
- `BepInEx/config/DailyQuest/quest_player.json`
- `BepInEx/config/DailyQuest/webhook_config.json`

### quest_config.json
This file contains the main DailyQuest settings and quest definitions.
- `GearRepairOnClaim`: whether gear repair is enabled when rewards are claimed.
- `Quests`: the list of available quests.
- `ID`: the unique ID of the quest.
- `Name`: the name of the quest.
- `Difficulty`: the quest difficulty (`easy` = Quest 1, `medium` = Quest 2, `hard` = Quest 3).
- `TargetPrefabs`: the target prefab IDs for the quest.
- `RequiredKills`: the number of kills required to complete the quest.
- `Reward.Prefab`: the reward prefab ID.
- `Reward.Name`: the display name of the reward.
- `Reward.Amount`: the reward amount.

### quest_player.json
This file stores each player's daily quest assignments and progress.
- Do not edit `quest_player.json` unless you know exactly what you are doing.

### webhook_config.json
This file controls the optional DailyQuest Discord webhook.
- `enabled`: enables or disables webhook sending.
- `webhookUrl`: your Discord webhook URL.

## Credits
- **odjit** and **zfolmt** for the original code and assistance with this mod.
- **V Rising modding community**

## License
This project is licensed under the AGPL-3.0 license.

## Notes
> - This mod was first made for my own server and originally ran through KindredCommands. It has now been separated into a standalone mod so that everyone can use it.
> - If you have any problems or run into bugs, please report them to me in the [V Rising Modding Community](https://discord.com/invite/QG2FmueAG9)
> **Del** (delta_663)

# DailyQuest

DailyQuest is a **server-side** V Rising mod that gives each player **three rotating daily kill quests**.

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

## Config Files

After the first server start, the following files will be created:
- `BepInEx/config/DailyQuest/quest_config.json`
- `BepInEx/config/DailyQuest/quest_player.json`

### quest_config.json
This file defines:
- whether gear repair is enabled when rewards are claimed
- which quests are available
- required kill amounts
- target prefab IDs
- reward items and amounts
- [Sample daily quests from my server](https://docs.google.com/spreadsheets/d/1hFdY5FR3PcHFJUyLiL4qcRrRfmHRlvD9xCdu-Tjfy7o/edit?gid=214678967#gid=214678967)

### quest_player.json
This file stores each player's daily quest assignment and progress.
Do not edit `quest_player.json` unless you know exactly what you are doing.

> **Notes**
> - This mod was first made for my own server and originally ran through KindredCommands. It has now been separated into a standalone mod so that everyone can use it.
> - If you have any problems or run into bugs, please report them to me in the [V Rising Modding Community](https://discord.com/invite/QG2FmueAG9)
> **Del** (delta_663)
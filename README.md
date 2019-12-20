# VRCDynamicBones (Dynamic Bones Advanced Settings)
Optimizes the performance of dynamic bones, significantly reduces the load on the CPU and brings global dynamic bone support.

> **Disclaimer:**
> This mod uses [VRCModLoader](https://github.com/Slaynash/VRCModLoader) to work properly, so you will need to install that first.
>  
>  **Warning:**
>  The VRChat team is not very keen on modding or reverse engineering the game, while the mod does not include anything that would ruin the fun for others, using it may still be a bannable offence.
>   
>  **USE IT AT YOUR OWN RISK**, I am not responsible for any bans or any punishments you may get by using this mod!

## Requirements
- [VRCModLoader by Slaynash](https://github.com/Slaynash/VRCModLoader)
- [VRCMenuUtils](https://github.com/AtiLion/VRCMenuUtils)

## How to install
Install VRCModLoader and VRCMenuUtils using [VRChatModManager](https://github.com/Slaynash/VRChatModInstaller/releases)  
or download and install them manually  
> 1. Download the latest version of the mod from the Releases section of it's GitHub page  
> 2. Navigate to your VRChat game directory *(usually it's C:\Program Files (x86)\Steam\steamapps\common\VRChat)*  
> 3. Drag/copy the DLL file that you have downloaded into the Mods folder  

## How to enable/use
Launch the game;  
Uncheck the '*Limit Dynamic Bone Usage*' in the performance options;  
If you're using emmVRC mod, disable it's global dynamic bones functionality.  
To use this mod open your quick menu and on the left there should be a *Dynamic Bones Advanced* button,  
after pressing it, a page with options will appear:

![](/docs/dynamic_bones_mod.png)

## Buttons description:

    Advanced Settings        Enabled (this mod will control dynamic bones) / Disabled (default dynamic bones behaviour)
    Mode                     Local / Global (between you and other players) / Global (between all players) / Disabled
    Working Distance         3m/5m/10m/20m/40m/INFINITE (maximum distance from you to other players at which theirs dynamic bones will stay enabled)
    Update Rate              Constant / Distance Dependent update rate
    Max Update Rate          30/60/90/120/Display Rate (update rate for dynamic bones that are local or close to you)
    Min Update Rate          15/30/60/90/Display Rate (update rate for dynamic bones that are far away when Distance Dependent mode is enabled)
    Local Colliders Filter   All / Upper Body / Hands Only (filters specific colliders for your avatar)
    Others Colliders Filter  All / Upper Body / Hands Only (filters specific colliders for other players)


## Unity Test Project

Allows you to test different options and their impact on performance.
Included avatar have 6 dynamic bones (105 joints) and 8 colliders.
You can move avatars around to observe collisions between them.

![](/docs/dynamic_bones_unity_test_project.png)

## Requirements (Unity project)
- [Dynamic Bone Asset for Unity](https://assetstore.unity.com/packages/tools/animation/dynamic-bone-16743)

![](https://img.shields.io/github/downloads/zakriamansoor47/SLAYER_Revive/total?style=for-the-badge&label=Downloads)
# Accepting Paid Request! Discord: Slayer47#7002
# Donation
<a href="https://www.buymeacoffee.com/slayer47" target="_blank"><img src="https://www.buymeacoffee.com/assets/img/custom_images/orange_img.png" alt="Buy Me A Coffee" style="height: 41px !important;width: 174px !important;box-shadow: 0px 3px 2px 0px rgba(190, 190, 190, 0.5) !important;-webkit-box-shadow: 0px 3px 2px 0px rgba(190, 190, 190, 0.5) !important;" ></a>

## Description:
- Revive teammates with 'E' (+use button).
- Revive Progress Display. 
- Limit Revives. 
- Revive Cost Modes.

## Installation:
**1.** Upload files to your server.

**2.** Edit **configs/plugins/SLAYER_Revive/SLAYER_Revive.json** if you want to change the settings.

**3.** Change the Map **or** Restart the Server **or** Load the Plugin.

## Video:
https://www.youtube.com/watch?v=1S3Znymv8JE

## Configuration:
```json
{
  "PluginEnabled": true,
  "revive_DrawBeacon": true,		// Draw Beacon when revivng
  "revive_DrawReviveSign": true,	// Draw a revive sign(partical) over dead body of players
  "revive_CountDeath": true,	// Count the death or Decrease Death of the player who got revived by his teammate (true=Count, false=Decrease)
  "revive_ReviveLimit": 2,			// How many times a Player can revive in a Round
  "revive_ReviveFrag": 1,			// Give how many Frags (Kills) to the player who revived his teammate
  "revive_cost_mode": 0,			// Cost mode of revive? (0=Disabled, 1=Health, 2=Money, 3=Both)
  "revive_cost_health": 10,			// How much Health taken away from reviver after he revive his teammate?
  "revive_cost_money": 1000,		// How much Money taken away from reviver after he revive his teammate?
  "revive_RevivedHealth": 100,		// After getting revived, what will be the health of that Player?
  "revive_timer_delay": 5,			// How many seconds needs to revive a teammate?
  "revive_delay": 15,				// How many seconds of delay should be between each revive? (0=No Delay)
  "revive_distance": 150,			// Maximum Distance from dead body of the teammate for revivng?
  "ConfigVersion": 1
}
```

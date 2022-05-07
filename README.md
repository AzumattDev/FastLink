Vanilla UI to quickly join servers configured in a YAML file.


`Client side mod with live updated configurations via Azumatt.FastLink_servers.yml`

---

> ## Configuration Options
`[UI]`
* Position of the UI [Not Synced with Server]
    * Sets the anchor position of the UI
        * Default value:  -900, 200

> ## Example (default) YAML
```yml
# Configure your servers for Azumatt's FastLink mod in this file.
# Servers are automatically sorted alphabetically when shown in the list.
# This file live updates the in-game listing. Feel free to change it while in the main menu.

Example Server:
  address: example.com
  port: 1234
  password: somepassword

Some IPv6 Server:
  address: 2606:2800:220:1:248:1893:25c8:1946
  port: 4023
  password: a password with spaces

Passwordless IPv4 Server:
  address: 93.184.216.34
  port: 9999

# You can optionally change the color of your server name. Does not work for the address and port. Also, can show PvP status.
<color=red>Another IPv4 Server</color>:
  address: 192.0.2.146
  port: 9999
  ispvp: true
```

> ## Installation Instructions
***You must have BepInEx installed correctly! I can not stress this enough.***

#### Windows (Steam)
1. Locate your game folder manually or start Steam client and :
    * Right click the Valheim game in your steam library
    * "Go to Manage" -> "Browse local files"
    * Steam should open your game folder
2. Extract the contents of the archive into the BepInEx\plugins folder.
3. Locate Azumatt.FastLink.cfg and Azumatt.FastLink_servers.yml under BepInEx\config and configure the mod to your needs

#### Server
`This mod is not needed on a server. It's client only. Install on each client that you wish to have the mod load.`



`Feel free to reach out to me on discord if you need manual download assistance.`


# Author Information

### Azumatt

`DISCORD:` Azumatt#2625

`STEAM:` https://steamcommunity.com/id/azumatt/

### Thank you Blaxxun! - [Blaxxun YAML contribution](https://github.com/AzumattDev/FastLink/commit/bfcf290c83e785ced14e3c2d93da2739e86b3102)

For Questions or Comments, find me in the Odin Plus Team Discord:
[![https://i.imgur.com/XXP6HCU.png](https://i.imgur.com/XXP6HCU.png)](https://discord.gg/Pb6bVMnFb2)

***
> # Update Information (Latest listed first)
> ### v1.0.0
> - Initial Release
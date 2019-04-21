## ChatCommands - BepInEx plugin for commands via chat

##### PLEASE NOTE: Versions before 1.3.0 are not multiplayer safe - Please update.

If you've ever had to use console commands a lot during a game, im sure you hated opening the console too.

So, here it is.

Console commands, via chat. (That's all it does, I promise)

To use, call the command just as you would in the console, but with a `/` (configurable) just before the command. Eg. `say hi` can be
run from chat by writing `/say hi`. Please also note that only you see the response.


##### If you have any problems with this plugin, feel free to shoot me a ping or PM on the [modding discord](https://discord.gg/hMdjd9y "Risk of Rain 2 Modding") - my handle is viseyth#3934.

##### From a feature only point this plugin is complete. If you don't agree, feel free send me a message, too.

---

#### ATTENTION: Using `/` before commands in the real console gives you a few seconds of not responding and an OutOfMemoryException!
(It does work after the few seconds again, though.)

---

### Features:
#### Calling registered commands
This plugin is able to run all registered console commands, internal and from mods. (If the mod was done right, and adds
`ConCommand`s to `Console.concommandCatalog`)

#### Setting registered variables 
You can set them just as in the console, eg. `/volume_master 10` or `/volume_master = 10`.

#### Console output in chat
You will get all output you would have gotten when running in the console.

#### Status response
You will get a response from system, if your command executed successfully. This includes text from custom
`ConCommandExceptions` thrown inside console commands.

#### Configurability `(v1.4.0+)`
You are able to customize your prefix (default is `/`), and if you still want your commands shown after execution. You
are also able to set the config to remove other players commands, although that only works if you both are using the
same prefix.

## ChatCommands - BepInEx plugin for commands via chat

If you've ever had to use console commands a lot during a game, im sure you hated opening the console too.

So, here it is.

Console commands, via chat. (That's all it does, I promise)

To use, call the command just as you would in the console, but with a `/` just before the command. Eg. `say hi` can be
run from chat by writing `/say hi`. Please also note that only you see command and response.

> #### ATTENTION: Using `/` before commands in the real console gives you a few seconds of not responding and an OutOfMemoryException!
> (It does work after the few seconds again, though.)

If you have any problems with this plugin, feel free to shoot me a ping or PM on the [modding discord](https://discord.gg/hMdjd9y "Risk of Rain 2 Modding") - my handle is viseyth#3934.

## What works:
#### Calling registered commands
This plugin checks for registered console commands, and if the command is registered, runs the command via call to the
console. So, as long as your favourite mod adds actual console commands, this mod will work with it with no extra
configuration required.

#### Setting registered variables (since 1.2.0)
Yeah, I completely forgot about that. You can now set them just as in the console, eg. `/volume_master 10` or
`/volume_master = 10`.

#### Status response (since 1.1.0)
Now calling and catching console functions by _itself_, will now actually show you the exception message if something
went wrong. On the other side, since that means i had to walk the whole mile myself, this means the plugin will probably
need attention on new updates.

## What doesn't (yet):
#### Console output in chat
If I ever find a nice solution to this problem, it will be added.
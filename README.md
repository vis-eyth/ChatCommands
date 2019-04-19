## ChatCommands - BepInEx plugin for commands via chat

If you've ever had to use console commands a lot during a game, im sure you hated opening the console too.

So, here it is.

Console commands, via chat. (That's all it does, I promise)

### What works:
#### Calling registered commands
This plugin checks for registered console commands, and if the command is registered, runs the command via call to the
console. So, as long as your favourite mod adds actual console commands, this mod will work with it with no extra
configuration required.

#### Status response (since 1.1.0)
Now calling and catching console functions by _itself_, will now actually show you the exception message if something
went wrong. On the other side, since that means i had to walk the whole mile myself, this means the plugin will probably
need attention on new updates.

### What doesn't (yet):
#### Console output in chat
If I ever find a nice solution to this problem, it will be added.
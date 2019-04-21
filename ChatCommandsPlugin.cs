/**
 * MIT License
 *
 * Copyright (c) 2019 Vis'Eyth (viseyth#3934)
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ChatCommands
{
    [BepInPlugin("com.viseyth.ror2.chatcommands", "ChatCommands", "1.4.0")]
    public class ChatCommandsPlugin : BaseUnityPlugin
    {
        private static ConfigWrapper<bool> _deleteLocalCommands;
        private static ConfigWrapper<bool> _deleteRemoteCommands;
        private static ConfigWrapper<string> _commandPrefix;
        
        // ReSharper disable IdentifierTypo InconsistentNaming MergeConditionalExpression
        private static Console _bconsole;
        private static Console _console
            => _bconsole == null
                ? _bconsole = Console.instance
                : _bconsole;
        private static IDictionary _bcatalog;
        private static IDictionary _catalog
            => _bcatalog == null
                ? _bcatalog = _console.GetFieldValue<IDictionary>("concommandCatalog")
                : _bcatalog;
        private static List<string> _bchatlog;
        private static List<string> _chatlog
            => _bchatlog == null
                ? _bchatlog = typeof(Chat).GetFieldValue<List<string>>("log")
                : _bchatlog;
        
        private static bool _listening;
        private static readonly List<Console.Log> _queue = new List<Console.Log>();
        // ReSharper restore IdentifierTypo InconsistentNaming MergeConditionalExpression


        private void Awake()
        {
            _deleteLocalCommands = Config.Wrap("Game"
                , "DeleteLocalCommands"
                ,"If your command input in chat should be deleted after execution."
                , false);
            _deleteRemoteCommands = Config.Wrap("Game"
                , "DeleteRemoteCommands"
                ,"If others command inputs in chat should be deleted after execution. (only works if prefix is the same)"
                , false);
            _commandPrefix = Config.Wrap("Game"
                , "CommandPrefix"
                , "The prefix used to detect commands in chat. One character only."
                , "/");
            
            Chat.onChatChanged += () =>
            {
                var str = _chatlog[_chatlog.Count - 1];
                var msg = ReconstructFromString(str);

                if (msg == null || !msg.text.StartsWith(_commandPrefix.Value[0].ToString()))
                    return;

                // here we have a command
                var user = msg.sender.GetComponent<NetworkUser>();
                if ( !user.isLocalPlayer && _deleteRemoteCommands.Value
                    || user.isLocalPlayer && _deleteLocalCommands.Value )
                    _chatlog.Remove(str);
                
                if (user.isLocalPlayer)
                    ExecuteCommand(msg);
            };
            Application.logMessageReceived += (message, trace, type) =>
            {
                if (_listening)
                    _queue.Add(new Console.Log
                    {
                        logType = type,
                        message = message,
                        stackTrace = trace
                    });
            };
        }

        private static Chat.UserChatMessage ReconstructFromString(string message) {
            var match = Regex.Match(message
                , @"<color=#e5eefc><noparse>(.+?)<\/noparse>: <noparse>(.+?)<\/noparse><\/color>"
                , RegexOptions.Compiled);

            if (!match.Success || match.Groups.Count < 3)
                return null;

            GameObject user = null;

            foreach (var nUser in NetworkUser.readOnlyInstancesList) {
                if (nUser.userName != match.Groups[1].Value.Trim())
                    continue;

                user = nUser.gameObject;
                break;
            }

            if (user == null)
                return null;

            return new Chat.UserChatMessage {
                sender = user,
                text = match.Groups[2].Value.Trim()
            };
        }

        private static void ExecuteCommand(Chat.UserChatMessage message)
        {
            var callString = message.text.TrimStart(_commandPrefix.Value[0]);
            if (callString.Length == 0)
                return;

            var user = message.sender.GetComponent<NetworkUser>();
            List<string> args;
            try {
                args = ParseCommand(callString, message.sender);
            }
            catch (System.Exception ex) {
                ShowResponse($"Command \"{callString}\" could not be parsed: {ex}", true);
                return;
            }

            var cmd = args[0];
            args.RemoveAt(0);

            try
            {
                var isCmd = _catalog.Contains(cmd);
                if (!isCmd && _console.FindConVar(cmd) == null)
                    throw new ConCommandException("Command is not registered.");

                var flags = isCmd
                    ? _catalog[cmd].GetFieldValue<ConVarFlags>("flags")
                    : _console.FindConVar(cmd).flags;

                if ((flags & ConVarFlags.SenderMustBeServer) != ConVarFlags.None &&
                    !NetworkServer.active)
                    throw new ConCommandException("Command is only usable by the host.");

                if ((flags & ConVarFlags.Cheat) != ConVarFlags.None && !RoR2Application.cvCheats.boolValue)
                    throw new ConCommandException("Command cannot be used while cheats are disabled.");

                if ((flags & ConVarFlags.ExecuteOnServer) != ConVarFlags.None &&
                    !NetworkServer.active)
                {
                    _console.InvokeMethod("ForwardCmdToServer", new ConCommandArgs
                    {
                        sender = user,
                        commandName = cmd,
                        userArgs = args
                    });
                    ShowResponse($"Command \"{cmd}\" sent to server.");
                    return;
                }

                if (isCmd)
                {
                    var command = _catalog[cmd].GetFieldValue<Console.ConCommandDelegate>("action");

                    _listening = true;
                    command(new ConCommandArgs
                    {
                        sender = user,
                        commandName = cmd,
                        userArgs = args
                    });
                    _listening = false;
                    foreach (var log in _queue)
                        ShowResponse(log.message, log.logType == LogType.Error);
                    _queue.Clear();
                    
                    ShowResponse($"Command \"{cmd}\" executed successfully.");
                }
                else
                {
                    _console.FindConVar(cmd).SetString(args[0]);
                    
                    ShowResponse($"Variable \"{cmd}\" set to \"{args[0]}\".");
                }
            }
            catch (ConCommandException ex)
            {
                ShowResponse($"Command \"{cmd}\" failed: {ex.Message}", true);
            }
        }

        private static void ShowResponse(string response, bool error = false)
        {
            var message = "<color=#" + (error ? "f08080" : "98fb98") + ">System: " + response + "</color>";
            _chatlog.Add(message);
        }

        /// <summary>
        /// Splits user input into command and arguments, and removes unnecessary characters.
        /// Function behavior is copied from RoR2.Console.SubmitCmd - if problems arise, its probably this.
        /// </summary>
        /// <param name="command">The string to be parsed and split into command + arguments</param>
        /// <param name="sender">For parsing of vstr, the sender is required.</param>
        /// <returns>A string list with command at index 0, then all arguments.</returns>
        private static List<string> ParseCommand(string command, GameObject sender)
        {
            // Console.Lexer does not take kindly to slashes, and even though we have removed the prefix already,
            // the user might have used slashes somewhere else.
            if (command.Contains("/"))
                command = command.Replace('/', ' ');
            
            var lexer = Reflection
                .GetNestedType<Console>("Lexer")
                .GetConstructor(new[] {typeof(string)})?
                .Invoke(new object[] {command});

            var tokens = lexer.InvokeMethod<Queue<string>>("GetTokens");

            var args = new List<string>();
            var vstrFlag = false;

            while (tokens.Count != 0)
            {
                var token = tokens.Dequeue();
                if (token == ";")
                {
                    vstrFlag = false;
                    if (args.Count > 0)
                    {
                        break;
                    }
                }
                else
                {
                    if (vstrFlag)
                    {
                        token = _console.InvokeMethod<string>("GetVstrValue", sender, token);
                        vstrFlag = false;
                    }

                    if (token == "vstr") vstrFlag = true;
                    else args.Add(token);
                }
            }

            return args;
        }
    }
}
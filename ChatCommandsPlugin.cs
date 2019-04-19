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
using System.Linq;
using System.Text.RegularExpressions;
using BepInEx;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ChatCommands
{
    [BepInPlugin("com.viseyth.ror2.chatcommands", "ChatCommands", "1.3.0")]
    public class ChatCommandsPlugin : BaseUnityPlugin
    {
        private static Console _console;
        private static IDictionary _catalog;

        private static bool _listening;
        private static List<Console.Log> _queue;

        private void Awake()
        {
            _listening = false;
            _queue = new List<Console.Log>();
            
            Chat.onChatChanged += () =>
            {
                var message = Chat.readOnlyLog.Last();
                if (IsCommandSyntax(message)
                    && ParseSender(GetUsername(message)) != null
                    && ParseSender(GetUsername(message)).GetComponent<NetworkUser>().isLocalPlayer)
                {
                    ExecuteCommand(new Chat.UserChatMessage
                    {
                        sender = ParseSender(GetUsername(message)),
                        text = message
                    });
                }
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

        private static void ExecuteCommand(Chat.UserChatMessage message)
        {
            if (_console == null)
                _console = Console.instance;
            if (_catalog == null)
                _catalog = _console.GetFieldValue<IDictionary>("concommandCatalog");
            
            var callString = GetCommand(message.text);
            if (callString.Length == 0)
                return;

            var user = message.sender.GetComponent<NetworkUser>();
            var args = ParseCommand(callString, message.sender);

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
            typeof(Chat).GetFieldValue<List<string>>("log").InvokeMethod("Add", message);
        }

        private static readonly Regex CommandRegex =
            new Regex(@"<color=#e5eefc><noparse>(.+?)<\/noparse>: <noparse>\/(.+?)<\/noparse><\/color>");

        private static bool IsCommandSyntax(string input) => CommandRegex.IsMatch(input);
        private static string GetUsername(string input) =>
            !IsCommandSyntax(input) || CommandRegex.Match(input).Groups.Count < 2
                ? ""
                : CommandRegex.Match(input).Groups[1].Value.Trim();
        private static string GetCommand(string input) =>
            !IsCommandSyntax(input) || CommandRegex.Match(input).Groups.Count < 3
                ? ""
                : CommandRegex.Match(input).Groups[2].Value.Trim();

        private static List<string> ParseCommand(string command, GameObject sender)
        {
            // Console.Lexer does not take kindly to slashes, and even though we have removed the prefix already,
            // the user might have used slashes somewhere else.
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

        private static GameObject ParseSender(string username)
        {
            foreach (var user in NetworkUser.readOnlyLocalPlayersList)
            {
                if (user.userName == username)
                    return user.gameObject;
            }

            return null;
        }
    }
}
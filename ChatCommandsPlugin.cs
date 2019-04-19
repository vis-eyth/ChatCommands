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
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ChatCommands
{
    [BepInPlugin("com.viseyth.ror2.chatcommands", "ChatCommands", "1.1.0")]
    public class ChatCommandsPlugin : BaseUnityPlugin
    {
        private Console _console;
        private IDictionary _catalog;

        private void Awake()
        {
            On.RoR2.Console.Awake += (orig, self) =>
            {
                _console = self;
                _catalog = _console.GetFieldValue<IDictionary>("concommandCatalog");
                orig(self);
            };
            On.RoR2.Chat.SendBroadcastChat_ChatMessageBase += (orig, message) =>
            {
                if (message is Chat.UserChatMessage msg && IsCommandSyntax(msg.text))
                {
                    Chat.AddMessage(msg.ConstructChatString());
                    ExecuteCommand(msg);
                }
                else
                    orig(message);
            };
        }

        private void ExecuteCommand(Chat.UserChatMessage message)
        {
            var callString = GetCommand(message.text);
            if (callString.Length == 0)
                return;

            var user = message.sender.GetComponent<NetworkUser>();
            var args = ParseCommand(callString, message.sender);

            Debug.Log(user.userName + " called command \"" + args[0] + "\" using chat with \"" +
                      string.Join(" ", args) + "\".");

            var cmd = args[0];
            args.RemoveAt(0);

            try
            {
                if (!_catalog.Contains(cmd))
                    throw new ConCommandException("Command is not registered.");

                var flags = _catalog[cmd].GetFieldValue<ConVarFlags>("flags");

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

                var command = _catalog[cmd].GetFieldValue<Console.ConCommandDelegate>("action");

                command(new ConCommandArgs
                {
                    sender = user,
                    commandName = cmd,
                    userArgs = args
                });

                ShowResponse($"Command \"{cmd}\" executed successfully.");
            }
            catch (ConCommandException ex)
            {
                ShowResponse($"Command \"{cmd}\" failed: {ex.Message}", true);
            }
        }

        private static void ShowResponse(string response, bool error = false)
        {
            var message = "<color=#" + (error ? "f08080" : "98fb98") + ">System: " + response + "</color>";
            Chat.AddMessage(message);
        }

        private static readonly Regex CommandRegex = new Regex(@"^\/([\d\w]+(?: | .+?)*)(?:$|;)");

        private static bool IsCommandSyntax(string input) => CommandRegex.IsMatch(input);

        private static string GetCommand(string input) =>
            !IsCommandSyntax(input) || CommandRegex.Match(input).Groups.Count < 2
                ? ""
                : CommandRegex.Match(input).Groups[1].Value.Trim();

        private List<string> ParseCommand(string command, GameObject sender)
        {
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
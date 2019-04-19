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
using System.Text.RegularExpressions;
using BepInEx;
using RoR2;
using UnityEngine;

namespace ChatCommands
{
    // ReSharper disable once StringLiteralTypo
    [BepInPlugin("com.viseyth.ror2.chatcommands", "ChatCommands", "1.0.0")]
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
                if (!(message is Chat.UserChatMessage msg && TryCommand(msg)))
                    orig(message);
            };
        }

        private bool TryCommand(Chat.UserChatMessage message)
        {
            if (!ParseCommand(message.text, out var command, out var callString))
                return false;

            var user = message.sender.GetComponent<NetworkUser>();
            Debug.Log(user.userName + " called command \"" + command + "\" using chat with \"" + callString + "\".");

            if (!_catalog.Contains(command))
            {
                SendResponse("Command \"" + command + "\" is not registered.", true);
                return true;
            }

            var flags = _catalog[command].GetFieldValue<ConVarFlags>("flags");

            if ((flags & ConVarFlags.Cheat) != ConVarFlags.None && !RoR2Application.cvCheats.boolValue)
            {
                SendResponse("Command \"" + command + "\" cannot be used while cheats are disabled.", true);
                return true;
            }

            if ((flags & ConVarFlags.SenderMustBeServer) != ConVarFlags.None &&
                !UnityEngine.Networking.NetworkServer.active)
            {
                SendResponse("Command \"" + command + "\" is only usable by the host.", true);
                return true;
            }

            _console.SubmitCmd(user, callString.Substring(1));
            SendResponse("Executed \"" + callString + "\".", false);

            return true;
        }

        private static void SendResponse(string response, bool error)
        {
            var message = "<color=#" + (error ? "f08080" : "98fb98") + ">System: " + response + "</color>";
            Chat.AddMessage(message);
        }

        private static readonly Regex CommandRegex = new Regex(@"^\/([\d\w]+)(?: | .+?)*(?:$|;)");

        private static bool ParseCommand(string input, out string command, out string call)
        {
            var match = CommandRegex.Match(input);
            if (match.Success)
            {
                command = match.Groups[1].Value;
                call = match.Groups[0].Value;

                return true;
            }

            command = call = "";
            return false;
        }
    }
}
#nullable enable

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SecondBotEvents.Services.Ai
{
    public enum OwnerIntentDisposition
    {
        NoMatch,
        Match,
        Clarify
    }

    public sealed record OwnerIntentContext(
        string LastInventoryFolder = "",
        string LastAnimation = "");

    public sealed record OwnerIntentResult(
        OwnerIntentDisposition Disposition,
        string ToolKey,
        IReadOnlyDictionary<string, object> Arguments,
        string Clarification = "",
        string LocalReply = "")
    {
        public static OwnerIntentResult NoMatch() =>
            new(OwnerIntentDisposition.NoMatch, "", EmptyArguments);

        public static OwnerIntentResult Match(string toolKey, IReadOnlyDictionary<string, object>? arguments = null) =>
            new(OwnerIntentDisposition.Match, toolKey, arguments ?? EmptyArguments);

        public static OwnerIntentResult Reply(string reply) =>
            new(OwnerIntentDisposition.Match, "", EmptyArguments, LocalReply: reply);

        public static OwnerIntentResult Clarify(string message) =>
            new(OwnerIntentDisposition.Clarify, "", EmptyArguments, message);

        private static readonly IReadOnlyDictionary<string, object> EmptyArguments =
            new Dictionary<string, object>();
    }

    /// <summary>
    /// Pure, deliberately conservative parser for zero-token owner IM commands.
    /// It performs no I/O and never decides whether the sender is the owner; callers
    /// must establish that before invoking it.
    /// </summary>
    public static class OwnerIntentRouter
    {
        private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        public static OwnerIntentResult Route(string? input, OwnerIntentContext? context = null)
        {
            context ??= new OwnerIntentContext();
            string message = Normalize(input);
            if (message.Length == 0 || message.Length > 500)
                return OwnerIntentResult.NoMatch();
            if (IsUnsafeToGuess(message))
            {
                if (Regex.IsMatch(message, @"\b(?:inventory|inventories|animations?)\b", Options))
                    return OwnerIntentResult.Clarify("Please ask for one inventory or animation action at a time.");
                return OwnerIntentResult.NoMatch();
            }

            if (IsMatch(message, @"^(?:what(?:'s| is) my uuid|tell me my uuid|my uuid)$"))
                return OwnerIntentResult.Reply("Your verified owner UUID is {owner_uuid}.");

            if (IsMatch(message, @"^(?:(?:where|what region|what sim) (?:are you|you are) (?:currently|right now|now)?|where are you|current location|your location|your position|what is your position)$"))
                return OwnerIntentResult.Match("position");
            if (IsMatch(message, @"^(?:what|which)(?: region| sim)(?: are you in| is this)?$|^(?:region|sim)(?: name)?$"))
                return OwnerIntentResult.Match("sim_name");
            if (IsMatch(message, @"^(?:what|which) parcel(?: are you in| is this)?$|^parcel name$"))
                return OwnerIntentResult.Match("parcel_name");
            if (IsMatch(message, @"^(?:parcel )?(?:uuid|id)$|^what is (?:this |your )?parcel (?:uuid|id)$"))
                return OwnerIntentResult.Match("parcel_uuid");
            if (IsMatch(message, @"^(?:what is )?(?:this |your )?parcel size$"))
                return OwnerIntentResult.Match("parcel_size");
            if (IsMatch(message, @"^(?:what is )?(?:this |your )?parcel traffic$"))
                return OwnerIntentResult.Match("parcel_traffic");
            if (IsMatch(message, @"^(?:what is )?(?:this |your )?parcel description$"))
                return OwnerIntentResult.Match("parcel_description");
            if (IsMatch(message, @"^(?:what are )?(?:this |your )?parcel flags$"))
                return OwnerIntentResult.Match("parcel_flags");

            if (IsMatch(message, @"^(?:who|what avatars?|which avatars?|anyone) (?:is |are )?(?:nearby|near you|around you)$|^(?:show|list|check) (?:me )?(?:the )?(?:nearby avatars?|people nearby)$"))
                return OwnerIntentResult.Match("nearby_details");
            if (IsMatch(message, @"^(?:who|what) is nearby$|^nearby$"))
                return OwnerIntentResult.Match("nearby");
            if (IsMatch(message, @"^(?:(?:show|list|check) (?:me )?(?:your |the )?friends(?: list)?|who are your friends|friends list)$"))
                return OwnerIntentResult.Match("friends_list");
            if (IsMatch(message, @"^(?:(?:show|list|check) (?:me )?(?:your |the )?groups(?: list)?|what groups are you in|groups list)$"))
                return OwnerIntentResult.Match("groups");

            if (IsMatch(message, @"^(?:(?:show|list|check) (?:me )?(?:your |the )?inventory folders|(?:show|list|check) (?:me )?(?:your |the )?inventory|what folders are in your inventory|inventory folders)$"))
                return OwnerIntentResult.Match("inventory_folders");
            if (IsMatch(message, @"^(?:what animations do you have(?: to play)?|(?:show|list|search for|find) (?:me )?(?:the )?animations(?: in your inventory)?|search your inventory for animations|(?:tell me )?what(?:'s| is) inside your animations folder)$"))
                return InventoryContents("Animations");

            Match folder = Regex.Match(message,
                @"^(?:(?:tell me )?what(?:'s| is) (?:inside|in) (?:your |the )?(?<folder1>.+?)(?: folder)?|(?:show|list|check) (?:me )?(?:inside |the contents of )?(?:your |the )?(?<folder2>.+?) folder)$",
                Options);
            string capturedFolder = folder.Groups["folder1"].Success ? folder.Groups["folder1"].Value : folder.Groups["folder2"].Value;
            if (folder.Success && CleanEntity(capturedFolder, out string folderName))
                return InventoryContents(folderName);

            if (IsMatch(message, @"^(?:what(?:'s| is) inside|what(?:'s| is) in it|list its contents)$"))
                return string.IsNullOrWhiteSpace(context.LastInventoryFolder)
                    ? OwnerIntentResult.Clarify("Which inventory folder should I check?")
                    : InventoryContents(context.LastInventoryFolder);

            Match play = Regex.Match(message, @"^(?:please )?(?:play|start|run) (?:the )?(?:animation )?(?<name>.+?)(?: animation)?$", Options);
            if (play.Success && CleanEntity(play.Groups["name"].Value, out string animationToPlay))
                return Animation("animation_start", animationToPlay);

            if (IsMatch(message, @"^(?:stop|reset|end) (?:all |your )?animations$"))
                return OwnerIntentResult.Match("reset_animations");
            if (IsMatch(message, @"^(?:stop it|stop that animation)$"))
                return string.IsNullOrWhiteSpace(context.LastAnimation)
                    ? OwnerIntentResult.Clarify("Which animation should I stop?")
                    : Animation("animation_stop", context.LastAnimation);

            Match stop = Regex.Match(message, @"^(?:please )?(?:stop|end) (?:the )?(?:animation (?<name1>.+)|(?<name2>.+?) animation)$", Options);
            string capturedAnimation = stop.Groups["name1"].Success ? stop.Groups["name1"].Value : stop.Groups["name2"].Value;
            if (stop.Success && CleanEntity(capturedAnimation, out string animationToStop))
                return Animation("animation_stop", animationToStop);

            if (IsMatch(message, @"^(?:please )?(?:stand|stand up|get up)$"))
                return OwnerIntentResult.Match("stand");
            if (IsMatch(message, @"^(?:please )?(?:stop moving|stop walking|cancel movement|stop autopilot)$"))
                return OwnerIntentResult.Match("autopilot_stop");
            if (IsMatch(message, @"^(?:please )?(?:come to me|request (?:a )?teleport(?: to me)?|ask me (?:for|to send) (?:a )?teleport)$"))
                return OwnerIntentResult.Match("request_teleport");

            Match dialog = Regex.Match(message,
                @"^(?:please )?(?:click|press|choose|select) [""']?(?<button>.+?)[""']? (?:on|for|in) dialog (?:id )?(?<id>[1-9][0-9]*)$",
                Options);
            if (dialog.Success && CleanEntity(dialog.Groups["button"].Value, out string button)
                && int.TryParse(dialog.Groups["id"].Value, out int dialogId))
            {
                return OwnerIntentResult.Match("dialog_response", new Dictionary<string, object>
                {
                    ["dialogid"] = dialogId,
                    ["buttontext"] = button
                });
            }
            if (IsMatch(message, @"^(?:accept|approve|decline|reject|cancel) (?:the )?dialog(?: (?:id )?[1-9][0-9]*)?$"))
                return OwnerIntentResult.Clarify("Tell me the dialog ID and exact button label, for example: click Yes on dialog 3.");

            // Never spend provider tokens merely to interpret inventory wording.
            // Ambiguous owner requests are clarified locally instead.
            if (Regex.IsMatch(message, @"\b(?:delete|remove|purge|destroy)\b", Options)
                && Regex.IsMatch(message, @"\b(?:inventory|folder|item)\b", Options))
                return OwnerIntentResult.Clarify("Destructive inventory actions are not available through conversational AI.");
            if (Regex.IsMatch(message, @"\b(?:inventory|inventories)\b", Options))
            {
                if (Regex.IsMatch(message, @"\banimations?\b", Options))
                    return InventoryContents("Animations");
                return OwnerIntentResult.Clarify("Which inventory folder should I check? For example: list my Animations folder.");
            }
            if (Regex.IsMatch(message, @"\banimations?\b", Options))
                return OwnerIntentResult.Clarify("Should I list the Animations folder, play an animation, stop one, or reset all animations?");

            return OwnerIntentResult.NoMatch();
        }

        private static OwnerIntentResult InventoryContents(string folder) =>
            OwnerIntentResult.Match("inventory_contents", new Dictionary<string, object> { ["folder"] = folder });

        private static OwnerIntentResult Animation(string tool, string animation) =>
            OwnerIntentResult.Match(tool, new Dictionary<string, object> { ["animation"] = animation });

        private static bool IsUnsafeToGuess(string message)
        {
            if (message.Contains("<secondbot_tool>", StringComparison.OrdinalIgnoreCase)) return true;
            if (Regex.IsMatch(message, @"\b(?:don't|do not|dont|never|without)\b", Options)) return true;
            if (message.Contains('\n') || message.Contains(';')) return true;
            if (Regex.IsMatch(message, @"\b(?:and then|then|after that|also)\b", Options)) return true;
            return false;
        }

        private static bool CleanEntity(string source, out string entity)
        {
            entity = source.Trim().Trim('"', '\'', ' ', '.', '?', '!');
            if (entity.EndsWith(" folder", StringComparison.OrdinalIgnoreCase))
                entity = entity[..^7].Trim();
            if (entity.Length == 0 || entity.Length > 128) return false;
            if (Regex.IsMatch(entity, @"[<>\r\n;]")) return false;
            if (Regex.IsMatch(entity, @"\b(?:and then|then|also)\b", Options)) return false;
            return true;
        }

        private static string Normalize(string? source)
        {
            string value = Regex.Replace(source ?? "", @"\s+", " ").Trim();
            return value.TrimEnd('.', '?', '!', ' ');
        }

        private static bool IsMatch(string input, string pattern) => Regex.IsMatch(input, pattern, Options);
    }
}

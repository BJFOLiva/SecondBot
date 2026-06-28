using OpenMetaverse;
using SecondBotEvents.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SecondBotEvents.Commands
{
    [ClassInfo("Animations and Gestures")]
    internal class AnimationCommands(EventsSecondBot setmaster) : CommandsAPI(setmaster)
    {
        [About("Toggles if animation requests from this avatar (used for remote poseballs) are accepted")]
        [ReturnHints("Granted perm animation")]
        [ReturnHints("Removed perm animation")]
        [ReturnHintsFailure("avatar lookup")]
        [ArgHints("avatar", "Who to accept requests from", "AVATAR")]
        [CmdTypeSet()]
        public object AddToAllowAnimations(string avatar)
        {
            ProcessAvatar(avatar);
            if (avataruuid == UUID.Zero)
            {
                return BasicReply("avatar lookup", [avatar]);
            }
            // @todo accept storage
            return Failure("@todo");
        }

        [About("Attempts to play a gesture \n using the inventory uuid")]
        [ReturnHintsFailure("Error with gesture inventory uuid")]
        [ReturnHints("Accepted")]
        [ArgHints("gesture", "inventory uuid for the gesture to trigger", "UUID")]
        [ReturnHintsFailure("unable to get gesture from inventory")]
        [CmdTypeDo()]
        public object PlayGesture(string gesture)
        {
            if (UUID.TryParse(gesture, out UUID gestureUUID) == false)
            {
                return BasicReply("Error with gesture inventory uuid", [gesture]);
            }
            InventoryItem itm = GetClient().Inventory.FetchItem(gestureUUID, GetClient().Self.AgentID, TimeSpan.FromSeconds(15));
            if (itm == null)
            {
                return BasicReply("unable to get getsture from inventory", [gesture]);
            }
            GetClient().Self.PlayGesture(itm.AssetUUID);
            return BasicReply("Accepted", [gesture]);
        }
        [About("Attempts to play a gesture \n using the real asset uuid not the inventory uuid")]
        [ReturnHintsFailure("Error with gesture asset uuid")]
        [ReturnHints("Accepted")]
        [ArgHints("gesture", "asset uuid for the gesture to trigger", "UUID")]
        [CmdTypeDo()]
        public object PlayGestureDirect(string gesture)
        {
            if (UUID.TryParse(gesture, out UUID gestureUUID) == false)
            {
                return BasicReply("Error with gesture", [gesture]);
            }
            GetClient().Self.PlayGesture(gestureUUID);
            return BasicReply("Accepted", [gesture]);
        }

        [About("Resets the animation stack for the bot")]
        [ReturnHints("Accepted - X stopped animations")]
        [CmdTypeDo()]
        public object ResetAnimations()
        {
            List<UUID> animations = [.. GetClient().Self.SignaledAnimations.Copy().Keys];
            foreach (UUID anim in animations)
            {
                GetClient().Self.AnimationStop(anim, true);
            }
            return BasicReply("Accepted - "+animations.Count.ToString()+" stopped animations");
        }

        [About("Starts playing a body animation using its inventory item UUID")]
        [ReturnHints("Accepted")]
        [ReturnHintsFailure("Error with animation uuid")]
        [ReturnHintsFailure("unable to get animation from inventory")]
        [ArgHints("animation", "inventory uuid of the animation to play", "UUID")]
        [CmdTypeDo()]
        public object AnimationStart(string animation)
        {
            if (UUID.TryParse(animation, out UUID animUUID) == false)
            {
                return BasicReply("Error with animation uuid", [animation]);
            }
            InventoryItem itm = GetClient().Inventory.FetchItem(animUUID, GetClient().Self.AgentID, TimeSpan.FromSeconds(15));
            if (itm == null)
            {
                return BasicReply("unable to get animation from inventory", [animation]);
            }
            GetClient().Self.AnimationStart(itm.AssetUUID, true);
            return BasicReply("Accepted", [animation]);
        }

        [About("Stops playing a body animation using its asset UUID")]
        [ReturnHints("Accepted")]
        [ReturnHintsFailure("Error with animation asset uuid")]
        [ArgHints("animation", "asset uuid of the animation to stop", "UUID")]
        [CmdTypeDo()]
        public object AnimationStop(string animation)
        {
            if (UUID.TryParse(animation, out UUID animUUID) == false)
            {
                return BasicReply("Error with animation asset uuid", [animation]);
            }
            GetClient().Self.AnimationStop(animUUID, true);
            return BasicReply("Accepted", [animation]);
        }

    }
}

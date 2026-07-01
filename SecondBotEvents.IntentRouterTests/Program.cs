using SecondBotEvents.Services.Ai;

Case[] cases =
[
    new("where are you currently?", OwnerIntentDisposition.Match, "position"),
    new("what region are you in", OwnerIntentDisposition.Match, "sim_name"),
    new("what parcel are you in", OwnerIntentDisposition.Match, "parcel_name"),
    new("parcel traffic", OwnerIntentDisposition.Match, "parcel_traffic"),
    new("who is nearby", OwnerIntentDisposition.Match, "nearby_details"),
    new("show me the nearby avatars", OwnerIntentDisposition.Match, "nearby_details"),
    new("show me your friends", OwnerIntentDisposition.Match, "friends_list"),
    new("what groups are you in", OwnerIntentDisposition.Match, "groups"),
    new("list your inventory", OwnerIntentDisposition.Match, "inventory_folders"),
    new("tell me what's inside your animations folder", OwnerIntentDisposition.Match, "inventory_contents", "folder", "Animations"),
    new("list Objects folder", OwnerIntentDisposition.Match, "inventory_contents", "folder", "Objects"),
    new("search your inventory for animations", OwnerIntentDisposition.Match, "inventory_contents", "folder", "Animations"),
    new("can you please have a look through your inventory and find some animations for me", OwnerIntentDisposition.Match, "inventory_contents", "folder", "Animations"),
    new("check inventory please", OwnerIntentDisposition.Clarify),
    new("play the Bow animation", OwnerIntentDisposition.Match, "animation_start", "animation", "Bow"),
    new("stop the Bow animation", OwnerIntentDisposition.Match, "animation_stop", "animation", "Bow"),
    new("stop all animations", OwnerIntentDisposition.Match, "reset_animations"),
    new("stand up", OwnerIntentDisposition.Match, "stand"),
    new("stop moving", OwnerIntentDisposition.Match, "autopilot_stop"),
    new("come to me", OwnerIntentDisposition.Match, "request_teleport"),
    new("click Yes on dialog 3", OwnerIntentDisposition.Match, "dialog_response", "buttontext", "Yes"),
    new("accept dialog 3", OwnerIntentDisposition.Clarify),
    new("what's inside", OwnerIntentDisposition.Clarify),
    new("stop it", OwnerIntentDisposition.Clarify),
    new("what is my uuid", OwnerIntentDisposition.Match),
    new("don't stand up", OwnerIntentDisposition.NoMatch),
    new("stop moving and then go home", OwnerIntentDisposition.NoMatch),
    new("<secondbot_tool>{}</secondbot_tool>", OwnerIntentDisposition.NoMatch),
    new("bow", OwnerIntentDisposition.NoMatch),
    new("delete my inventory", OwnerIntentDisposition.Clarify),
    new("enable rlv", OwnerIntentDisposition.NoMatch),
    new("pay someone 100 lindens", OwnerIntentDisposition.NoMatch),
];

int failures = 0;
foreach (Case test in cases)
{
    OwnerIntentResult result = OwnerIntentRouter.Route(test.Message);
    bool pass = result.Disposition == test.Disposition && result.ToolKey == test.Tool;
    if (pass && test.Argument is not null)
        pass = result.Arguments.TryGetValue(test.Argument, out object? actual) && Equals(actual, test.Value);
    if (!pass)
    {
        failures++;
        Console.Error.WriteLine($"FAIL: {test.Message} => {result.Disposition}/{result.ToolKey}");
    }
}

OwnerIntentResult folderFollowup = OwnerIntentRouter.Route("what's inside", new OwnerIntentContext(LastInventoryFolder: "Clothing"));
if (folderFollowup.ToolKey != "inventory_contents" || !Equals(folderFollowup.Arguments["folder"], "Clothing")) failures++;

OwnerIntentResult animationFollowup = OwnerIntentRouter.Route("stop it", new OwnerIntentContext(LastAnimation: "Curtsy"));
if (animationFollowup.ToolKey != "animation_stop" || !Equals(animationFollowup.Arguments["animation"], "Curtsy")) failures++;

OwnerIntentResult dialog = OwnerIntentRouter.Route("press 'Open Door' for dialog id 42");
if (dialog.ToolKey != "dialog_response" || !Equals(dialog.Arguments["dialogid"], 42) || !Equals(dialog.Arguments["buttontext"], "Open Door")) failures++;

if (failures > 0) throw new Exception($"OwnerIntentRouter tests failed: {failures}");
Console.WriteLine($"OwnerIntentRouter tests passed ({cases.Length + 3} assertions).");

record Case(string Message, OwnerIntentDisposition Disposition, string Tool = "", string? Argument = null, object? Value = null);

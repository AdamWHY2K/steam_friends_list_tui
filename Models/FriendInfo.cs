using SteamKit2;

namespace SteamFriendsTUI.Models;

public record FriendInfo(
    SteamID SteamId,
    string Name,
    EPersonaState State,
    string StatusText,
    string GameText,
    DateTime LastSeen
);

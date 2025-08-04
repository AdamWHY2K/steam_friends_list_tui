using SteamKit2;

namespace SteamFriendsCLI.Models;

public record FriendInfo(
    SteamID SteamId,
    string Name,
    EPersonaState State,
    string StatusText,
    string GameText,
    DateTime LastSeen
);

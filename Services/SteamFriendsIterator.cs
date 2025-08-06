using SteamKit2;

namespace SteamFriendsTUI.Services;

public static class SteamFriendsIterator
{
    /// <summary>
    /// Iterates through all friends and executes an action for each friend relationship
    /// </summary>
    public static void ForEachFriend(SteamFriends steamFriends, Action<SteamID, EFriendRelationship> action)
    {
        int totalCount = steamFriends.GetFriendCount();

        for (int i = 0; i < totalCount; i++)
        {
            SteamID steamIdFriend = steamFriends.GetFriendByIndex(i);
            EFriendRelationship relationship = steamFriends.GetFriendRelationship(steamIdFriend);
            action(steamIdFriend, relationship);
        }
    }

    /// <summary>
    /// Iterates through friends of a specific relationship type
    /// </summary>
    public static void ForEachFriendOfType(SteamFriends steamFriends, EFriendRelationship targetRelationship, Action<SteamID> action)
    {
        ForEachFriend(steamFriends, (steamId, relationship) =>
        {
            if (relationship == targetRelationship)
            {
                action(steamId);
            }
        });
    }

    /// <summary>
    /// Counts friends by relationship type
    /// </summary>
    public static Dictionary<EFriendRelationship, int> CountFriendsByRelationship(SteamFriends steamFriends)
    {
        var counts = new Dictionary<EFriendRelationship, int>();

        ForEachFriend(steamFriends, (steamId, relationship) =>
        {
            counts.TryGetValue(relationship, out int currentCount);
            counts[relationship] = currentCount + 1;
        });

        return counts;
    }

    /// <summary>
    /// Gets all friends of a specific relationship type
    /// </summary>
    public static List<SteamID> GetFriendsOfType(SteamFriends steamFriends, EFriendRelationship targetRelationship)
    {
        var friends = new List<SteamID>();

        ForEachFriendOfType(steamFriends, targetRelationship, steamId =>
        {
            friends.Add(steamId);
        });

        return friends;
    }
}

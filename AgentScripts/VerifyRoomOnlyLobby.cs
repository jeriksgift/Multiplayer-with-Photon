using System;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

public static class VerifyRoomOnlyLobby
{
    public static string Check()
    {
        var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI Toolkit/MyUIToolkits/lobby.uxml");
        var root = tree.Instantiate();
        if (root.Q<TextField>("NickNameTextField") != null) throw new Exception("Lobby still contains a nickname field.");
        var fields = root.Q("Container").Query<TextField>().ToList();
        if (fields.Count != 1 || fields[0].name != "RoomNameTextField") throw new Exception("Room name must be the lobby's only entry field.");
        if (root.Q<TextField>("FriendPlayerId").maxLength != 10) throw new Exception("Friend IDs must have a 10-digit entry field.");
        foreach (var bad in new[] { "123", "0123456789", "12345678901", "abcdefghij", "１２３４５６７８９０" })
            if (AccountSession.IsPublicPlayerId(bad)) throw new Exception("Invalid public ID was accepted.");
        if (!AccountSession.IsPublicPlayerId("1000000000") || !AccountSession.IsPublicPlayerId("9999999999"))
            throw new Exception("Valid 10-digit ID range was rejected.");
        return "Imported lobby UXML has only the room-name field; friend input is limited to 10 digits; C# public-ID validation passed.";
    }
}

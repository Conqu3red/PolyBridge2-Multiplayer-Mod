using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Class holding lobby messages.
/// </summary>
public class LobbyMessaging : BaseMessaging
{
    /// <summary>
    /// Initializes a new instance of the <see cref="T:LobbyMessaging"/> class.
    /// </summary>
    /// <param name="client">Client.</param>
    public LobbyMessaging(ServerCommunication client) : base(client) { }

    // Register messages
    public const string Register = "register";
    public UnityAction OnConnectedToServer;


    // Bridge action messages

    //public const string BridgeAction = "BridgeAction";
    public UnityAction<BridgeActionModel> OnBridgeAction;

    // Bridge
    public const string BridgeAction = "BridgeAction";

    public const string MousePosition = "MousePosition";
    
    // popups and messages
    public const string PopupMessage = "PopupMessage";
    public const string ConsoleMessage = "ConsoleMessage";
    public const string TopLeftMessage = "TopLeftMessage";
    public const string ChatMessage = "ChatMessage";

    // owner/config management
    public const string ServerConfig = "ServerConfig";
    public const string ServerInfo = "ServerInfo";
    public const string KickUser = "KickUser";
    public const string BanUser = "BanUser";
    public const string CreateInvite = "CreateInvite";


    /// <summary>
    /// Sends echo message to the server.
    /// </summary>
    /// <param name="request">Request.</param>
    public void SendBridgeAction(BridgeActionModel request)
    {
        if (MultiplayerMod.MultiplayerMod.instance.serverIsFrozen && request.action != MultiplayerMod.actionType.FREEZE) return;
        var message = new MessageModel
        {
            type = BridgeAction,
            content = request.Serialize()
        };
        client.SendRequest(message.Serialize());
    }
}

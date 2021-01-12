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

    // Echo messages
    public const string Echo = "echo";
    public UnityAction<EchoMessageModel> OnEchoMessage;

    /// <summary>
    /// Sends echo message to the server.
    /// </summary>
    /// <param name="request">Request.</param>
    public void EchoMessage(EchoMessageModel request)
    {
        var message = new MessageModel
        {
            method = "echo",
            message = JsonUtility.ToJson(request)
        };
        client.SendRequest(JsonUtility.ToJson(message));
    }
}

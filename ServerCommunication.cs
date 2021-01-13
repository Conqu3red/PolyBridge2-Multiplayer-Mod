﻿using UnityEngine;

/// <summary>
/// Forefront class for the server communication.
/// </summary>
public class ServerCommunication
{
    // Server IP address
    [SerializeField]
    public string hostIP;

    // Server port
    [SerializeField]
    public int port = 3000;

    // Flag to use localhost
    [SerializeField]
    public bool useLocalhost = true;

    // Address used in code
    public string host => useLocalhost ? "localhost" : hostIP;

    public string path = "";
    // Final server address
    public string server;

    // WebSocket Client
    public WsClient client;

    // Class with messages for "lobby"
    public LobbyMessaging Lobby { private set; get; }

    /// <summary>
    /// Unity method called on initialization
    /// </summary>
    public ServerCommunication(){}

    public void Init(){
        server = "ws://" + host + ":" + port + "/" + path;
        client = new WsClient(server);

        // Messaging
        Lobby = new LobbyMessaging(this);
    }

    /// <summary>
    /// Unity method called every frame
    /// </summary>
    public void Update()
    {
        // Check if server send new messages
        var cqueue = client.receiveQueue;
        string msg;
        while (cqueue.TryPeek(out msg))
        {
            // Parse newly received messages
            cqueue.TryDequeue(out msg);
            HandleMessage(msg);
        }
    }

    /// <summary>
    /// Method responsible for handling server messages
    /// </summary>
    /// <param name="msg">Message.</param>
    public void HandleMessage(string msg)
    {
        //Debug.Log("<CLIENT> <RECV>: " + msg);

        // Deserializing message from the server
        var message = JsonUtility.FromJson<MessageModel>(msg);

        // Picking correct method for message handling
        if (message.metadata == "server_closed"){
            P2PMod.P2PMod.Disconnect();
        }
        switch (message.type)
        {
            case LobbyMessaging.Register:
                Lobby.OnConnectedToServer?.Invoke();
                break;
            case LobbyMessaging.BridgeAction:
                Lobby.OnBridgeAction?.Invoke(JsonUtility.FromJson<BridgeActionModel>(message.content));
                break;
            case LobbyMessaging.ConsoleMessage:
                uConsole.Log(message.content);
                break;
            case LobbyMessaging.PopupMessage:
                PopUpMessage.DisplayOkOnly(message.content, null);
                break;
            case LobbyMessaging.TopLeftMessage:
                GameUI.ShowMessage(ScreenMessageLocation.TOP_LEFT, message.content, 3f);
                break;
            default:
                Debug.LogError("Unknown type of method: " + message.type);
                break;
        }
    }

    /// <summary>
    /// Call this method to connect to the server
    /// </summary>
    public async void ConnectToServer()
    {
        await client.Connect();
    }

    /// <summary>
    /// Method which sends data through websocket
    /// </summary>
    /// <param name="message">Message.</param>
    public void SendRequest(string message)
    {
        client.Send(message);
    }
}
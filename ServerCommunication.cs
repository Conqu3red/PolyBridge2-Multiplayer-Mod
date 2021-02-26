using UnityEngine;
using System.IO;
using System;
using System.Text;
using System.Collections.Generic;
using UnityEngine.UI;
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
    public bool isOwner;

    // Class with messages for "lobby"
    public LobbyMessaging Lobby { private set; get; }
    public string logFileName;
    public bool ssl = false;

    /// <summary>
    /// Unity method called on initialization
    /// </summary>
    public ServerCommunication(){}

    public void Init(){
        isOwner = false;
        string time = string.Format("{0:yyyy-MM-dd HH-mm-ss}", DateTime.Now);
        logFileName = $"session_{time}.log";
        server = (ssl ? "wss" : "ws") + "://" + host + ":" + port + "/" + path;
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
        byte[] msg;
        if (Bridge.IsSimulating()) return;
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
    public void HandleMessage(byte[] msg)
    {
        //Debug.Log("<CLIENT> <RECV>: " + msg);

        // Deserializing message from the server
        int offset = 0;
        var message = new MessageModel(msg, ref offset);

        // Picking correct method for message handling
        if (message.metadata == "server_closed"){
            MultiplayerMod.MultiplayerMod.Disconnect();
        }
        if (message.metadata == "owner"){
            isOwner = true;
        }
        if (message.metadata == "connected"){
            MultiplayerMod.MultiplayerMod.syncLayout();
        }
        offset = 0;
        //Debug.Log(message.type);
        switch (message.type)
        {
            case LobbyMessaging.BridgeAction:
                Lobby.OnBridgeAction?.Invoke(new BridgeActionModel(message.content, ref offset));
                break;
            case "ConnectionResponse":
                MultiplayerMod.MultiplayerMod.GUIValues.ConnectionResponse = MultiplayerMod.MultiplayerMod.GetJustStringFromBytes(message.content);
                break;
            case LobbyMessaging.ServerInfo:
                MultiplayerMod.MultiplayerMod.instance.serverInfo = new ServerInfoModel(message.content, ref offset);
                MultiplayerMod.MultiplayerMod.RemoveDisconnectedUsersFromMousePositions();
                break;
            case LobbyMessaging.KickUser:
                MultiplayerMod.MultiplayerMod.GUIValues.kickResponse = MultiplayerMod.MultiplayerMod.GetJustStringFromBytes(message.content);
                break;
            case LobbyMessaging.ServerConfig:
                MultiplayerMod.MultiplayerMod.GUIValues.ConfigResponse = MultiplayerMod.MultiplayerMod.GetJustStringFromBytes(message.content);
                break;
            case LobbyMessaging.CreateInvite:
                MultiplayerMod.MultiplayerMod.GUIValues.InviteResponse = MultiplayerMod.MultiplayerMod.GetJustStringFromBytes(message.content);
                break;
            case LobbyMessaging.MousePosition:
                MousePositionModel mousePosition = new MousePositionModel(message.content, ref offset);
                mousePosition.position.z = -1.1f;
                MultiplayerMod.MultiplayerMod.instance.HandleMousePositionRecieved(mousePosition);
                break;
            case LobbyMessaging.PopupMessage:
                PopUpMessage.DisplayOkOnly(MultiplayerMod.MultiplayerMod.GetJustStringFromBytes(message.content), null);
                if (isOwner) MultiplayerMod.MultiplayerMod.ActionLog($"Popup Message - {message.content}");
                break;
            case LobbyMessaging.TopLeftMessage:
                GameUI.ShowMessage(ScreenMessageLocation.TOP_LEFT, MultiplayerMod.MultiplayerMod.GetJustStringFromBytes(message.content), 3f);
                if (isOwner) MultiplayerMod.MultiplayerMod.ActionLog($"Info Message - {message.content}");
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

    public bool IsConnected(){
        if (client != null){
            return client.IsConnectionOpen();
        }
        return false;
    }
    public bool IsConnecting(){
        if (client != null){
            return client.IsConnecting();
        }
        return false;
    }

    /// <summary>
    /// Method which sends data through websocket
    /// </summary>
    /// <param name="message">Message.</param>
    public void SendRequest(byte[] message)
    {
        client.Send(message);
    }
}

public class PointerHandler {
    public PointerHandler(){
        this.Container = MultiplayerMod.MultiplayerMod.Instantiate(new GameObject("cursorContainer"));
        this.color = Color.red;
        SpriteRenderer renderer = this.Container.AddComponent<SpriteRenderer>();
        

    }
    public GameObject Container;
    public Color color;
}

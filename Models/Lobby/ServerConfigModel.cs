using MultiplayerMod;
using System.Collections.Generic;

public enum ConfigAction {
    USER_CAP,
    ACCEPTING_CONNECTIONS,
    CHANGE_PASSWORD,
    CHANGE_LOBBY_MODE
}
public class ServerConfigModel
{
    public ServerConfigModel(){}
    public ServerConfigModel(byte[] bytes, ref int offset){
        this.Deserialize(bytes, ref offset);
    }
    public ConfigAction action;
    public int userCap = 0;
    public bool acceptingConnections = false;
    public string newPassword = "";
    public LobbyMode lobbyMode = LobbyMode.PUBLIC;

    public byte[] Serialize(){
        List<byte> list = new List<byte>();
        list.AddRange(ByteSerializer.SerializeInt((int)this.action));
        list.AddRange(ByteSerializer.SerializeInt(this.userCap));
        list.AddRange(ByteSerializer.SerializeBool(this.acceptingConnections));
        list.AddRange(ByteSerializer.SerializeString(this.newPassword));
        list.AddRange(ByteSerializer.SerializeInt((int)this.lobbyMode));
        return list.ToArray();
    }
    public void Deserialize(byte[] bytes, ref int offset){
        this.action = (ConfigAction)ByteSerializer.DeserializeInt(bytes, ref offset);
        this.userCap = ByteSerializer.DeserializeInt(bytes, ref offset);
        this.acceptingConnections = ByteSerializer.DeserializeBool(bytes, ref offset);
        this.newPassword = ByteSerializer.DeserializeString(bytes, ref offset);
        this.lobbyMode = (LobbyMode)ByteSerializer.DeserializeInt(bytes, ref offset);
    }
}
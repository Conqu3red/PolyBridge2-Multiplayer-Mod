using MultiplayerMod;
using System.Collections.Generic;
/// <summary>
/// Echo message model.
/// </summary>
public class ServerInfoModel
{
    public ServerInfoModel(){}
    public ServerInfoModel(byte[] bytes, ref int offset){
        this.Deserialize(bytes, ref offset);
    }
    public int userCount;
    public int userCap;
    public bool acceptingConnections;
    public LobbyMode lobbyMode;
    public bool isFrozen;
    public string[] playerNames;

    public byte[] Serialize(){
        List<byte> list = new List<byte>();
        list.AddRange(ByteSerializer.SerializeInt(this.userCount));
        list.AddRange(ByteSerializer.SerializeInt(this.userCap));
        list.AddRange(ByteSerializer.SerializeBool(this.acceptingConnections));
        list.AddRange(ByteSerializer.SerializeInt((int)this.lobbyMode));
        list.AddRange(ByteSerializer.SerializeBool(this.isFrozen));
        list.AddRange(ByteSerializer.SerializeStrings(this.playerNames));
        return list.ToArray();
    }
    public void Deserialize(byte[] bytes, ref int offset){
        this.userCount = ByteSerializer.DeserializeInt(bytes, ref offset);
        this.userCap = ByteSerializer.DeserializeInt(bytes, ref offset);
        this.acceptingConnections = ByteSerializer.DeserializeBool(bytes, ref offset);
        this.lobbyMode = (LobbyMode)ByteSerializer.DeserializeInt(bytes, ref offset);
        this.isFrozen = ByteSerializer.DeserializeBool(bytes, ref offset);
        this.playerNames = ByteSerializer.DeserializeStrings(bytes, ref offset);
    }
}

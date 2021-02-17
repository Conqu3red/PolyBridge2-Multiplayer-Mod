using MultiplayerMod;
using System.Collections.Generic;
/// <summary>
/// Echo message model.
/// </summary>
[System.Serializable]
public class BridgeActionModel
{
    public BridgeActionModel(){}
    public BridgeActionModel(byte[] bytes, ref int offset){
        this.Deserialize(bytes, ref offset);
    }
    public actionType action;
    public byte[] content;
    public string username = "";
    public string metadata = "";
    public bool playSound = false;

    public byte[] Serialize(){
        List<byte> list = new List<byte>();
        list.AddRange(ByteSerializer.SerializeInt((int)this.action));
        list.AddRange(ByteSerializer.SerializeByteArray(this.content));
        list.AddRange(ByteSerializer.SerializeString(this.username));
        list.AddRange(ByteSerializer.SerializeString(this.metadata));
        list.AddRange(ByteSerializer.SerializeBool(this.playSound));
        return list.ToArray();
    }
    public void Deserialize(byte[] bytes, ref int offset){
        this.action = (actionType)ByteSerializer.DeserializeInt(bytes, ref offset);
        this.content = ByteSerializer.DeserializeByteArray(bytes, ref offset);
        this.username = ByteSerializer.DeserializeString(bytes, ref offset);
        this.metadata = ByteSerializer.DeserializeString(bytes, ref offset);
        this.playSound = ByteSerializer.DeserializeBool(bytes, ref offset);
    }
}

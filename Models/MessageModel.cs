using System.Collections.Generic;
/// <summary>
/// Message model.
/// </summary>
public class MessageModel
{
    public MessageModel(){}
    public MessageModel(byte[] bytes, ref int offset){
        this.Deserialize(bytes, ref offset);
    }
    public string type;
    public byte[] content;
    public string metadata;

    public byte[] Serialize(){
        List<byte> list = new List<byte>();
        list.AddRange(ByteSerializer.SerializeString(this.type));
        list.AddRange(ByteSerializer.SerializeByteArray(this.content));
        list.AddRange(ByteSerializer.SerializeString(this.metadata));
        return list.ToArray();
    }
    public void Deserialize(byte[] bytes, ref int offset){
        this.type = ByteSerializer.DeserializeString(bytes, ref offset);
        this.content = ByteSerializer.DeserializeByteArray(bytes, ref offset);
        this.metadata = ByteSerializer.DeserializeString(bytes, ref offset);
    }
}

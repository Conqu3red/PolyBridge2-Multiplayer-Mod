using MultiplayerMod;
using System.Collections.Generic;
/// <summary>
/// Echo message model.
/// </summary>
[System.Serializable]
public class KickUserModel
{
    public KickUserModel(){}
    public KickUserModel(byte[] bytes, ref int offset){
        this.Deserialize(bytes, ref offset);
    }
    public string username = "";
    public string reason = "";

    public byte[] Serialize(){
        List<byte> list = new List<byte>();
        list.AddRange(ByteSerializer.SerializeString(this.username));
        list.AddRange(ByteSerializer.SerializeString(this.reason));
        return list.ToArray();
    }

    public void Deserialize(byte[] bytes, ref int offset){
        this.username = ByteSerializer.DeserializeString(bytes, ref offset);
        this.reason = ByteSerializer.DeserializeString(bytes, ref offset);
    }
}

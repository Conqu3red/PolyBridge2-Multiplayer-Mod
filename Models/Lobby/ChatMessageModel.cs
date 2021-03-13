using MultiplayerMod;
using System.Collections.Generic;
using UnityEngine;
[System.Serializable]
public class ChatMessageModel
{
    public ChatMessageModel(){}
    public ChatMessageModel(byte[] bytes, ref int offset){
        this.Deserialize(bytes, ref offset);
    }
    public string message = "";
    public string username = "";
    public Color nameColor;

    public byte[] Serialize(){
        List<byte> list = new List<byte>();
        list.AddRange(ByteSerializer.SerializeString(this.message));
        list.AddRange(ByteSerializer.SerializeString(this.username));
        list.AddRange(ByteSerializer.SerializeColor(this.nameColor));
        return list.ToArray();
    }
    public void Deserialize(byte[] bytes, ref int offset){
        this.message = ByteSerializer.DeserializeString(bytes, ref offset);
        this.username = ByteSerializer.DeserializeString(bytes, ref offset);
        this.nameColor = ByteSerializer.DeserializeColor(bytes, ref offset);
    }
}

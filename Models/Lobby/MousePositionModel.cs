using MultiplayerMod;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// Echo message model.
/// </summary>
public class MousePositionModel
{
    public MousePositionModel(){}
    public MousePositionModel(byte[] bytes, ref int offset){
        this.Deserialize(bytes, ref offset);
    }
    public string username = "";
    public Vector3 position;
    public PointerMode pointerMode;
    public Color pointerColor;

    public byte[] Serialize(){
        List<byte> list = new List<byte>();
        list.AddRange(ByteSerializer.SerializeString(this.username));
        list.AddRange(ByteSerializer.SerializeVector3(this.position));
        list.AddRange(ByteSerializer.SerializeInt((int)this.pointerMode));
        list.AddRange(ByteSerializer.SerializeColor(this.pointerColor));
        return list.ToArray();
    }
    public void Deserialize(byte[] bytes, ref int offset){
        this.username = ByteSerializer.DeserializeString(bytes, ref offset);
        this.position = ByteSerializer.DeserializeVector3(bytes, ref offset);
        this.pointerMode = (PointerMode)ByteSerializer.DeserializeInt(bytes, ref offset);
        this.pointerColor = ByteSerializer.DeserializeColor(bytes, ref offset);
    }
}

import json
import uuid
from hashlib import sha256
import time
import sys
import struct
from binary import *

class Message:
    def Deserialize(_bytes: bytes):
        stream = BinaryStream(_bytes)
        this = {}
        this["type"] = stream.ReadString()
        this["content"] = stream.ReadByteArray()
        this["metadata"] = stream.ReadString()
        return this
    def Serialize(this):
        stream = BinaryStream()
        stream.WriteString(this["type"])
        stream.WriteByteArray(this["content"])
        stream.WriteString(this.get("metadata", ""))
        return stream.base_stream

class BridgeActionModel:
    def Deserialize(_bytes: bytes):
        stream = BinaryStream(_bytes)
        this = {}
        this["action"] = stream.ReadInt32()
        this["content"] = stream.ReadByteArray()
        this["username"] = stream.ReadString()
        this["metadata"] = stream.ReadString()
        this["playSound"] = stream.ReadBool()
        return this
    def Serialize(this):
        stream = BinaryStream()
        stream.WriteInt32(this["action"])
        stream.WriteByteArray(this["content"])
        stream.WriteString(this["username"])
        stream.WriteString(this["metadata"])
        stream.WriteBool(this["playSound"])
        return stream.base_stream

class ServerConfigModel:
    def Deserialize(_bytes: bytes):
        stream = BinaryStream(_bytes)
        this = {}
        this["action"] = stream.ReadInt32()
        this["userCap"] = stream.ReadInt32()
        this["acceptingConnections"] = stream.ReadBool()
        this["newPassword"] = stream.ReadString()
        this["lobbyMode"] = stream.ReadInt32()
        return this
    def Serialize(this):
        raise NotImplementedError()

class KickUserModel:
    def Deserialize(_bytes: bytes):
        stream = BinaryStream(_bytes)
        this = {}
        this["username"] = stream.ReadString()
        this["reason"] = stream.ReadString()
        return this
    def Serialize(this):
        raise NotImplementedError()

class LayoutModel:
    def Deserialize(_bytes: bytes):
        stream = BinaryStream(_bytes)
        this = {}
        this["targetAllUsers"] = stream.ReadBool()
        this["layoutData"] = stream.ReadByteArray()
        return this
    def Serialize(this):
        raise NotImplementedError()

class ServerInfoModel:
    def Deserialize(_bytes: bytes):
        raise NotImplementedError() # this is never actually needed as the server is the only one sending this data
    def Serialize(this):
        stream = BinaryStream()
        stream.WriteInt32(this["usersConnected"])
        stream.WriteInt32(this["userCap"])
        stream.WriteBool(this["acceptingConnections"])
        stream.WriteInt32(this["lobbyMode"])
        stream.WriteBool(this["isFrozen"])
        stream.WriteStrings(this["playerNames"])
        return stream.base_stream

class MousePositionModel:
    def Deserialize(_bytes: bytes):
        stream = BinaryStream(_bytes)
        this = {}
        this["username"] = stream.ReadString()
        this["position"] = stream.ReadVector3()
        this["pointerMode"] = stream.ReadInt32()
        this["pointerColor"] = stream.ReadColor()
        return this
    def Serialize(this):
        stream = BinaryStream()
        stream.WriteString(this["username"])
        stream.WriteVector3(this["position"])
        stream.WriteInt32(this["pointerMode"])
        stream.WriteColor(this["pointerColor"])
        return stream.base_stream

def stringResponse(string:str):
    stream = BinaryStream()
    stream.WriteString(string)
    return stream.base_stream

class ChatMessageModel:
    def Deserialize(_bytes: bytes):
        stream = BinaryStream(_bytes)
        this = {}
        this["message"] = stream.ReadString()
        this["username"] = stream.ReadString()
        this["nameColor"] = stream.ReadColor()
        return this
    def Serialize(this):
        stream = BinaryStream()
        stream.WriteString(this["message"])
        stream.WriteString(this["username"])
        stream.WriteColor(this["nameColor"])
        return stream.base_stream

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
        return this
    def Serialize(this):
        stream = BinaryStream()
        stream.WriteInt32(this["action"])
        stream.WriteByteArray(this["content"])
        stream.WriteString(this["username"])
        stream.WriteString(this["metadata"])
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

def stringResponse(string:str):
    stream = BinaryStream()
    stream.WriteString(string)
    return stream.base_stream

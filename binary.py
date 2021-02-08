from struct import *

class BinaryStream:
    def __init__(self, base_stream: bytes = b""):
        self.base_stream = base_stream
        self.offset = 0

    def ReadByte(self):
        if self.offset + length > len(self.base_stream):
            print(f"{self.offset=} {length=}")
            raise Exception("End of bytes reached")
        ret = self.base_stream[self.offset:self.offset+1]
        self.offset += 1

    def ReadBytes(self, length):
        if self.offset + length > len(self.base_stream):
            print(f"{self.offset=} {length=}")
            raise Exception("End of bytes reached")
        ret = self.base_stream[self.offset:self.offset+length]
        self.offset += length
        return ret
    
    def ReadByteArray(self):
        length = self.ReadInt32()
        if length > 0: 
            ret = self.ReadBytes(length)
            return ret

    def ReadChar(self):
        return self.unpack('b')

    def ReadUChar(self):
        return self.unpack('B')

    def ReadBool(self):
        return self.unpack('?')

    def ReadInt16(self):
        return self.unpack('h', 2)

    def ReadUInt16(self):
        return self.unpack('H', 2)

    def ReadInt32(self):
        return self.unpack('i', 4)

    def ReadUInt32(self):
        return self.unpack('I', 4)

    def ReadInt64(self):
        return self.unpack('q', 8)

    def ReadUInt64(self):
        return self.unpack('Q', 8)

    def ReadFloat(self):
        return self.unpack('f', 4)

    def ReadSingle(self):
        return self.unpack('f', 4)

    def ReadDouble(self):
        return self.unpack('d', 8)

    def ReadString(self):
        length = self.ReadUInt16()
        #print(length)
        if length > 0:
            return self.unpack(str(length) + 's', length).decode("utf-8")
        return ""

    def ReadStringSingleByteLength(self):
        length = int.from_bytes(self.ReadByte(), "little")
        print(length)
        return self.unpack(str(length) + 's', length).decode("utf-8")
    
    def WriteByteArray(self, value: bytes):
        self.WriteInt32(len(value))
        self.WriteBytes(value)
    
    def WriteBytes(self, value):
        self.base_stream += value

    def WriteChar(self, value):
        self.pack('c', value)

    def WriteUChar(self, value):
        self.pack('C', value)

    def WriteBool(self, value):
        self.pack('?', value)

    def WriteUInt8(self, value):
        self.pack('b', value)
    
    def WriteInt16(self, value):
        self.pack('h', value)

    def WriteUInt16(self, value):
        self.pack('H', value)

    def WriteInt32(self, value):
        self.pack('i', value)

    def WriteUInt32(self, value):
        self.pack('I', value)

    def WriteInt64(self, value):
        self.pack('q', value)

    def WriteUInt64(self, value):
        self.pack('Q', value)

    def WriteFloat(self, value):
        self.pack('f', value)

    def WriteDouble(self, value):
        self.pack('d', value)

    def WriteString(self, value):
        length = len(value)
        self.WriteUInt16(length)
        self.pack(str(length) + 's', value.encode("utf-8"))

    def WriteStringSingleByteLength(self, value):
        length = len(value)
        self.WriteUInt8(length)
        self.pack(str(length) + 's', value.encode("utf-8"))
    
    def pack(self, fmt, data):
        return self.WriteBytes(pack(fmt, data))

    def unpack(self, fmt, length = 1):
        return unpack(fmt, self.ReadBytes(length))[0]
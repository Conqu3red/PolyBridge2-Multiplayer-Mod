from websocket import create_connection
import json
from copy import *
import uuid
import time
class ActionType:
    CREATE_EDGE = 0
    CREATE_JOINT = 1
    DELETE_EDGE = 2
    DELETE_JOINT = 3
    TRANSLATE_JOINT = 4

message_template = {
    "type":"BridgeAction",
    "content":{
        "action":ActionType.CREATE_JOINT,
        "content":""
    }
}
MODE = "listen" + "no"
ws = create_connection("ws://127.0.0.1:8181/test?username=owner")

connections = []

for i in range(200):
    connections.append(create_connection("ws://127.0.0.1:8181/test?username=user"))
input()
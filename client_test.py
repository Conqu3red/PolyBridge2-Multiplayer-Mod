from websocket import create_connection
import json
from copy import *
import uuid
import time
from StructureModels import *
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

ws = create_connection("ws://127.0.0.1:11000/a?username=test_user")
print("ws", ws.recv())
while True:
    ws.send(
        MousePositionModel.Serialize(
            {
                "username":"a",
                "position":[5,5,0],
                "pointerMode":1,
                "pointerColor":[255,0,0]
            }
        )
    )
    print("sent mouse pos")
#ws2 = create_connection("ws://127.0.0.1:8181/test?username=user2&password=aaa")
#print("ws", ws.recv())
#print("ws2", ws2.recv())
#ws.send(json.dumps({
#    "type": "ServerConfig",
#    "content":json.dumps({
#        "newPassword":"bbb",
#        "lobbyMode":2
#    })
#}))
#ws.send(json.dumps({
#    "type": "ServerConfig",
#    "content":json.dumps({
#        "userCap":20
#    })
#}))
#ws.send(json.dumps({
#    "type": "ServerInfo",
#    "content":""
#}))
#print("ws", ws.recv())
#ws.send(json.dumps({
#    "type": "KickUser",
#    "content":json.dumps({"username":"user2", "reason":"yes"})
#}))
#print("ws", ws.recv())
#print("ws2", ws2.recv())
#ws.send(json.dumps({
#    "type": "CreateInvite",
#    "content":json.dumps({
#        "uses":1
#    })
#}))
#invite = json.loads(ws.recv())["content"]
#print(invite)
#ws3 = create_connection(f"ws://127.0.0.1:8181/test?username=user3&invite={invite}")
#print("ws", ws.recv())
#ws4 = create_connection(f"ws://127.0.0.1:8181/test?username=user4&invite={invite}")
#print("ws4", ws4.recv())
#ws5 = create_connection(f"ws://127.0.0.1:8181/test?username=user5")
#ws.send(json.dumps({
#    "type": "CreateInvite",
#    "content":json.dumps({
#        "uses":1
#    })
#}))
#invite = json.loads(ws.recv())["content"]
#print(invite)





#def createJoint(x:float,y:float,z:float, m_IsAnchor=False, m_IsSplit=False):
#    guid = str(uuid.uuid4())
#    joint = {
#        "m_Pos": {
#            "x": x,
#            "y": y,
#            "z": z
#        },
#        "m_IsAnchor": m_IsAnchor,
#        "m_IsSplit": m_IsSplit,
#        "m_Guid": guid
#    }
#    return joint
#def createEdge(m_NodeA_Guid, m_NodeB_Guid, m_Material=1):
#    edge = {
#        "m_Material": m_Material,
#        "m_NodeA_Guid": m_NodeA_Guid,
#        "m_NodeAPart": 1,
#        "m_NodeB_Guid": m_NodeB_Guid,
#        "m_NodeBPart": 0,
#    }
#    return edge
#
#
#def send_action(content, action):
#    message = deepcopy(message_template)
#    message["content"]["action"] = action
#    message["content"]["content"] = json.dumps(content)
#    message["content"] = json.dumps(message["content"])
#    ws.send(json.dumps(message))
#    print(f"sent action {action}")
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
    PISTON_SLIDER_TRANSLATE = 5
    SPRING_SLIDER_TRANSLATE = 6
    SPLIT_JOINT = 7
    UNSPLIT_JOINT = 8
    SYNC_LAYOUT = 9

message_template = {
    "type":"BridgeAction",
    "content":{
        "action":ActionType.CREATE_JOINT,
        "content":""
    }
}
MODE = "listen" + "no"

ws = create_connection("ws://127.0.0.1:8181/test?username=test_user")


if MODE == "listen":
    while True:
        message = ws.recv()
        print(json.loads(message))
def createJoint(x:float,y:float,z:float, m_IsAnchor=False, m_IsSplit=False):
    guid = str(uuid.uuid4())
    joint = {
        "m_Pos": {
            "x": x,
            "y": y,
            "z": z
        },
        "m_IsAnchor": m_IsAnchor,
        "m_IsSplit": m_IsSplit,
        "m_Guid": guid
    }
    return joint
def createEdge(m_NodeA_Guid, m_NodeB_Guid, m_Material=1):
    edge = {
        "m_Material": m_Material,
        "m_NodeA_Guid": m_NodeA_Guid,
        "m_NodeAPart": 1,
        "m_NodeB_Guid": m_NodeB_Guid,
        "m_NodeBPart": 0,
    }
    return edge


def send_action(content, action):
    message = deepcopy(message_template)
    message["content"]["action"] = action
    message["content"]["content"] = json.dumps(content)
    message["content"] = json.dumps(message["content"])
    ws.send(json.dumps(message))
    print(f"sent action {action}")


# TEST CASES #

joint1 = createJoint(0,7,0)
send_action(joint1, ActionType.CREATE_JOINT)

joint2 = createJoint(0,9,0)
send_action(joint2, ActionType.CREATE_JOINT)

edge1 = createEdge(joint1["m_Guid"], joint2["m_Guid"], 1)
send_action(edge1, ActionType.CREATE_EDGE)
print("created 1st road")


joint3 = createJoint(2,7,0)
send_action(joint3, ActionType.CREATE_JOINT)

joint4 = createJoint(2,9,0)
send_action(joint4, ActionType.CREATE_JOINT)

edge2 = createEdge(joint3["m_Guid"], joint4["m_Guid"], 4)
send_action(edge2, ActionType.CREATE_EDGE)
print("created metal")


time.sleep(1)
print("deleting metal")
send_action(joint3, ActionType.DELETE_JOINT)
send_action(joint4, ActionType.DELETE_JOINT)
send_action(edge2, ActionType.DELETE_EDGE)

joint3 = createJoint(3,7,0)
send_action(joint3, ActionType.CREATE_JOINT)

joint4 = createJoint(3,9,0)
send_action(joint4, ActionType.CREATE_JOINT)



edge2 = createEdge(joint3["m_Guid"], joint4["m_Guid"], 4)
send_action(edge2, ActionType.CREATE_EDGE)
print("created metal 2")
time.sleep(1)
joint4["m_Pos"]["x"] = 4
send_action(joint4, ActionType.TRANSLATE_JOINT)
print("translated joint of metal 2")

print("spring/hydraulic test")
joint1 = createJoint(0,10,0)
send_action(joint1, ActionType.CREATE_JOINT)

joint2 = createJoint(2,10,0)
send_action(joint2, ActionType.CREATE_JOINT)

edge1 = createEdge(joint1["m_Guid"], joint2["m_Guid"], 9)
send_action(edge1, ActionType.CREATE_EDGE)
print("created spring")

print("split joint test")
print("split")
send_action(joint1, ActionType.SPLIT_JOINT)
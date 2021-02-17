from simple_websocket_server import WebSocketServer, WebSocket
import json
from urllib import parse
import uuid
import signal
import traceback
from hashlib import sha256
import socket
import time
import ssl
import sys
from optparse import OptionParser
from StructureModels import *

class MessageType:
    # different responses for ui
    ConnectionResponse = "ConnectionResponse"

    # Bridge
    BridgeAction = "BridgeAction"

    MousePosition = "MousePosition"
    
    # popups and messages
    PopupMessage = "PopupMessage"
    ConsoleMessage = "ConsoleMessage"
    TopLeftMessage = "TopLeftMessage"
    
    # owner/config management
    ServerConfig = "ServerConfig"
    ServerInfo = "ServerInfo"
    KickUser = "KickUser"
    BanUser = "BanUser"
    CreateInvite = "CreateInvite"

class LobbyMode:
    public = 0
    password_locked = 1
    invite_only = 2
    accept_only = 3


class Lobby:
    def __init__(self, name, owner):
        self.name = name
        self.owner = owner
        self.clients = {}
        self.password = None
        self.user_cap = 1_000_000
        self.accepting_connections = True
        self.lobby_mode = LobbyMode.public
        self.invites = {}
        self.clients_awaiting_layout_sync = []
        self.frozen = False
    def isOwner(self, web_scocket):
        return web_scocket == self.clients[self.owner]

    def getName(self, web_scocket):
        return list(self.clients.keys())[list(self.clients.values()).index(web_scocket)]

    def isFull(self):
        return len(list(self.clients.keys())) >= self.user_cap

    def close(self):
        for client in self.clients.values():
            client.send_message(Message.Serialize({"type":MessageType.PopupMessage, "content":stringResponse("The server was closed by the host."), "metadata":"server_closed"}))
        #self.clients[self.owner].send_message(Message.Serialize({"type":"PopupMessage", "content":"Server Closed."}))
    def print(self, value):
        print(f"{self.name} - {value}")
        


class MultiplayerServer(WebSocket):
    def handle(self):
        try:
            server_name = parse.urlsplit(self.request.path).path.replace("/", "")
            parameters = dict(parse.parse_qsl(parse.urlsplit(self.request.path).query))
            username = parameters.get("username")
            server = open_lobbies.get(server_name)
            #print(bytes(self.data))
            if server:
                if self not in list(server.clients.values()): return
                message = Message.Deserialize(bytes(self.data))
                #print(message)
                if message["type"] == MessageType.ServerInfo:
                    response = {
                        "usersConnected": len(list(server.clients.keys())),
                        "userCap": server.user_cap,
                        "acceptingConnections": server.accepting_connections,
                        "lobbyMode": server.lobby_mode,
                        "isFrozen": server.frozen,
                        "playerNames": list(server.clients.keys())
                    }
                    self.send_message(Message.Serialize({"type":MessageType.ServerInfo, "content":ServerInfoModel.Serialize(response)}))

                if message["type"] == MessageType.KickUser and server.isOwner(self):
                    #print(message)
                    content = KickUserModel.Deserialize(message["content"])
                    user = server.clients.get(content["username"])
                    if user:
                        if content["username"] == server.owner:
                            self.send_message(Message.Serialize({"type":MessageType.KickUser, "content":stringResponse(f"You cannot kick yourself!"), "metadata":"KickUser"}))
                            return
                        user.send_message(Message.Serialize({"type":MessageType.PopupMessage, "content":stringResponse(f"You were kicked from the server. Reason: {content.get('reason') or 'No Reason Provided'}"), "metadata":"server_closed"}))
                        del server.clients[content["username"]]
                        server.print(f"{content['username']} was kicked.")
                        self.send_message(Message.Serialize({"type":MessageType.KickUser, "content":stringResponse(f"Removed {content['username']}"), "metadata":"KickUser"}))
                    else:
                        self.send_message(Message.Serialize({"type":MessageType.KickUser, "content":stringResponse(f"User not found."), "metadata":"KickUser"}))


                if message["type"] == MessageType.ServerConfig and server.isOwner(self):
                    content = ServerConfigModel.Deserialize(message["content"])
                    #print(content)
                    if content["action"] == 0: server.user_cap = content["userCap"]
                    if content["action"] == 1: server.accepting_connections = content["acceptingConnections"]
                    if content["action"] == 2:
                        server.password = sha256(content["newPassword"].encode("utf-8")).hexdigest()
                        if server.lobby_mode == LobbyMode.public: server.lobby_mode = LobbyMode.password_locked
                        server.print("Owner changed password.")
                        self.send_message(Message.Serialize({"type":MessageType.ServerConfig, "content":stringResponse(f"Changed password.")}))
                    if content["action"] == 3: server.lobby_mode = content["lobbyMode"]

                if message["type"] == MessageType.CreateInvite and server.isOwner(self):
                    uses = int.from_bytes(message["content"], "little")
                    invite = str(uuid.uuid4())
                    server.invites[invite] = uses
                    self.send_message(Message.Serialize({"type":MessageType.CreateInvite, "content":stringResponse(invite)}))


                if message["type"] == MessageType.BridgeAction:
                    action_content = BridgeActionModel.Deserialize(message["content"])
                    if action_content["action"] == 11:
                        layout = LayoutModel.Deserialize(action_content["content"])
                        if server.isOwner(self):
                            server.print("Host syncing layout with all users requested.")
                            if not layout["targetAllUsers"]:
                                action_content["username"] = server.getName(self)
                                message["content"] = BridgeActionModel.Serialize(action_content)
                                new_message = Message.Serialize(message)
                                
                                while len(server.clients_awaiting_layout_sync) > 0:
                                    name = server.clients_awaiting_layout_sync.pop()
                                    client = server.clients[name]
                                    if client != self:
                                        client.send_message(new_message)
                                    
                                return
                            server.clients_awaiting_layout_sync = []
                        else:
                            name = server.getName(self)
                            if name not in server.clients_awaiting_layout_sync:
                                action_content["username"] = server.getName(self)
                                message["content"] = BridgeActionModel.Serialize(action_content)
                                new_message = Message.Serialize(message)
                                
                                server.clients_awaiting_layout_sync.append(name)
                                server.print(f"Added {name} to waiting list for sync layout")
                                server.clients[server.owner].send_message(new_message)
                            return
                    if action_content["action"] == 12:
                        server.frozen = bool.from_bytes(action_content["content"], "little")
                    #server.print(action_content)
                    action_content["username"] = server.getName(self)
                    message["content"] = BridgeActionModel.Serialize(action_content)
                    new_message = Message.Serialize(message)
                    for name, client in server.clients.items():
                        if client != self:
                            #print(f"sending to {name}")
                            client.send_message(new_message)
                if message["type"] == MessageType.MousePosition:
                    action_content = MousePositionModel.Deserialize(message["content"])
                    action_content["username"] = server.getName(self)
                    #server.print(action_content)
                    message["content"] = MousePositionModel.Serialize(action_content)
                    new_message = Message.Serialize(message)
                    for name, client in server.clients.items():
                        if client != self:
                            #print(f"sending to {name}")
                            client.send_message(new_message)
            else: # user sending data to a non existant server
                self.close()            
        except Exception as e:
            print(e)
            traceback.print_tb(e.__traceback__)
            pass
    def connected(self):
        try:
            server_name = parse.urlsplit(self.request.path).path.replace("/", "")
            parameters = dict(parse.parse_qsl(parse.urlsplit(self.request.path).query))
            username = parameters.get("username")
            user_id = parameters.get("id")
            password = parameters.get("password", "")
            invite = parameters.get("invite")
            server = open_lobbies.get(server_name, None)
            if server: 
                # if server exists, connect to it
                #print(list(server.clients.keys()))
                orig = username
                n = 1
                while username in list(server.clients.keys()):
                    username = orig
                    n += 1
                    username += str(n)
                #print(username)
                if server.lobby_mode == LobbyMode.password_locked and server.password != sha256(password.encode("utf-8")).hexdigest():
                    self.send_message(Message.Serialize({"type":MessageType.ConnectionResponse, "content":stringResponse("The password provided was incorrect."), "metadata":"server_closed"}))
                elif server.lobby_mode == LobbyMode.invite_only and invite not in list(server.invites.keys()):
                    self.send_message(Message.Serialize({"type":MessageType.ConnectionResponse, "content":stringResponse("The invite provided was invalid."), "metadata":"server_closed"}))
                else:
                    if server.isFull():
                        self.send_message(Message.Serialize({"type":MessageType.ConnectionResponse, "content":stringResponse(f"Sorry but this lobby is full with {server.user_cap}/{server.user_cap} Players"), "metadata":"server_closed"}))
                        return
                    if not server.accepting_connections:
                        self.send_message(Message.Serialize({"type":MessageType.ConnectionResponse, "content":stringResponse(f"This lobby is not currently accepting connections"), "metadata":"server_closed"}))
                        return

                    server.clients[username] = self
                    self.send_message(Message.Serialize({"type":MessageType.ConnectionResponse, "content":stringResponse(f"Connected to server '{server_name}'"), "metadata":"connected"}))
                    for client in server.clients.values():
                        if client != self:
                            client.send_message(Message.Serialize({"type":"TopLeftMessage", "content":stringResponse(f"User '{username}' Connected")}))
                    server.print(f"{username} ({self.address[0]}:{self.address[1]}) connected.")
                    if invite:
                        server.invites[invite] -= 1
                        if server.invites[invite] < 1:
                            del server.invites[invite]
            else:
                #if server doesn't exist, create it and assign as owner
                open_lobbies[server_name] = Lobby(server_name, username)
                server = open_lobbies[server_name]
                if password:
                    server.password = sha256(password.encode("utf-8")).hexdigest()
                    server.lobby_mode = LobbyMode.password_locked
                server.clients[username] = self
                self.send_message(Message.Serialize({"type":MessageType.ConnectionResponse, "content":stringResponse(f"Opened server {server_name}"), "metadata":"owner"}))
                server.print(f"{username} ({self.address[0]}:{self.address[1]}) created this server.")
        except Exception as e:
            print(e)
            traceback.print_tb(e.__traceback__)
            pass
    def handle_close(self):
        for name, server in open_lobbies.items():
            if server.clients[server.owner] == self:
                server.print(f"Server was closed by owner.")
                server.close()
                del open_lobbies[server.name]
                break
            for name, client in server.clients.items():
                if client == self:
                    server.print(f"{name} ({self.address[0]}:{self.address[1]}) disconnected.")
                    
                    for _client in server.clients.values():
                        if _client != self:
                            _client.send_message(Message.Serialize({"type":MessageType.TopLeftMessage, "content":stringResponse(f"User '{name}' Disconnected.")}))
                    
                    del server.clients[name]


open_lobbies = {}
address = socket.gethostbyname(socket.gethostname())

parser = OptionParser(usage='usage: %prog [options]', version='%prog 1.0.0')
parser.add_option('--ssl', default=0, type='int', action='store', dest='ssl', help='ssl (1: on, 0: off (default))')
parser.add_option('--cert', default='./cert.pem', type='string', action='store', dest='cert', help='cert (./cert.pem)')
parser.add_option('--key', default='./key.pem', type='string', action='store', dest='key', help='key (./key.pem)')
parser.add_option('--ver', default=ssl.PROTOCOL_TLSv1, type=int, action='store', dest='ver', help='ssl version')
parser.add_option('--localhost', default=False, action='store_true', dest='localhost', help='whether to use localhost')
parser.add_option('--port', default=11000, type=int, action='store', dest='port', help='server port')
(options, args) = parser.parse_args()

sslopts = {}
if options.ssl == 1:
    sslopts = dict(certfile=options.cert, keyfile=options.key, ssl_version=options.ver)
if options.localhost: address = "127.0.0.1"
print(f"Running at {address}:{options.port} {'(using TLS/SSL)' if options.ssl == 1 else '(not using TLS/SSL)'}")
server = WebSocketServer(address, options.port, MultiplayerServer, **sslopts)

def keyboardInterruptHandler(signal, frame):
    print("KeyboardInterrupt (ID: {}) has been caught. Cleaning up...".format(signal))
    server.close()
    print("Clean up complete, terminating...")
    sys.exit()
signal.signal(signal.SIGINT, keyboardInterruptHandler)

server.serve_forever()
from simple_websocket_server import WebSocketServer, WebSocket
import json
from urllib import parse
import uuid
import signal
import traceback
from hashlib import sha256
import socket
import time
class MessageType:
    # Bridge
    BridgeAction = "BridgeAction"
    
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

def keyboardInterruptHandler(signal, frame):
    print("KeyboardInterrupt (ID: {}) has been caught. Cleaning up...".format(signal))
    for lobby_name in open_lobbies.keys():
        lobby = open_lobbies[lobby_name]
        lobby.clients[lobby.owner].send_message(json.dumps({"type":MessageType.PopupMessage, "content":"The Hosting Server was Terminated.", "metadata":"server_closed"}))
        lobby.close()
    
    print("Clean up complete, terminating...")
    exit(0)
signal.signal(signal.SIGINT, keyboardInterruptHandler)

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
    def isOwner(self, web_scocket):
        return web_scocket == self.clients[self.owner]

    def getName(self, web_scocket):
        return list(self.clients.keys())[list(self.clients.values()).index(web_scocket)]

    def isFull(self):
        return len(list(self.clients.keys())) >= self.user_cap

    def close(self):
        for client in self.clients.values():
            client.send_message(json.dumps({"type":MessageType.PopupMessage, "content":"The server was closed by the host.", "metadata":"server_closed"}))
        #self.clients[self.owner].send_message(json.dumps({"type":"PopupMessage", "content":"Server Closed."}))
    def print(self, value):
        print(f"{self.name} - {value}")
        


class MultiplayerServer(WebSocket):
    def handle(self):
        try:
            server_name = parse.urlsplit(self.request.path).path.replace("/", "")
            parameters = dict(parse.parse_qsl(parse.urlsplit(self.request.path).query))
            username = parameters.get("username")
            server = open_lobbies[server_name]
            #print(self.data)
            if server:
                if self not in list(server.clients.values()): return
                try:
                    message = json.loads(self.data)
                    #print(message)
                except json.JSONDecodeError:
                    return

                if message["type"] == MessageType.ServerInfo and server.isOwner(self):
                    mode_str = ""
                    if server.lobby_mode == LobbyMode.public: mode_str = "public" 
                    if server.lobby_mode == LobbyMode.password_locked: mode_str = "password_locked"
                    if server.lobby_mode == LobbyMode.invite_only: mode_str = "invite_only" 
                    if server.lobby_mode == LobbyMode.accept_only: mode_str = "accept_only" 

                    console_message = f"Lobby Mode: {mode_str}\nAccepting Connections: {server.accepting_connections}\n"
                    for user in list(server.clients.keys()):
                        console_message += f"username: {user}\n"
                    console_message += f"{len(list(server.clients.keys()))}/{server.user_cap} Players Connected."
                    self.send_message(json.dumps({"type":MessageType.ConsoleMessage, "content":console_message}))

                if message["type"] == MessageType.KickUser and server.isOwner(self):
                    #print(message)
                    content = json.loads(message["content"])
                    user = server.clients.get(content["username"])
                    if user:
                        user.send_message(json.dumps({"type":MessageType.PopupMessage, "content":f"You were kicked from the server. Reason: {content.get('reason') or 'No Reason Provided'}", "metadata":"server_closed"}))
                        del server.clients[content["username"]]
                        server.print(f"{content['username']} was kicked.")
                        self.send_message(json.dumps({"type":MessageType.ConsoleMessage, "content":f"Removed {content['username']}"}))
                    else:
                        self.send_message(json.dumps({"type":MessageType.ConsoleMessage, "content":f"User not found."}))


                if message["type"] == MessageType.ServerConfig and server.isOwner(self):
                    content = json.loads(message["content"])
                    if content.get("userCap") != None: server.user_cap = content["userCap"]
                    if content.get("acceptingConnections") != None: server.accepting_connections = content["acceptingConnections"]
                    if content.get("newPassword") != None:
                        server.password = content["newPassword"]
                        server.print("Owner changed password.")
                        self.send_message(json.dumps({"type":MessageType.ConsoleMessage, "content":f"Changed password."}))
                    if content.get("lobbyMode") != None: server.lobby_mode = content["lobbyMode"]

                if message["type"] == MessageType.CreateInvite and server.isOwner(self):
                    content = json.loads(message["content"])
                    invite = str(uuid.uuid4())
                    server.invites[invite] = content["uses"]
                    self.send_message(json.dumps({"type":MessageType.ConsoleMessage, "content":invite}))


                if message["type"] == MessageType.BridgeAction:
                    action_content = json.loads(message["content"])
                    if action_content["action"] == 11:
                        layout = json.loads(action_content["content"])
                        if server.isOwner(self):
                            server.print("Host syncing layout with all users requested.")
                            if not layout["targetAllUsers"]:
                                action_content["username"] = server.getName(self)
                                message["content"] = json.dumps(action_content)
                                new_message = json.dumps(message)
                                
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
                                message["content"] = json.dumps(action_content)
                                new_message = json.dumps(message)
                                
                                server.clients_awaiting_layout_sync.append(name)
                                server.print(f"Added {name} to waiting list for sync layout")
                                server.clients[server.owner].send_message(new_message)
                            return
                    #server.print(action_content)
                    for name, client in server.clients.items():
                        action_content["username"] = server.getName(self)
                        message["content"] = json.dumps(action_content)
                        new_message = json.dumps(message)
                        
                        if client != self:
                            #print(f"sending to {name}")
                            client.send_message(new_message)
                        
        except Exception as e:
            pass
            traceback.print_tb(e.__traceback__)
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
                    self.send_message(json.dumps({"type":MessageType.ConsoleMessage, "content":"The password provided was incorrect.", "metadata":"server_closed"}))
                elif server.lobby_mode == LobbyMode.invite_only and invite not in list(server.invites.keys()):
                    self.send_message(json.dumps({"type":MessageType.ConsoleMessage, "content":"The invite provided was invalid.", "metadata":"server_closed"}))
                else:
                    if server.isFull():
                        self.send_message(json.dumps({"type":MessageType.ConsoleMessage, "content":f"Sorry but this lobby is full with {server.user_cap}/{server.user_cap} Players", "metadata":"server_closed"}))
                        return
                    if not server.accepting_connections:
                        self.send_message(json.dumps({"type":MessageType.ConsoleMessage, "content":f"This lobby is not currently accepting connections", "metadata":"server_closed"}))
                        return

                    server.clients[username] = self
                    self.send_message(json.dumps({"type":MessageType.ConsoleMessage, "content":f"Connected to server '{server_name}'", "metadata":"connected"}))
                    for client in server.clients.values():
                        if client != self:
                            client.send_message(json.dumps({"type":"TopLeftMessage", "content":f"User '{username}' Connected"}))
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
                self.send_message(json.dumps({"type":MessageType.ConsoleMessage, "content":f"Opened server {server_name}", "metadata":"owner"}))
                server.print(f"{username} ({self.address[0]}:{self.address[1]}) created this server.")
        except Exception as e:
            print(e)
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
                            _client.send_message(json.dumps({"type":MessageType.TopLeftMessage, "content":f"User '{name}' Disconnected."}))
                    
                    del server.clients[name]


open_lobbies = {}
address = socket.gethostbyname(socket.gethostname())
port = 11000
print(f"Running at {address}:{port}")
server = WebSocketServer(address, port, MultiplayerServer)
server.serve_forever()
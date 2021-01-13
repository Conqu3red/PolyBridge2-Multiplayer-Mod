from simple_websocket_server import WebSocketServer, WebSocket
import json
from urllib import parse
import uuid
import signal

def keyboardInterruptHandler(signal, frame):
    print("KeyboardInterrupt (ID: {}) has been caught. Cleaning up...".format(signal))
    for lobby_name in open_lobbies.keys():
        lobby = open_lobbies[lobby_name]
        lobby.clients[lobby.owner].send_message(json.dumps({"type":"PopupMessage", "content":"The Hosting Server was Terminated.", "metadata":"server_closed"}))
        lobby.close()
    print("Clean up complete, terminating...")
    exit(0)
signal.signal(signal.SIGINT, keyboardInterruptHandler)

class Lobby:
    def __init__(self, name, owner):
        self.name = name
        self.owner = owner
        self.clients = {}
    def close(self):
        for client in self.clients.values():
            client.send_message(json.dumps({"type":"PopupMessage", "content":"The server was closed by the host.", "metadata":"server_closed"}))
        #self.clients[self.owner].send_message(json.dumps({"type":"PopupMessage", "content":"Server Closed."}))
    def print(self, value):
        print(f"{self.name} - {value}")


class MultiplayerServer(WebSocket):
    def handle(self):
        server_name = parse.urlsplit(self.request.path).path.replace("/", "")
        parameters = dict(parse.parse_qsl(parse.urlsplit(self.request.path).query))
        username = parameters.get("username")
        server = open_lobbies[server_name]
        if server:
            try:
                message = json.loads(self.data)
            except json.JSONDecodeError:
                return
            if message["type"] == "BridgeAction":
                action_content = json.loads(message["content"])
                #server.print(action_content)
                for client in list(server.clients.values()):
                    if client != self:
                        client.send_message(self.data)
   
    def connected(self):
        server_name = parse.urlsplit(self.request.path).path.replace("/", "")
        parameters = dict(parse.parse_qsl(parse.urlsplit(self.request.path).query))
        username = parameters.get("username")
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
            server.clients[username] = self
            self.send_message(json.dumps({"type":"ConsoleMessage", "content":f"Connected to server '{server_name}'"}))
            for client in server.clients.values():
                if client != self:
                    client.send_message(json.dumps({"type":"TopLeftMessage", "content":f"User '{username}' Connected"}))
            server.print(f"{username} ({self.address[0]}:{self.address[1]}) connected.")
        else:
            #if server doesn't exist, create it and assign as owner
            open_lobbies[server_name] = Lobby(server_name, username)
            server = open_lobbies[server_name]
            server.clients[username] = self
            self.send_message(json.dumps({"type":"ConsoleMessage", "content":f"Opened server {server_name}"}))
            server.print(f"{username} ({self.address[0]}:{self.address[1]}) created this server.")

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
                            _client.send_message(json.dumps({"type":"TopLeftMessage", "content":f"User '{name}' Disconnected."}))
                    
                    del server.clients[name]


open_lobbies = {}

server = WebSocketServer('127.0.0.1', 8181, MultiplayerServer)
server.serve_forever()
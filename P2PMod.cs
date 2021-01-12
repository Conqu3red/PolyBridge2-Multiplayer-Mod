using System;
using BepInEx;
using Logger = BepInEx.Logging.Logger;
using PolyTechFramework;
using UnityEngine;
using HarmonyLib;
using System.Reflection;
using System.Collections.Generic;
using BepInEx.Configuration;
using PolyPhysics.Viewers;
using Poly.Physics;
using Poly.Math;
using PolyPhysics;
using Common.Class;
using Common.Extension;
using System.Collections;
using System.IO;
using Fleck;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using ConsoleMod;
using System.Collections.Concurrent;

namespace P2PMod
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    // Specify the mod as a dependency of PTF
    [BepInDependency(PolyTechMain.PluginGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency(ConsoleMod.ConsoleMod.PluginGuid, BepInDependency.DependencyFlags.HardDependency)]
    // This Changes from BaseUnityPlugin to PolyTechMod.
    // This superclass is functionally identical to BaseUnityPlugin, so existing documentation for it will still work.
    public class P2PMod : PolyTechMod
    {
        public new const string
            PluginGuid = "org.bepinex.plugins.P2PMod",
            PluginName = "P2P Mod",
            PluginVersion = "0.1.0";
        
        public static ConfigDefinition
            modEnabledDef = new ConfigDefinition("P2P Mod", "Enable/Disable Mod");
        public static ConfigEntry<bool>
            modEnabled;
        public static ConfigEntry<string>
            username;
        public static P2PMod instance;
        public WebSocketServer server;
        public bool serverEnabled = false;
        public bool clientEnabled = false;
        public ServerCommunication communication;
        List<Fleck.IWebSocketConnection> sockets = new List<IWebSocketConnection> ();
        //ConcurrentBag<Fleck.IWebSocketConnection> sockets = new ConcurrentBag<IWebSocketConnection> ();

        public Dictionary<actionType, bool> ClientRecieving = new Dictionary<actionType, bool> {};
        
        void Awake()
        {
			if (instance == null) instance = this;
            // Use this if you wish to make the mod trigger cheat mode ingame.
            // Set this true if your mod effects physics or allows mods that you can't normally do.
            isCheat = false;
           
            modEnabled = Config.Bind(modEnabledDef, true, new ConfigDescription("Enable Mod"));
            username = Config.Bind("P2P Mod", "username", "name");

            modEnabled.SettingChanged += onEnableDisable;

            harmony = new Harmony("org.bepinex.plugins.P2PMod");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            PolyTechMain.registerMod(this);
            Logger.LogInfo("P2P MOD");

            uConsole.RegisterCommand("enable_server", new uConsole.DebugCommand(EnableServer));
            uConsole.RegisterCommand("disable_server", new uConsole.DebugCommand(DisableServer));
            uConsole.RegisterCommand("enable_client", new uConsole.DebugCommand(EnableClient));
            uConsole.RegisterCommand("disable_client", new uConsole.DebugCommand(DisableClient));
            uConsole.RegisterCommand("list_connections", new uConsole.DebugCommand(ListConnections));

        }

        public void Update(){
            if (instance.communication != null) instance.communication.Update();
        }

        /// <summary>
        /// Method called after connection with server was established.
        /// </summary>
        public void OnConnectedToServer()
        {
            instance.communication.Lobby.OnConnectedToServer -= instance.OnConnectedToServer;

            instance.communication.Lobby.OnEchoMessage += instance.OnReceivedEchoMessage;
        }
        public void OnReceivedEchoMessage(EchoMessageModel message)
        {
            BridgeEdgeProxy edgeProxy;
            BridgeEdge edge;
            BridgeSpringProxy springProxy;
            BridgeSpring spring;
            BridgeJointProxy jointProxy;
            BridgeJoint joint;
            instance.Logger.LogInfo($"<CLIENT> received {message.action}");
            switch (message.action){
                case actionType.CREATE_EDGE:
                    instance.ClientRecieving[actionType.CREATE_EDGE] = true;
                    edgeProxy = JsonUtility.FromJson<BridgeEdgeProxy>(message.content);
                    BridgeEdges.CreateEdgeFromProxy(edgeProxy);
                    instance.ClientRecieving[actionType.CREATE_EDGE] = false;
                    
                    break;
                case actionType.CREATE_JOINT:
                    instance.ClientRecieving[actionType.CREATE_JOINT] = true;
                    jointProxy = JsonUtility.FromJson<BridgeJointProxy>(message.content);
                    BridgeJoints.CreateJointFromProxy(jointProxy);
                    instance.ClientRecieving[actionType.CREATE_JOINT] = false;
                    break;
                case actionType.DELETE_EDGE:
                    edgeProxy = JsonUtility.FromJson<BridgeEdgeProxy>(message.content);
                    edge = BridgeEdges.FindEnabledEdgeByJointGuids(edgeProxy.m_NodeA_Guid, edgeProxy.m_NodeB_Guid, edgeProxy.m_Material);
                    edge.ForceDisable();
                    edge.SetStressColor(0f);
                    break;
                case actionType.DELETE_JOINT:
                    jointProxy = JsonUtility.FromJson<BridgeJointProxy>(message.content);
                    joint = BridgeJoints.FindByGuid(jointProxy.m_Guid);
                    joint.gameObject.SetActive(false);
                    break;
                case actionType.TRANSLATE_JOINT:
                    jointProxy = JsonUtility.FromJson<BridgeJointProxy>(message.content);
                    BridgeJointMovement.m_SelectedJoint = BridgeJoints.FindByGuid(jointProxy.m_Guid);
                    BridgeJointMovement.m_SelectedJoint.transform.position = jointProxy.m_Pos;
                    BridgeJointMovement.FinalizeMovement();
                    break;
                default:
                    instance.Logger.LogError("<CLIENT> recieved unexpected action");
                    break;
            }
            
        }



        
        public static void EnableServer(){
            if (instance.serverEnabled) {
                uConsole.Log("Server is already enabled");
                return;
            }
            instance.server = new WebSocketServer($"ws://127.0.0.1:8181/{username.Value}");
            
            
            instance.server.Start(socket =>
            {
                socket.OnOpen = () => {
                    instance.Logger.LogInfo("<SERVER> New Connection Opened");
                    instance.sockets.Add(socket);
                };
                socket.OnClose = () => {
                    instance.Logger.LogInfo("<SERVER> A Connection was Closed");
                    instance.sockets.Remove(socket);
                    //List<IWebSocketConnection> temp_store = new List<IWebSocketConnection>();
                    //while (!instance.sockets.IsEmpty){
                    //    instance.sockets.TryTake(out var t);
                    //    temp_store.Add(t);
                    //}
                    //foreach (var s in temp_store){
                    //    if (s == socket) continue;
                    //    instance.sockets.Add(s);
                    //}
                };
                socket.OnMessage = message => {
                    var message_decoded = JsonUtility.FromJson<MessageModel>(message);
                    if (message_decoded.method == LobbyMessaging.Echo){
                        var content = JsonUtility.FromJson<EchoMessageModel>(message_decoded.message);
                        instance.Logger.LogInfo($"<SERVER> received {content.action}");
                        /*switch (content.action){
                            case actionType.CREATE_EDGE:
                                //var edge = JsonUtility.FromJson<BridgeEdgeProxy>(content.content);
                                break;
                            case actionType.CREATE_JOINT:
                                //var joint = JsonUtility.FromJson<BridgeJointProxy>(content.content);
                                break;
                            case actionType.DELETE_EDGE:
                                //var edge = JsonUtility.FromJson<BridgeEdgeProxy>(content.content);
                                break;
                            case actionType.DELETE_JOINT:
                                //var joint = JsonUtility.FromJson<BridgeJointProxy>(content.content);
                                break;
                            default:
                                instance.Logger.LogError("Invalid Action");
                                break;
                        }*/
                    }
                    //socket.Send(message);
                    foreach (var s in instance.sockets){
                        if (s.ConnectionInfo.Id == socket.ConnectionInfo.Id) continue;
                        s.Send(message);
                    }
                };
            });
            uConsole.Log("Enabled Server");
            instance.serverEnabled = true;
        }
        public static void DisableServer(){
            if (instance.server == null){
                uConsole.Log("Server is already Disabled");
                return;
            }
            instance.server.ListenerSocket.Close();
            instance.server.Dispose();
            instance.server = null;
            uConsole.Log("Disabled Server");
            instance.serverEnabled = false;
        }
        public static void ListConnections(){
            if (!instance.serverEnabled) return;
            foreach (Fleck.IWebSocketConnection connection in instance.sockets){
                uConsole.Log($"IP: {connection.ConnectionInfo.ClientIpAddress} Path: {connection.ConnectionInfo.Path}");
            }
        }

        public static void EnableClient(){
            if (instance.clientEnabled){
                uConsole.Log("Client is already enabled");
                return;
            }
            instance.communication = new ServerCommunication();
            instance.communication.useLocalhost = true;
            instance.communication.port = 8181;
            instance.communication.Init();
            instance.communication.Lobby.OnConnectedToServer += instance.OnConnectedToServer;
            instance.communication.ConnectToServer();
            instance.OnConnectedToServer();
            uConsole.Log("Enabled Client");
            instance.clientEnabled = true;
        }
        public static void DisableClient(){
            if (!instance.clientEnabled){
                uConsole.Log("Client is already disabled.");
                return;
            }
            instance.communication.client.ws.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "closed",
                new CancellationToken()
            );
            instance.communication = null;
            uConsole.Log("Disabled Client");
            instance.clientEnabled = false;
        }

        public void onEnableDisable(object sender, EventArgs e)
        {
            this.isEnabled = modEnabled.Value;

            if (modEnabled.Value)
            {
                enableMod();
            }
            else
            {
                disableMod();
            }
        }
        public override void enableMod() 
        {
            modEnabled.Value = true;
        }
        // Use this method to execute code that will be ran when the mod is disabled.
        public override void disableMod() 
        {
            modEnabled.Value = false;
        }
        [HarmonyPatch(typeof(GameStateManager), "ChangeState")]
        public static class EnterBuildStatePatch {
            public static void Prefix(GameState state, ref GameState ___m_GameState){
                var prevState = ___m_GameState;
                if (state != GameState.BUILD) return;
                if (prevState != GameState.SIM && prevState != GameState.SANDBOX) return;
                //instance.Logger.LogInfo($"changing to {state} from {prevState}");
                instance.ClientRecieving[actionType.CREATE_EDGE] = true;
                instance.ClientRecieving[actionType.CREATE_JOINT] = true;
            }
            public static void Postfix(GameState state, ref GameState ___m_PrevGameState){
                var prevState = ___m_PrevGameState;
                if (state != GameState.BUILD) return;
                if (prevState != GameState.SIM && prevState != GameState.SANDBOX) return;
                //instance.Logger.LogInfo($"changed to {state} from {prevState}");
                instance.ClientRecieving[actionType.CREATE_EDGE] = false;
                instance.ClientRecieving[actionType.CREATE_JOINT] = false;
            }
        }


        [HarmonyPatch(typeof(BridgeEdges), "CreateEdge")]
        public static class CreateEdgePatch {
            public static void Postfix(
                BridgeJoint jointA, 
                BridgeJoint jointB, 
                BridgeMaterialType materialType,
                ref BridgeEdge __result,
                Edge physicsEdge_onlyUsedWhenBreakingEdgesInSimulation = null
            ){
                if (!instance.clientEnabled) return;
                instance.ClientRecieving.TryGetValue(actionType.CREATE_EDGE, out var ClientIsRecieving);
                if (ClientIsRecieving) return;
                var edge = new BridgeEdgeProxy(__result);
                var message = new EchoMessageModel {
                    action = actionType.CREATE_EDGE,
                    content = JsonUtility.ToJson(edge)
                };
                instance.Logger.LogInfo("<CLIENT> sending CREATE_EDGE");
                instance.communication.Lobby.EchoMessage(message);
        
            }
        }



        [HarmonyPatch(typeof(BridgeJoints), "CreateJoint")]
        public static class CreateJointPatch {
            public static void Postfix(
                Vector3 pos, 
                string guid,
                ref BridgeJoint __result
            ){
                if (!instance.clientEnabled) return;
                instance.ClientRecieving.TryGetValue(actionType.CREATE_JOINT, out var ClientIsRecieving);
                if (ClientIsRecieving) return;
                var joint = new BridgeJointProxy(__result);
                var message = new EchoMessageModel {
                    action = actionType.CREATE_JOINT,
                    content = JsonUtility.ToJson(joint)
                };
                instance.Logger.LogInfo("<CLIENT> sending CREATE_JOINT");
                instance.communication.Lobby.EchoMessage(message);
        
            }
        }

        [HarmonyPatch(typeof(BridgeSelectionSet), "DeleteSelectionSet")]
        public static class EdgeAndJointDeletePatch {
            public static void Prefix() {
                if (!instance.clientEnabled) return;
                List<BridgeJoint> list = new List<BridgeJoint>();
                // firstly pretend to delete everything so we can see what is being deleted
		        foreach (BridgeJoint bridgeJoint in BridgeSelectionSet.m_Joints)
		        {
		        	if (!bridgeJoint.m_IsAnchor)
		        	{
		        		bridgeJoint.gameObject.SetActive(false);
		        		list.Add(bridgeJoint);
		        	}
		        }
		        foreach (BridgeEdge bridgeEdge in BridgeSelectionSet.m_Edges)
		        {
		        	bridgeEdge.ForceDisable();
		        	//bridgeEdge.SetStressColor(0f);
		        }
		        BridgeEdges.UpdateManual();
		        List<BridgeJoint> orphanedJoints = BridgeJoints.GetOrphanedJoints();

                //reinstate the joints
                foreach (BridgeJoint bridgeJoint in BridgeSelectionSet.m_Joints)
		        {
		        	if (!bridgeJoint.m_IsAnchor)
		        	{
		        		bridgeJoint.gameObject.SetActive(true);
		        	}
		        }
		        foreach (BridgeEdge bridgeEdge in BridgeSelectionSet.m_Edges)
		        {
		        	bridgeEdge.ForceEnable();
		        	//bridgeEdge.SetStressColor(0f);
		        }
                BridgeEdges.UpdateManual();


		        list.AddRange(orphanedJoints);
		        foreach (BridgeJoint j in list){

                    var joint = new BridgeJointProxy(j);
                    var message = new EchoMessageModel {
                        action = actionType.DELETE_JOINT,
                        content = JsonUtility.ToJson(joint)
                    };
                    instance.Logger.LogInfo("<CLIENT> sending DELETE_JOINT");
                    instance.communication.Lobby.EchoMessage(message);
                }

                foreach (BridgeEdge e in BridgeSelectionSet.m_Edges){
                    var joint = new BridgeEdgeProxy(e);
                    var message = new EchoMessageModel {
                        action = actionType.DELETE_EDGE,
                        content = JsonUtility.ToJson(joint)
                    };
                    instance.Logger.LogInfo("<CLIENT> sending DELETE_EDGE");
                    instance.communication.Lobby.EchoMessage(message);
                    
                }
                

            }

            [HarmonyPatch(typeof(BridgeJointMovement), "FinalizeMovement")]
            public static class TranslateJointPatch {
                public static void Prefix(){
                    if (!instance.clientEnabled) return;
                    Vector3 translation = BridgeJointMovement.m_SelectedJoint.transform.position - BridgeJointMovement.m_SelectedJoint.m_BuildPos;
		            if (!Mathf.Approximately(translation.magnitude, 0f)){
                        var joint = new BridgeJointProxy(BridgeJointMovement.m_SelectedJoint);
                        joint.m_Pos = BridgeJointMovement.m_SelectedJoint.transform.position;
                        var message = new EchoMessageModel {
                            action = actionType.TRANSLATE_JOINT,
                            content = JsonUtility.ToJson(joint)
                        };
                        instance.Logger.LogInfo("<CLIENT> sending TRANSLATE_JOINT");
                        instance.communication.Lobby.EchoMessage(message);
                    }
                }
            }
        }
        Harmony harmony;
    }

    public enum actionType {
        CREATE_EDGE,
        CREATE_JOINT,
        DELETE_EDGE,
        DELETE_JOINT,
        TRANSLATE_JOINT,
	    PISTON_SLIDER_TRANSLATE,
	    SPRING_SLIDER_TRANSLATE,
	    SPLIT_JOINT,
	    UNSPLIT_JOINT
    }
    
    
}
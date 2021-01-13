using System;
using BepInEx;
using Logger = BepInEx.Logging.Logger;
using PolyTechFramework;
using UnityEngine;
using HarmonyLib;
using System.Reflection;
using System.Collections.Generic;
using BepInEx.Configuration;
using PolyPhysics;
using System.Net.WebSockets;
using System.Threading;

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
        public static string ClientName = "";
        public bool clientEnabled = false;
        public ServerCommunication communication;

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
            uConsole.RegisterCommand("name", new uConsole.DebugCommand(Name));
            uConsole.RegisterCommand("connect", new uConsole.DebugCommand(Connect));
            uConsole.RegisterCommand("disconnect", new uConsole.DebugCommand(Disconnect));
            uConsole.RegisterCommand("connection_info", new uConsole.DebugCommand(ConnectionInfo));
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
            instance.communication.Lobby.OnBridgeAction += instance.OnBridgeAction;
        }
        public void OnBridgeAction(BridgeActionModel message)
        {
            BridgeEdgeProxy edgeProxy;
            BridgeEdge edge;
            BridgeSpringProxy springProxy;
            PistonProxy pistonProxy;
            BridgeJointProxy jointProxy;
            BridgeJoint joint;
            instance.Logger.LogInfo($"<CLIENT> received {message.action}");
            switch (message.action){
                case actionType.CREATE_EDGE:
                    instance.ClientRecieving[actionType.CREATE_EDGE] = true;
                    edgeProxy = JsonUtility.FromJson<BridgeEdgeProxy>(message.content);
                    //BridgeEdges.CreateEdgeFromProxy(edgeProxy);
                    BridgeEdge edgeFromJoints = BridgeEdges.GetEdgeFromJoints(
                        BridgeJoints.FindByGuid(edgeProxy.m_NodeA_Guid),
                        BridgeJoints.FindByGuid(edgeProxy.m_NodeB_Guid)
                    );
                    if (edgeFromJoints){
                        edgeFromJoints.ForceDisable();
                    }
                    edge = BridgeEdges.CreateEdgeWithPistonOrSpring(
                        BridgeJoints.FindByGuid(edgeProxy.m_NodeA_Guid),
                        BridgeJoints.FindByGuid(edgeProxy.m_NodeB_Guid),
                        edgeProxy.m_Material
                    );
                    
                    if (edge.IsPiston())
		            {
		            	Pistons.GetPistonOnEdge(edge).m_Slider.MakeVisible();
		            }
		            if (edge.IsSpring())
		            {
		            	edge.m_SpringCoilVisualization.m_Slider.MakeVisible();
		            }

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
                case actionType.SPRING_SLIDER_TRANSLATE:
                    springProxy = JsonUtility.FromJson<BridgeSpringProxy>(message.content);
                    var spring = BridgeEdges.FindEnabledEdgeByJointGuids(
                        springProxy.m_NodeA_Guid,
                        springProxy.m_NodeB_Guid,
                        BridgeMaterialType.SPRING
                    );
                    spring.m_SpringCoilVisualization.m_Slider.SetNormalizedValue(springProxy.m_NormalizedValue);
				    spring.m_SpringCoilVisualization.UpdateFreeLengthFromSliderPos();
                    spring.m_SpringCoilVisualization.MaybeRecreateLinks();
				    spring.m_SpringCoilVisualization.UpdateLinks();
                    break;
                case actionType.PISTON_SLIDER_TRANSLATE:
                    pistonProxy = JsonUtility.FromJson<PistonProxy>(message.content);
                    var piston = Pistons.GetPistonOnEdge(BridgeEdges.FindEnabledEdgeByJointGuids(
                        pistonProxy.m_NodeA_Guid,
                        pistonProxy.m_NodeB_Guid,
                        BridgeMaterialType.HYDRAULICS
                    ));
                    piston.m_Slider.SetNormalizedValue(pistonProxy.m_NormalizedValue);
                    piston.m_Slider.MakeVisible();
                    break;
                default:
                    instance.Logger.LogError("<CLIENT> recieved unexpected action");
                    break;
            }
            
        }
        public static void Name(){
            if (uConsole.GetNumParameters() == 1){
                ClientName = uConsole.GetString();
                uConsole.Log($"Set name to {ClientName}");
                return;
            }
            uConsole.Log($"Name is: {ClientName}");
            
        }
        public static void Connect(){
            if (instance.clientEnabled){
                uConsole.Log("Already Connected to a server");
                return;
            }
            if (ClientName == ""){
                uConsole.Log("You need to set a name using name <name>");
                return;
            }
            if (uConsole.GetNumParameters() < 3){
                uConsole.Log("Usage: Connect <host_ip> <port> <server_name>");
                return;
            }
            string hostIP = uConsole.GetString();
            int port = uConsole.GetInt();
            string server_name = uConsole.GetString();
            instance.communication = new ServerCommunication();
            instance.communication.useLocalhost = false;
            instance.communication.hostIP = hostIP;
            instance.communication.path = $"{server_name}?username={ClientName}";
            instance.communication.port = port;
            instance.communication.Init();
            instance.communication.Lobby.OnConnectedToServer += instance.OnConnectedToServer;
            instance.communication.ConnectToServer();
            instance.OnConnectedToServer();
            //uConsole.Log("Enabled Client");
            instance.clientEnabled = true;
        }
        public static void Disconnect(){
            if (!instance.clientEnabled){
                uConsole.Log("You aren't connected to anything.");
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
        public static void ConnectionInfo(){
            if (!instance.clientEnabled) return;
            uConsole.Log($"Connected to {instance.communication.hostIP}:{instance.communication.port}");
            uConsole.Log($"Connected to server {instance.communication.path}");
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
                if (Bridge.IsSimulating()) return;
                if (physicsEdge_onlyUsedWhenBreakingEdgesInSimulation) return;
                var edge = new BridgeEdgeProxy(__result);
                var message = new BridgeActionModel {
                    action = actionType.CREATE_EDGE,
                    content = JsonUtility.ToJson(edge)
                };
                instance.Logger.LogInfo("<CLIENT> sending CREATE_EDGE");
                instance.communication.Lobby.SendBridgeAction(message);
        
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
                var message = new BridgeActionModel {
                    action = actionType.CREATE_JOINT,
                    content = JsonUtility.ToJson(joint)
                };
                instance.Logger.LogInfo("<CLIENT> sending CREATE_JOINT");
                instance.communication.Lobby.SendBridgeAction(message);
        
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
                    var message = new BridgeActionModel {
                        action = actionType.DELETE_JOINT,
                        content = JsonUtility.ToJson(joint)
                    };
                    instance.Logger.LogInfo("<CLIENT> sending DELETE_JOINT");
                    instance.communication.Lobby.SendBridgeAction(message);
                }

                foreach (BridgeEdge e in BridgeSelectionSet.m_Edges){
                    var joint = new BridgeEdgeProxy(e);
                    var message = new BridgeActionModel {
                        action = actionType.DELETE_EDGE,
                        content = JsonUtility.ToJson(joint)
                    };
                    instance.Logger.LogInfo("<CLIENT> sending DELETE_EDGE");
                    instance.communication.Lobby.SendBridgeAction(message);
                    
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
                        var message = new BridgeActionModel {
                            action = actionType.TRANSLATE_JOINT,
                            content = JsonUtility.ToJson(joint)
                        };
                        instance.Logger.LogInfo("<CLIENT> sending TRANSLATE_JOINT");
                        instance.communication.Lobby.SendBridgeAction(message);
                    }
                }
            }
            [HarmonyPatch(typeof(BridgeSprings), "ForceSliderRelease")]
            public static class SpringSlideTranslatePatch {
                public static void Prefix(
                    ref float ___m_NormalizedSliderValueWhenStartMoving
                ){
                    if (!instance.clientEnabled) return;
                    if (BridgeSprings.m_SliderFollowingMouse){
		            	float num = BridgeSprings.m_SliderFollowingMouse.GetNormalizedValue() - ___m_NormalizedSliderValueWhenStartMoving;
		            	if (!Mathf.Approximately(num, 0f)){
                            var spring = new BridgeSpringProxy(BridgeSprings.m_SliderFollowingMouse.m_BridgeSpring);
                            var message = new BridgeActionModel {
                                action = actionType.SPRING_SLIDER_TRANSLATE,
                                content = JsonUtility.ToJson(spring)
                            };
                            instance.Logger.LogInfo("<CLIENT> sending SPRING_SLIDER_TRANSLATE");
                            instance.communication.Lobby.SendBridgeAction(message);
                        }
                        
                    }
                }
            }

            [HarmonyPatch(typeof(Pistons), "ForceSliderRelease")]
            public static class PistonSlideTranslatePatch {
                public static void Prefix(
                    ref float ___m_NormalizedSliderValueWhenStartMoving
                ){
                    if (!instance.clientEnabled) return;
                    if (Pistons.m_SliderFollowingMouse){
		            	float num = Pistons.m_SliderFollowingMouse.GetNormalizedValue() - ___m_NormalizedSliderValueWhenStartMoving;
		            	if (!Mathf.Approximately(num, 0f)){
                            var piston = new PistonProxy(Pistons.m_SliderFollowingMouse.m_Piston);
                            var message = new BridgeActionModel {
                                action = actionType.PISTON_SLIDER_TRANSLATE,
                                content = JsonUtility.ToJson(piston)
                            };
                            instance.Logger.LogInfo("<CLIENT> sending SPRING_SLIDER_TRANSLATE");
                            instance.communication.Lobby.SendBridgeAction(message);
                        }
                        
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
	    UNSPLIT_JOINT,
        SYNC_LAYOUT
    }
    
    
}
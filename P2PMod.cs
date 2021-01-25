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
using UnityEngine.UI;

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
        public static string serverName = "";
        public bool clientEnabled = false;
        public ServerCommunication communication;

        public Dictionary<actionType, bool> ClientRecieving = new Dictionary<actionType, bool> {};
        
        void Awake()
        {
            this.repositoryUrl = "https://github.com/Conqu3red/PolyBridge2-Multiplayer-Mod/";
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
            uConsole.RegisterCommand("connect", new uConsole.DebugCommand(Connect));
            uConsole.RegisterCommand("disconnect", new uConsole.DebugCommand(Disconnect));
            uConsole.RegisterCommand("connection_info", new uConsole.DebugCommand(ConnectionInfo));
            uConsole.RegisterCommand("accept_connections", new uConsole.DebugCommand(setAcceptConnections));
            uConsole.RegisterCommand("set_password", new uConsole.DebugCommand(setPassword));
            uConsole.RegisterCommand("set_user_cap", new uConsole.DebugCommand(setUserCap));
            uConsole.RegisterCommand("set_lobby_mode", new uConsole.DebugCommand(setLobbyMode));


            uConsole.RegisterCommand("kick","kick <username> <?reason>", new uConsole.DebugCommand(KickUser));

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
            // TODO: send joints/edges as a list so less packets need to be sent when bulk deleting etc
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

                    edge = BridgeEdges.FindDisabledEdgeByJointGuids(
                        edgeProxy.m_NodeA_Guid, 
                        edgeProxy.m_NodeB_Guid, 
                        edgeProxy.m_Material
                    );
		            if (edge){
		            	edge.ForceEnable();
		            	edge.RefreshJointSelectorNumbers();
		            	break;
		            }

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
                    joint = BridgeJoints.FindByGuid(jointProxy.m_Guid);
	                if (joint)
	                {
	                	joint.gameObject.SetActive(true);
	                	break;
	                }
                    BridgeJoints.CreateJointFromProxy(jointProxy);
                    instance.ClientRecieving[actionType.CREATE_JOINT] = false;
                    break;
                case actionType.DELETE_EDGE:
                    edgeProxy = JsonUtility.FromJson<BridgeEdgeProxy>(message.content);
                    edge = BridgeEdges.FindEnabledEdgeByJointGuids(edgeProxy.m_NodeA_Guid, edgeProxy.m_NodeB_Guid, edgeProxy.m_Material);
                    if (edge){
                        edge.ForceDisable();
                        edge.SetStressColor(0f);
                    }
                    break;
                case actionType.DELETE_JOINT:
                    jointProxy = JsonUtility.FromJson<BridgeJointProxy>(message.content);
                    joint = BridgeJoints.FindByGuid(jointProxy.m_Guid);
                    joint.gameObject.SetActive(false);
                    break;
                case actionType.TRANSLATE_JOINT:
                    jointProxy = JsonUtility.FromJson<BridgeJointProxy>(message.content);
                    joint = BridgeJoints.FindByGuid(jointProxy.m_Guid);
                    joint.transform.position = jointProxy.m_Pos;
                    joint.m_BuildPos = joint.transform.position;
                    joint.TryRecreateSpringVisualizationForAttachedEdges();
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
                case actionType.SPLIT_JOINT:
                    jointProxy = JsonUtility.FromJson<BridgeJointProxy>(message.content);
                    joint = BridgeJoints.FindByGuid(jointProxy.m_Guid);
                    instance.ClientRecieving[actionType.SPLIT_JOINT] = true;
                    joint.Split();
                    joint.ResetJointSelectors();
                    instance.ClientRecieving[actionType.SPLIT_JOINT] = false;
                    break;
                case actionType.UNSPLIT_JOINT:
                    jointProxy = JsonUtility.FromJson<BridgeJointProxy>(message.content);
                    instance.ClientRecieving[actionType.UNSPLIT_JOINT] = true;
                    joint = BridgeJoints.FindByGuid(jointProxy.m_Guid);
                    joint.UnSplit();
                    instance.ClientRecieving[actionType.UNSPLIT_JOINT] = false;
                    break;
                case actionType.SPLIT_MODIFY:
                    edgeProxy = JsonUtility.FromJson<BridgeEdgeProxy>(message.content);
                    edge = BridgeEdges.FindEnabledEdgeByJointGuids(edgeProxy.m_NodeA_Guid, edgeProxy.m_NodeB_Guid, edgeProxy.m_Material);
                    edge.m_JointAPart = edgeProxy.m_JointAPart;
                    edge.m_JointBPart = edgeProxy.m_JointBPart;
                    edge.RefreshJointSelectorNumbers();
                    break;
                case actionType.HYDRAULIC_CONTROLLER_ACTION:
                    HydraulicsControllerActionModel content = JsonUtility.FromJson<HydraulicsControllerActionModel>(message.content);
                    instance.Logger.LogInfo("- " + content.action.ToString());
                    // figure out what phases we are applying this to
                    List<HydraulicsControllerPhase> phases = new List<HydraulicsControllerPhase> ();
                    if (content.doForEveryPhase) phases = HydraulicsController.m_ControllerPhases;
                    else {
                        HydraulicsPhase hydraulicsPhase = HydraulicsPhases.FindByGuid(content.phaseGuid);
                        HydraulicsControllerPhase hydraulicsControllerPhase = HydraulicsController.FindControllerPhaseWithHydraulicsPhase(hydraulicsPhase);
                        phases.Add(hydraulicsControllerPhase);
                        
                    }
                    if (content.phaseMustBeAcceptingAdditions){
                        List<HydraulicsControllerPhase> phases2 = new List<HydraulicsControllerPhase> ();
                        foreach (var phase in phases){
                            if (!phase.m_DisableNewAdditions) phases2.Add(phase);
                        }
                        phases = phases2;
                    }

                    if (content.action == HydraulicsControllerAction.SET_THREE_WAY_SPLIT_JOINT_TOGGLE_STATE){
                        GameUI.m_Instance.m_HydraulicsController.m_ThreeWayJointsToggle.isOn = content.ThreeWaySplitJointToggleState;
                        SandboxSettings.m_ThreeWaySplitJointsEnabled = GameUI.m_Instance.m_HydraulicsController.m_ThreeWayJointsToggle.isOn;
	                    Profile.Save();
                        break;
                    }

                    foreach (HydraulicsControllerPhase hydraulicsControllerPhase in phases){
                        if (hydraulicsControllerPhase == null) continue;
                        switch (content.action){
                            case HydraulicsControllerAction.ADD_SPLIT_JOINT:
                                if (content.doForEverySplitJoint){
                                    foreach (BridgeJoint bridgeJoint in BridgeJoints.m_Joints)
		                            {
		                            	if (bridgeJoint.m_IsSplit && bridgeJoint.gameObject.activeInHierarchy)
		                            	{
		                            		if (!hydraulicsControllerPhase.AffectsSplitJoint(bridgeJoint))
		                            		{
		                            			hydraulicsControllerPhase.AddSplitJoint(bridgeJoint, SplitJointState.ALL_SPLIT);
		                            		}
		                            		else
		                            		{
		                            			hydraulicsControllerPhase.SetStateForJoint(bridgeJoint, SplitJointState.ALL_SPLIT);
		                            		}
		                            	}
		                            }
                                }
                                else {
                                    joint = BridgeJoints.FindByGuid(content.jointGuid);
                                    hydraulicsControllerPhase.AddSplitJoint(joint, (joint.m_SplitJointState == SplitJointState.NONE_SPLIT) ? SplitJointState.ALL_SPLIT : joint.m_SplitJointState);
                                }
                                break;
                            case HydraulicsControllerAction.REMOVE_SPLIT_JOINT:
                                if (content.doForEverySplitJoint){
                                    hydraulicsControllerPhase.RemoveAllSplitJoints();
                                }
                                else if (content.weirdRemoveFlagForJointBeingDestroyed){
                                    joint = BridgeJoints.FindByGuid(content.jointGuid);
                                    foreach (BridgeSplitJoint bridgeSplitJoint in hydraulicsControllerPhase.m_SplitJoints)
			                        {
			                        	if (bridgeSplitJoint.m_BridgeJoint == joint)
			                        	{
			                        		hydraulicsControllerPhase.m_SplitJoints.Remove(bridgeSplitJoint);
			                        		break;
			                        	}
			                        }
                                }
                                else {
                                    joint = BridgeJoints.FindByGuid(content.jointGuid);
                                    hydraulicsControllerPhase.RemoveSplitJoint(joint);
                                }
                                break;
                            case HydraulicsControllerAction.ADD_PISTON:
                                if (content.doForEveryPiston){
                                    foreach (Piston item in Pistons.m_Pistons)
		                            {
		                            	if (!hydraulicsControllerPhase.m_Pistons.Contains(item))
		                            	{
		                            		hydraulicsControllerPhase.m_Pistons.Add(item);
		                            	}
		                            }
                                }
                                else {
                                    pistonProxy = JsonUtility.FromJson<PistonProxy>(content.pistonProxySerialized);
                                    piston = Pistons.GetPistonOnEdge(BridgeEdges.FindEnabledEdgeByJointGuids(
                                        pistonProxy.m_NodeA_Guid,
                                        pistonProxy.m_NodeB_Guid,
                                        BridgeMaterialType.HYDRAULICS
                                    ));
                                    if (!hydraulicsControllerPhase.m_Pistons.Contains(piston)){
                                        hydraulicsControllerPhase.m_Pistons.Add(piston);
                                    }
                                }
                                break;
                            case HydraulicsControllerAction.REMOVE_PISTON:
                                if (content.doForEveryPiston){
                                    hydraulicsControllerPhase.m_Pistons.Clear();
                                }
                                else {
                                    pistonProxy = JsonUtility.FromJson<PistonProxy>(content.pistonProxySerialized);
                                    piston = Pistons.GetPistonOnEdge(BridgeEdges.FindEnabledEdgeByJointGuids(
                                        pistonProxy.m_NodeA_Guid,
                                        pistonProxy.m_NodeB_Guid,
                                        BridgeMaterialType.HYDRAULICS
                                    ));
                                    if (hydraulicsControllerPhase.m_Pistons.Contains(piston)){
                                        hydraulicsControllerPhase.m_Pistons.Remove(piston);
                                    }
                                }
                                break;
                            case HydraulicsControllerAction.SET_DISABLE_NEW_ADDITIONS:
                                foreach (var phase in phases){
                                    phase.m_DisableNewAdditions = content.DisableAdditonsState;
                                }
                                break;
                            case HydraulicsControllerAction.SET_SPLIT_JOINT_STATE:
                                joint = BridgeJoints.FindByGuid(content.jointGuid);
	                            hydraulicsControllerPhase.SetStateForJoint(joint, content.splitJointState);
                                break;
                            default:
                                instance.Logger.LogError("Unrecognized Hydraulic controller action! " + content.action.ToString());
                                break;
                            
                        }
                        if (hydraulicsControllerPhase != null && hydraulicsControllerPhase.m_HydraulicsPhase != null)
		                {
		                	EventStage stageWithUnit = EventTimelines.GetStageWithUnit(hydraulicsControllerPhase.m_HydraulicsPhase.gameObject);
		                	if (stageWithUnit != null && GameUI.m_Instance.m_HydraulicsController.isActiveAndEnabled)
		                	{
		                		GameUI.m_Instance.m_HydraulicsController.m_Stages.EnableOffIconForStage(stageWithUnit, hydraulicsControllerPhase.m_DisableNewAdditions);
		                	}
		                }
                    }
                    break;
                default:
                    instance.Logger.LogError("<CLIENT> recieved unexpected action");
                    break;
            }
            
        }

        public static Dictionary<string, string> getOptionalParams(List<string> parameters){
            Dictionary<string, string> optional_params = new Dictionary<string, string>();
            foreach (var p in parameters){
                //instance.Logger.LogInfo(p);
                string[] split = p.Split('=');
                //instance.Logger.LogInfo($"{split[0].ToString()} {split[1].ToString()}");
                optional_params[split[0].ToString()] = split[1].ToString();
            }
            return optional_params;
        }
        public static void Connect(){
            /*
                Usage:
                connect <host_ip> <port> <server_name>
                optional:
                password=<password>
                invite=<invite>

                Examples:
                connect 127.0.0.1 8181 test
                connect 127.0.0.1 8181 test password=abc123
                connect 127.0.0.1 8181 test invite=52fc013e-bf9a-4859-ba3e-01e1531d7d03
            */
            


            if (instance.clientEnabled){
                uConsole.Log("Already Connected to a server");
                return;
            }
            
            if (uConsole.GetNumParameters() < 3){
                uConsole.Log("Usage (? signifies optional): Connect <host_ip> <port> <server_name> <?password>");
                return;
            }
            

            string hostIP = uConsole.GetString();
            int port = uConsole.GetInt();
            serverName = uConsole.GetString();
            string password, invite;
            ClientName = Workshop.GetLocalPlayerDisplayName();
            string id = Workshop.GetLocalPlayerId();
            List<string> parameters = uConsole.m_Instance.GetAllParameters();
            parameters = parameters.GetRange(3, parameters.Count - 3);
            Dictionary<string, string> optional_params = getOptionalParams(parameters);
            
            optional_params.TryGetValue("password", out password);
            optional_params.TryGetValue("invite", out invite);

            instance.communication = new ServerCommunication();
            instance.communication.useLocalhost = false;
            instance.communication.hostIP = hostIP;
            instance.communication.path = $"{serverName}?username={ClientName}&id={id}";
            if (password != null) instance.communication.path += $"&password={password}";
            if (invite != null) instance.communication.path += $"&invite={invite}";
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
            try {
                if (instance != null && instance.communication != null){
                    instance.communication.client.ws.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "closed",
                        new CancellationToken()
                    );
                }
            }
            catch {
                
            }
            instance.communication = null;
            uConsole.Log("Disabled Client");
            instance.clientEnabled = false;
        }
        public static void ConnectionInfo(){
            if (!instance.clientEnabled) return;
            uConsole.Log($"Connected to {instance.communication.hostIP}:{instance.communication.port}");
            uConsole.Log($"Connected to server {serverName}");
            instance.communication.SendRequest(JsonUtility.ToJson(
                new MessageModel {
                    type = LobbyMessaging.ServerInfo
                }
            ));
        }

        public static void KickUser(){
            if (!instance.clientEnabled){
                uConsole.Log("You aren't connected to anything.");
                return;
            }
            string username = uConsole.GetString();
            var parameters = uConsole.m_Instance.GetAllParameters();
            parameters.RemoveAt(0);
            string reason = parameters.Join(null, " ");
           
            var content = new KickUserModel {
                username = username,
                reason = reason
            };
            var message = new MessageModel {
                type = LobbyMessaging.KickUser,
                content = JsonUtility.ToJson(content) 
            };

            
            instance.communication.SendRequest(JsonUtility.ToJson(message));
        }
        public static void setPassword(){
            if (!instance.clientEnabled){
                uConsole.Log("You aren't connected to anything.");
                return;
            }
            string password = uConsole.GetString();
            var content = new SetPasswordModel {
                newPassword = password
            };
            var message = new MessageModel {
                type = LobbyMessaging.ServerConfig,
                content = JsonUtility.ToJson(content) 
            };
            instance.communication.SendRequest(JsonUtility.ToJson(message));
        }
        public static void setUserCap(){
            if (!instance.clientEnabled){
                uConsole.Log("You aren't connected to anything.");
                return;
            }
            int userCap = uConsole.GetInt();
            var content = new SetUserCapModel {
                userCap = userCap
            };
            var message = new MessageModel {
                type = LobbyMessaging.ServerConfig,
                content = JsonUtility.ToJson(content) 
            };
            instance.communication.SendRequest(JsonUtility.ToJson(message));
        }
        public static void setAcceptConnections(){
            if (!instance.clientEnabled){
                uConsole.Log("You aren't connected to anything.");
                return;
            }
            bool acceptingConnections = uConsole.GetBool();
            var content = new SetAcceptingConnectionsModel {
                acceptingConnections = acceptingConnections
            };
            var message = new MessageModel {
                type = LobbyMessaging.ServerConfig,
                content = JsonUtility.ToJson(content) 
            };
            instance.communication.SendRequest(JsonUtility.ToJson(message));
        }
        public static void setLobbyMode(){
            if (!instance.clientEnabled){
                uConsole.Log("You aren't connected to anything.");
                return;
            }
            if (uConsole.GetNumParameters() == 0) return;
            string lobby_mode = uConsole.GetString().ToLower();
            LobbyMode mode;
            if (LobbyMode.TryParse(lobby_mode, true, out mode)){
                uConsole.Log("Changing Mode...");
            }
            else {
                uConsole.Log("Invalid Mode! Valid modes are: public, password, password_locked, invite_only");
                return;
            }

            var content = new SetLobbyModeModel {
                lobbyMode = mode
            };
            var message = new MessageModel {
                type = LobbyMessaging.ServerConfig,
                content = JsonUtility.ToJson(content) 
            };
            instance.communication.SendRequest(JsonUtility.ToJson(message));
        }

        public static void CreateInvite(){
            if (!instance.clientEnabled){
                uConsole.Log("You aren't connected to anything.");
                return;
            }
            int uses;
            uses = (uConsole.GetNumParameters() == 1) ? uConsole.GetInt() : 1;
            var content = new CreateInviteModel {
                uses = uses
            };
            var message = new MessageModel {
                type = LobbyMessaging.ServerConfig,
                content = JsonUtility.ToJson(content) 
            };
            instance.communication.SendRequest(JsonUtility.ToJson(message));
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
                if (Bridge.IsSimulating()) return;
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

        [HarmonyPatch(typeof(BridgeJointPlacement), "ProcessDoubleClickOnJoint")]
        public static class SplitJointCreateDeletePatch {
            public static void Prefix(BridgeJoint joint){
                if (!instance.clientEnabled) return;
                actionType action = actionType.SPLIT_JOINT;
                if (joint.m_IsSplit)
		        {
		        	action = actionType.UNSPLIT_JOINT;
                    instance.Logger.LogInfo("<CLIENT> sending UNSPLIT_JOINT");
		        }
		        else if (HydraulicsPhases.m_Phases.Count > 0)
		        {
		        	
		        	action = actionType.SPLIT_JOINT;
                    instance.Logger.LogInfo("<CLIENT> sending SPLIT_JOINT");
		        }

                var message = new BridgeActionModel {
                    action = action,
                    content = JsonUtility.ToJson(new BridgeJointProxy(joint))
                };
                
                instance.communication.Lobby.SendBridgeAction(message);
            }
        }
        [HarmonyPatch(typeof(BridgeJointSelector), "Cycle")]
        public static class SplitJointChangeNumbersPatch {
            public static void Postfix(bool forward, ref BridgeJointSelector __instance){
                if (!instance.clientEnabled) return;
                var edge = new BridgeEdgeProxy(__instance.m_Edge);
                var message = new BridgeActionModel {
                    action = actionType.SPLIT_MODIFY,
                    content = JsonUtility.ToJson(edge)
                };
                
                instance.communication.Lobby.SendBridgeAction(message);
            }
        }
        // Hydraulic Controller Patches


        [HarmonyPatch(typeof(HydraulicsController), "AddSplitJointToPhase")]
        public static class AddSplitJointToPhasePatch {
            public static void Postfix(BridgeJoint joint, HydraulicsPhase hydraulicsPhase)
            {
                if (!instance.clientEnabled) return;
                if (Bridge.IsSimulating()) return;
                actionType action = actionType.HYDRAULIC_CONTROLLER_ACTION;
                HydraulicsControllerPhase hydraulicsControllerPhase = HydraulicsController.FindControllerPhaseWithHydraulicsPhase(hydraulicsPhase);
	            if (hydraulicsControllerPhase != null)
	            {
                    var content = new HydraulicsControllerActionModel {
                        action = HydraulicsControllerAction.ADD_SPLIT_JOINT,
                        phaseGuid = hydraulicsPhase.m_Guid,
                        jointGuid = joint.m_Guid

                    };
	            	var message = new BridgeActionModel {
                        action = action,
                        content = JsonUtility.ToJson(content)
                    };
                    instance.Logger.LogInfo("<CLIENT> sending HYDRAULIC_CONTROLLER_ACTION - AddSplitJointToPhase");
                    instance.communication.Lobby.SendBridgeAction(message);
	            }
            }
        }
        
        [HarmonyPatch(typeof(HydraulicsController), "AddAllSplitJointsToPhase")]
        public static class AddAllSplitJointsToPhasePatch {
            public static void Postfix(HydraulicsPhase hydraulicsPhase)
            {
                if (!instance.clientEnabled) return;
                if (Bridge.IsSimulating()) return;
                actionType action = actionType.HYDRAULIC_CONTROLLER_ACTION;
                HydraulicsControllerPhase hydraulicsControllerPhase = HydraulicsController.FindControllerPhaseWithHydraulicsPhase(hydraulicsPhase);
	            if (hydraulicsControllerPhase != null)
	            {
                    var content = new HydraulicsControllerActionModel {
                        action = HydraulicsControllerAction.ADD_SPLIT_JOINT,
                        phaseGuid = hydraulicsPhase.m_Guid,
                        doForEverySplitJoint = true

                    };
	            	var message = new BridgeActionModel {
                        action = action,
                        content = JsonUtility.ToJson(content)
                    };
                    instance.Logger.LogInfo("<CLIENT> sending HYDRAULIC_CONTROLLER_ACTION - AddAllSplitJointsToPhase");
                    instance.communication.Lobby.SendBridgeAction(message);
	            }
            }
        }

        [HarmonyPatch(typeof(HydraulicsController), "AddSplitJointToAllPhasesAcceptingNewAdditions")]
        public static class AddSplitJointToAllPhasesAcceptingNewAdditionsPatch {
            public static void Postfix(BridgeJoint joint)
            {
                if (!instance.clientEnabled) return;
                if (Bridge.IsSimulating()) return;
                instance.ClientRecieving.TryGetValue(actionType.SPLIT_JOINT, out var ClientIsRecieving);
                if (ClientIsRecieving) return;
                actionType action = actionType.HYDRAULIC_CONTROLLER_ACTION;
                var content = new HydraulicsControllerActionModel {
                    action = HydraulicsControllerAction.ADD_SPLIT_JOINT,
                    jointGuid = joint.m_Guid,
                    doForEveryPhase = true,
                    phaseMustBeAcceptingAdditions = true
                };
	            var message = new BridgeActionModel {
                    action = action,
                    content = JsonUtility.ToJson(content)
                };
                instance.Logger.LogInfo("<CLIENT> sending HYDRAULIC_CONTROLLER_ACTION - AddSplitJointToAllPhasesAcceptingNewAdditions");
                instance.communication.Lobby.SendBridgeAction(message);
	            
            }
        } 
        
        [HarmonyPatch(typeof(HydraulicsController), "RemoveSplitJointFromPhase")]
        public static class RemoveSplitJointFromPhasePatch {
            public static void Postfix(BridgeJoint joint, HydraulicsPhase hydraulicsPhase)
            {
                if (!instance.clientEnabled) return;
                if (Bridge.IsSimulating()) return;
                actionType action = actionType.HYDRAULIC_CONTROLLER_ACTION;
                HydraulicsControllerPhase hydraulicsControllerPhase = HydraulicsController.FindControllerPhaseWithHydraulicsPhase(hydraulicsPhase);
	            if (hydraulicsControllerPhase != null)
	            {
                    var content = new HydraulicsControllerActionModel {
                        action = HydraulicsControllerAction.REMOVE_SPLIT_JOINT,
                        phaseGuid = hydraulicsPhase.m_Guid,
                        jointGuid = joint.m_Guid

                    };
	            	var message = new BridgeActionModel {
                        action = action,
                        content = JsonUtility.ToJson(content)
                    };
                    instance.Logger.LogInfo("<CLIENT> sending HYDRAULIC_CONTROLLER_ACTION - RemoveSplitJointFromPhase");
                    instance.communication.Lobby.SendBridgeAction(message);
	            }
            }
        }
        [HarmonyPatch(typeof(HydraulicsController), "RemoveSplitJointFromAllPhases")]
        public static class RemoveSplitJointFromAllPhasesPatch {
            public static void Postfix(BridgeJoint joint)
            {
                if (!instance.clientEnabled) return;
                if (Bridge.IsSimulating()) return;
                instance.ClientRecieving.TryGetValue(actionType.UNSPLIT_JOINT, out var ClientIsRecieving);
                if (ClientIsRecieving) return;
                actionType action = actionType.HYDRAULIC_CONTROLLER_ACTION;
                
                var content = new HydraulicsControllerActionModel {
                    action = HydraulicsControllerAction.REMOVE_SPLIT_JOINT,
                    jointGuid = joint.m_Guid,
                    doForEveryPhase = true

                };
	            var message = new BridgeActionModel {
                    action = action,
                    content = JsonUtility.ToJson(content)
                };
                instance.Logger.LogInfo("<CLIENT> sending HYDRAULIC_CONTROLLER_ACTION - RemoveSplitJointFromAllPhases");
                instance.communication.Lobby.SendBridgeAction(message);
	            
            }
        }
        
        //[HarmonyPatch(typeof(HydraulicsController), "RemoveJointFromAllPhases")]
        //public static class RemoveJointFromAllPhasesPatch {
        //    public static void Postfix(BridgeJoint joint)
        //    {
        //        if (!instance.clientEnabled) return;
        //        if (Bridge.IsSimulating()) return;
        //        actionType action = actionType.HYDRAULIC_CONTROLLER_ACTION;
        //        
        //        var content = new HydraulicsControllerActionModel {
        //            action = HydraulicsControllerAction.REMOVE_SPLIT_JOINT,
        //            jointGuid = joint.m_Guid,
        //            doForEveryPhase = true,
        //            weirdRemoveFlagForJointBeingDestroyed = true
        //        };
	    //        var message = new BridgeActionModel {
        //            action = action,
        //            content = JsonUtility.ToJson(content)
        //        };
        //        instance.Logger.LogInfo("<CLIENT> sending HYDRAULIC_CONTROLLER_ACTION - RemoveJointFromAllPhases");
        //        instance.communication.Lobby.SendBridgeAction(message);
	    //        
        //    }
        //}
        
        [HarmonyPatch(typeof(HydraulicsController), "RemoveAllSplitJointsFromPhase")]
        public static class RemoveAllSplitJointsFromPhasePatch {
            public static void Postfix(HydraulicsPhase hydraulicsPhase)
            {
                if (!instance.clientEnabled) return;
                if (Bridge.IsSimulating()) return;
                actionType action = actionType.HYDRAULIC_CONTROLLER_ACTION;
                HydraulicsControllerPhase hydraulicsControllerPhase = HydraulicsController.FindControllerPhaseWithHydraulicsPhase(hydraulicsPhase);
	            if (hydraulicsControllerPhase != null)
	            {
                    var content = new HydraulicsControllerActionModel {
                        action = HydraulicsControllerAction.REMOVE_SPLIT_JOINT,
                        phaseGuid = hydraulicsPhase.m_Guid,
                        doForEverySplitJoint = true
                    };
	            	var message = new BridgeActionModel {
                        action = action,
                        content = JsonUtility.ToJson(content)
                    };
                    instance.Logger.LogInfo("<CLIENT> sending HYDRAULIC_CONTROLLER_ACTION - RemoveSplitJointFromPhase");
                    instance.communication.Lobby.SendBridgeAction(message);
	            }
            }
        }

        [HarmonyPatch(typeof(HydraulicsController), "SetSplitJointStateForPhase")]
        public static class SetSplitJointStateForPhasePatch {
            public static void Postfix(HydraulicsPhase hydraulicsPhase, BridgeJoint joint, SplitJointState state){
                if (!instance.clientEnabled) return;
                if (Bridge.IsSimulating()) return;
                instance.ClientRecieving.TryGetValue(actionType.SPLIT_JOINT, out var ClientIsRecieving);
                if (ClientIsRecieving) return;
                instance.ClientRecieving.TryGetValue(actionType.UNSPLIT_JOINT, out ClientIsRecieving);
                if (ClientIsRecieving) return;
                actionType action = actionType.HYDRAULIC_CONTROLLER_ACTION;
                HydraulicsControllerPhase hydraulicsControllerPhase = HydraulicsController.FindControllerPhaseWithHydraulicsPhase(hydraulicsPhase);
	            if (hydraulicsControllerPhase != null)
	            {
                    var content = new HydraulicsControllerActionModel {
                        action = HydraulicsControllerAction.SET_SPLIT_JOINT_STATE,
                        phaseGuid = hydraulicsPhase.m_Guid,
                        jointGuid = joint.m_Guid,
                        splitJointState = state
                    };
	            	var message = new BridgeActionModel {
                        action = action,
                        content = JsonUtility.ToJson(content)
                    };
                    instance.Logger.LogInfo("<CLIENT> sending HYDRAULIC_CONTROLLER_ACTION - SetSplitJointStateForPhase");
                    instance.communication.Lobby.SendBridgeAction(message);
	            }
            }
        }

        [HarmonyPatch(typeof(HydraulicsController), "ToggleSplitJoint")]
        public static class ToggleSplitJointPatch {
            public static void Prefix(HydraulicsPhase hydraulicsPhase, BridgeJoint joint){
                if (!instance.clientEnabled) return;
                if (Bridge.IsSimulating()) return;
                actionType action = actionType.HYDRAULIC_CONTROLLER_ACTION;
                HydraulicsControllerPhase hydraulicsControllerPhase = HydraulicsController.FindControllerPhaseWithHydraulicsPhase(hydraulicsPhase);
	            if (hydraulicsControllerPhase != null)
	            {
                    HydraulicsControllerAction internal_action;
                    if (hydraulicsControllerPhase.AffectsSplitJoint(joint)) internal_action = HydraulicsControllerAction.REMOVE_SPLIT_JOINT;
                    else internal_action = HydraulicsControllerAction.ADD_SPLIT_JOINT;
                    var content = new HydraulicsControllerActionModel {
                        action = internal_action,
                        phaseGuid = hydraulicsPhase.m_Guid,
                        jointGuid = joint.m_Guid,
                    };
	            	var message = new BridgeActionModel {
                        action = action,
                        content = JsonUtility.ToJson(content)
                    };
                    instance.Logger.LogInfo("<CLIENT> sending HYDRAULIC_CONTROLLER_ACTION - ToggleSplitJoint");
                    instance.communication.Lobby.SendBridgeAction(message);
	            }
            }
        }

        [HarmonyPatch(typeof(HydraulicsController), "AddPistonToHydraulicsPhase")]
        public static class AddPistonToHydraulicsPhasePatch {
            public static void Postfix(HydraulicsPhase hydraulicsPhase, Piston piston)
            {
                if (!instance.clientEnabled) return;
                if (Bridge.IsSimulating()) return;
                actionType action = actionType.HYDRAULIC_CONTROLLER_ACTION;
                HydraulicsControllerPhase hydraulicsControllerPhase = HydraulicsController.FindControllerPhaseWithHydraulicsPhase(hydraulicsPhase);
	            if (hydraulicsControllerPhase != null)
	            {
                    var content = new HydraulicsControllerActionModel {
                        action = HydraulicsControllerAction.ADD_PISTON,
                        phaseGuid = hydraulicsPhase.m_Guid,
                        pistonProxySerialized = JsonUtility.ToJson(new PistonProxy(piston))

                    };
	            	var message = new BridgeActionModel {
                        action = action,
                        content = JsonUtility.ToJson(content)
                    };
                    instance.Logger.LogInfo("<CLIENT> sending HYDRAULIC_CONTROLLER_ACTION - AddPistonToHydraulicsPhase");
                    instance.communication.Lobby.SendBridgeAction(message);
	            }
            }
        }

        [HarmonyPatch(typeof(HydraulicsController), "AddAllPistonsToPhase")]
        public static class AddAllPistonsToPhasePatch {
            public static void Postfix(HydraulicsPhase hydraulicsPhase)
            {
                if (!instance.clientEnabled) return;
                if (Bridge.IsSimulating()) return;
                actionType action = actionType.HYDRAULIC_CONTROLLER_ACTION;
                
                HydraulicsControllerPhase hydraulicsControllerPhase = HydraulicsController.FindControllerPhaseWithHydraulicsPhase(hydraulicsPhase);
	            if (hydraulicsControllerPhase != null)
	            {
                    var content = new HydraulicsControllerActionModel {
                        action = HydraulicsControllerAction.ADD_PISTON,
                        phaseGuid = hydraulicsPhase.m_Guid,
                        doForEveryPiston = true

                    };
	            	var message = new BridgeActionModel {
                        action = action,
                        content = JsonUtility.ToJson(content)
                    };
                    instance.Logger.LogInfo("<CLIENT> sending HYDRAULIC_CONTROLLER_ACTION - AddAllPistonsToPhase");
                    instance.communication.Lobby.SendBridgeAction(message);
	            }
	            
            }
        }

        [HarmonyPatch(typeof(HydraulicsController), "AddPistonToAllPhasesAcceptingNewAdditions")]
        public static class AddPistonToAllPhasesAcceptingNewAdditionsPatch {
            public static void Postfix(Piston piston)
            {
                if (!instance.clientEnabled) return;
                if (Bridge.IsSimulating()) return;
                actionType action = actionType.HYDRAULIC_CONTROLLER_ACTION;
                var content = new HydraulicsControllerActionModel {
                    action = HydraulicsControllerAction.ADD_PISTON,
                    pistonProxySerialized = JsonUtility.ToJson(new PistonProxy(piston)),
                    doForEveryPhase = true,
                    phaseMustBeAcceptingAdditions = true

                };
	            var message = new BridgeActionModel {
                    action = action,
                    content = JsonUtility.ToJson(content)
                };
                instance.Logger.LogInfo("<CLIENT> sending HYDRAULIC_CONTROLLER_ACTION - AddPistonToHydraulicsPhase");
                instance.communication.Lobby.SendBridgeAction(message);
	            
            }
        }

        [HarmonyPatch(typeof(HydraulicsController), "RemoveAllPistonsFromPhase")]
        public static class RemoveAllPistonsFromPhasePatch {
            public static void Postfix(HydraulicsPhase hydraulicsPhase)
            {
                if (!instance.clientEnabled) return;
                if (Bridge.IsSimulating()) return;
                actionType action = actionType.HYDRAULIC_CONTROLLER_ACTION;
                
                HydraulicsControllerPhase hydraulicsControllerPhase = HydraulicsController.FindControllerPhaseWithHydraulicsPhase(hydraulicsPhase);
	            if (hydraulicsControllerPhase != null)
	            {
                    var content = new HydraulicsControllerActionModel {
                        action = HydraulicsControllerAction.REMOVE_PISTON,
                        phaseGuid = hydraulicsPhase.m_Guid,
                        doForEveryPiston = true

                    };
	            	var message = new BridgeActionModel {
                        action = action,
                        content = JsonUtility.ToJson(content)
                    };
                    instance.Logger.LogInfo("<CLIENT> sending HYDRAULIC_CONTROLLER_ACTION - RemoveAllPistonsFromPhase");
                    instance.communication.Lobby.SendBridgeAction(message);
	            }
	            
            }
        }

        [HarmonyPatch(typeof(HydraulicsController), "RemovePistonFromAllPhases")]
        public static class RemovePistonFromAllPhasesPatch {
            public static void Postfix(Piston piston)
            {
                if (!instance.clientEnabled) return;
                if (Bridge.IsSimulating()) return;
                actionType action = actionType.HYDRAULIC_CONTROLLER_ACTION;
                
                var content = new HydraulicsControllerActionModel {
                    action = HydraulicsControllerAction.REMOVE_PISTON,
                    pistonProxySerialized = JsonUtility.ToJson(new PistonProxy(piston))
                };
	            var message = new BridgeActionModel {
                    action = action,
                    content = JsonUtility.ToJson(content)
                };
                instance.Logger.LogInfo("<CLIENT> sending HYDRAULIC_CONTROLLER_ACTION - AddPistonToHydraulicsPhase");
                instance.communication.Lobby.SendBridgeAction(message);
	            
            }
        }

        [HarmonyPatch(typeof(HydraulicsController), "TogglePiston")]
        public static class TogglePistonPatch {
            public static void Prefix(HydraulicsPhase hydraulicsPhase, Piston piston){
                if (!instance.clientEnabled) return;
                if (Bridge.IsSimulating()) return;
                actionType action = actionType.HYDRAULIC_CONTROLLER_ACTION;
                HydraulicsControllerPhase hydraulicsControllerPhase = HydraulicsController.FindControllerPhaseWithHydraulicsPhase(hydraulicsPhase);
	            if (hydraulicsControllerPhase != null)
	            {
                    HydraulicsControllerAction internal_action;
                    if (hydraulicsControllerPhase.m_Pistons.Contains(piston)) internal_action = HydraulicsControllerAction.REMOVE_PISTON;
		            else internal_action = HydraulicsControllerAction.ADD_PISTON;
                    
                    var content = new HydraulicsControllerActionModel {
                        action = internal_action,
                        phaseGuid = hydraulicsPhase.m_Guid,
                        pistonProxySerialized = JsonUtility.ToJson(new PistonProxy(piston))
                    };
	            	var message = new BridgeActionModel {
                        action = action,
                        content = JsonUtility.ToJson(content)
                    };
                    instance.Logger.LogInfo("<CLIENT> sending HYDRAULIC_CONTROLLER_ACTION - TogglePiston");
                    instance.communication.Lobby.SendBridgeAction(message);
	            }
            }
        }

        [HarmonyPatch(typeof(HydraulicsController), "DisableNewAdditionsFromPhase")]
        public static class DisableNewAdditionsFromPhasePatch {
            public static void Postfix(HydraulicsPhase hydraulicsPhase){
                if (!instance.clientEnabled) return;
                if (Bridge.IsSimulating()) return;
                actionType action = actionType.HYDRAULIC_CONTROLLER_ACTION;
                HydraulicsControllerPhase hydraulicsControllerPhase = HydraulicsController.FindControllerPhaseWithHydraulicsPhase(hydraulicsPhase);
	            if (hydraulicsControllerPhase != null)
	            {
                    var content = new HydraulicsControllerActionModel {
                        action = HydraulicsControllerAction.SET_DISABLE_NEW_ADDITIONS,
                        phaseGuid = hydraulicsPhase.m_Guid,
                        DisableAdditonsState = true
                    };
	            	var message = new BridgeActionModel {
                        action = action,
                        content = JsonUtility.ToJson(content)
                    };
                    instance.Logger.LogInfo("<CLIENT> sending HYDRAULIC_CONTROLLER_ACTION - DisableNewAdditionsFromPhase");
                    instance.communication.Lobby.SendBridgeAction(message);
	            }
            }
        }

        [HarmonyPatch(typeof(HydraulicsController), "EnableNewAdditionsFromPhase")]
        public static class EnableNewAdditionsFromPhasePatch {
            public static void Postfix(HydraulicsPhase hydraulicsPhase){
                if (!instance.clientEnabled) return;
                if (Bridge.IsSimulating()) return;
                actionType action = actionType.HYDRAULIC_CONTROLLER_ACTION;
                HydraulicsControllerPhase hydraulicsControllerPhase = HydraulicsController.FindControllerPhaseWithHydraulicsPhase(hydraulicsPhase);
	            if (hydraulicsControllerPhase != null)
	            {
                    var content = new HydraulicsControllerActionModel {
                        action = HydraulicsControllerAction.SET_DISABLE_NEW_ADDITIONS,
                        phaseGuid = hydraulicsPhase.m_Guid,
                        DisableAdditonsState = false
                    };
	            	var message = new BridgeActionModel {
                        action = action,
                        content = JsonUtility.ToJson(content)
                    };
                    instance.Logger.LogInfo("<CLIENT> sending HYDRAULIC_CONTROLLER_ACTION - EnableNewAdditionsFromPhase");
                    instance.communication.Lobby.SendBridgeAction(message);
	            }
            }
        }

        [HarmonyPatch(typeof(Panel_HydraulicsController), "OnThreeWayJointsToggle")]
        public static class OnThreeWayJointsTogglePatch {
            public static void Postfix(ref Panel_HydraulicsController __instance){
                if (!instance.clientEnabled) return;
                if (Bridge.IsSimulating()) return;
                actionType action = actionType.HYDRAULIC_CONTROLLER_ACTION;
                var content = new HydraulicsControllerActionModel {
                    action = HydraulicsControllerAction.SET_THREE_WAY_SPLIT_JOINT_TOGGLE_STATE,
                    ThreeWaySplitJointToggleState = __instance.m_ThreeWayJointsToggle.isOn
                };
                var message = new BridgeActionModel {
                    action = action,
                    content = JsonUtility.ToJson(content)
                };
                instance.Logger.LogInfo("<CLIENT> sending HYDRAULIC_CONTROLLER_ACTION - OnThreeWayJointsToggle");
                instance.communication.Lobby.SendBridgeAction(message);
            }
        }

        // COPY/CUT/PASTE SUPPORT
        [HarmonyPatch(typeof(ClipboardManager), "MaybeSplitPastedJoint")]
        public static class MaybeSplitPastedJointPatch {
            public static void Postfix(BridgeJoint pastedJoint, BridgeJoint sourceJoint){
                if (!instance.clientEnabled) return;
                actionType action = actionType.SPLIT_JOINT;
                
                if (pastedJoint && pastedJoint.m_IsSplit)
		        {
                    var message = new BridgeActionModel {
                        action = action,
                        content = JsonUtility.ToJson(new BridgeJointProxy(pastedJoint))
                    };
                    instance.communication.Lobby.SendBridgeAction(message);
                }
                
            }
        }

        [HarmonyPatch]
        public static class UndoPatch {
            static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(BridgeUndo), "UndoCreateEdge");
                yield return AccessTools.Method(typeof(BridgeUndo), "UndoCreateJoint");
                yield return AccessTools.Method(typeof(BridgeUndo), "UndoDeleteEdge");
                yield return AccessTools.Method(typeof(BridgeUndo), "UndoDeleteJoint");
                yield return AccessTools.Method(typeof(BridgeUndo), "UndoSplitJoint");
                yield return AccessTools.Method(typeof(BridgeUndo), "UndoUnSplitJoint");
                yield return AccessTools.Method(typeof(BridgeUndo), "UndoTranslateJoint");
                yield return AccessTools.Method(typeof(BridgeUndo), "UndoTranslatePistonSlider");
                yield return AccessTools.Method(typeof(BridgeUndo), "UndoTranslateSpringSlider");
            }
            public static void Postfix(BridgeActionPacket packet, MethodBase __originalMethod){
                actionType action;
                switch (__originalMethod.Name){
                    case "UndoCreateEdge": action = actionType.CREATE_EDGE; break;
                    case "UndoCreateJoint": action = actionType.CREATE_JOINT; break;
                    case "UndoDeleteEdge": action = actionType.DELETE_EDGE; break;
                    case "UndoDeleteJoint": action = actionType.DELETE_JOINT; break;
                    case "UndoSplitJoint": action = actionType.SPLIT_JOINT; break;
                    case "UndoUnSplitJoint": action = actionType.UNSPLIT_JOINT; break;
                    case "UndoTranslateJoint": action = actionType.TRANSLATE_JOINT; break;
                    case "UndoTranslatePistonSlider": action = actionType.PISTON_SLIDER_TRANSLATE; break;
                    case "UndoTranslateSpringSlider": action = actionType.SPRING_SLIDER_TRANSLATE; break;
                    default:
                        instance.Logger.LogError($"Invalid Undo action detected from {__originalMethod.Name}");
                        return;
                }
                processUndo(action, packet);
            }
        }

        [HarmonyPatch]
        public static class RedoPatch {
            static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(BridgeRedo), "RedoTranslateJoint");
                yield return AccessTools.Method(typeof(BridgeRedo), "RedoTranslatePistonSlider");
                yield return AccessTools.Method(typeof(BridgeRedo), "RedoTranslateSpringSlider");
            }
            public static void Postfix(BridgeActionPacket packet, MethodBase __originalMethod){
                actionType action;
                switch (__originalMethod.Name){
                    case "RedoTranslateJoint": action = actionType.TRANSLATE_JOINT; break;
                    case "RedoTranslatePistonSlider": action = actionType.PISTON_SLIDER_TRANSLATE; break;
                    case "RedoTranslateSpringSlider": action = actionType.SPRING_SLIDER_TRANSLATE; break;
                    default:
                        instance.Logger.LogError($"Invalid Redo action detected from {__originalMethod.Name}");
                        return;
                }
                processUndo(action, packet);
            }
        }
        
        
        public static void processUndo(
            actionType action,
            BridgeActionPacket packet
        ){
            var message = new BridgeActionModel {
            };
            switch (action){
                case actionType.CREATE_EDGE:
                    message.action = actionType.DELETE_EDGE;
                    message.content = JsonUtility.ToJson(packet.m_Edge);
                    break;
                
                case actionType.CREATE_JOINT:
                    message.action = actionType.DELETE_JOINT;
                    message.content = JsonUtility.ToJson(packet.m_Joint);
                    break;
                
                case actionType.DELETE_EDGE:
                    message.action = actionType.CREATE_EDGE;
                    message.content = JsonUtility.ToJson(packet.m_Edge);
                    break;
                
                case actionType.DELETE_JOINT:
                    message.action = actionType.CREATE_JOINT;
                    message.content = JsonUtility.ToJson(packet.m_Joint);
                    break;
                
                case actionType.SPLIT_JOINT:
                    message.action = actionType.UNSPLIT_JOINT;
                    message.content = JsonUtility.ToJson(packet.m_Joint);
                    break;

                case actionType.UNSPLIT_JOINT:
                    message.action = actionType.SPLIT_JOINT;
                    message.content = JsonUtility.ToJson(packet.m_Joint);
                    break;
                
                case actionType.TRANSLATE_JOINT:
                    message.action = actionType.TRANSLATE_JOINT;
                    packet.m_Joint.m_Pos = BridgeJoints.FindByGuid(packet.m_Joint.m_Guid).m_BuildPos;
                    message.content = JsonUtility.ToJson(packet.m_Joint);
                    break;
                
                case actionType.PISTON_SLIDER_TRANSLATE:
                    message.action = actionType.PISTON_SLIDER_TRANSLATE;
                    Piston piston = Pistons.FindByGuid(packet.m_Piston.m_Guid);
                    packet.m_Piston.m_NormalizedValue = piston.m_Slider.GetNormalizedValue();
                    message.content = JsonUtility.ToJson(packet.m_Piston);
                    break;
                
                case actionType.SPRING_SLIDER_TRANSLATE:
                    message.action = actionType.SPRING_SLIDER_TRANSLATE;
                    BridgeSpring spring = BridgeSprings.FindByGuid(packet.m_Spring.m_Guid);
                    packet.m_Spring.m_NormalizedValue = spring.m_Slider.GetNormalizedValue();
                    message.content = JsonUtility.ToJson(packet.m_Spring);
                    break;
                default:
                    instance.Logger.LogError($"Invalid undo action - {action}");
                    return;
            }
            instance.Logger.LogInfo($"<CLIENT> sending {message.action} - UndoHandle");
            instance.communication.Lobby.SendBridgeAction(message);
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
        SPLIT_MODIFY,
        HYDRAULIC_CONTROLLER_ACTION,
        SYNC_LAYOUT
    }
    public enum HydraulicsControllerAction {
        ADD_SPLIT_JOINT,
        REMOVE_SPLIT_JOINT,
        SET_SPLIT_JOINT_STATE,
        ADD_PISTON,
        REMOVE_PISTON,
        SET_DISABLE_NEW_ADDITIONS,
        SET_THREE_WAY_SPLIT_JOINT_TOGGLE_STATE,
        //ADD_PHASE,
        //REMOVE_PHASE
    }
    [System.Serializable]
    public class HydraulicsControllerActionModel {
        public HydraulicsControllerAction action;
        public string phaseGuid;
        public string jointGuid;
        public string pistonProxySerialized;
        public SplitJointState splitJointState;
        public bool ThreeWaySplitJointToggleState = false;
        public bool doForEveryPhase = false;
        public bool phaseMustBeAcceptingAdditions = false;
        public bool doForEverySplitJoint = false;
        public bool doForEveryPiston = false;
        public bool DisableAdditonsState = false;
        public bool weirdRemoveFlagForJointBeingDestroyed = false;
    }
    
    
}
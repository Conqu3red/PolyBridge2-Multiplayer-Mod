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
using System.IO;
using System.Text;
using System.Collections;

namespace MultiplayerMod
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    // Specify the mod as a dependency of PTF
    [BepInDependency(PolyTechMain.PluginGuid, BepInDependency.DependencyFlags.HardDependency)]
    // This Changes from BaseUnityPlugin to PolyTechMod.
    // This superclass is functionally identical to BaseUnityPlugin, so existing documentation for it will still work.
    public partial class MultiplayerMod : PolyTechMod
    {
        public new const string
            PluginGuid = "org.bepinex.plugins.MultiplayerMod",
            PluginName = "Multiplayer Mod",
            PluginVersion = "1.0.1";
        
        public static ConfigDefinition
            modEnabledDef = new ConfigDefinition("Multiplayer Mod", "Enable/Disable Mod");
        public static ConfigEntry<bool>
            modEnabled,
            logActions,
            showMice;
        public static ConfigEntry<float>
            backupFrequency,
            writeToLogFrequency;
        private static ConfigEntry<BepInEx.Configuration.KeyboardShortcut> _keybind;
        public static ConfigEntry<Color> mouseColor;
        public static MultiplayerMod instance;
        public static string ClientName = "";
        public static string serverName = "";
        //public bool clientEnabled = false;
        public bool preventSendActions = false;
        public ServerCommunication communication;
        public Dictionary<actionType, bool> ClientRecieving = new Dictionary<actionType, bool> {};
        public System.Timers.Timer BackupTimer = new System.Timers.Timer();
        public List<string> logBuffer = new List<string> ();
        public string backupFolder = "MultiplayerBackups";
        public bool serverIsFrozen = false;
        public ServerInfoModel serverInfo;
        Coroutine GetServerInfo;
        Coroutine SendMousePos;
        public Dictionary<string, PointerHandler> mousePositions = new Dictionary<string, PointerHandler> {};
        public GameObject canvas;
        public GameObject _canvas;

        public Dictionary<PointerMode, Sprite> pointerSprites = new Dictionary<PointerMode, Sprite> {};
        public Sprite normalPointer;
        public Sprite movePointer;
        public Sprite toggleSelectPointer;
        public List<SyncRequest> syncRequests = new List<SyncRequest> {};
        void Awake()
        {
            this.repositoryUrl = "https://github.com/Conqu3red/PolyBridge2-Multiplayer-Mod/";
			if (instance == null) instance = this;
            // Use this if you wish to make the mod trigger cheat mode ingame.
            // Set this true if your mod effects physics or allows mods that you can't normally do.
            isCheat = false;
           
            modEnabled = Config.Bind(modEnabledDef, true, new ConfigDescription("Enable Mod"));
            logActions = Config.Bind("Multiplayer Mod", "Log Actions (only logs if you are host)", false);
            

            backupFrequency = Config.Bind("Multiplayer Mod", "Backup Frequency (seconds)", 60f);
            writeToLogFrequency = Config.Bind("Multiplayer Mod", "Save to action log every amount of lines", 100f);
            
            _keybind = Config.Bind("Multiplayer Mod", "Keybind to open GUI", new BepInEx.Configuration.KeyboardShortcut(KeyCode.F3));
            
            mouseColor = Config.Bind("Multiplayer Mod", "Mouse Color", Color.red);

            showMice = Config.Bind("Multiplayer Mod", "Show other users mice", true);

            BackupTimer.Elapsed += (sender, args) => { backupLayout(); };
            BackupTimer.AutoReset = true;
            BackupTimer.Interval = backupFrequency.Value*1000;
            BackupTimer.Start();

            modEnabled.SettingChanged += onEnableDisable;
            backupFrequency.SettingChanged += (object sender, EventArgs e) => {
                if (backupFrequency.Value < 5){
                    backupFrequency.Value = 5;
                }
                BackupTimer.Interval = backupFrequency.Value*1000;
            };


            harmony = new Harmony("org.bepinex.plugins.MultiplayerMod");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            this.authors = new string[] {"Conqu3red"};

            PolyTechMain.registerMod(this);
        }

        public bool clientEnabled 
        {
            get {
                if (communication != null){
                    return communication.IsConnected();
                }
                return false;
            }
            set {}
        }
        public bool clientConnecting
        {
            get {
                if (communication != null){
                    return communication.IsConnecting();
                }
                return false;
            }
            set {}
        }

        public void Start(){
            var windowBackground = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            windowBackground.SetPixel(0, 0, new Color(0.5f, 0.5f, 0.5f, 1));
            windowBackground.Apply();
            WindowBackground = windowBackground;
            _canvas = new GameObject("cursorCanvas");
            DontDestroyOnLoad(_canvas);
            canvas = Instantiate(_canvas, base.transform);
            DontDestroyOnLoad(canvas);
        }

        [HarmonyPatch(typeof(GameUI), "StartManual")]
        public static class GameUIStartPatch {
            public static void Postfix(){
                instance.normalPointer = Sprite.Create(
                    GameUI.m_Instance.m_PointerNormalTexture, 
                    new Rect(0, 0, GameUI.m_Instance.m_PointerNormalTexture.width, GameUI.m_Instance.m_PointerNormalTexture.height), 
                    new Vector2(0f, 15/16f),
                    96f
                );
                instance.movePointer = Sprite.Create(
                    GameUI.m_Instance.m_PointerMoveTexture, 
                    new Rect(0, 0, GameUI.m_Instance.m_PointerMoveTexture.width, GameUI.m_Instance.m_PointerMoveTexture.height), 
                    new Vector2(0.5f, 0.5f),
                    96f
                );
                instance.toggleSelectPointer = Sprite.Create(
                    GameUI.m_Instance.m_PointerSelectToggleTexture, 
                    new Rect(0, 0, GameUI.m_Instance.m_PointerSelectToggleTexture.width, GameUI.m_Instance.m_PointerSelectToggleTexture.height), 
                    new Vector2(0f,15/16f),
                    96f
                );
                
                instance.pointerSprites[PointerMode.INVALID] = instance.normalPointer;
                instance.pointerSprites[PointerMode.NORMAL] = instance.normalPointer;
                instance.pointerSprites[PointerMode.MOVE] = instance.movePointer;
                instance.pointerSprites[PointerMode.SELECT_TOGGLE] = instance.toggleSelectPointer;
            }
        }
        public void Update(){
            if (!DisplayingWindow && _keybind.Value.IsUp())
            {
                DisplayingWindow = true;
            }
            if (communication != null) {
                communication.Update();
                if (serverIsFrozen || Bridge.IsSimulating() || !showMice.Value){
                    if (canvas.activeInHierarchy){
                        canvas.SetActive(false);
                    }
                }
                else if (!canvas.activeInHierarchy){
                    canvas.SetActive(true);
                }
            }
            if (syncRequests.Count > 0 && GameStateManager.GetState() != GameState.SIM){
                processSyncRequest(syncRequests[0]);
                syncRequests.RemoveAt(0);
            }
            //UpdateMouseDrawers();

        }

        /// <summary>
        /// Method called after connection with server was established.
        /// </summary>
        public void OnConnectedToServer()
        {
            instance.communication.Lobby.OnConnectedToServer -= instance.OnConnectedToServer;
            instance.communication.Lobby.OnBridgeAction += instance.OnBridgeAction;
            GetServerInfo = StartCoroutine(InvokeRepeat("ConnectionInfo", 0, 2.5f));
            SendMousePos = StartCoroutine(InvokeRepeat("SendMousePosition", 0, 0.1f)); // this is a bit spammy but eh
        }

        IEnumerator InvokeRepeat(string function, float delay, float interval) {
            yield return new WaitForSecondsRealtime(delay);
            while (true) {
                Invoke(function, 0f);
                yield return new WaitForSecondsRealtime(interval);
            }
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
            int offset = 0;
            instance.Logger.LogInfo($"<CLIENT> received {message.action}");
            
            
            if (instance.communication.isOwner) {
                ActionLog($"Recieved {message.action} from user {message.username}");
            }
            
            
            // switch to handle actions
            switch (message.action){
                case actionType.CREATE_EDGE:
                    edgeProxy = new BridgeEdgeProxy(message.content, ref offset);
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
                    var NodeA = BridgeJoints.FindByGuid(edgeProxy.m_NodeA_Guid);
                    var NodeB = BridgeJoints.FindByGuid(edgeProxy.m_NodeB_Guid);
                    if (!NodeA || !NodeB){
                        AlertRecieveError("Node/Joint Missing when trying to create edge");
                        break;
                    }

                    BridgeEdge edgeFromJoints = BridgeEdges.GetEdgeFromJoints(
                        NodeA,
                        NodeB
                    );
                    if (edgeFromJoints){
                        edgeFromJoints.ForceDisable();
                    }
                    
                    instance.ClientRecieving[actionType.CREATE_EDGE] = true;
                    edge = BridgeEdges.CreateEdgeWithPistonOrSpring(
                        NodeA,
                        NodeB,
                        edgeProxy.m_Material
                    );
                    if (message.playSound){
                        //BridgeAudio.PlayCreateEdge(edgeProxy.m_Material);
                    }
                    instance.ClientRecieving[actionType.CREATE_EDGE] = false;
                    //if (edge.IsPiston()) // I don't think this code should be running
		            //{
		            //	Pistons.GetPistonOnEdge(edge).m_Slider.MakeVisible();
		            //}
		            //if (edge.IsSpring())
		            //{
		            //	edge.m_SpringCoilVisualization.m_Slider.MakeVisible();
		            //}
                    break;
                
                case actionType.CREATE_JOINT:
                    jointProxy = new BridgeJointProxy(25, message.content, ref offset);
                    joint = BridgeJoints.FindByGuid(jointProxy.m_Guid);
	                if (joint)
	                {
	                	joint.gameObject.SetActive(true);
	                	break;
	                }
                    instance.ClientRecieving[actionType.CREATE_JOINT] = true;
                    BridgeJoints.CreateJointFromProxy(jointProxy);
                    instance.ClientRecieving[actionType.CREATE_JOINT] = false;
                    break;
                case actionType.DELETE_EDGE:
                    edgeProxy = new BridgeEdgeProxy(message.content, ref offset);
                    edge = BridgeEdges.FindEnabledEdgeByJointGuids(edgeProxy.m_NodeA_Guid, edgeProxy.m_NodeB_Guid, edgeProxy.m_Material);
                    if (edge){
                        edge.ForceDisable();
                        edge.SetStressColor(0f);
                        if (message.playSound){
                            //InterfaceAudio.Play("ui_build_delete");
                        }
                    }
                    BridgeJoints.DeleteOrphanedJoints();
                    break;
                case actionType.DELETE_JOINT:
                    jointProxy = new BridgeJointProxy(25, message.content, ref offset);
                    joint = BridgeJoints.FindByGuid(jointProxy.m_Guid);
                    if (joint){
                        joint.gameObject.SetActive(false);
                    }
                    BridgeJoints.DeleteOrphanedJoints();
                    break;
                case actionType.TRANSLATE_JOINT:
                    jointProxy = new BridgeJointProxy(25, message.content, ref offset);
                    joint = BridgeJoints.FindByGuid(jointProxy.m_Guid);
                    if (joint){
                        joint.transform.position = jointProxy.m_Pos;
                        joint.m_BuildPos = joint.transform.position;
                        joint.TryRecreateSpringVisualizationForAttachedEdges();
                    }
                    break;
                case actionType.SPRING_SLIDER_TRANSLATE:
                    springProxy = new BridgeSpringProxy(message.content, ref offset);
                    var spring = BridgeEdges.FindEnabledEdgeByJointGuids(
                        springProxy.m_NodeA_Guid,
                        springProxy.m_NodeB_Guid,
                        BridgeMaterialType.SPRING
                    );
                    if (spring){
                        spring.m_SpringCoilVisualization.m_Slider.SetNormalizedValue(springProxy.m_NormalizedValue);
				        spring.m_SpringCoilVisualization.UpdateFreeLengthFromSliderPos();
                        spring.m_SpringCoilVisualization.MaybeRecreateLinks();
				        spring.m_SpringCoilVisualization.UpdateLinks();
                    }
                    break;
                case actionType.PISTON_SLIDER_TRANSLATE:
                    pistonProxy = new PistonProxy(25, message.content, ref offset);
                    var piston = Pistons.GetPistonOnEdge(BridgeEdges.FindEnabledEdgeByJointGuids(
                        pistonProxy.m_NodeA_Guid,
                        pistonProxy.m_NodeB_Guid,
                        BridgeMaterialType.HYDRAULICS
                    ));
                    if (piston){
                        piston.m_Slider.SetNormalizedValue(pistonProxy.m_NormalizedValue);
                        piston.m_Slider.MakeVisible();
                    }
                    break;
                case actionType.SPLIT_JOINT:
                    jointProxy = new BridgeJointProxy(25, message.content, ref offset);
                    joint = BridgeJoints.FindByGuid(jointProxy.m_Guid);
                    if (joint){
                        instance.ClientRecieving[actionType.SPLIT_JOINT] = true;
                        joint.Split();
                        joint.ResetJointSelectors();
                        instance.ClientRecieving[actionType.SPLIT_JOINT] = false;
                    }
                    break;
                case actionType.UNSPLIT_JOINT:
                    jointProxy = new BridgeJointProxy(25, message.content, ref offset);
                    joint = BridgeJoints.FindByGuid(jointProxy.m_Guid);
                    if (joint){
                        instance.ClientRecieving[actionType.UNSPLIT_JOINT] = true;
                        joint.UnSplit();
                        instance.ClientRecieving[actionType.UNSPLIT_JOINT] = false;
                    }
                    break;
                case actionType.SPLIT_MODIFY:
                    edgeProxy = new BridgeEdgeProxy(message.content, ref offset);
                    edge = BridgeEdges.FindEnabledEdgeByJointGuids(edgeProxy.m_NodeA_Guid, edgeProxy.m_NodeB_Guid, edgeProxy.m_Material);
                    if (edge){
                        edge.m_JointAPart = edgeProxy.m_JointAPart;
                        edge.m_JointBPart = edgeProxy.m_JointBPart;
                        edge.RefreshJointSelectorNumbers();
                    }
                    break;
                case actionType.HYDRAULIC_CONTROLLER_ACTION:
                    HydraulicsControllerActionModel content = new HydraulicsControllerActionModel(message.content, ref offset);
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
                case actionType.SYNC_LAYOUT:
                    syncRequests.Add(new SyncRequest { requestActive = true, message = message});
                    break;

                case actionType.FREEZE:
                    serverIsFrozen = message.content == new byte[] {1} ? true : false;
                    instance.Logger.LogInfo(serverIsFrozen);
                    if (serverIsFrozen){
                        PopUpMessage.DisplayOkOnly("Changes frozen by host.", null);
                    }
                    break;
                default:
                    instance.Logger.LogError("<CLIENT> recieved unexpected action");
                    break;
            }
            
        }

        public void processSyncRequest(SyncRequest request){
            BridgeActionModel message = request.message;
            int offset = 0;
            SyncLayoutModel layout = new SyncLayoutModel();
            BridgeActionModel _message = new BridgeActionModel { action = actionType.SYNC_LAYOUT };
            if (instance.communication.isOwner){
                instance.Logger.LogInfo("sending layout as requested");
                layout.layoutData = SandboxLayout.SerializeToProxies().SerializeBinary();
                _message.content = layout.Serialize();
                instance.communication.Lobby.SendBridgeAction(_message);
                return;
            }
            layout = new SyncLayoutModel(message.content, ref offset);
            preventSendActions = true;
            int num = 0;
			var result = new SandboxLayoutData(layout.layoutData, ref num);
            Sandbox.Clear();
            Sandbox.Load(result.m_ThemeStubKey, result, true);
            SandboxUndo.Clear();
		    SandboxUndo.SnapShot();
		    PointsOfView.OnLayoutLoaded();
		    if (GameStateManager.GetState() == GameState.BUILD)
		    {
		    	GameStateBuild.SetWaterProperties();
		    	GameStateBuild.SetOverrideColorsForBuildMode();
		    }
            preventSendActions = false;
            PopUpMessage.DisplayOkOnly("The host has synced their layout with you", null);
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

        public static void AlertRecieveError(string message){
            if (!instance.communication.isOwner){
                PopUpMessage.Display(
                    "A problem occurred: " + message + " - Press tick to attempt to sync layout with the host or cross to dismiss this message.",
                    () => syncLayout(),
                    () => {}
                );
            }
            
        }

        public static void ActionLog(string message){
            string logItem;
            if (!logActions.Value) return;
            if (instance.logBuffer.Count >= 100){
                string path = Path.Combine(SandboxLayout.GetSavePath(), instance.backupFolder);
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                path = Path.Combine(path, instance.communication.logFileName);
                if (!File.Exists(path))
                {
                    // Create a file to write to.
                    using (StreamWriter sw = File.CreateText(path)) {}	
                }
                using (StreamWriter sw = File.AppendText(path))
                {
                    while (instance.logBuffer.Count > 0){
                        logItem = instance.logBuffer[instance.logBuffer.Count-1];
                        instance.logBuffer.RemoveAt(instance.logBuffer.Count-1);
                        sw.WriteLine(logItem);
                    }
                }
                return;
            }
            string prefix = string.Format("[{0:yyyy-MM-dd HH:mm:ss}]", DateTime.Now);
            instance.logBuffer.Add($"{prefix} {message}");
            
        }
        public void backupLayout(){
            if (!clientEnabled) return;
            if (!communication.isOwner) return;
            if (GameStateManager.GetState() == GameState.SIM) return;
            try {
                string filename = string.Format("{0:yyyy-MM-dd HH-mm-ss}.layout", DateTime.Now);
                Logger.LogInfo("Performing Layout Backup...");
                byte[] layoutData = SandboxLayout.SerializeToProxies().SerializeBinary();
                string path = Path.Combine(SandboxLayout.GetSavePath(), backupFolder);
                //Logger.LogInfo(path + " " + filename);
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                Utils.WriteBytes(path, filename, layoutData);
            }
            catch (Exception ex){
                Logger.LogError($"Caught error when backing up layout: {ex.Message}");
            }
        }

        public static void RaiseConnectionError(string error){
            GUIValues.ConnectionResponse = $"<color=red>Connection Error occured: {error}</color>";
        }

        public static void Connect(){
            //if (instance.clientEnabled){
            //    uConsole.Log("Already Connected to a server");
            //    return;
            //}
            //
            //if (uConsole.GetNumParameters() < 3){
            //    uConsole.Log("Usage (? signifies optional): Connect <host_ip> <port> <server_name> <?password>");
            //    return;
            //}
            
            if (GameStateManager.GetState() != GameState.BUILD){
                GUIValues.ConnectionResponse = "You must be in build mode to start/connect to a session";
                return;
            }


            string hostIP = GUIValues.ip;
            int port;
            try {
                port = int.Parse(GUIValues.port);
            }
            catch {
                RaiseConnectionError("Invalid port");
                return;
            }
            serverName = GUIValues.sessionName;
            string password, invite;
            ClientName = Workshop.GetLocalPlayerDisplayName();
            string id = Workshop.GetLocalPlayerId();
            password = GUIValues.password;
            invite = GUIValues.invite;
            if (serverName == ""){
                RaiseConnectionError("Invalid Session Name");
                return;
            }
            if (hostIP == ""){
                RaiseConnectionError("Invalid host IP");
                return;
            }
            instance.communication = new ServerCommunication();
            instance.communication.useLocalhost = false;
            instance.communication.hostIP = hostIP;
            instance.communication.path = $"{serverName}?username={ClientName}&id={id}";
            if (password != "") instance.communication.path += $"&password={password}";
            if (invite != "") instance.communication.path += $"&invite={invite}";
            instance.communication.port = port;
            instance.communication.ssl = GUIValues.secureConnection;
            instance.communication.Init();
            instance.communication.Lobby.OnConnectedToServer += instance.OnConnectedToServer;
            instance.communication.ConnectToServer();
            //uConsole.Log("Enabled Client");
        }
        public static void Disconnect(){
            //if (!instance.clientEnabled){
            //    uConsole.Log("You aren't connected to anything.");
            //    return;
            //}
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
            instance.serverInfo = null;
            instance.logBuffer.Clear();
            GUIValues.Reset();
            if (instance.GetServerInfo != null) instance.StopCoroutine(instance.GetServerInfo);
            if (instance.SendMousePos != null) instance.StopCoroutine(instance.SendMousePos);
            foreach (string key in instance.mousePositions.Keys){
                Destroy(instance.mousePositions[key].Container);
            }
            instance.mousePositions.Clear();
            //uConsole.Log("Disabled Client");
            //instance.clientEnabled = false;
        }
        public void ConnectionInfo(){
            if (!clientEnabled) return;
            communication.SendRequest(
                new MessageModel {
                    type = LobbyMessaging.ServerInfo
                }.Serialize()
            );
        }
        public void SendMousePosition(){
            if (Bridge.IsSimulating()) return;
            Vector3 pos = Cameras.MainCamera().ScreenToWorldPoint(Input.mousePosition);
            communication.SendRequest(
                new MessageModel {
                    type = LobbyMessaging.MousePosition,
                    content = new MousePositionModel {
                        position = pos,
                        pointerMode = GameUI.GetPointerMode(),
                        pointerColor = mouseColor.Value
                    }.Serialize()
                }.Serialize()
            );
        }

        public void HandleMousePositionRecieved(MousePositionModel mousePosition){
            PointerHandler handler;
            SpriteRenderer renderer;
            if (!mousePositions.TryGetValue(mousePosition.username, out handler)){
                Debug.Log("new mouse: " + mousePosition.username);
                handler = new PointerHandler();
                handler.Container.transform.parent = canvas.transform;
                mousePositions[mousePosition.username] = handler;
            }
            mousePosition.position.z = -2.5f;
            renderer = handler.Container.GetComponent<SpriteRenderer>();
            renderer.transform.position = mousePosition.position;
            renderer.sprite = pointerSprites[mousePosition.pointerMode];
            handler.color = mousePosition.pointerColor;
            renderer.color = handler.color;
        }

        public static void RemoveDisconnectedUsersFromMousePositions(){
            var foundUser = false;
            List<string> UsersToRemove = new List<string> {};
            foreach (var username in instance.mousePositions.Keys){
                foundUser = false;
                foreach (string userConnected in instance.serverInfo.playerNames){
                    if (username == userConnected){
                        // user mouse already accounted for
                        foundUser = true;
                        break;
                    }
                }
                // user is no longer connected, set their mouse for removal
                if (!foundUser) UsersToRemove.Add(username);
            }

            foreach (string username in UsersToRemove){
                Destroy(instance.mousePositions[username].Container);
                instance.mousePositions.Remove(username);
                //instance.Logger.LogInfo($"Removed {username}");
            }
            foreach (string key in instance.mousePositions.Keys){
                //instance.Logger.LogInfo($"{key} : {instance.mousePositions[key]}");
            }
        }

        public void SetFreeze(bool value){
            var message = new BridgeActionModel {
                action = actionType.FREEZE,
                content = BitConverter.GetBytes(true)
            };
            instance.Logger.LogInfo($"<CLIENT> sending {message.action}");
            instance.communication.Lobby.SendBridgeAction(message);
            PopUpMessage.DisplayOkOnly(
                "Changes frozen until you sync your layout with all connected users.", 
                null
            );
            instance.serverIsFrozen = true;
        }

        public static void KickUser(string username){
            if (!instance.clientEnabled){
                //uConsole.Log("You aren't connected to anything.");
                return;
            }
           
            var content = new KickUserModel {
                username = username,
                reason = GUIValues.kickUserReason
            };
            var message = new MessageModel {
                type = LobbyMessaging.KickUser,
                content = content.Serialize()
            };

            
            instance.communication.SendRequest(message.Serialize());
        }
        public static void setPassword(){
            if (!instance.clientEnabled){
                //uConsole.Log("You aren't connected to anything.");
                return;
            }
            string password = GUIValues.changingPassword;
            var content = new ServerConfigModel {
                action = ConfigAction.CHANGE_PASSWORD,
                newPassword = password
            };
            var message = new MessageModel {
                type = LobbyMessaging.ServerConfig,
                content = content.Serialize()
            };
            instance.communication.SendRequest(message.Serialize());
        }
        public static void setUserCap(){
            if (!instance.clientEnabled){
                //uConsole.Log("You aren't connected to anything.");
                return;
            }
            int userCap;
            try {
                userCap = int.Parse(GUIValues.userCap);
            }
            catch {
                return;
            }
            var content = new ServerConfigModel {
                action = ConfigAction.USER_CAP,
                userCap = userCap
            };
            var message = new MessageModel {
                type = LobbyMessaging.ServerConfig,
                content = content.Serialize() 
            };
            instance.communication.SendRequest(message.Serialize());
        }
        public static void setAcceptConnections(){
            if (!instance.clientEnabled){
                //uConsole.Log("You aren't connected to anything.");
                return;
            }
            bool acceptingConnections = GUIValues.acceptingConnections;
            var content = new ServerConfigModel {
                action = ConfigAction.ACCEPTING_CONNECTIONS,
                acceptingConnections = acceptingConnections
            };
            var message = new MessageModel {
                type = LobbyMessaging.ServerConfig,
                content = content.Serialize() 
            };
            instance.communication.SendRequest(message.Serialize());
        }
        public static void setLobbyMode(LobbyMode mode){
            if (!instance.clientEnabled){
                //uConsole.Log("You aren't connected to anything.");
                return;
            }

            var content = new ServerConfigModel {
                action = ConfigAction.CHANGE_LOBBY_MODE,
                lobbyMode = mode
            };
            var message = new MessageModel {
                type = LobbyMessaging.ServerConfig,
                content = content.Serialize()
            };
            instance.communication.SendRequest(message.Serialize());
        }

        public static void CreateInvite(){
            if (!instance.clientEnabled){
                //uConsole.Log("You aren't connected to anything.");
                return;
            }
            int uses = 1;
            int.TryParse(GUIValues.inviteUses, out uses);
            
            GUIValues.inviteUses = uses.ToString();
            var message = new MessageModel {
                type = LobbyMessaging.CreateInvite,
                content = BitConverter.GetBytes(uses) 
            };
            instance.communication.SendRequest(message.Serialize());
        }

        public static void syncLayout(){
            if (!instance.clientEnabled){
                //uConsole.Log("You aren't connected to anything.");
                return;
            }
            if (GameStateManager.GetState() == GameState.SIM){
                PopUpMessage.DisplayOkOnly("You must exit the simulation before syncing layout.", null);
                return;
            }
            SyncLayoutModel layout = new SyncLayoutModel();
            var message = new BridgeActionModel { action = actionType.SYNC_LAYOUT };
            
            if (instance.communication.isOwner){
                if (instance.serverIsFrozen){
                    instance.communication.Lobby.SendBridgeAction(
                        new BridgeActionModel {
                            action = actionType.FREEZE,
                            content = BitConverter.GetBytes(false)
                        }
                    );
                    instance.serverIsFrozen = false;
                }
                //uConsole.Log("Force Syncing layout with all connected clients...");
                layout.layoutData = SandboxLayout.SerializeToProxies().SerializeBinary();
                layout.targetAllUsers = true;
                message.content = layout.Serialize();
                instance.communication.Lobby.SendBridgeAction(message);
                return;
            }
            if (instance.serverIsFrozen){
                //uConsole.Log("Changes are currently frozen.");
                return;
            }
            //uConsole.Log("Requesting owner for layout...");
            message.content = layout.Serialize();
            instance.communication.Lobby.SendBridgeAction(message);
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
                if (instance.preventSendActions || !instance.clientEnabled) return;
                instance.ClientRecieving.TryGetValue(actionType.CREATE_EDGE, out var ClientIsRecieving);
                if (ClientIsRecieving) return;
                if (Bridge.IsSimulating()) return;
                if (physicsEdge_onlyUsedWhenBreakingEdgesInSimulation) return;
                var edge = new BridgeEdgeProxy(__result);
                var message = new BridgeActionModel {
                    action = actionType.CREATE_EDGE,
                    content = edge.SerializeBinary(),
                    playSound = true
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
                if (instance.preventSendActions || !instance.clientEnabled) return;
                instance.ClientRecieving.TryGetValue(actionType.CREATE_JOINT, out var ClientIsRecieving);
                if (ClientIsRecieving) return;
                if (Bridge.IsSimulating()) return;
                var joint = new BridgeJointProxy(__result);
                var message = new BridgeActionModel {
                    action = actionType.CREATE_JOINT,
                    content = joint.SerializeBinary()
                };
                instance.Logger.LogInfo("<CLIENT> sending CREATE_JOINT");
                instance.communication.Lobby.SendBridgeAction(message);
        
            }
        }

        [HarmonyPatch(typeof(BridgeSelectionSet), "DeleteSelectionSet")]
        public static class EdgeAndJointDeletePatch {
            public static void Prefix() {
                if (instance.preventSendActions || !instance.clientEnabled) return;
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
                        content = joint.SerializeBinary()
                    };
                    instance.Logger.LogInfo("<CLIENT> sending DELETE_JOINT");
                    instance.communication.Lobby.SendBridgeAction(message);
                }
                bool playSound = true;
                foreach (BridgeEdge e in BridgeSelectionSet.m_Edges){
                    var joint = new BridgeEdgeProxy(e);
                    var message = new BridgeActionModel {
                        action = actionType.DELETE_EDGE,
                        content = joint.SerializeBinary(),
                        playSound = playSound
                    };
                    playSound = false;
                    instance.Logger.LogInfo("<CLIENT> sending DELETE_EDGE");
                    instance.communication.Lobby.SendBridgeAction(message);
                    
                }
                

            }

            [HarmonyPatch(typeof(BridgeJointMovement), "FinalizeMovement")]
            public static class TranslateJointPatch {
                public static void Prefix(){
                    if (instance.preventSendActions) return;
                    if (instance.preventSendActions || !instance.clientEnabled) return;
                    Vector3 translation = BridgeJointMovement.m_SelectedJoint.transform.position - BridgeJointMovement.m_SelectedJoint.m_BuildPos;
		            if (!Mathf.Approximately(translation.magnitude, 0f)){
                        var joint = new BridgeJointProxy(BridgeJointMovement.m_SelectedJoint);
                        joint.m_Pos = BridgeJointMovement.m_SelectedJoint.transform.position;
                        var message = new BridgeActionModel {
                            action = actionType.TRANSLATE_JOINT,
                            content = joint.SerializeBinary()
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
                    if (instance.preventSendActions || !instance.clientEnabled) return;
                    if (BridgeSprings.m_SliderFollowingMouse){
		            	float num = BridgeSprings.m_SliderFollowingMouse.GetNormalizedValue() - ___m_NormalizedSliderValueWhenStartMoving;
		            	if (!Mathf.Approximately(num, 0f)){
                            var spring = new BridgeSpringProxy(BridgeSprings.m_SliderFollowingMouse.m_BridgeSpring);
                            var message = new BridgeActionModel {
                                action = actionType.SPRING_SLIDER_TRANSLATE,
                                content = spring.SerializeBinary()
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
                    if (instance.preventSendActions || !instance.clientEnabled) return;
                    if (Pistons.m_SliderFollowingMouse){
		            	float num = Pistons.m_SliderFollowingMouse.GetNormalizedValue() - ___m_NormalizedSliderValueWhenStartMoving;
		            	if (!Mathf.Approximately(num, 0f)){
                            var piston = new PistonProxy(Pistons.m_SliderFollowingMouse.m_Piston);
                            var message = new BridgeActionModel {
                                action = actionType.PISTON_SLIDER_TRANSLATE,
                                content = piston.SerializeBinary()
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
                if (instance.preventSendActions || !instance.clientEnabled) return;

                actionType action = actionType.SPLIT_JOINT;
                if (joint.m_IsSplit)
		        {
		        	action = actionType.UNSPLIT_JOINT;
                    instance.Logger.LogInfo("<CLIENT> sending UNSPLIT_JOINT");
		        }
		        else 
		        {
                    if (HydraulicsPhases.m_Phases.Count < 1) return;
		        	action = actionType.SPLIT_JOINT;
                    instance.Logger.LogInfo("<CLIENT> sending SPLIT_JOINT");
		        }

                var message = new BridgeActionModel {
                    action = action,
                    content = new BridgeJointProxy(joint).SerializeBinary()
                };
                
                instance.communication.Lobby.SendBridgeAction(message);
            }
        }
        [HarmonyPatch(typeof(BridgeJointSelector), "Cycle")]
        public static class SplitJointChangeNumbersPatch {
            public static void Postfix(bool forward, ref BridgeJointSelector __instance){
                if (instance.preventSendActions || !instance.clientEnabled) return;
                var edge = new BridgeEdgeProxy(__instance.m_Edge);
                var message = new BridgeActionModel {
                    action = actionType.SPLIT_MODIFY,
                    content = edge.SerializeBinary()
                };
                
                instance.communication.Lobby.SendBridgeAction(message);
            }
        }
        // Hydraulic Controller Patches


        [HarmonyPatch(typeof(HydraulicsController), "AddSplitJointToPhase")]
        public static class AddSplitJointToPhasePatch {
            public static void Postfix(BridgeJoint joint, HydraulicsPhase hydraulicsPhase)
            {
                if (instance.preventSendActions || !instance.clientEnabled) return;
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
                        content = content.Serialize()
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
                if (instance.preventSendActions || !instance.clientEnabled) return;
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
                        content = content.Serialize()
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
                if (instance.preventSendActions || !instance.clientEnabled) return;
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
                    content = content.Serialize()
                };
                instance.Logger.LogInfo("<CLIENT> sending HYDRAULIC_CONTROLLER_ACTION - AddSplitJointToAllPhasesAcceptingNewAdditions");
                instance.communication.Lobby.SendBridgeAction(message);
	            
            }
        } 
        
        [HarmonyPatch(typeof(HydraulicsController), "RemoveSplitJointFromPhase")]
        public static class RemoveSplitJointFromPhasePatch {
            public static void Postfix(BridgeJoint joint, HydraulicsPhase hydraulicsPhase)
            {
                if (instance.preventSendActions || !instance.clientEnabled) return;
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
                        content = content.Serialize()
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
                if (instance.preventSendActions || !instance.clientEnabled) return;
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
                    content = content.Serialize()
                };
                instance.Logger.LogInfo("<CLIENT> sending HYDRAULIC_CONTROLLER_ACTION - RemoveSplitJointFromAllPhases");
                instance.communication.Lobby.SendBridgeAction(message);
	            
            }
        }
        
        //[HarmonyPatch(typeof(HydraulicsController), "RemoveJointFromAllPhases")]
        //public static class RemoveJointFromAllPhasesPatch {
        //    public static void Postfix(BridgeJoint joint)
        //    {
        //        if (instance.preventSendActions || !instance.clientEnabled) return;
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
        //            content = content.Serialize()
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
                if (instance.preventSendActions || !instance.clientEnabled) return;
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
                        content = content.Serialize()
                    };
                    instance.Logger.LogInfo("<CLIENT> sending HYDRAULIC_CONTROLLER_ACTION - RemoveSplitJointFromPhase");
                    instance.communication.Lobby.SendBridgeAction(message);
	            }
            }
        }

        [HarmonyPatch(typeof(HydraulicsController), "SetSplitJointStateForPhase")]
        public static class SetSplitJointStateForPhasePatch {
            public static void Postfix(HydraulicsPhase hydraulicsPhase, BridgeJoint joint, SplitJointState state){
                if (instance.preventSendActions || !instance.clientEnabled) return;
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
                        content = content.Serialize()
                    };
                    instance.Logger.LogInfo("<CLIENT> sending HYDRAULIC_CONTROLLER_ACTION - SetSplitJointStateForPhase");
                    instance.communication.Lobby.SendBridgeAction(message);
	            }
            }
        }

        [HarmonyPatch(typeof(HydraulicsController), "ToggleSplitJoint")]
        public static class ToggleSplitJointPatch {
            public static void Prefix(HydraulicsPhase hydraulicsPhase, BridgeJoint joint){
                if (instance.preventSendActions || !instance.clientEnabled) return;
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
                        content = content.Serialize()
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
                if (instance.preventSendActions || !instance.clientEnabled) return;
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
                        content = content.Serialize()
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
                if (instance.preventSendActions || !instance.clientEnabled) return;
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
                        content = content.Serialize()
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
                if (instance.preventSendActions || !instance.clientEnabled) return;
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
                    content = content.Serialize()
                };
                instance.Logger.LogInfo("<CLIENT> sending HYDRAULIC_CONTROLLER_ACTION - AddPistonToHydraulicsPhase");
                instance.communication.Lobby.SendBridgeAction(message);
	            
            }
        }

        [HarmonyPatch(typeof(HydraulicsController), "RemoveAllPistonsFromPhase")]
        public static class RemoveAllPistonsFromPhasePatch {
            public static void Postfix(HydraulicsPhase hydraulicsPhase)
            {
                if (instance.preventSendActions || !instance.clientEnabled) return;
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
                        content = content.Serialize()
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
                if (instance.preventSendActions || !instance.clientEnabled) return;
                if (Bridge.IsSimulating()) return;
                actionType action = actionType.HYDRAULIC_CONTROLLER_ACTION;
                
                var content = new HydraulicsControllerActionModel {
                    action = HydraulicsControllerAction.REMOVE_PISTON,
                    pistonProxySerialized = JsonUtility.ToJson(new PistonProxy(piston))
                };
	            var message = new BridgeActionModel {
                    action = action,
                    content = content.Serialize()
                };
                instance.Logger.LogInfo("<CLIENT> sending HYDRAULIC_CONTROLLER_ACTION - AddPistonToHydraulicsPhase");
                instance.communication.Lobby.SendBridgeAction(message);
	            
            }
        }

        [HarmonyPatch(typeof(HydraulicsController), "TogglePiston")]
        public static class TogglePistonPatch {
            public static void Prefix(HydraulicsPhase hydraulicsPhase, Piston piston){
                if (instance.preventSendActions || !instance.clientEnabled) return;
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
                        content = content.Serialize()
                    };
                    instance.Logger.LogInfo("<CLIENT> sending HYDRAULIC_CONTROLLER_ACTION - TogglePiston");
                    instance.communication.Lobby.SendBridgeAction(message);
	            }
            }
        }

        [HarmonyPatch(typeof(HydraulicsController), "DisableNewAdditionsFromPhase")]
        public static class DisableNewAdditionsFromPhasePatch {
            public static void Postfix(HydraulicsPhase hydraulicsPhase){
                if (instance.preventSendActions || !instance.clientEnabled) return;
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
                        content = content.Serialize()
                    };
                    instance.Logger.LogInfo("<CLIENT> sending HYDRAULIC_CONTROLLER_ACTION - DisableNewAdditionsFromPhase");
                    instance.communication.Lobby.SendBridgeAction(message);
	            }
            }
        }

        [HarmonyPatch(typeof(HydraulicsController), "EnableNewAdditionsFromPhase")]
        public static class EnableNewAdditionsFromPhasePatch {
            public static void Postfix(HydraulicsPhase hydraulicsPhase){
                if (instance.preventSendActions || !instance.clientEnabled) return;
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
                        content = content.Serialize()
                    };
                    instance.Logger.LogInfo("<CLIENT> sending HYDRAULIC_CONTROLLER_ACTION - EnableNewAdditionsFromPhase");
                    instance.communication.Lobby.SendBridgeAction(message);
	            }
            }
        }

        [HarmonyPatch(typeof(Panel_HydraulicsController), "OnThreeWayJointsToggle")]
        public static class OnThreeWayJointsTogglePatch {
            public static void Postfix(ref Panel_HydraulicsController __instance){
                if (instance.preventSendActions || !instance.clientEnabled) return;
                if (Bridge.IsSimulating()) return;
                actionType action = actionType.HYDRAULIC_CONTROLLER_ACTION;
                var content = new HydraulicsControllerActionModel {
                    action = HydraulicsControllerAction.SET_THREE_WAY_SPLIT_JOINT_TOGGLE_STATE,
                    ThreeWaySplitJointToggleState = __instance.m_ThreeWayJointsToggle.isOn
                };
                var message = new BridgeActionModel {
                    action = action,
                    content = content.Serialize()
                };
                instance.Logger.LogInfo("<CLIENT> sending HYDRAULIC_CONTROLLER_ACTION - OnThreeWayJointsToggle");
                instance.communication.Lobby.SendBridgeAction(message);
            }
        }

        // COPY/CUT/PASTE SUPPORT
        [HarmonyPatch(typeof(ClipboardManager), "MaybeSplitPastedJoint")]
        public static class MaybeSplitPastedJointPatch {
            public static void Postfix(BridgeJoint pastedJoint, BridgeJoint sourceJoint){
                if (instance.preventSendActions || !instance.clientEnabled) return;
                actionType action = actionType.SPLIT_JOINT;
                
                if (pastedJoint && pastedJoint.m_IsSplit)
		        {
                    var message = new BridgeActionModel {
                        action = action,
                        content = new BridgeJointProxy(pastedJoint).SerializeBinary()
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
                if (instance.preventSendActions || !instance.clientEnabled) return;
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
                if (instance.preventSendActions || !instance.clientEnabled) return;
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
                    message.content = packet.m_Edge.SerializeBinary();
                    break;
                
                case actionType.CREATE_JOINT:
                    message.action = actionType.DELETE_JOINT;
                    message.content = packet.m_Joint.SerializeBinary();
                    break;
                
                case actionType.DELETE_EDGE:
                    message.action = actionType.CREATE_EDGE;
                    message.content = packet.m_Edge.SerializeBinary();
                    break;
                
                case actionType.DELETE_JOINT:
                    message.action = actionType.CREATE_JOINT;
                    message.content = packet.m_Joint.SerializeBinary();
                    break;
                
                case actionType.SPLIT_JOINT:
                    message.action = actionType.UNSPLIT_JOINT;
                    message.content = packet.m_Joint.SerializeBinary();
                    break;

                case actionType.UNSPLIT_JOINT:
                    message.action = actionType.SPLIT_JOINT;
                    message.content = packet.m_Joint.SerializeBinary();
                    break;
                
                case actionType.TRANSLATE_JOINT:
                    message.action = actionType.TRANSLATE_JOINT;
                    packet.m_Joint.m_Pos = BridgeJoints.FindByGuid(packet.m_Joint.m_Guid).m_BuildPos;
                    message.content = packet.m_Joint.SerializeBinary();
                    break;
                
                case actionType.PISTON_SLIDER_TRANSLATE:
                    message.action = actionType.PISTON_SLIDER_TRANSLATE;
                    Piston piston = Pistons.FindByGuid(packet.m_Piston.m_Guid);
                    packet.m_Piston.m_NormalizedValue = piston.m_Slider.GetNormalizedValue();
                    message.content = packet.m_Piston.SerializeBinary();
                    break;
                
                case actionType.SPRING_SLIDER_TRANSLATE:
                    message.action = actionType.SPRING_SLIDER_TRANSLATE;
                    BridgeSpring spring = BridgeSprings.FindByGuid(packet.m_Spring.m_Guid);
                    packet.m_Spring.m_NormalizedValue = spring.m_Slider.GetNormalizedValue();
                    message.content = packet.m_Spring.SerializeBinary();
                    break;
                default:
                    instance.Logger.LogError($"Invalid undo action - {action}");
                    return;
            }
            instance.Logger.LogInfo($"<CLIENT> sending {message.action} - UndoHandle");
            instance.communication.Lobby.SendBridgeAction(message);
        }


        
        [HarmonyPatch]
        public static class PauseMenuPatch {
            static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(Panel_PauseMenu), "ExitToMainMenu");
                yield return AccessTools.Method(typeof(Panel_PauseMenu), "ExitToCampaignMenu");
                yield return AccessTools.Method(typeof(Panel_PauseMenu), "ExitToWorkshop");
                yield return AccessTools.Method(typeof(Panel_LevelFailed), "OnExit");
                yield return AccessTools.Method(typeof(Panel_LevelComplete), "OnExit");
                yield return AccessTools.Method(typeof(Panel_LevelComplete), "OnNextLevel");
                yield return AccessTools.Method(typeof(Panel_TopBar), "OnEdit");

            }
            public static bool Prefix(){
                if (!instance.clientEnabled || instance.serverIsFrozen) return true;
                if (instance.communication.isOwner){
                    instance.SetFreeze(true);
                    return true;
                }
                else {
                    PopUpMessage.DisplayOkOnly(
                        "Please disconnect from the active session before exiting.", 
                        null
                    );
                    return false;
                }
            }
        }

        [HarmonyPatch(typeof(Panel_PauseMenu), "OnLoadGame")]
        public static class LoadButtonPatch {
            public static bool Prefix(){
                if (!instance.clientEnabled || instance.serverIsFrozen) return true;
                if (!instance.communication.isOwner){
                    PopUpMessage.DisplayOkOnly(
                        "Please disconnect from the active session before trying to load a layout/bridge.", 
                        null
                    );
                    return false;
                }
                return true;
            }
        }


        public static string GetJustStringFromBytes(byte[] bytes)
        {
            int offset = 0;
        	ushort num = BitConverter.ToUInt16(bytes, offset);
        	offset += 2;
        	if (num > 0)
        	{
        		string @string = Encoding.UTF8.GetString(bytes, offset, (int)num);
        		offset += (int)num;
        		return @string;
        	}
        	return "";
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
        SYNC_LAYOUT,
        FREEZE
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
    public class HydraulicsControllerActionModel {
        public HydraulicsControllerActionModel(){}
        public HydraulicsControllerActionModel(byte[] bytes, ref int offset){
            this.Deserialize(bytes, ref offset);
        }
        public HydraulicsControllerAction action = HydraulicsControllerAction.ADD_SPLIT_JOINT;
        public string phaseGuid = "";
        public string jointGuid = "";
        public string pistonProxySerialized = "";
        public SplitJointState splitJointState = SplitJointState.NONE_SPLIT;
        public bool ThreeWaySplitJointToggleState = false;
        public bool doForEveryPhase = false;
        public bool phaseMustBeAcceptingAdditions = false;
        public bool doForEverySplitJoint = false;
        public bool doForEveryPiston = false;
        public bool DisableAdditonsState = false;
        public bool weirdRemoveFlagForJointBeingDestroyed = false;

        public byte[] Serialize(){
            List<byte> list = new List<byte>();
            list.AddRange(ByteSerializer.SerializeInt((int)this.action));
            list.AddRange(ByteSerializer.SerializeString(this.phaseGuid));
            list.AddRange(ByteSerializer.SerializeString(this.jointGuid));
            list.AddRange(ByteSerializer.SerializeString(this.pistonProxySerialized));
            list.AddRange(ByteSerializer.SerializeInt((int)this.splitJointState));
            list.AddRange(ByteSerializer.SerializeBool(this.ThreeWaySplitJointToggleState));
            list.AddRange(ByteSerializer.SerializeBool(this.doForEveryPhase));
            list.AddRange(ByteSerializer.SerializeBool(this.phaseMustBeAcceptingAdditions));
            list.AddRange(ByteSerializer.SerializeBool(this.doForEverySplitJoint));
            list.AddRange(ByteSerializer.SerializeBool(this.doForEveryPiston));
            list.AddRange(ByteSerializer.SerializeBool(this.DisableAdditonsState));
            list.AddRange(ByteSerializer.SerializeBool(this.weirdRemoveFlagForJointBeingDestroyed));
            return list.ToArray();
        }
        public void Deserialize(byte[] bytes, ref int offset){
            this.action = (HydraulicsControllerAction)ByteSerializer.DeserializeInt(bytes, ref offset);
            this.phaseGuid = ByteSerializer.DeserializeString(bytes, ref offset);
            this.jointGuid = ByteSerializer.DeserializeString(bytes, ref offset);
            this.pistonProxySerialized = ByteSerializer.DeserializeString(bytes, ref offset);
            this.splitJointState = (SplitJointState)ByteSerializer.DeserializeInt(bytes, ref offset);
            this.ThreeWaySplitJointToggleState = ByteSerializer.DeserializeBool(bytes, ref offset);
            this.doForEveryPhase = ByteSerializer.DeserializeBool(bytes, ref offset);
            this.phaseMustBeAcceptingAdditions = ByteSerializer.DeserializeBool(bytes, ref offset);
            this.doForEverySplitJoint = ByteSerializer.DeserializeBool(bytes, ref offset);
            this.doForEveryPiston = ByteSerializer.DeserializeBool(bytes, ref offset);
            this.DisableAdditonsState = ByteSerializer.DeserializeBool(bytes, ref offset);
            this.weirdRemoveFlagForJointBeingDestroyed = ByteSerializer.DeserializeBool(bytes, ref offset);
        }
    }

    [System.Serializable]
    public class SyncLayoutModel {
        public SyncLayoutModel(){}
        public SyncLayoutModel(byte[] bytes, ref int offset){
            this.Deserialize(bytes, ref offset);
        }
        public byte[] layoutData;
        public bool targetAllUsers;

        public byte[] Serialize(){
            List<byte> list = new List<byte>();
            list.AddRange(ByteSerializer.SerializeBool(this.targetAllUsers));
            list.AddRange(ByteSerializer.SerializeByteArray(this.layoutData));
            return list.ToArray();
        }
        public void Deserialize(byte[] bytes, ref int offset){
            this.targetAllUsers = ByteSerializer.DeserializeBool(bytes, ref offset);
            this.layoutData = ByteSerializer.DeserializeByteArray(bytes, ref offset);
        }
    }
    
    public class SyncRequest {
        public bool requestActive = false;
        public BridgeActionModel message = null;
    }
    
}
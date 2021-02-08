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
namespace MultiplayerMod
{
    public partial class MultiplayerMod {
        internal Rect WindowRect { get; private set; }
        internal int LeftColumnWidth { get; private set; }
        internal int RightColumnWidth { get; private set; }
        private Rect _screenRect;
        private bool _displayingWindow = false;
        private Texture2D WindowBackground;
        private int inputWidth = 250;
        public Vector2 scrollPosition;

        public void Start(){
            var windowBackground = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            windowBackground.SetPixel(0, 0, new Color(0.5f, 0.5f, 0.5f, 1));
            windowBackground.Apply();
            WindowBackground = windowBackground;
        }
        private void OnGUI(){
            if (DisplayingWindow)
            {
                if (Event.current.type == UnityEngine.EventType.KeyUp && Event.current.keyCode == _keybind.Value.MainKey)
                {
                    DisplayingWindow = false;
                    return;
                }

                if (GUI.Button(_screenRect, string.Empty, GUI.skin.box) &&
                        !WindowRect.Contains(Input.mousePosition))
                        DisplayingWindow = false;

                GUI.Box(WindowRect, GUIContent.none, new GUIStyle { normal = new GUIStyleState { background = WindowBackground } });
                WindowRect = GUILayout.Window(-69, WindowRect, MultiplayerWindow, "Multiplayer Mod");
                EatInputInRect(WindowRect);
            }
        }
        private void CalculateWindowRect()
        {
            var width = Mathf.Min(Screen.width, 650);
            var height = Screen.height < 560 ? Screen.height : Screen.height - 100;
            var offsetX = Mathf.RoundToInt((Screen.width - width) / 2f);
            var offsetY = Mathf.RoundToInt((Screen.height - height) / 2f);
            WindowRect = new Rect(offsetX, offsetY, width, height);

            _screenRect = new Rect(0, 0, Screen.width, Screen.height);

            LeftColumnWidth = Mathf.RoundToInt(WindowRect.width / 2.5f);
            RightColumnWidth = (int)WindowRect.width - LeftColumnWidth - 115;
        }

        public static void Horizontal(System.Action block, GUIStyle style = null){
            if (style != null) GUILayout.BeginHorizontal(style);
            else GUILayout.BeginHorizontal();
		    block();
		    GUILayout.EndHorizontal();
        }
        public static void Vertical(System.Action block, GUIStyle style = null){
            if (style != null) GUILayout.BeginVertical(style);
            else GUILayout.BeginVertical();
		    block();
		    GUILayout.EndVertical();
        }

        public static void DrawHeader(string text){
            var _style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15
            };
            GUILayout.Label(text, _style);
        }

        private void MultiplayerWindow(int id){
            try {
                scrollPosition = GUILayout.BeginScrollView(scrollPosition);
                // top info / credits
                GUILayout.BeginHorizontal(GUI.skin.box);
                {
                    GUILayout.Label("Mod created by Conqu3red.", GUILayout.ExpandWidth(false));
                }
                GUILayout.EndHorizontal();

                // connection
                Vertical(() =>
                {
                    Horizontal(() => {
                        GUILayout.FlexibleSpace();
                        DrawHeader("Connection");
                        GUILayout.FlexibleSpace();
                    });
                    Horizontal(() =>
                    {
                        GUILayout.Label("Server IP:");
                        GUIValues.ip = GUILayout.TextField(GUIValues.ip, GUILayout.Width(inputWidth));
                    });
                    Horizontal(() =>
                    {
                        GUILayout.Label("Server Port:");
                        GUIValues.port = GUILayout.TextField(GUIValues.port, GUILayout.Width(inputWidth));
                    });
                    Horizontal(() =>
                    {
                        GUILayout.Label("Session Name:");
                        GUIValues.sessionName = GUILayout.TextField(GUIValues.sessionName, GUILayout.Width(inputWidth));
                    });
                    Horizontal(() =>
                    {
                        GUILayout.Label("(optional) Session Password:");
                        GUIValues.password = GUILayout.PasswordField(GUIValues.password, '*', GUILayout.Width(inputWidth));
                    });
                    Horizontal(() =>
                    {
                        GUILayout.Label("(optional) Session Invite:");
                        GUIValues.invite = GUILayout.PasswordField(GUIValues.invite, '*', GUILayout.Width(inputWidth));
                    });
                    Horizontal(() => {
                        GUIValues.secureConnection = GUILayout.Toggle(GUIValues.secureConnection, "Connect using TLS/SSL");
                    });
                    if (clientConnecting){
                        GUILayout.Button("Connecting...");
                    }
                    else if (!clientEnabled){
                        if (GUILayout.Button("Connect")){
                            Connect();
                        }
                    }
                    else {
                        if (GUILayout.Button(communication.isOwner ? "Disconnect (Closes Session)" : "Disconnect")){
                            Disconnect();
                        }
                    }
                    if (GUIValues.ConnectionResponse != ""){
                        GUILayout.Label(GUIValues.ConnectionResponse);
                    }

                }, GUI.skin.box);

                if (clientEnabled && communication.isOwner){
                    Vertical(() =>
                    {
                        Horizontal(() => {
                            GUILayout.FlexibleSpace();
                            DrawHeader("Host Options");
                            GUILayout.FlexibleSpace();
                        });
                        // set password
                        Horizontal(() => {
                            GUIValues.changingPassword = GUILayout.PasswordField(GUIValues.changingPassword, '*', GUILayout.Width(inputWidth));
                            if (GUILayout.Button("Set password")){
                                setPassword();
                            }
                        });
                        Horizontal(() => {
                            GUIValues.userCap = GUILayout.TextField(GUIValues.userCap, GUILayout.Width(inputWidth));
                            if (GUILayout.Button("Set user cap")){
                                setUserCap();
                            }
                        });
                        Horizontal(() => {
                            GUIValues.lobbyMode = GUILayout.TextField(GUIValues.lobbyMode, GUILayout.Width(inputWidth));
                            if (GUILayout.Button("Set lobby mode")){
                                setLobbyMode();
                            }
                        });
                        GUILayout.Label("Valid Lobby modes are: public, password_locked, invite_only");
                        Horizontal(() => {
                            GUIValues.acceptingConnections = GUILayout.Toggle(GUIValues.acceptingConnections, "Accepting Connections");
                            if (GUIValues.acceptingConnections != GUIValues._acceptingConnectionsPrevFrame){
                                setAcceptConnections();
                            }
                            GUIValues._acceptingConnectionsPrevFrame = GUIValues.acceptingConnections;
                        });
                        
                        // create invites
                        Horizontal(() => {
                            GUILayout.Label("number of allowed invite uses:");
                            GUIValues.inviteUses = GUILayout.TextField(GUIValues.inviteUses, GUILayout.Width(inputWidth));
                        });
                        if (GUILayout.Button("Create invite")){
                            CreateInvite();
                        }
                        // revoke invite/clear invites (backend implementation needed)
                        if (GUIValues.InviteResponse != "") GUILayout.TextField(GUIValues.InviteResponse);

                        // sync with all users
                        if (GUILayout.Button("Sync layout with all connected users")){
                            syncLayout();
                        }
                        
                        // Kick user
                        Horizontal(() => {
                            GUILayout.Label("Username to kick:");
                            GUIValues.kickUser = GUILayout.TextField(GUIValues.kickUser, GUILayout.Width(inputWidth));
                        });
                        Horizontal(() => {
                            GUILayout.Label("(optional) Kick reason:");
                            GUIValues.kickUserReason = GUILayout.TextField(GUIValues.kickUserReason, GUILayout.Width(inputWidth));
                        });
                        Horizontal(() => {
                            if (GUILayout.Button("Kick user")){
                                KickUser();
                            }
                        });
                        if (GUIValues.kickResponse != ""){
                            GUILayout.Label(GUIValues.kickResponse);
                        }
                        
                        // set lobby mode
                    }, GUI.skin.box);
                }
                else if (clientEnabled){
                    Vertical(() =>
                    {
                        Horizontal(() => {
                            GUILayout.FlexibleSpace();
                            DrawHeader("Client Options");
                            GUILayout.FlexibleSpace();
                        });
                        // sync layout
                        if (GUILayout.Button("Request layout sync from host")){
                            syncLayout();
                        }
                    }, GUI.skin.box);
                }
                if (clientEnabled){
                    Vertical(() =>
                    {
                        Horizontal(() => {
                            GUILayout.FlexibleSpace();
                            DrawHeader("Connection Details");
                            GUILayout.FlexibleSpace();
                        });
                        GUILayout.Label(GUIValues.serverInfoString);
                        if (GUILayout.Button("Refresh")) ConnectionInfo();
                    }, GUI.skin.box);
                }


                GUILayout.EndScrollView();
            }
            catch (ArgumentException ex){
                instance.Logger.LogError(ex.Message);
            }
        }
        
        public static void EatInputInRect(Rect eatRect)
        {
            if (eatRect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)))
                Input.ResetInputAxes();
        }

        public bool DisplayingWindow
        {
            get => _displayingWindow;
            set
            {
                if (_displayingWindow == value) return;
                _displayingWindow = value;

                if (_displayingWindow)
                {
                    CalculateWindowRect();
                }
            }
        }


        public static class GUIValues {
            public static string ip = "";
            public static string port = "";
            public static string sessionName = "";
            public static string invite = "";
            public static string password = "";
            public static string changingPassword = "";
            public static string userCap = "";
            public static bool acceptingConnections = true;
            public static bool _acceptingConnectionsPrevFrame = true;
            public static string kickUser = "";
            public static string kickUserReason = "";
            public static string kickResponse = "";
            public static string serverInfoString = "";
            public static string ConnectionResponse = "";
            public static string ConfigResponse = "";
            public static string InviteResponse = "";
            public static string inviteUses = "1";
            public static string lobbyMode = "";
            public static bool secureConnection = false;
            public static void resetMessages(){
                GUIValues.ConnectionResponse = "";
                GUIValues.InviteResponse = "";
                GUIValues.kickResponse = "";
                GUIValues.serverInfoString = "";
            }
        }
    }
}
using P2PMod;
using System.Collections.Generic;
/// <summary>
/// Echo message model.
/// </summary>
[System.Serializable]
public class SetPasswordModel
{
    public string newPassword;
}
[System.Serializable]
public class SetUserCapModel
{
    public int userCap;
}

[System.Serializable]
public class SetAcceptingConnectionsModel
{
    public bool acceptingConnections;
}

[System.Serializable]
public class SetLobbyModeModel
{
    public LobbyMode lobbyMode;

}

[System.Serializable]
public class ServerConfigModel
{
    public int userCap;
    public bool acceptingConnections;
    public string newPassword;
    public LobbyMode lobbyMode;
}

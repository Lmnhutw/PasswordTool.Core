namespace PasswordTool.Presentation;

public enum AppFlowState
{
    Loading,
    FirstLaunch,
    Recover,
    CreateMasterPassword,
    SetupAuthenticator,
    Unlock,
    Unlocked
}

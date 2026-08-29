using System.Threading;
using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.Auth.Microsoft;
using XboxAuthNet.Game;

namespace TopuLauncher;

internal static class CmlLibCompatibilityExtensions
{
    // CmlLib.Core.Auth.Microsoft 3.3.1 exposes JEGameAccount as a value type,
    // while the launcher source was written against the newer Authenticate(account)
    // overload. Keep the existing launcher code intact and bridge that API here.
    public static async Task<MSession> Authenticate(
        this JELoginHandler handler,
        JEGameAccount account)
    {
        var authenticator = handler.CreateAuthenticator(account, default(CancellationToken));
        authenticator.AddMicrosoftOAuthForJE(oauth => oauth.Silent());
        authenticator.AddXboxAuthForJE(xbox => xbox.Basic());
        authenticator.AddJEAuthenticator();
        return await authenticator.ExecuteForLauncherAsync();
    }

    // The old launcher calls Signout(selectedAccount). The 3.3.1 API's
    // selected-account signout is not available with the same overload shape,
    // so sign out the active Microsoft session while leaving saved account data
    // intact until the user explicitly removes it from the launcher.
    public static Task Signout(
        this JELoginHandler handler,
        JEGameAccount account)
    {
        return handler.Signout();
    }
}

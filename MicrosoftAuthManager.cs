using System;
using System.Diagnostics;
using System.Threading.Tasks;
using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using XboxAuthNet.Game.OAuth;

namespace TopuLauncher
{
    public class MicrosoftAuthManager
    {
        private readonly JELoginHandler _loginHandler;

        public MicrosoftAuthManager()
        {
            // Initialize CmlLib Microsoft Authentication Handler
            _loginHandler = JELoginHandlerBuilder.BuildDefault();
        }

        public async Task<MSession?> AuthenticateWithDeviceCode(Action<string, string> onCodeReceived)
        {
            try
            {
                // Authenticate using Microsoft OAuth Device Code
                var session = await _loginHandler.AuthenticateInteractively(deviceCode =>
                {
                    // Pass code and URL to UI callback
                    onCodeReceived?.Invoke(deviceCode.UserCode, deviceCode.VerificationUrl);

                    // Auto-open browser on user's machine
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = deviceCode.VerificationUrl,
                        UseShellExecute = true
                    });

                    return Task.CompletedTask;
                });

                return session;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Microsoft Auth Error: {ex.Message}");
                return null;
            }
        }
    }
}

using System.Threading.Tasks;
using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using XboxAuthNet.Game.OAuth;

namespace launcher_m.Core
{
    public class MicrosoftAuthService
    {
        public async Task<MSession> LoginInteractive()
        {
            var loginHandler = new JELoginHandlerBuilder().Build();

            await loginHandler.Signout();

            return await loginHandler.Authenticate();
        }

        public async Task<MSession> LoginSilent()
        {
            var loginHandler = new JELoginHandlerBuilder().Build();
            return await loginHandler.AuthenticateSilently();
        }
    }
}
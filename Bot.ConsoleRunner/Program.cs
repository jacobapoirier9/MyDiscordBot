using Bot.Library;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace Bot.ConsoleRunner
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (System.Diagnostics.Debugger.IsAttached)
            {
                await new BotRunner().RunAsync(args);
            }
            else
            {
                #pragma warning disable CA1416
                ServiceBase.Run(new Service());
            }
        }
    }

    public class Service : ServiceBase
    {
        protected override async void OnStart(string[] args)
        {
            await new BotRunner().RunAsync(args);
            base.OnStart(args);
        }
    }
}

using enviro.Factories;
using enviro.Models;
using enviro.Services;
using Microsoft.Extensions.DependencyInjection;

namespace enviro
{
    internal static class Program
    {
        [STAThread]
        public static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var services = new ServiceCollection();

            services.AddSingleton<IEnvService, EnvService>();

            services.AddSingleton<IContextMenuFactory, ContextMenuFactory>();
            services.AddSingleton<IPathGridFactory, PathGridFactory>();
            services.AddSingleton<ITabFactory, TabFactory>();

            services.AddSingleton<IUpdateService, UpdateService>();
            services.AddSingleton<IPathAdapter, EnvAdapter>();

            services.AddSingleton<MetadataRepository>();

            services.AddSingleton<IActionFactory<EnvModel>, EntryFormFactory>();

            services.AddTransient<MainForm>();
            services.AddTransient<AboutForm>();

            var provider = services.BuildServiceProvider();
            var mainForm = provider.GetRequiredService<MainForm>();

            Application.Run(mainForm);
        }
    }
}

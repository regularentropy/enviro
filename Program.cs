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
            services.AddSingleton<IConfigService, ConfigService>();

            services.AddSingleton<MetadataRepository>();

            services.AddSingleton<IActionFactory<EnvModel>, EntryFormFactory>();

            services.AddTransient<MainForm>();
            services.AddTransient<AboutForm>();

            var provider = services.BuildServiceProvider();

            var configManager = provider.GetRequiredService<IConfigService>();

            var mainForm = provider.GetRequiredService<MainForm>();

            // Saving config only when it was changed
            Application.ApplicationExit += (s, e) =>
            {
                if (configManager.IsDirty) configManager.Save();
            };

            Application.Run(mainForm);
        }
    }
}

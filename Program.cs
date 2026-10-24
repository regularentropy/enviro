using enviro.Factories;
using enviro.Models;
using enviro.Services;
using enviro.Static;
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

            // Built before the container: the crash path must not depend on DI.
            var appLog = new AppLog();
            Exception? lastReported = null;

            void ReportUnhandled(Exception? exception)
            {
                // One log entry and one dialog per exception, even when both handlers see it.
                if (exception is null || ReferenceEquals(exception, lastReported))
                    return;

                lastReported = exception;
                appLog.Error("Unhandled exception", exception);
                DialogHelper.ShowError(
                    $"Enviro hit an unexpected error. The details were written to:\n{appLog.FilePath}",
                    "Unexpected error");
            }

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => ReportUnhandled(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) => ReportUnhandled(e.ExceptionObject as Exception);

            var services = new ServiceCollection();

            services.AddSingleton<IAppLog>(appLog);
            services.AddSingleton<IEnvService, EnvService>();

            services.AddSingleton<IContextMenuFactory, ContextMenuFactory>();
            services.AddSingleton<IEnvGridFactory, EnvGridFactory>();
            services.AddSingleton<ITabFactory, TabFactory>();

            services.AddSingleton<ICorruptedPathService, CorruptedPathService>();

            services.AddSingleton<IUpdateService, UpdateService>();
            services.AddSingleton<IEnvAdapter, EnvAdapter>();
            services.AddSingleton<IConfigService, ConfigService>();

            services.AddSingleton<MetadataRepository>();

            services.AddSingleton<IActionFactory<EnvModel>, EntryFormFactory>();

            // The shell orchestrates an apply through this; it owns no UI.
            services.AddSingleton<ApplyCoordinator>();

            // A factory delegate instead of the container: MainForm resolves nothing itself.
            services.AddSingleton<Func<AboutForm>>(sp => () => sp.GetRequiredService<AboutForm>());

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

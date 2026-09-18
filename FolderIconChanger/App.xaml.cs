using System.Windows;
using System.Windows.Threading;
using FolderIconChanger.Cli;
using FolderIconChanger.Helpers;

namespace FolderIconChanger;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppLog.Initialize();
        AppLog.Info("Application starting.");

        if (e.Args.Length > 0 && CliRunner.TryRun(e.Args, out int exitCode))
        {
            Shutdown(exitCode);
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLog.Error(args.ExceptionObject as Exception, "Unhandled AppDomain exception.");

        Application.Current.MainWindow = new FolderIconChanger.MainWindow();
        Application.Current.MainWindow.Show();
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLog.Error(e.Exception, "Dispatcher unhandled exception.");
        e.Handled = true;

        if (Application.Current.MainWindow is FolderIconChanger.MainWindow window)
        {
            window.ShowUnexpectedError();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppLog.Info("Application exiting.");
        base.OnExit(e);
    }
}
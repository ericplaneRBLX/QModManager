namespace QModManager.Patching
{
    using System;
    using System.IO;
    using System.Reflection;
    using API;
    using Checks;
    using HarmonyLib;
    using Utility;

    internal static class Patcher
    {
        internal const string IDRegex = "[^0-9a-zA-Z_]";

        internal static string QModBaseDir => Path.Combine(Environment.CurrentDirectory, "QMods");

        private static bool Patched = false;
        internal static QModGame CurrentlyRunningGame { get; private set; } = QModGame.None;
        internal static int ErrorModCount { get; private set; }

        internal static void Patch()
        {
            try
            {
                if (Patched)
                {
                    Logger.Warn("Patch method was called multiple times!");
                    return; // Halt patching
                }

                Patched = true;

                var gameDetector = new GameDetector();

                if (!gameDetector.IsValidGameRunning || !gameDetector.IsValidGameVersion)
                    return;

                CurrentlyRunningGame = gameDetector.CurrentlyRunningGame;

                if (QModBaseDir == null)
                {
                    Logger.Fatal("A fatal error has occurred.");
                    Logger.Fatal("There was an error with the QMods directory");
                    Logger.Fatal("Please make sure that you ran Subnautica from Steam/Epic/Discord, and not from the executable file!");

                    new Dialog()
                    {
                        message = "A fatal error has occurred. QModManager could not be initialized.",
                        color = Dialog.DialogColor.Red,
                        leftButton = Dialog.Button.SeeLog,
                        rightButton = Dialog.Button.Close,
                    }.Show();

                    return;
                }

                try
                {
                    Logger.Info("Folder structure:");
                    IOUtilities.LogFolderStructureAsTree();
                    Logger.Info("Folder structure ended.");
                }
                catch (Exception e)
                {
                    Logger.Error("There was an error while trying to display the folder structure.");
                    Logger.Exception(e);
                }

                PatchHarmony();

                if (NitroxCheck.IsInstalled)
                {
                    Logger.Fatal($"Nitrox was detected!");

                    new Dialog()
                    {
                        message = "Both QModManager and Nitrox detected. QModManager is not compatible with Nitrox. Please uninstall one of them.",
                        leftButton = Dialog.Button.Disabled,
                        rightButton = Dialog.Button.Disabled,
                        color = Dialog.DialogColor.Red,
                    }.Show();

                    return;
                }

                VersionCheck.Check();

                Logger.Info("Started loading mods");

                AddAssemblyResolveEvent();
            }
            catch (FatalPatchingException pEx)
            {
                Logger.Fatal($"A fatal patching exception has been caught! Patching ended prematurely!");
                Logger.Exception(pEx);

                new Dialog()
                {
                    message = "A fatal patching exception has been caught. QModManager could not be initialized.",
                    color = Dialog.DialogColor.Red,
                    leftButton = Dialog.Button.SeeLog,
                }.Show();
            }
            catch (Exception e)
            {
                Logger.Fatal("An unhandled exception has been caught! Patching ended prematurely!");
                Logger.Exception(e);

                new Dialog()
                {
                    message = "An unhandled exception has been caught. QModManager could not be initialized.",
                    color = Dialog.DialogColor.Red,
                    leftButton = Dialog.Button.SeeLog,
                }.Show();
            }
        }

        private static void AddAssemblyResolveEvent()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                FileInfo[] allDlls = new DirectoryInfo(QModBaseDir).GetFiles("*.dll", SearchOption.AllDirectories);
                foreach (FileInfo dll in allDlls)
                {
                    if (args.Name.Contains(Path.GetFileNameWithoutExtension(dll.Name)))
                    {
                        return Assembly.LoadFrom(dll.FullName);
                    }
                }

                return null;
            };

            Logger.Debug("Added AssemblyResolve event");
        }

        // Store the instance for use by MainMenuMessages
        internal static Harmony hInstance;
        private static void PatchHarmony()
        {
            try
            {
                Logger.Debug("Applying Harmony patches...");

                hInstance = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), "qmodmanager");

                Logger.Debug("Patched!");
            }
            catch (Exception e)
            {
                Logger.Error("There was an error while trying to apply Harmony patches.");
                Logger.Exception(e);
            }
        }
    }
}

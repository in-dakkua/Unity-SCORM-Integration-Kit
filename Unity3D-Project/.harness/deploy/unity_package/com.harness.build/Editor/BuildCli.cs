// BuildCli.cs — Punto de entrada de build en batchmode del modulo de
// despliegue del harness (.harness/deploy). Lo invoca .harness/deploy/deploy.ps1:
//
//   Unity.exe -batchmode -quit -projectPath <p> -buildTarget Android|iOS
//     -executeMethod HarnessBuild.BuildCli.Build
//     -harnessOut <ruta> [-harnessVersion 1.2.0] [-harnessBuildNumber 58]
//     [-harnessBundleId com.x.y] [-harnessTeamId ABCDE12345]
//     [-harnessDevelopment] -logFile <log>
//
// Secretos de firma Android: SOLO por variables de entorno del proceso
// (HARNESS_KEYSTORE_PATH, HARNESS_KEYSTORE_PASS, HARNESS_KEYALIAS_NAME,
// HARNESS_KEYALIAS_PASS). Nunca por argumentos (se verian en la lista de
// procesos) ni escritos en disco.
//
// Todos los cambios de PlayerSettings que hace este script se RESTAURAN al
// terminar: el build no deja el proyecto del developer modificado.
// Compatible con Unity 2022.3 y 6000.x. Addressables se detecta por
// reflexion para no obligar a depender del paquete.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HarnessBuild
{
    public static class BuildCli
    {
        const string Tag = "[HarnessBuild]";

        public static void Build()
        {
            int exitCode = 1;
            try
            {
                exitCode = Run(Environment.GetCommandLineArgs()) ? 0 : 1;
            }
            catch (Exception e)
            {
                Debug.LogError($"{Tag} FAIL excepcion: {e}");
                exitCode = 1;
            }
            Debug.Log($"{Tag} RESULT exit={exitCode}");
            EditorApplication.Exit(exitCode);
        }

        static bool Run(string[] args)
        {
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            if (target != BuildTarget.Android && target != BuildTarget.iOS)
            {
                Debug.LogError($"{Tag} FAIL buildTarget activo no soportado: {target}. Pasa -buildTarget Android|iOS.");
                return false;
            }

            string outPath = Arg(args, "-harnessOut");
            if (string.IsNullOrEmpty(outPath))
            {
                Debug.LogError($"{Tag} FAIL falta -harnessOut");
                return false;
            }

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError($"{Tag} FAIL no hay escenas activas en Build Settings.");
                return false;
            }

            var group = target == BuildTarget.Android ? BuildTargetGroup.Android : BuildTargetGroup.iOS;
            var named = NamedBuildTarget.FromBuildTargetGroup(group);
            var restore = new List<Action>();

            try
            {
                string version = Arg(args, "-harnessVersion");
                if (!string.IsNullOrEmpty(version))
                {
                    string old = PlayerSettings.bundleVersion;
                    restore.Add(() => PlayerSettings.bundleVersion = old);
                    PlayerSettings.bundleVersion = version;
                }

                string bundleId = Arg(args, "-harnessBundleId");
                if (!string.IsNullOrEmpty(bundleId))
                {
                    string old = PlayerSettings.GetApplicationIdentifier(named);
                    restore.Add(() => PlayerSettings.SetApplicationIdentifier(named, old));
                    PlayerSettings.SetApplicationIdentifier(named, bundleId);
                }

                string buildNumber = Arg(args, "-harnessBuildNumber");
                bool development = HasFlag(args, "-harnessDevelopment");

                if (target == BuildTarget.Android)
                {
                    if (!ConfigureAndroid(buildNumber, restore)) return false;
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
                }
                else
                {
                    ConfigureIos(buildNumber, Arg(args, "-harnessTeamId"), restore);
                    Directory.CreateDirectory(outPath);
                }

                if (!BuildAddressablesIfPresent()) return false;

                Debug.Log($"{Tag} INFO target={target} id={PlayerSettings.GetApplicationIdentifier(named)} " +
                          $"version={PlayerSettings.bundleVersion} scenes={scenes.Length} out={outPath}");

                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outPath,
                    target = target,
                    targetGroup = group,
                    options = development ? BuildOptions.Development : BuildOptions.None,
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                var summary = report.summary;
                Debug.Log($"{Tag} INFO result={summary.result} errors={summary.totalErrors} " +
                          $"warnings={summary.totalWarnings} size={summary.totalSize} time={summary.totalTime}");
                return summary.result == BuildResult.Succeeded;
            }
            finally
            {
                for (int i = restore.Count - 1; i >= 0; i--)
                {
                    try { restore[i](); } catch (Exception e) { Debug.LogWarning($"{Tag} WARN no se pudo restaurar un ajuste: {e.Message}"); }
                }
            }
        }

        static bool ConfigureAndroid(string buildNumber, List<Action> restore)
        {
            if (!string.IsNullOrEmpty(buildNumber))
            {
                if (!int.TryParse(buildNumber, out int code) || code <= 0)
                {
                    Debug.LogError($"{Tag} FAIL -harnessBuildNumber debe ser un entero > 0 en Android (versionCode).");
                    return false;
                }
                int oldCode = PlayerSettings.Android.bundleVersionCode;
                restore.Add(() => PlayerSettings.Android.bundleVersionCode = oldCode);
                PlayerSettings.Android.bundleVersionCode = code;
            }

            bool oldAab = EditorUserBuildSettings.buildAppBundle;
            restore.Add(() => EditorUserBuildSettings.buildAppBundle = oldAab);
            EditorUserBuildSettings.buildAppBundle = true;

            string ksPath = Environment.GetEnvironmentVariable("HARNESS_KEYSTORE_PATH");
            string ksPass = Environment.GetEnvironmentVariable("HARNESS_KEYSTORE_PASS");
            string alias = Environment.GetEnvironmentVariable("HARNESS_KEYALIAS_NAME");
            string aliasPass = Environment.GetEnvironmentVariable("HARNESS_KEYALIAS_PASS");

            if (string.IsNullOrEmpty(ksPath))
            {
                Debug.LogWarning($"{Tag} WARN sin HARNESS_KEYSTORE_PATH: el .aab saldra firmado con la clave debug (no apto para Google Play).");
                return true;
            }
            if (!File.Exists(ksPath) || string.IsNullOrEmpty(ksPass) || string.IsNullOrEmpty(alias) || string.IsNullOrEmpty(aliasPass))
            {
                Debug.LogError($"{Tag} FAIL firma Android incompleta: keystore inexistente o faltan pass/alias en el entorno.");
                return false;
            }

            bool oldCustom = PlayerSettings.Android.useCustomKeystore;
            string oldName = PlayerSettings.Android.keystoreName;
            string oldAlias = PlayerSettings.Android.keyaliasName;
            restore.Add(() =>
            {
                PlayerSettings.Android.keystorePass = "";
                PlayerSettings.Android.keyaliasPass = "";
                PlayerSettings.Android.keystoreName = oldName;
                PlayerSettings.Android.keyaliasName = oldAlias;
                PlayerSettings.Android.useCustomKeystore = oldCustom;
            });
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = ksPath;
            PlayerSettings.Android.keystorePass = ksPass;
            PlayerSettings.Android.keyaliasName = alias;
            PlayerSettings.Android.keyaliasPass = aliasPass;
            Debug.Log($"{Tag} INFO firma Android con keystore propio (alias '{alias}').");
            return true;
        }

        static void ConfigureIos(string buildNumber, string teamId, List<Action> restore)
        {
            if (!string.IsNullOrEmpty(buildNumber))
            {
                string old = PlayerSettings.iOS.buildNumber;
                restore.Add(() => PlayerSettings.iOS.buildNumber = old);
                PlayerSettings.iOS.buildNumber = buildNumber;
            }
            if (!string.IsNullOrEmpty(teamId))
            {
                string oldTeam = PlayerSettings.iOS.appleDeveloperTeamID;
                bool oldAuto = PlayerSettings.iOS.appleEnableAutomaticSigning;
                restore.Add(() =>
                {
                    PlayerSettings.iOS.appleDeveloperTeamID = oldTeam;
                    PlayerSettings.iOS.appleEnableAutomaticSigning = oldAuto;
                });
                PlayerSettings.iOS.appleDeveloperTeamID = teamId;
                PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            }
        }

        // Addressables sin dependencia de compilacion: si el paquete no esta,
        // no hace nada. Si esta y hay settings, construye el contenido SIEMPRE
        // (no se fia de la preferencia por maquina "Build Addressables on
        // Player Build", que en batchmode no es determinista).
        static bool BuildAddressablesIfPresent()
        {
            Type defaultObj = FindType("UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject");
            Type settingsType = FindType("UnityEditor.AddressableAssets.Settings.AddressableAssetSettings");
            if (defaultObj == null || settingsType == null) return true;

            object settings = defaultObj.GetProperty("Settings", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (settings == null)
            {
                Debug.Log($"{Tag} INFO Addressables instalado pero sin settings: se omite.");
                return true;
            }

            MethodInfo build = settingsType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "BuildPlayerContent" && m.GetParameters().Length == 1 && m.GetParameters()[0].IsOut);
            if (build == null)
            {
                Debug.LogError($"{Tag} FAIL Addressables presente pero no se encontro BuildPlayerContent(out result).");
                return false;
            }

            Debug.Log($"{Tag} INFO construyendo contenido de Addressables...");
            var parameters = new object[1];
            build.Invoke(null, parameters);
            string error = parameters[0]?.GetType().GetProperty("Error")?.GetValue(parameters[0]) as string;
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError($"{Tag} FAIL Addressables: {error}");
                return false;
            }
            Debug.Log($"{Tag} INFO Addressables OK.");
            return true;
        }

        static Type FindType(string fullName)
        {
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = a.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }

        static string Arg(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        static bool HasFlag(string[] args, string name) =>
            args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
    }
}

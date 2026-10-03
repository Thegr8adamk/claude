using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RexMagnum.EditorTools
{
    /// <summary>
    /// Menu "Rex Magnum > Make Windows Game (.exe)", "Make Android Game (.apk)" and "Make Web Game (HTML)" (brief section 10).
    /// Output: &lt;project&gt;/Builds/Windows/RexMagnum.exe and &lt;project&gt;/Builds/Android/RexMagnum.apk.
    /// Android: IL2CPP, ARM64, landscape, ASTC textures, Vulkan + OpenGL ES 3, minimum Android 8.
    /// </summary>
    public static class BuildTools
    {
        const string Product = "Rex Magnum: Vacation Violence";

        static string[] Scenes()
        {
            var s = EditorBuildSettings.scenes.Where(x => x.enabled).Select(x => x.path).ToArray();
            if (s.Length == 0) throw new System.Exception("No scenes. Run Rex Magnum > Build Island first.");
            return s;
        }

        /// <summary>Shared player settings. False when the owner stopped the build (occlusion dialog).</summary>
        static bool Common()
        {
            PlayerSettings.companyName = "Rex Magnum";
            PlayerSettings.productName = Product;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            IncludeShader("Universal Render Pipeline/Unlit");
            // occlusion culling: if the island scene has none, a dialog gives the time and lets the owner skip or stop
            return IslandBuilder.EnsureOcclusion();
        }

        /// <summary>Shaders the game only finds by name (Shader.Find: aim line, harpoon rope) are left out of a build
        /// unless a material uses them; add them to Graphics Settings > Always Included Shaders.</summary>
        static void IncludeShader(string name)
        {
            var sh = Shader.Find(name);
            if (sh == null) return;
            var gs = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var list = gs.FindProperty("m_AlwaysIncludedShaders");
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == sh) return;
            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = sh;
            gs.ApplyModifiedProperties();
        }

        static void Report(BuildReport r, string path)
        {
            if (r.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[Rex Magnum] Build done: {path} ({r.summary.totalSize / (1024 * 1024)} MB)");
                EditorUtility.RevealInFinder(path);
            }
            else Debug.LogError($"[Rex Magnum] Build failed: {r.summary.result}. See the Console for errors.");
        }

        [MenuItem("Rex Magnum/Make Windows Game (.exe)", priority = 20)]
        public static void Windows()
        {
            if (!Common()) return;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Builds", "Windows", "RexMagnum.exe");
            var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes(), locationPathName = path, target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
            });
            Report(r, path);
        }

        /// <summary>Sets a PlayerSettings.WebGL property by name when this Unity version has it (names differ by version).</summary>
        static void WebSetting(string name, object value)
        {
            var p = typeof(PlayerSettings.WebGL).GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (p == null || !p.CanWrite) return;
            try { p.SetValue(null, value); } catch (System.Exception) { }
        }

        /// <summary>
        /// Menu "Rex Magnum > Make Web Game (HTML)": a WebGL 2 build in &lt;project&gt;/Builds/Web (index.html + Build/
        /// + TemplateData/). Needs the "WebGL Build Support" module (Unity Hub > Installs > Add modules). Unity then opens
        /// the game in the browser through its own small local web server (a double-click on index.html does not work:
        /// browsers block game files opened from a folder). Low quality tier by default (browser memory).
        /// </summary>
        [MenuItem("Rex Magnum/Make Web Game (HTML)", priority = 22)]
        public static void Web()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                EditorUtility.DisplayDialog("Rex Magnum", "WebGL Build Support is not installed.\n\nOpen Unity Hub > Installs > " +
                    "this Unity version > the gear icon > Add modules > tick \"WebGL Build Support\" > Install. Then run this menu again.", "OK");
                return;
            }
            if (!Common()) return;
            // compressed files that still load from any simple web host (itch.io, GitHub Pages) and from Unity's server
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            WebSetting("decompressionFallback", true);
            WebSetting("dataCaching", true);
            WebSetting("nameFilesAsHashes", false);
            WebSetting("exceptionSupport", WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly);
            // the island is big: let the browser memory grow up to 4 GB (Unity 6 property names)
            var growth = typeof(PlayerSettings.WebGL).Assembly.GetType("UnityEditor.WebGLMemoryGrowthMode");
            if (growth != null && growth.IsEnum) WebSetting("memoryGrowthMode", System.Enum.ToObject(growth, 2));   // 2 = geometric
            WebSetting("maximumMemorySize", 4096);
            WebSetting("initialMemorySize", 512);
            // our page: title picture, loading bar, FULL SCREEN button (Assets/WebGLTemplates/RexMagnum)
            // (kept in Assets/RexMagnum/WebTemplate so a folder swap brings it; Unity reads templates from Assets/WebGLTemplates)
            if (CopyWebTemplate()) PlayerSettings.WebGL.template = "PROJECT:RexMagnum";
            // web: let the page keep the game's saves (PlayerPrefs / save.json live in the browser's IndexedDB)
            WebSetting("autoSyncPersistentDataPath", true);
            // a smaller download: WebGL-only texture sizes (kept in each texture's import settings; the PC and phone
            // builds keep theirs). This replaced the global 1024 px build override: the 1024 cap alone still left
            // ~237 megapixels of textures (the 217 MB Web.data)
            WebTextureCaps();
            string path = WebFolder;
            // the web player: plain Forward (with Forward+ the browser drew no world) and no orientation lock call
            var modes = new System.Collections.Generic.List<(Object, string, int)>();
            var oldOrient = PlayerSettings.defaultInterfaceOrientation;
            BuildReport r;
            try
            {
                ForceForward(modes);
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
                r = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = Scenes(), locationPathName = path, target = BuildTarget.WebGL, options = BuildOptions.None
                });
            }
            finally
            {
                RestoreModes(modes);
                PlayerSettings.defaultInterfaceOrientation = oldOrient;
            }
            if (r.summary.result == BuildResult.Succeeded) WriteWebLauncher(path);
            Report(r, path);
            if (r.summary.result == BuildResult.Succeeded) PlayWeb();
        }

        /// <summary>WebGL import override on every game texture: at most 512 px (1024 for the menus' pictures, Rex, his
        /// arms and the weapons: seen up close or full screen), crunched DXT (several times smaller to download; the
        /// browser gets plain DXT). Only textures whose WebGL setting is not already this are changed (one reimport,
        /// the first time).</summary>
        static void WebTextureCaps()
        {
            int changed = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/RexMagnum" }))
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    if (!(AssetImporter.GetAtPath(p) is TextureImporter ti)) continue;
                    // not lightmaps, cursors, single-channel data maps, CPU-read or HDR sources (crunch can't do HDR)
                    if (ti.textureType == TextureImporterType.Lightmap || ti.textureType == TextureImporterType.Cursor ||
                        ti.textureType == TextureImporterType.SingleChannel || ti.isReadable) continue;
                    string ext = Path.GetExtension(p).ToLowerInvariant();
                    if (ext == ".exr" || ext == ".hdr") continue;
                    bool near = p.Contains("/Resources/UI/") || p.Contains("/Characters/Rex/") || p.Contains("/Weapons/");
                    int want = Mathf.Min(near ? 1024 : 512, ti.maxTextureSize);
                    var w = ti.GetPlatformTextureSettings("WebGL");
                    // crunch only for colour / normal maps with a compressed default (not uncompressed UI or data maps)
                    bool crunch = ti.textureCompression != TextureImporterCompression.Uncompressed;
                    if (w.overridden && w.maxTextureSize == want && w.crunchedCompression == crunch) continue;
                    w.overridden = true;
                    w.maxTextureSize = want;
                    w.format = TextureImporterFormat.Automatic;
                    w.textureCompression = crunch ? TextureImporterCompression.Compressed : TextureImporterCompression.Uncompressed;
                    w.crunchedCompression = crunch;
                    w.compressionQuality = 50;
                    ti.SetPlatformTextureSettings(w);
                    ti.SaveAndReimport();
                    changed++;
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            if (changed > 0) Debug.Log($"[Rex Magnum] Web texture sizes set on {changed} textures (WebGL only).");
        }

        static bool CopyWebTemplate()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string src = Path.Combine(Application.dataPath, "RexMagnum", "WebTemplate");
            if (!File.Exists(Path.Combine(src, "index.html"))) return false;
            string dst = Path.Combine(Application.dataPath, "WebGLTemplates", "RexMagnum");
            Directory.CreateDirectory(Path.Combine(dst, "TemplateData"));
            foreach (var f in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            {
                if (f.EndsWith(".meta")) continue;
                string rel = f.Substring(src.Length + 1);
                string to = Path.Combine(dst, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(to));
                File.Copy(f, to, true);
            }
            AssetDatabase.Refresh();
            return true;
        }

        /// <summary>Web build only: every URP renderer to plain Forward, no depth priming, intermediate texture "Auto",
        /// renderer features (SSAO) off. Returns what to put back after the build.</summary>
        static void ForceForward(System.Collections.Generic.List<(Object, string, int)> old)
        {
            void Set(Object o, SerializedObject so, string prop, int v)
            {
                var sp = so.FindProperty(prop);
                if (sp == null) return;
                bool b = sp.propertyType == SerializedPropertyType.Boolean;
                int cur = b ? (sp.boolValue ? 1 : 0) : sp.intValue;
                if (cur == v) return;
                old.Add((o, prop, cur));
                if (b) sp.boolValue = v != 0; else sp.intValue = v;
            }
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                var data = AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(guid));
                if (data == null) continue;
                var so = new SerializedObject(data);
                Set(data, so, "m_RenderingMode", 0);          // Forward
                Set(data, so, "m_DepthPrimingMode", 0);       // Disabled
                Set(data, so, "m_IntermediateTextureMode", 0); // Auto
                so.ApplyModifiedPropertiesWithoutUndo();
                var feats = so.FindProperty("m_RendererFeatures");
                for (int k = 0; feats != null && k < feats.arraySize; k++)
                {
                    var f = feats.GetArrayElementAtIndex(k).objectReferenceValue;
                    if (f == null) continue;
                    var fso = new SerializedObject(f);
                    Set(f, fso, "m_Active", 0);
                    fso.ApplyModifiedPropertiesWithoutUndo();
                }
                Debug.Log($"[Rex Magnum] Web build: {data.name} uses Forward, no depth priming, no renderer features for this build");
            }
            AssetDatabase.SaveAssets();
        }

        static void RestoreModes(System.Collections.Generic.List<(Object, string, int)> old)
        {
            foreach (var (o, prop, v) in old)
            {
                if (o == null) continue;
                var so = new SerializedObject(o);
                var sp = so.FindProperty(prop);
                if (sp == null) continue;
                if (sp.propertyType == SerializedPropertyType.Boolean) sp.boolValue = v != 0; else sp.intValue = v;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.SaveAssets();
        }

        static string WebFolder => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Builds", "Web");

        /// <summary>PLAY_WEB_GAME.bat + serve.ps1 in the web folder: a double-click starts a small local web server
        /// (Windows PowerShell, nothing to install) and opens the game in the browser, without Unity.</summary>
        static void WriteWebLauncher(string folder)
        {
            File.WriteAllText(Path.Combine(folder, "serve.ps1"),
@"# Rex Magnum web game: a small local web server for this folder (close this window to stop it).
# -Lan: phones on the same Wi-Fi can open it too (needs one admin 'Yes' to open the port).
param([switch]$Lan)
$port = 8093
$root = $PSScriptRoot
$types = @{ '.html'='text/html'; '.js'='application/javascript'; '.wasm'='application/wasm'; '.css'='text/css';
            '.png'='image/png'; '.jpg'='image/jpeg'; '.ico'='image/x-icon'; '.json'='application/json'; '.data'='application/octet-stream';
            '.unityweb'='application/octet-stream' }
$http = New-Object System.Net.HttpListener
if ($Lan) {
  $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
  if (-not $isAdmin) { Start-Process powershell -Verb RunAs -ArgumentList ""-NoProfile -ExecutionPolicy Bypass -File `""$PSCommandPath`"" -Lan""; exit }
  netsh http add urlacl url=""http://+:$port/"" user=Everyone 2>$null | Out-Null
  if (-not (Get-NetFirewallRule -DisplayName 'Rex Magnum web game' -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName 'Rex Magnum web game' -Direction Inbound -Protocol TCP -LocalPort $port -Action Allow -Profile Private | Out-Null
  }
  $http.Prefixes.Add(""http://+:$port/"")
} else { $http.Prefixes.Add(""http://localhost:$port/"") }
try { $http.Start() } catch { Start-Process ""http://localhost:$port/""; exit }   # already running: just open the page
if ($Lan) {
  $ips = Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' } | ForEach-Object { $_.IPAddress }
  Write-Host """"
  Write-Host ""ON YOUR PHONE (same Wi-Fi), open one of these in Chrome:"" -ForegroundColor Yellow
  foreach ($ip in $ips) { Write-Host ""   http://$($ip):$port/"" -ForegroundColor Green }
  Write-Host """"
  Write-Host ""Keep this window open while you play. Close it to stop.""
} else {
  Start-Process ""http://localhost:$port/""
  Write-Host ""Rex Magnum is running at http://localhost:$port/  (close this window to stop)""
}
while ($http.IsListening) {
  $ctx = $http.GetContext()
  $rel = [Uri]::UnescapeDataString($ctx.Request.Url.AbsolutePath.TrimStart('/'))
  if ($rel -eq '') { $rel = 'index.html' }
  $file = [IO.Path]::GetFullPath((Join-Path $root $rel))
  if ($file.StartsWith($root) -and (Test-Path $file -PathType Leaf)) {
    $ext = [IO.Path]::GetExtension($file).ToLower()
    $ctx.Response.ContentType = if ($types.ContainsKey($ext)) { $types[$ext] } else { 'application/octet-stream' }
    $bytes = [IO.File]::ReadAllBytes($file)
    $ctx.Response.ContentLength64 = $bytes.Length
    $ctx.Response.OutputStream.Write($bytes, 0, $bytes.Length)
  } else { $ctx.Response.StatusCode = 404 }
  $ctx.Response.Close()
}
");
            File.WriteAllText(Path.Combine(folder, "PLAY_ON_PHONE.bat"),
                "@echo off\r\ntitle Rex Magnum web game (phone)\r\npowershell -NoProfile -ExecutionPolicy Bypass -File \"%~dp0serve.ps1\" -Lan\r\npause\r\n");
            File.WriteAllText(Path.Combine(folder, "PLAY_WEB_GAME.bat"),
                "@echo off\r\ntitle Rex Magnum web game\r\npowershell -NoProfile -ExecutionPolicy Bypass -File \"%~dp0serve.ps1\"\r\n");
        }

        static System.Net.HttpListener webServer;
        const int WebPort = 8093;

        /// <summary>Menu "Rex Magnum > Play Web Game": opens the last web build in the browser through a small local
        /// web server inside Unity (no new build). The server stops when Unity closes.</summary>
        [MenuItem("Rex Magnum/Play Web Game (last build)", priority = 23)]
        public static void PlayWeb()
        {
            string root = WebFolder;
            if (!File.Exists(Path.Combine(root, "index.html")))
            {
                EditorUtility.DisplayDialog("Rex Magnum", "No web build yet. Run Rex Magnum > Make Web Game (HTML) first.", "OK");
                return;
            }
            if (webServer == null || !webServer.IsListening)
            {
                webServer = new System.Net.HttpListener();
                webServer.Prefixes.Add($"http://localhost:{WebPort}/");
                try { webServer.Start(); }
                catch (System.Exception e) { webServer = null; Debug.LogWarning("[Rex Magnum] Web server: " + e.Message + " (PLAY_WEB_GAME.bat may already run it)"); }
                if (webServer != null)
                {
                    var server = webServer;
                    new System.Threading.Thread(() => Serve(server, root)) { IsBackground = true }.Start();
                }
            }
            Application.OpenURL($"http://localhost:{WebPort}/");
            Debug.Log($"[Rex Magnum] Web game: http://localhost:{WebPort}/  (files: {root})");
        }

        static void Serve(System.Net.HttpListener server, string root)
        {
            string full = Path.GetFullPath(root);
            while (server.IsListening)
            {
                System.Net.HttpListenerContext ctx;
                try { ctx = server.GetContext(); } catch (System.Exception) { return; }
                try
                {
                    string rel = System.Uri.UnescapeDataString(ctx.Request.Url.AbsolutePath.TrimStart('/'));
                    if (rel.Length == 0) rel = "index.html";
                    string file = Path.GetFullPath(Path.Combine(full, rel));
                    if (file.StartsWith(full) && File.Exists(file))
                    {
                        string ext = Path.GetExtension(file).ToLowerInvariant();
                        ctx.Response.ContentType = ext == ".html" ? "text/html" : ext == ".js" ? "application/javascript" :
                                                   ext == ".wasm" ? "application/wasm" : ext == ".css" ? "text/css" :
                                                   ext == ".png" ? "image/png" : ext == ".jpg" ? "image/jpeg" : "application/octet-stream";
                        var bytes = File.ReadAllBytes(file);
                        ctx.Response.ContentLength64 = bytes.Length;
                        ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
                    }
                    else ctx.Response.StatusCode = 404;
                }
                catch (System.Exception) { }
                finally { try { ctx.Response.Close(); } catch (System.Exception) { } }
            }
        }

        [MenuItem("Rex Magnum/Make Android Game (.apk)", priority = 21)]
        public static void Android()
        {
            if (!Common()) return;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.rexmagnum.vacationviolence");
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,
                new[] { UnityEngine.Rendering.GraphicsDeviceType.Vulkan, UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            EditorUserBuildSettings.buildAppBundle = false;
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Builds", "Android", "RexMagnum.apk");
            var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes(), locationPathName = path, target = BuildTarget.Android, options = BuildOptions.None
            });
            Report(r, path);
        }
    }
}

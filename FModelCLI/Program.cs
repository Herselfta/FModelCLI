/*
 * FModelCLI - Command-line wrapper for FModel
 * Copyright (C) 2026 FModelCLI Contributors
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO.Compression;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
using CUE4Parse.Compression;
using Serilog;
using CUE4Parse_Conversion.Textures.BC;

namespace FModelCLI
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Initialize Logging
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console()
                .CreateLogger();

            // Prepare .data directory for dependencies (near the EXE)
            var exePath = Environment.ProcessPath ?? AppContext.BaseDirectory;
            var dataDir = Path.Combine(Path.GetDirectoryName(exePath)!, ".data");
            Directory.CreateDirectory(dataDir);
            Log.Information("Dependencies directory: {DataDir}", dataDir);

            // Ensure dependencies are downloaded (proxy-friendly)
            await EnsureDependencies(dataDir);

            // Initialize Oodle
            var oodlePath = Path.Combine(dataDir, OodleHelper.OODLE_DLL_NAME);
            try
            {
                OodleHelper.Initialize(oodlePath);
                if (OodleHelper.Instance != null)
                    Log.Information("Oodle initialized successfully.");
                else
                    Log.Warning("Oodle initialization finished but Instance is null.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize Oodle");
            }

            // Initialize Zlib
            var zlibPath = Path.Combine(dataDir, ZlibHelper.DLL_NAME);
            try
            {
                ZlibHelper.Initialize(zlibPath);
                if (ZlibHelper.Instance != null)
                    Log.Information("Zlib initialized successfully.");
                else
                    Log.Warning("Zlib initialization finished but Instance is null.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize Zlib");
            }

            // Initialize Detex
            var detexPath = Path.Combine(dataDir, DetexHelper.DLL_NAME);
            try
            {
                if (!File.Exists(detexPath))
                {
                    Log.Information("Extracting Detex...");
                    DetexHelper.LoadDll(detexPath);
                }
                DetexHelper.Initialize(detexPath);
                Log.Information("Detex initialized successfully.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize Detex");
            }

            if (args.Length < 2)
            {
                Console.WriteLine("Usage: FModelCLI <GameDir> <AES> <OutputDir> [Filter]");
                Console.WriteLine("       FModelCLI <GameDir> <AES> --list [Filter]  (list files only)");
                return;
            }

            string gameDir = args[0];
            string aesKey = args[1];
            bool listOnly = args.Length > 2 && args[2] == "--list";
            string outputDir = listOnly ? "" : (args.Length > 2 ? args[2] : "");
            string filter = "";
            
            if (listOnly && args.Length > 3)
                filter = args[3].ToLower();
            else if (!listOnly && args.Length > 3)
                filter = args[3].ToLower();

            // Validate arguments
            if (string.IsNullOrWhiteSpace(gameDir))
            {
                Console.WriteLine("[Error] Game directory cannot be empty");
                return;
            }
            if (string.IsNullOrWhiteSpace(aesKey))
            {
                Console.WriteLine("[Error] AES key cannot be empty");
                return;
            }
            if (!listOnly && string.IsNullOrWhiteSpace(outputDir))
            {
                Console.WriteLine("[Error] Output directory cannot be empty");
                return;
            }

            // Parse keys
            var keys = new List<FAesKey>();
            var keyParts = aesKey.Replace("\"", "").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in keyParts)
            {
                var dictContent = part.Trim();
                if (string.IsNullOrEmpty(dictContent)) continue;
                if (!dictContent.StartsWith("0x")) dictContent = "0x" + dictContent;
                try { 
                    keys.Add(new FAesKey(dictContent)); 
                } catch (Exception ex) {
                    Console.WriteLine($"[Error] Failed to parse key {dictContent}: {ex.Message}");
                }
            }
            Console.WriteLine($"[Info] Parsed {keys.Count} valid keys.");

            var version = new VersionContainer(EGame.GAME_WutheringWaves);
            var provider = new DefaultFileProvider(gameDir, SearchOption.AllDirectories, version, StringComparer.OrdinalIgnoreCase); 
            provider.Initialize();

            // Submit all keys to all possible GUIDs first
            var zeroGuid = new FGuid("00000000000000000000000000000000");
            foreach (var k in keys) provider.SubmitKey(zeroGuid, k);

            if (provider.UnloadedVfs.Count > 0)
            {
                foreach (var vfs in provider.UnloadedVfs)
                {
                    foreach (var k in keys)
                    {
                        provider.SubmitKey(vfs.EncryptionKeyGuid, k);
                    }
                }
            }
            
            provider.Mount();
            Console.WriteLine($"[Scan] Final Files: {provider.Files.Count}");

            int successCount = 0;
            int totalChecked = 0;

            foreach (var file in provider.Files)
            {
                totalChecked++;
                if (!string.IsNullOrEmpty(filter) && !file.Key.ToLower().Contains(filter))
                    continue;

                // Skip non-essential assets
                if (file.Key.EndsWith(".uasset") || file.Key.EndsWith(".uexp") || file.Key.EndsWith(".ubulk"))
                    continue;

                if (listOnly)
                {
                    Console.WriteLine($"[File] {file.Key}");
                    successCount++;
                    continue;
                }

                try
                {
                    var fileData = file.Value.Read();
                    var outPath = Path.Combine(outputDir, file.Key);

                    var directoryPath = Path.GetDirectoryName(outPath);
                    if (!string.IsNullOrEmpty(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }
                    File.WriteAllBytes(outPath, fileData);
                    
                    Console.WriteLine($"[Export] {file.Key}");
                    successCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Fail] {file.Key}: {ex.Message}");
                }
            }

            Console.WriteLine($"[Done] {(listOnly ? "Listed" : "Extracted")} {successCount} files (Scanned {totalChecked}).");
        }

        private static async Task EnsureDependencies(string dataDir)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "FModelCLI/1.0.0");

            // Oodle
            var oodlePath = Path.Combine(dataDir, OodleHelper.OODLE_DLL_NAME);
            if (!File.Exists(oodlePath))
            {
                Log.Information("Downloading Oodle (proxy-friendly)...");
                const string oodleUrl = "https://github.com/WorkingRobot/OodleUE/releases/download/2026-01-25-1223/clang-cl-x64-release.zip";
                var zipPath = Path.Combine(dataDir, "oodle.zip");
                await DownloadFileAsync(client, oodleUrl, zipPath);
                
                Log.Information("Extracting Oodle...");
                using (var zip = ZipFile.OpenRead(zipPath))
                {
                    var entry = zip.GetEntry("bin/oodle-data-shared.dll");
                    if (entry != null)
                    {
                        entry.ExtractToFile(oodlePath, true);
                    }
                }
                File.Delete(zipPath);
            }

            // Zlib
            var zlibPath = Path.Combine(dataDir, ZlibHelper.DLL_NAME);
            if (!File.Exists(zlibPath))
            {
                Log.Information("Downloading Zlib (proxy-friendly)...");
                var gzPath = Path.Combine(dataDir, "zlib.gz");
                await DownloadFileAsync(client, ZlibHelper.DOWNLOAD_URL, gzPath);
                
                Log.Information("Extracting Zlib...");
                using (var fs = File.OpenRead(gzPath))
                using (var gz = new GZipStream(fs, CompressionMode.Decompress))
                using (var outFs = File.Create(zlibPath))
                {
                    await gz.CopyToAsync(outFs);
                }
                File.Delete(gzPath);
            }

            // VgmStream (Audio)
            var vgmCheckPath = Path.Combine(dataDir, "vgmstream-cli.exe");
            if (!File.Exists(vgmCheckPath))
            {
                Log.Information("Downloading VgmStream (proxy-friendly)...");
                const string vgmUrl = "https://github.com/vgmstream/vgmstream/releases/latest/download/vgmstream-win.zip";
                var zipPath = Path.Combine(dataDir, "vgmstream.zip");
                await DownloadFileAsync(client, vgmUrl, zipPath);
                
                Log.Information("Extracting VgmStream...");
                ZipFile.ExtractToDirectory(zipPath, dataDir, true);
                File.Delete(zipPath);
            }
        }

        private static async Task DownloadFileAsync(HttpClient client, string url, string path)
        {
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            await using var fs = File.Create(path);
            await response.Content.CopyToAsync(fs);
        }
    }
}

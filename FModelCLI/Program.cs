/*
 * FModelCLI - Command-line wrapper for FModel
 * Copyright (C) 2026 FModelCLI Contributors
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using System.IO;
using System.Collections.Generic;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;

namespace FModelCLI
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: FModelCLI <GameDir> <AES> <OutputDir> [Filter]");
                return;
            }

            string gameDir = args[0];
            string aesKey = args[1];
            string outputDir = args[2];
            string filter = args.Length > 3 ? args[3].ToLower() : "";

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
            if (string.IsNullOrWhiteSpace(outputDir))
            {
                Console.WriteLine("[Error] Output directory cannot be empty");
                return;
            }

            Console.WriteLine($"[Init] Loading provider for: {gameDir}");
            Console.WriteLine($"[Debug] Raw Keys: {aesKey}");

            // Parse keys
            var keys = new List<FAesKey>();
            var keyParts = aesKey.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in keyParts)
            {
                var dictContent = part.Trim();
                if (!dictContent.StartsWith("0x")) dictContent = "0x" + dictContent;
                try { keys.Add(new FAesKey(dictContent)); } catch {}
            }
            Console.WriteLine($"[Debug] Parsed {keys.Count} keys.");

            // Use the enum directly from the source code
            var version = new VersionContainer(EGame.GAME_WutheringWaves);
            
            var provider = new DefaultFileProvider(gameDir, SearchOption.AllDirectories, version, StringComparer.OrdinalIgnoreCase); 
            provider.Initialize();

            // Submit global keys
            var zeroGuid = new FGuid("00000000000000000000000000000000");
            foreach (var k in keys) provider.SubmitKey(zeroGuid, k);

            Console.WriteLine($"[Scan] Initial Files found: {provider.Files.Count}");
            Console.WriteLine($"[Scan] Unloaded VFS count: {provider.UnloadedVfs.Count}");

            if (provider.UnloadedVfs.Count > 0)
            {
                Console.WriteLine("[Info] Brute-forcing keys on unloaded VFS...");
                foreach (var vfs in provider.UnloadedVfs)
                {
                    // FModel logic often relies on matching GUIDs. 
                    // Since we don't have the mapping, we brute force.
                    foreach (var k in keys)
                    {
                        provider.SubmitKey(vfs.EncryptionKeyGuid, k);
                    }
                }
                
                provider.Mount();
                Console.WriteLine($"[Scan] Files after mount: {provider.Files.Count}");
            }

            int successCount = 0;

            foreach (var file in provider.Files)
            {
                if (!string.IsNullOrEmpty(filter) && !file.Key.ToLower().Contains(filter))
                    continue;

                // Skip non-essential assets if just checking or extracting basics
                if (file.Key.EndsWith(".uasset") || file.Key.EndsWith(".uexp") || file.Key.EndsWith(".ubulk"))
                 {
                    // For now, let's skip them unless strictly needed.
                    continue; 
                 }

                try
                {
                    var fileData = file.Value.Read();
                    var relativePath = file.Key; 
                    var outPath = Path.Combine(outputDir, relativePath);

                    var directoryPath = Path.GetDirectoryName(outPath);
                    if (!string.IsNullOrEmpty(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }
                    File.WriteAllBytes(outPath, fileData);
                    
                    Console.WriteLine($"[Export] {relativePath}");
                    successCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Fail] {file.Key}: {ex.Message}");
                }
            }

            Console.WriteLine($"[Done] Extracted {successCount} files.");
        }
    }
}

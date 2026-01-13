using System;
using System.IO;
using System.Diagnostics;
using System.Reflection;

namespace Hft.Infrastructure
{
    public static class PerformanceShadowCopy
    {
        public static string PerformShadowCopy()
        {
            var currentProcess = Process.GetCurrentProcess();
            var mainModule = currentProcess.MainModule;
            if (mainModule == null) throw new InvalidOperationException("Cannot get main module.");

            string originalPath = Path.GetDirectoryName(mainModule.FileName) 
                                  ?? throw new InvalidOperationException("Cannot determine executable path.");
            
            // If already running from a shadow dir, just return
            if (originalPath.Contains("shadow_active"))
            {
                Console.WriteLine($"[Shadow] Already running in shadow copy: {originalPath}");
                return originalPath;
            }

            string baseDir = Path.GetDirectoryName(originalPath) ?? originalPath;
            string shadowDir = Path.Combine(baseDir, "shadow_active", DateTime.UtcNow.Ticks.ToString());

            Console.WriteLine($"[Shadow] Creating shadow copy at: {shadowDir}");
            Directory.CreateDirectory(shadowDir);

            foreach (var file in Directory.GetFiles(originalPath))
            {
                string dest = Path.Combine(shadowDir, Path.GetFileName(file));
                File.Copy(file, dest, overwrite: true);
            }
            
            // Also copy subdirectories (recursive or just top level? standard net apps usually flat or specific folders)
            // For now, let's assume flat or critical deps are in root. 
            // Better: copy all recursively.
            CopyDirectory(originalPath, shadowDir);

            return shadowDir;
        }

        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            var dir = new DirectoryInfo(sourceDir);

            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath, true);
            }

            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                // Avoid recursive loop if shadow is inside source
                if (subDir.Name == "shadow_active") continue;

                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir);
            }
        }
    }
}

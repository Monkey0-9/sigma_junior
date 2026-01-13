using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hft.Core;

namespace Hft.Runner
{
    internal static class Program
    {
        private const string ShadowCopyOutputArg = "--shadow-copy-output";
        private const string UdpPortArg = "--udp-port";
        private const string MetricsPortArg = "--metrics-port";
        private const string MetricsPortDefault = "9180";
        private const string UdpPortDefault = "5005";

        private static async Task Main(string[] args)
        {
            // Parse Arguments
            string? shadowDir = ParseArg(args, ShadowCopyOutputArg);
            int udpPort = int.Parse(ParseArg(args, UdpPortArg) ?? UdpPortDefault, CultureInfo.InvariantCulture);
            int metricsPort = int.Parse(ParseArg(args, MetricsPortArg) ?? MetricsPortDefault, CultureInfo.InvariantCulture);

            if (!string.IsNullOrEmpty(shadowDir))
            {
                PerformShadowCopy(shadowDir);
                return; // The child process will handle the run
            }

            Console.WriteLine(Strings.PlatformStart);

            using var engine = new TradingEngine(udpPort, metricsPort);

            // Setup Graceful Shutdown Hook
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            try
            {
                await engine.StartAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine(Strings.ShutdownCancelled);
            }
            catch (ObjectDisposedException ex)
            {
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, Strings.FatalError, ex.Message));
                Console.WriteLine(ex.StackTrace);
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, Strings.FatalError, ex.Message));
                Console.WriteLine(ex.StackTrace);
            }
            catch (IOException ex)
            {
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, Strings.FatalError, ex.Message));
                Console.WriteLine(ex.StackTrace);
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, Strings.FatalError, ex.Message));
                Console.WriteLine(ex.StackTrace);
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, Strings.FatalError, ex.Message));
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                engine.Stop();
                Console.WriteLine(Strings.ShutdownComplete);
            }
        }

        private static string? ParseArg(string[] args, string flag)
        {
            int idx = Array.IndexOf(args, flag);
            return (idx >= 0 && idx < args.Length - 1) ? args[idx + 1] : null;
        }

        private static void PerformShadowCopy(string targetDir)
        {
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, Strings.ShadowCopyProgress, targetDir));
            Directory.CreateDirectory(targetDir);

            // Basic recursive copy (omitting complex error handling for institutional demo)
            string source = AppDomain.CurrentDomain.BaseDirectory;
            foreach (string dirPath in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(dirPath.Replace(source, targetDir, StringComparison.InvariantCulture));
            foreach (string newPath in Directory.GetFiles(source, "*.*", SearchOption.AllDirectories))
                File.Copy(newPath, newPath.Replace(source, targetDir, StringComparison.InvariantCulture), true);

            Console.WriteLine(Strings.ShadowCopyComplete);
            // Instructions: User should run the binary from targetDir manually or use a watcher script
        }
    }
}


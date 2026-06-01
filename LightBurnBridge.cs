using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace DepthLayerStudio;

public sealed class LightBurnImportResult
{
    public LightBurnImportResult(bool success, string message)
    {
        Success = success;
        Message = message;
    }

    public bool Success { get; }
    public string Message { get; }
}

public static class LightBurnBridge
{
    private const int CommandPort = 19840;
    private const int ResponsePort = 19841;
    private static readonly string DefaultLightBurnPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        "LightBurn",
        "LightBurn.exe");

    public static bool IsInstalled => File.Exists(DefaultLightBurnPath);

    public static LightBurnImportResult OpenProject(string projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath) || !File.Exists(projectPath))
        {
            return new LightBurnImportResult(false, "Export the LightBurn project before sending it.");
        }

        var extension = Path.GetExtension(projectPath);

        if (!string.Equals(extension, ".lbrn2", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(extension, ".lbrn", StringComparison.OrdinalIgnoreCase))
        {
            return new LightBurnImportResult(false, "DepthLayer Studio expected a LightBurn project file to send.");
        }

        if (!IsInstalled)
        {
            return new LightBurnImportResult(false, "LightBurn was not found in C:\\Program Files\\LightBurn.");
        }

        if (!Ping())
        {
            StartLightBurn();
            WaitForLightBurn();
        }

        if (!Ping())
        {
            return new LightBurnImportResult(
                false,
                "LightBurn did not answer. Open LightBurn and make sure UDP automation is enabled, then try again.");
        }

        var response = SendCommand($"LOADFILE:{projectPath}");

        if (!string.Equals(response, "OK", StringComparison.OrdinalIgnoreCase))
        {
            return new LightBurnImportResult(
                false,
                "LightBurn did not open the project. Save any open LightBurn work, then try again or open the .lbrn2 file manually.");
        }

        return new LightBurnImportResult(true, "Opened the LightBurn project with separate layer settings.");
    }

    public static LightBurnImportResult ImportFiles(IEnumerable<string> filePaths)
    {
        var paths = filePaths
            .Where(File.Exists)
            .Where(path => string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (paths.Count == 0)
        {
            return new LightBurnImportResult(false, "Export the PNG masks before sending them to LightBurn.");
        }

        if (!IsInstalled)
        {
            return new LightBurnImportResult(false, "LightBurn was not found in C:\\Program Files\\LightBurn.");
        }

        if (!Ping())
        {
            StartLightBurn();
            WaitForLightBurn();
        }

        if (!Ping())
        {
            return new LightBurnImportResult(
                false,
                "LightBurn did not answer the import command. Open LightBurn and make sure UDP automation is enabled, then try again.");
        }

        var failures = new List<string>();

        foreach (var path in paths)
        {
            var response = SendCommand($"IMPORT:{path}");

            if (!string.Equals(response, "OK", StringComparison.OrdinalIgnoreCase))
            {
                failures.Add(Path.GetFileName(path));
            }

            Thread.Sleep(180);
        }

        if (failures.Count > 0)
        {
            return new LightBurnImportResult(
                false,
                "LightBurn did not import: " + string.Join(", ", failures));
        }

        return new LightBurnImportResult(true, $"Imported {paths.Count} PNG mask file(s) into LightBurn.");
    }

    private static void StartLightBurn()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = DefaultLightBurnPath,
                UseShellExecute = true,
            });
        }
        catch
        {
            // The caller will report that LightBurn did not answer.
        }
    }

    private static void WaitForLightBurn()
    {
        for (var attempt = 0; attempt < 18; attempt += 1)
        {
            Thread.Sleep(500);

            if (Ping())
            {
                return;
            }
        }
    }

    private static bool Ping()
    {
        return string.Equals(SendCommand("PING"), "OK", StringComparison.OrdinalIgnoreCase);
    }

    private static string SendCommand(string command)
    {
        try
        {
            using var receiver = new UdpClient(AddressFamily.InterNetwork);
            receiver.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            receiver.Client.Bind(new IPEndPoint(IPAddress.Loopback, ResponsePort));
            receiver.Client.ReceiveTimeout = 1500;

            using var sender = new UdpClient();
            var bytes = Encoding.UTF8.GetBytes(command);
            sender.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Loopback, CommandPort));

            var remote = new IPEndPoint(IPAddress.Any, 0);
            var response = receiver.Receive(ref remote);
            return Encoding.UTF8.GetString(response).Trim();
        }
        catch
        {
            return string.Empty;
        }
    }
}

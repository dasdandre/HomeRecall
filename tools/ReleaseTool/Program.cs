using System;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("=== HomeRecall Release Tool ===");
Console.ResetColor();

// Annahme: Tool liegt in tools/ReleaseTool, Projekt in ../../
string projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));

// Check for uncommitted changes
string status = GetGitOutput("status --porcelain");
if (!string.IsNullOrWhiteSpace(status))
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("Warning: You have uncommitted changes:");
    Console.WriteLine(status);
    Console.ResetColor();

    Console.Write("Do you want to continue anyway? (y/n): ");
    var confirm = Console.ReadLine();
    if (confirm?.Trim().ToLower() != "y")
    {
        Console.WriteLine("Aborted.");
        return;
    }
}

string configFile = Path.Combine(projectRoot, "homerecall", "config.yaml");

if (!File.Exists(configFile))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Error: Config file not found at '{configFile}'");
    return;
}

string content = File.ReadAllText(configFile);
// Find version: "1.2.3"
var match = Regex.Match(content, @"version:\s*""(\d+\.\d+\.\d+)""");

if (!match.Success)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Error: Could not find version string (e.g. version: \"1.0.0\") in {configFile}");
    return;
}

string currentVersion = match.Groups[1].Value;
string[] parts = currentVersion.Split('.');
int major = int.Parse(parts[0]);
int minor = int.Parse(parts[1]);
int patch = int.Parse(parts[2]);

// Bump Patch
patch++;
string newVersion = $"{major}.{minor}.{patch}";

Console.WriteLine($"Current Version: {currentVersion}");
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"New Version:     {newVersion}");
Console.ResetColor();

Console.Write("\nDo you want to update version, commit, and push tag? (y/n): ");
var input = Console.ReadLine();
if (input?.Trim().ToLower() != "y")
{
    Console.WriteLine("Aborted.");
    return;
}

// Update File content
string newContent = content.Replace($"version: \"{currentVersion}\"", $"version: \"{newVersion}\"");
File.WriteAllText(configFile, newContent);
Console.WriteLine($"Updated {configFile}");

// Run Git commands
RunGit($"add homerecall/config.yaml");
RunGit($"commit -m \"chore: bump version to {newVersion}\"");
RunGit("push");

string tagName = $"v{newVersion}";
RunGit($"tag {tagName}");
RunGit($"push origin {tagName}");

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"\nSuccess! Released {tagName}. GitHub Actions should start building now.");
Console.ResetColor();

void RunGit(string args)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"> git {args}");
    Console.ResetColor();

    var psi = new ProcessStartInfo
    {
        FileName = "git",
        Arguments = args,
        WorkingDirectory = projectRoot,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };

    using var process = Process.Start(psi);
    process!.WaitForExit();

    string output = process.StandardOutput.ReadToEnd();
    string error = process.StandardError.ReadToEnd();

    if (process.ExitCode != 0)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error executing git command: {error}");
        Console.ResetColor();
        Environment.Exit(1);
    }
    else
    {
        if (!string.IsNullOrWhiteSpace(output)) Console.WriteLine(output);
        if (!string.IsNullOrWhiteSpace(error)) Console.WriteLine(error); // Git often outputs to stderr even on success
    }
}

string GetGitOutput(string args)
{
    var psi = new ProcessStartInfo
    {
        FileName = "git",
        Arguments = args,
        WorkingDirectory = projectRoot,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };

    using var process = Process.Start(psi);
    // Read output before waiting to avoid deadlocks
    string output = process!.StandardOutput.ReadToEnd();
    process.WaitForExit();
    
    return output;
}


using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DataForeman.App.Services;

/// <summary>
/// Validates C# scripts using Roslyn compilation (no execution).
/// Used by the Monaco editor to show real-time error markers.
/// </summary>
public sealed class ScriptValidationService
{
    /// <summary>
    /// Represents a single diagnostic from script compilation.
    /// </summary>
    public sealed class DiagnosticEntry
    {
        public string Severity { get; init; } = "Error";
        public string Message { get; init; } = string.Empty;
        public int StartLine { get; init; }
        public int StartColumn { get; init; }
        public int EndLine { get; init; }
        public int EndColumn { get; init; }
    }

    // Lightweight stub globals type that matches the Engine's ScriptGlobals signature.
    // This allows validation to resolve method calls like ReadTag, WriteTag, Log, etc.
    public class ValidationGlobals
    {
        // Input
        public object? Input { get; set; }

        // Tag Read
        public object? ReadTag(string tagPath) => null;
        public double ReadTagDouble(string tagPath) => 0;
        public bool ReadTagBool(string tagPath) => false;
        public string ReadTagString(string tagPath) => "";
        public int ReadTagInt(string tagPath) => 0;

        // Tag Write
        public void WriteTag(string tagPath, object value) { }
        public void WriteTags(Dictionary<string, object> tagValues) { }

        // State
        public object? GetState(string key) => null;
        public T GetState<T>(string key, T defaultValue) => defaultValue;
        public void SetState(string key, object? value) { }
        public bool HasState(string key) => false;
        public void ClearState(string key) { }

        // Logging
        public void Log(string message) { }
        public void Log(string format, params object[] args) { }

        // Timing
        public DateTime UtcNow => DateTime.UtcNow;
        public void Delay(int milliseconds) { }

        // Helpers
        public double Clamp(double value, double min, double max) => value;
        public double Scale(double value, double inMin, double inMax, double outMin, double outMax) => value;
        public Dictionary<string, object?> ParseJson(string json) => new();
        public string ToJson(object? value) => "";
    }

    private static readonly ScriptOptions ValidationOptions = ScriptOptions.Default
        .AddReferences(
            typeof(object).Assembly,
            typeof(Enumerable).Assembly,
            typeof(Math).Assembly,
            typeof(JsonSerializer).Assembly,
            typeof(Regex).Assembly,
            typeof(Task).Assembly)
        .AddImports(
            "System",
            "System.Linq",
            "System.Math",
            "System.Collections.Generic",
            "System.Text",
            "System.Text.Json",
            "System.Text.RegularExpressions",
            "System.Threading.Tasks");

    /// <summary>
    /// Compiles the script and returns diagnostics (errors/warnings) with positions.
    /// </summary>
    public List<DiagnosticEntry> Validate(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return new List<DiagnosticEntry>();

        try
        {
            var script = CSharpScript.Create<object>(
                code,
                ValidationOptions,
                globalsType: typeof(ValidationGlobals));

            var diagnostics = script.Compile();

            return diagnostics
                .Where(d => d.Severity >= DiagnosticSeverity.Warning)
                .Select(d =>
                {
                    var span = d.Location.GetLineSpan();
                    return new DiagnosticEntry
                    {
                        Severity = d.Severity == DiagnosticSeverity.Error ? "Error" : "Warning",
                        Message = d.GetMessage(),
                        StartLine = span.StartLinePosition.Line + 1,
                        StartColumn = span.StartLinePosition.Character + 1,
                        EndLine = span.EndLinePosition.Line + 1,
                        EndColumn = span.EndLinePosition.Character + 1
                    };
                })
                .ToList();
        }
        catch (Exception ex)
        {
            return new List<DiagnosticEntry>
            {
                new()
                {
                    Severity = "Error",
                    Message = ex.Message,
                    StartLine = 1, StartColumn = 1,
                    EndLine = 1, EndColumn = 1
                }
            };
        }
    }
}

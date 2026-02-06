using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using DataForeman.Engine.Drivers;
using DataForeman.Shared.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Logging;

namespace DataForeman.Engine.Services;

/// <summary>
/// Sandboxed globals object exposed to user C# scripts.
/// Provides tag I/O, state management, logging, timing, and string helpers
/// so that complex multi-method scripts can be written.
/// </summary>
public sealed class ScriptGlobals
{
    private readonly IStateMachineTagReader? _tagReader;
    private readonly IStateMachineTagWriter? _tagWriter;
    private readonly Dictionary<string, object?> _state;
    private readonly Action<string> _log;

    public ScriptGlobals(
        IStateMachineTagReader? tagReader,
        IStateMachineTagWriter? tagWriter,
        Dictionary<string, object?>? state,
        Action<string>? log,
        object? input = null)
    {
        _tagReader = tagReader;
        _tagWriter = tagWriter;
        _state = state ?? new();
        _log = log ?? (_ => { });
        Input = input;
    }

    // ─── Input ───────────────────────────────────────────────

    /// <summary>The input value passed from a previous node (flows only).</summary>
    public object? Input { get; }

    // ─── Tag Read ────────────────────────────────────────────

    /// <summary>Read the current value of a tag ("ConnectionName/TagName").</summary>
    public object? ReadTag(string tagPath)
    {
        return _tagReader?.GetCurrentTagValue(tagPath)?.Value;
    }

    /// <summary>Read tag value as a double (returns 0 on failure).</summary>
    public double ReadTagDouble(string tagPath)
    {
        var val = ReadTag(tagPath);
        if (val is double d) return d;
        if (val is float f) return f;
        if (val is int i) return i;
        if (val is long l) return l;
        if (val != null && double.TryParse(val.ToString(), out var parsed)) return parsed;
        return 0.0;
    }

    /// <summary>Read tag value as a boolean (returns false on failure).</summary>
    public bool ReadTagBool(string tagPath)
    {
        var val = ReadTag(tagPath);
        if (val is bool b) return b;
        if (val is int i) return i != 0;
        if (val is double d) return d != 0;
        if (val != null && bool.TryParse(val.ToString(), out var parsed)) return parsed;
        return false;
    }

    /// <summary>Read tag value as a string (returns "" on failure).</summary>
    public string ReadTagString(string tagPath)
    {
        var val = ReadTag(tagPath);
        return val?.ToString() ?? "";
    }

    /// <summary>Read tag value as an integer (returns 0 on failure).</summary>
    public int ReadTagInt(string tagPath)
    {
        var val = ReadTag(tagPath);
        if (val is int i) return i;
        if (val is double d) return (int)d;
        if (val is float f) return (int)f;
        if (val is long l) return (int)l;
        if (val != null && int.TryParse(val.ToString(), out var parsed)) return parsed;
        return 0;
    }

    // ─── Tag Write ───────────────────────────────────────────

    /// <summary>Write a value to a tag ("ConnectionName/TagName").</summary>
    public void WriteTag(string tagPath, object value)
    {
        if (_tagWriter == null) return;
        Task.Run(() => _tagWriter.WriteTagValueAsync(tagPath, value)).GetAwaiter().GetResult();
    }

    /// <summary>Write multiple tags at once. Keys are tag paths, values are the values to write.</summary>
    public void WriteTags(Dictionary<string, object> tagValues)
    {
        foreach (var kvp in tagValues)
            WriteTag(kvp.Key, kvp.Value);
    }

    // ─── State (persisted across scan cycles) ────────────────

    /// <summary>Get a value from the persistent state dictionary.</summary>
    public object? GetState(string key)
    {
        return _state.TryGetValue(key, out var val) ? val : null;
    }

    /// <summary>Get a typed value from state, with a default.</summary>
    public T GetState<T>(string key, T defaultValue)
    {
        if (_state.TryGetValue(key, out var val) && val is T typed)
            return typed;
        return defaultValue;
    }

    /// <summary>Set a value in the persistent state dictionary.</summary>
    public void SetState(string key, object? value)
    {
        _state[key] = value;
    }

    /// <summary>Check whether a state key exists.</summary>
    public bool HasState(string key) => _state.ContainsKey(key);

    /// <summary>Remove a key from persistent state.</summary>
    public void ClearState(string key) => _state.Remove(key);

    // ─── Logging ─────────────────────────────────────────────

    /// <summary>Log a message to the execution output.</summary>
    public void Log(string message) => _log(message);

    /// <summary>Log a formatted message to the execution output.</summary>
    public void Log(string format, params object[] args) => _log(string.Format(format, args));

    // ─── Timing ──────────────────────────────────────────────

    /// <summary>Current UTC timestamp.</summary>
    public DateTime UtcNow => DateTime.UtcNow;

    /// <summary>Pause execution for the specified number of milliseconds (max 5000).</summary>
    public void Delay(int milliseconds)
    {
        var capped = Math.Min(milliseconds, 5000);
        Thread.Sleep(capped);
    }

    // ─── String / Math helpers ───────────────────────────────

    /// <summary>Clamp a numeric value between min and max.</summary>
    public double Clamp(double value, double min, double max)
        => Math.Max(min, Math.Min(max, value));

    private const double DivisionByZeroGuard = 1e-12;

    /// <summary>Linearly scale a value from one range to another.</summary>
    public double Scale(double value, double inMin, double inMax, double outMin, double outMax)
    {
        if (Math.Abs(inMax - inMin) < DivisionByZeroGuard) return outMin;
        return (value - inMin) / (inMax - inMin) * (outMax - outMin) + outMin;
    }

    /// <summary>Parse a JSON string into a dictionary.</summary>
    public Dictionary<string, object?> ParseJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(json) ?? new();
        }
        catch
        {
            return new();
        }
    }

    /// <summary>Serialize an object to a JSON string.</summary>
    public string ToJson(object? value)
    {
        return JsonSerializer.Serialize(value);
    }
}

/// <summary>
/// Result of validating a C# script without executing it.
/// </summary>
public sealed class ScriptDiagnostic
{
    public string Severity { get; init; } = "Error";
    public string Message { get; init; } = string.Empty;
    public int StartLine { get; init; }
    public int StartColumn { get; init; }
    public int EndLine { get; init; }
    public int EndColumn { get; init; }
}

/// <summary>
/// Result of executing a C# script.
/// </summary>
public sealed class ScriptExecutionResult
{
    public bool Success { get; init; }
    public object? ReturnValue { get; init; }
    public string? ErrorMessage { get; init; }
    public List<string> LogMessages { get; init; } = new();
    public double ElapsedMs { get; init; }
}

/// <summary>
/// Service that compiles and executes C# scripts using Roslyn.
/// Provides validation (diagnostics) and sandboxed execution
/// for both flow nodes and state machine conditions/actions.
/// </summary>
public sealed class CSharpScriptService
{
    private readonly ILogger<CSharpScriptService> _logger;
    private readonly IStateMachineTagReader? _tagReader;
    private readonly IStateMachineTagWriter? _tagWriter;
    private readonly ConcurrentDictionary<string, Script<object>> _compiledCache = new();

    private static readonly ScriptOptions DefaultScriptOptions = ScriptOptions.Default
        .AddReferences(
            typeof(object).Assembly,
            typeof(Enumerable).Assembly,
            typeof(Math).Assembly,
            typeof(JsonSerializer).Assembly,
            typeof(Console).Assembly,
            typeof(Regex).Assembly,
            typeof(Task).Assembly,
            typeof(System.Net.Http.HttpClient).Assembly)
        .AddImports(
            "System",
            "System.Linq",
            "System.Math",
            "System.Collections.Generic",
            "System.Text",
            "System.Text.Json",
            "System.Text.RegularExpressions",
            "System.Threading.Tasks");

    public CSharpScriptService(
        ILogger<CSharpScriptService> logger,
        IStateMachineTagReader? tagReader = null,
        IStateMachineTagWriter? tagWriter = null)
    {
        _logger = logger;
        _tagReader = tagReader;
        _tagWriter = tagWriter;
    }

    /// <summary>
    /// Validates a C# script and returns compilation diagnostics
    /// (errors and warnings with line/column positions).
    /// </summary>
    public List<ScriptDiagnostic> Validate(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return new List<ScriptDiagnostic>();

        try
        {
            var script = CSharpScript.Create<object>(
                code,
                DefaultScriptOptions,
                globalsType: typeof(ScriptGlobals));

            var diagnostics = script.Compile();
            return diagnostics
                .Where(d => d.Severity >= DiagnosticSeverity.Warning)
                .Select(d =>
                {
                    var lineSpan = d.Location.GetLineSpan();
                    return new ScriptDiagnostic
                    {
                        Severity = d.Severity == DiagnosticSeverity.Error ? "Error" : "Warning",
                        Message = d.GetMessage(),
                        StartLine = lineSpan.StartLinePosition.Line + 1,
                        StartColumn = lineSpan.StartLinePosition.Character + 1,
                        EndLine = lineSpan.EndLinePosition.Line + 1,
                        EndColumn = lineSpan.EndLinePosition.Character + 1
                    };
                })
                .ToList();
        }
        catch (Exception ex)
        {
            return new List<ScriptDiagnostic>
            {
                new()
                {
                    Severity = "Error",
                    Message = ex.Message,
                    StartLine = 1,
                    StartColumn = 1,
                    EndLine = 1,
                    EndColumn = 1
                }
            };
        }
    }

    /// <summary>
    /// Compiles and executes a C# script with the provided globals.
    /// Returns the result (or the return value of the last expression).
    /// Scripts are cached by their code hash for performance.
    /// </summary>
    public async Task<ScriptExecutionResult> ExecuteAsync(
        string code,
        Dictionary<string, object?>? state = null,
        object? input = null,
        int timeoutMs = 10000,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return new ScriptExecutionResult { Success = true, ReturnValue = null };

        var logMessages = new List<string>();
        var globals = new ScriptGlobals(
            _tagReader,
            _tagWriter,
            state,
            msg => logMessages.Add(msg),
            input);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var script = GetOrCompileScript(code);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);

            var result = await script.RunAsync(globals, cts.Token);
            stopwatch.Stop();

            return new ScriptExecutionResult
            {
                Success = true,
                ReturnValue = result.ReturnValue,
                LogMessages = logMessages,
                ElapsedMs = stopwatch.Elapsed.TotalMilliseconds
            };
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return new ScriptExecutionResult
            {
                Success = false,
                ErrorMessage = $"Script execution timed out after {timeoutMs}ms",
                LogMessages = logMessages,
                ElapsedMs = stopwatch.Elapsed.TotalMilliseconds
            };
        }
        catch (CompilationErrorException cex)
        {
            stopwatch.Stop();
            return new ScriptExecutionResult
            {
                Success = false,
                ErrorMessage = string.Join("; ", cex.Diagnostics.Select(d => d.GetMessage())),
                LogMessages = logMessages,
                ElapsedMs = stopwatch.Elapsed.TotalMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Script execution failed");
            return new ScriptExecutionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                LogMessages = logMessages,
                ElapsedMs = stopwatch.Elapsed.TotalMilliseconds
            };
        }
    }

    /// <summary>
    /// Evaluates a C# expression as a boolean condition.
    /// Used by state machine trigger conditions.
    /// Returns false on any error.
    /// </summary>
    public async Task<bool> EvaluateConditionAsync(
        string code,
        Dictionary<string, object?>? state = null,
        CancellationToken ct = default)
    {
        var result = await ExecuteAsync(code, state, input: null, timeoutMs: 5000, ct: ct);
        if (!result.Success) return false;

        return result.ReturnValue switch
        {
            bool b => b,
            int i => i != 0,
            double d => d != 0,
            string s => !string.IsNullOrEmpty(s),
            null => false,
            _ => true
        };
    }

    private Script<object> GetOrCompileScript(string code)
    {
        return _compiledCache.GetOrAdd(code, key =>
        {
            var script = CSharpScript.Create<object>(
                key,
                DefaultScriptOptions,
                globalsType: typeof(ScriptGlobals));

            script.Compile();
            return script;
        });
    }
}

// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.IO.Pipes;
using System.Text;

namespace DispHub.Platform.Shell;

/// <summary>
/// Keeps one DispHub per user session. A second launch sends its command line (e.g. a disphub:// link) to the
/// first one and exits.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _cancel = new();
    private readonly bool _owned;

    public SingleInstance(string name = "DispHub")
    {
        _mutex = new Mutex(initiallyOwned: true, $@"Local\{name}.SingleInstance", out _owned);
        Name = name;
    }

    public string Name { get; }

    public bool IsFirstInstance => _owned;

    /// <summary>Raised on a background thread with the arguments another launch forwarded.</summary>
    public event EventHandler<string[]>? Activated;

    public void StartListening() => _ = Task.Run(ListenAsync);

    /// <summary>Sends <paramref name="args"/> to the running instance. Returns false if it couldn't be reached.</summary>
    public bool SendToFirstInstance(string[] args)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeNameFor(Name), PipeDirection.Out, PipeOptions.CurrentUserOnly);
            client.Connect(TimeSpan.FromSeconds(3));
            byte[] payload = Encoding.UTF8.GetBytes(string.Join('\n', args));
            client.Write(payload);
            return true;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        _cancel.Cancel();
        if (_owned)
            _mutex.ReleaseMutex();
        _mutex.Dispose();
        _cancel.Dispose();
    }

    private async Task ListenAsync()
    {
        while (!_cancel.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    PipeNameFor(Name), PipeDirection.In, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(_cancel.Token);
                using var reader = new StreamReader(server, Encoding.UTF8);
                string text = await reader.ReadToEndAsync(_cancel.Token);
                string[] args = text.Length == 0 ? [] : text.Split('\n');
                Activated?.Invoke(this, args);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException)
            {
                // A client disconnected mid-write; keep listening.
            }
        }
    }

    private static string PipeNameFor(string name) => $"{name}.{Environment.UserName}.Activation";
}

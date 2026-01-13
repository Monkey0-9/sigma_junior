using System;

namespace Hft.Risk
{
    /// <summary>
    /// Event arguments for kill switch state changes.
    /// </summary>
    public sealed class KillSwitchEventArgs : EventArgs
    {
        public string Reason { get; }

        public KillSwitchEventArgs(string reason)
        {
            Reason = reason ?? throw new ArgumentNullException(nameof(reason));
        }
    }

    /// <summary>
    /// Institutional Kill Switch.
    /// Provides a fail-safe mechanism to halt all trading activity.
    /// Implements IDisposable for proper lifecycle management.
    /// </summary>
    public sealed class KillSwitch : IDisposable
    {
        private bool _isEngaged;
        private bool _disposed;
        private readonly object _lock = new();

        public bool IsEngaged
        {
            get
            {
                lock (_lock) return _isEngaged;
            }
        }

        public event EventHandler<KillSwitchEventArgs>? OnEngaged;
        public event EventHandler<KillSwitchEventArgs>? OnDisengaged;

        /// <summary>
        /// Engages the kill switch, halting all order generation.
        /// </summary>
        /// <param name="reason">The reason for engagement</param>
        public void Engage(string reason)
        {
            lock (_lock)
            {
                if (_isEngaged) return;
                _isEngaged = true;
                Console.WriteLine($"[CRITICAL] KILL SWITCH ENGAGED: {reason}");
                OnEngaged?.Invoke(this, new KillSwitchEventArgs(reason));
            }
        }

        /// <summary>
        /// Disengages the kill switch, allowing trading to resume if safe.
        /// </summary>
        /// <param name="reason">The reason for disengagement</param>
        public void Disengage(string reason)
        {
            lock (_lock)
            {
                if (!_isEngaged) return;
                _isEngaged = false;
                Console.WriteLine($"[INFO] Kill switch disengaged: {reason}");
                OnDisengaged?.Invoke(this, new KillSwitchEventArgs(reason));
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            // Ensure we leave the system in a safe state
            Engage("KillSwitch Disposed");
            GC.SuppressFinalize(this);
        }
    }
}

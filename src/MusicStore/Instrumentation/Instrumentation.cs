using System;
using System.Diagnostics;
using TRACING = System.Diagnostics.Tracing;

namespace MusicStore.Instrumentation
{
    [TRACING.EventSource(Name = "aspnet-JitBench-MusicStore")]
    public class Logger : TRACING.EventSource
    {
        public static Logger Log = new Logger();

        [TRACING.Event(1)]
        public void ProgramStarted()
        {
            WriteEvent(1);
        }

        [TRACING.Event(2)]
        public void ServerStarted()
        {
            WriteEvent(2);
        }

        [TRACING.Event(3)]
        public void RequestBatchServed(int requestNumber)
        {
            WriteEvent(3, requestNumber);
        }
    }
}


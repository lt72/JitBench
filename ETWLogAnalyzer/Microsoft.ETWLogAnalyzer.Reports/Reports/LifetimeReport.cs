using System;
using System.Collections.Generic;
using Microsoft.ETWLogAnalyzer.Abstractions;
using Microsoft.ETWLogAnalyzer.ReportVisitors;
using Microsoft.ETWLogAnalyzer.Framework;
using TRACING = Microsoft.Diagnostics.Tracing;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;

namespace Microsoft.ETWLogAnalyzer.Reports
{
    /// <summary>
    /// Report that sumarizes time taken by each thread and the process itself as well as the time taken to main, server started,
    /// and first request served.
    /// </summary>
    public class LifetimeReport : IReport
    {
        private struct ThreadLifeInfo
        {
            public MethodUniqueIdentifier FirstMethodJitted { get; private set; }
            public double Stop { get; private set; }
            public double Start { get; private set; }

            public ThreadLifeInfo(double startTime, double stopTime, MethodUniqueIdentifier firstMethodJitted)
            {
                Start = startTime;
                Stop = stopTime;
                FirstMethodJitted = firstMethodJitted;
            }
        }

        private static readonly string FormatString = "{0, -35}:\t{1:F2}";
        private double _processStartTime;
        private double _processEndTime;
        private double _timeToMain;
        private double _timeToServerStarted;
        private double _timeToFirstRequest;
        private string _processName;
        private int _pid;
        private int _methodCount;
        private Dictionary<int, ThreadLifeInfo> _threadInfoTable;
        public string Name => "lifetime_report.txt";

        public LifetimeReport()
        {
            _threadInfoTable = new Dictionary<int, ThreadLifeInfo>();
        }

        public bool Analyze(IEventModel data)
        {
            _processStartTime = data.ProcessStart.TimeStampRelativeMSec;
            _processEndTime = data.ProcessStop.TimeStampRelativeMSec;
            _processName = data.ProcessStart.ProcessName;
            _pid = data.ProcessStart.ProcessID;
            _methodCount = System.Linq.Enumerable.Count(data.JittedMethodsList);

            foreach (int threadId in data.ThreadList)
            {
                var startVisitor = new GetFirstMatchingEventVisitor<PARSERS.Kernel.ThreadTraceData>(
                        x => x.Opcode == Diagnostics.Tracing.TraceEventOpcode.Start);
                var stopVisitor = new GetFirstMatchingEventVisitor<PARSERS.Kernel.ThreadTraceData>(
                        x => x.Opcode == Diagnostics.Tracing.TraceEventOpcode.Stop);
                var jitVisitor = new GetFirstMatchingEventVisitor<PARSERS.Clr.MethodLoadUnloadVerboseTraceData>();
                var timeToMainVisitor = new GetFirstMatchingEventVisitor<TRACING.TraceEvent>(ev => { return ev.ProviderName == "aspnet-JitBench-MusicStore" && ev.EventName == "ProgramStarted"; } );
                var timeToServerStartedVisitor = new GetFirstMatchingEventVisitor<TRACING.TraceEvent>((TRACING.TraceEvent ev) => { return ev.ProviderName == "aspnet-JitBench-MusicStore" && ev.EventName == "ServerStarted"; });
                var timeToFirstRequestVisitor = new GetFirstMatchingEventVisitor<TRACING.TraceEvent>((TRACING.TraceEvent ev) => { return ev.ProviderName == "aspnet-JitBench-MusicStore" && ev.EventName == "RequestBatchServed"; });

                Controller.RunVisitorForResult(startVisitor, data.GetThreadTimeline(threadId));
                Controller.RunVisitorForResult(stopVisitor, data.GetThreadTimeline(threadId));
                Controller.RunVisitorForResult(jitVisitor, data.GetThreadTimeline(threadId));
                Controller.RunVisitorForResult(timeToMainVisitor, data.GetThreadTimeline(threadId));
                Controller.RunVisitorForResult(timeToServerStartedVisitor, data.GetThreadTimeline(threadId));
                Controller.RunVisitorForResult(timeToFirstRequestVisitor, data.GetThreadTimeline(threadId));

                if (startVisitor.State != VisitorState.Done
                    || stopVisitor.State != VisitorState.Done
                    || jitVisitor.State == VisitorState.Error
                    || timeToMainVisitor.State == VisitorState.Error
                    || timeToServerStartedVisitor.State == VisitorState.Error
                    || timeToFirstRequestVisitor.State == VisitorState.Error)
                {
                    return false;
                }

                var methodUniqueId = (jitVisitor.Result == null) ? null : new MethodUniqueIdentifier(jitVisitor.Result);

                var threadLifeInfo = new ThreadLifeInfo(
                    startVisitor.Result.TimeStampRelativeMSec, stopVisitor.Result.TimeStampRelativeMSec,
                    methodUniqueId);

                _threadInfoTable.Add(threadId, threadLifeInfo);

                if (timeToMainVisitor.Result != timeToMainVisitor.DefaultResult)
                {
                    _timeToMain = timeToMainVisitor.Result.TimeStampRelativeMSec - _processStartTime;
                }
                if (timeToServerStartedVisitor.Result != timeToServerStartedVisitor.DefaultResult)
                {
                    _timeToServerStarted = timeToServerStartedVisitor.Result.TimeStampRelativeMSec - _processStartTime;
                }
                if (timeToFirstRequestVisitor.Result != timeToFirstRequestVisitor.DefaultResult)
                {
                    _timeToFirstRequest = timeToFirstRequestVisitor.Result.TimeStampRelativeMSec - _processStartTime;
                }
            }
            return true;
        }

        public bool Persist(string folderPath)
        {
            using (var writer = new ReportWriters.PlainTextWriter(System.IO.Path.Combine(folderPath, Name)))
            {
                writer.WriteTitle("Process data");

                writer.WriteLine(String.Format(FormatString, "Process name [-]", _processName));
                writer.WriteLine(String.Format(FormatString, "Process ID [-]", _pid));
                writer.WriteLine(String.Format(FormatString, "Process start time [ms]", _processStartTime));
                writer.WriteLine(String.Format(FormatString, "Process stop time [ms]", _processEndTime));
                writer.WriteLine(String.Format(FormatString, "Process duration [ms]", _processEndTime - _processStartTime));
                writer.WriteLine(String.Format(FormatString, "Time To Program Start [ms]", _timeToMain));
                writer.WriteLine(String.Format(FormatString, "Time To Server Start [ms]", _timeToServerStarted));
                writer.WriteLine(String.Format(FormatString, "Time To Request Served [ms]", _timeToFirstRequest));
                writer.WriteLine(String.Format(FormatString, "Methods Jitted [-]", _methodCount));

                writer.SkipLine();
                writer.SkipLine();
                writer.WriteTitle("Thread lifetime information");

                writer.Write($"\nThe process used {_threadInfoTable.Count} thread(s) as follows:");

                foreach (var threadInfo in _threadInfoTable)
                {
                    writer.WriteHeader("Thread " + threadInfo.Key);
                    writer.AddIndentationLevel();
                    var threadLifeInfo = threadInfo.Value;
                    writer.WriteLine(String.Format(FormatString, "Start time [ms]", threadLifeInfo.Start));
                    writer.WriteLine(String.Format(FormatString, "Stop time [ms]", threadLifeInfo.Stop));
                    writer.WriteLine(String.Format(FormatString, "First method Jitted [-]", threadLifeInfo.FirstMethodJitted));
                    writer.RemoveIndentationLevel();
                }
            }

            return true;
        }
    }
}

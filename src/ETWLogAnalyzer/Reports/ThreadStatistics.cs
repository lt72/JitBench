using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MusicStore.ETWLogAnalyzer.ReportWriters;

namespace MusicStore.ETWLogAnalyzer.Reports
{
    internal class ThreadStatistics : ReportBase
    {
        private class ThreadQuantumInfo
        {
            public int IntervalCount = 0;
            public double TotalActiveTime = 0;
            public double AverageActiveThreadTime => TotalActiveTime / IntervalCount;
            public double MinQuantumTimeUsage = Double.MaxValue;
            public double MaxQuantumTimeUsage = Double.MinValue;
            public double QuantumEfficiency => TotalActiveTime / (IntervalCount * QuantumLength);
            public double Lifetime = 0;
        }

        // LORENZO-TODO: consider moving this to ThreadQuantumInfo or make it a singleton
        static internal double QuantumLength { get; private set; }

        private readonly Dictionary<int, ThreadQuantumInfo> _threadQuantumInfoList;
            
        public ThreadStatistics(double quantumLength = Double.MinValue)
        {
            QuantumLength = quantumLength != Double.MinValue ? quantumLength : 20;

            // LORENZO-TODO: consider makign 'int' (threadID) a value type (struct)  struct ThreadId { public uint64 Id; } and use top 32 bits to disambiguate...
            _threadQuantumInfoList = new Dictionary<int, ThreadQuantumInfo>();
        }

        public override ReportBase Analyze(ETWData data)
        {
            foreach (int threadId in data.ThreadList)
            {
                var threadQuantumStatistics = new ThreadQuantumInfo();
                List<ETWData.ETWTimeInterval> activeIntervals = data.GetActiveIntervalsForThread(threadId);
                threadQuantumStatistics.IntervalCount = activeIntervals.Count;
                threadQuantumStatistics.Lifetime = data.ThreadLifeIntervals[threadId].Duration;

                foreach (var interval in activeIntervals)
                {
                    threadQuantumStatistics.TotalActiveTime += interval.Duration;
                    threadQuantumStatistics.MinQuantumTimeUsage = Math.Min(threadQuantumStatistics.MinQuantumTimeUsage, interval.Duration);
                    threadQuantumStatistics.MaxQuantumTimeUsage = Math.Max(threadQuantumStatistics.MaxQuantumTimeUsage, interval.Duration);
                }

                _threadQuantumInfoList.Add(threadId, threadQuantumStatistics);
            }
            return this;
        }

        public override void Persist(TextReportWriter writer, bool dispose)
        {
            writer.WriteTitle("Thread Usage");
            writer.Write($"\nThe process used {_threadQuantumInfoList.Count} thread(s) as follows:");

            foreach(var threadInfo in _threadQuantumInfoList)
            {
                writer.WriteHeader("Thread " + threadInfo.Key.ToString());

                writer.AddIndentationLevel();
                var quantumStats = threadInfo.Value;
                var formatString = "{0, -35}:\t{1,9}";
                writer.WriteLine(String.Format(formatString, "Intervals [-]", quantumStats.IntervalCount));
                writer.WriteLine(String.Format(formatString, "Nominal running time [ms]", quantumStats.Lifetime));
                writer.WriteLine(String.Format(formatString, "Effective running time [ms]", quantumStats.TotalActiveTime));
                writer.WriteLine(String.Format(formatString, "Average active thread time [ms]", quantumStats.AverageActiveThreadTime));
                writer.WriteLine(String.Format(formatString, "Min time used [ms]", quantumStats.MinQuantumTimeUsage));
                writer.WriteLine(String.Format(formatString, "Max time Used [ms]", quantumStats.MaxQuantumTimeUsage));
                writer.WriteLine(String.Format(formatString, "Quantum Efficiency [%]", quantumStats.QuantumEfficiency * 100));
                writer.RemoveIndentationLevel();
            }

            if (dispose)
            {
                writer.Dispose();
            }
        }
    }
}

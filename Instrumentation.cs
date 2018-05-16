using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace MHWSharpnessExtractor
{
    public static class Instrumentation
    {
        private static long networkTime;
        private static long procesingTime;
        private static long realTotalTime;

        public static long NetworkTime => networkTime;
        public static long ProcessingTime => procesingTime;
        public static long RealTotalTime => realTotalTime;

        public static Stopwatch BeginTotalMeasure()
        {
            return Stopwatch.StartNew();
        }

        public static void EndTotalMeasure(Stopwatch sw)
        {
            sw.Stop();
            Interlocked.Add(ref realTotalTime, sw.ElapsedMilliseconds);
        }

        public static Stopwatch BeginNetworkMeasure()
        {
            return Stopwatch.StartNew();
        }

        public static void EndNetworkMeasure(Stopwatch sw)
        {
            sw.Stop();
            Interlocked.Add(ref networkTime, sw.ElapsedMilliseconds);
        }

        public static Stopwatch BeginProcessingMeasure()
        {
            return Stopwatch.StartNew();
        }

        public static void EndProcessingMeasure(Stopwatch sw)
        {
            sw.Stop();
            Interlocked.Add(ref procesingTime, sw.ElapsedMilliseconds);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ClaudeCliProtocol.Tests
{
    /// <summary>Tests that measure the thread pool of the whole process: they run alone, after every other test.</summary>
    [CollectionDefinition("alone", DisableParallelization = true)]
    public class AloneCollection
    {
    }

    [Collection("alone")]
    public class ProcessTransportThreadTests
    {
        [Fact]
        public async Task A_running_claude_holds_no_thread_of_the_pool()
        {
            // A thread of the pool held for the whole life of a claude is one fewer for the application. With a few
            // conversations open, a small machine would have none left and everything else would wait.
            var transports = new List<ProcessTransport>();
            try
            {
                var before = MostFreeWorkers();
                for (var i = 0; i < 4; i++)
                {
                    var transport = new ProcessTransport(ProcessTransportTests.Options());
                    transport.Start();
                    transports.Add(transport);
                }
                foreach (var transport in transports) await ProcessTransportTests.Next(transport); // each one is up and read

                var during = MostFreeWorkers();

                Assert.True(before - during <= 1,
                    "Four running claudes hold " + (before - during) + " threads of the pool.");
            }
            finally
            {
                foreach (var transport in transports) await transport.DisposeAsync();
            }
        }

        /// <summary>
        /// The most workers of the pool seen free over a moment: a thread that is busy the whole time is held, not
        /// passing by.
        /// </summary>
        private static int MostFreeWorkers()
        {
            var most = 0;
            var watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < 1500)
            {
                ThreadPool.GetAvailableThreads(out var workers, out _);
                most = Math.Max(most, workers);
                Thread.Sleep(20);
            }
            return most;
        }
    }
}

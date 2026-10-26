// Copyright 2025 Keyfactor
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.


using Microsoft.Extensions.Logging;
using System;
using Xunit.Abstractions;

namespace Keyfactor.Extensions.Orchestrator.Aws.Acm.Tests.TestUtilities
{
    /// <summary>
    /// Routes extension log output into the xUnit test output so a failing assertion can be read
    /// alongside the Debug/Trace lines the job emitted. Mirrors the pattern used by the Keyfactor
    /// sample orchestrator extension's unit tests.
    /// </summary>
    public class XunitLogger : ILogger
    {
        private readonly ITestOutputHelper _output;
        private readonly string _category;

        public XunitLogger(string category, ITestOutputHelper output)
        {
            _category = category;
            _output = output;
        }

        public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
            Func<TState, Exception, string> formatter)
        {
            // ITestOutputHelper throws if written to after the test has completed (a job that logs
            // from a background continuation, for example). Swallow that rather than failing an
            // otherwise-passing test on a logging side effect.
            try
            {
                _output.WriteLine($"[{logLevel}] {_category}: {formatter(state, exception)}");
                if (exception != null) _output.WriteLine(exception.ToString());
            }
            catch (InvalidOperationException) { }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new NullScope();
            public void Dispose() { }
        }
    }

    public class XunitLogger<T> : XunitLogger, ILogger<T>
    {
        public XunitLogger(ITestOutputHelper output) : base(typeof(T).Name, output) { }
    }
}

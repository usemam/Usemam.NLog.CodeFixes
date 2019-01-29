using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestHelper;

namespace Usemam.NLog.CodeFixes.Test
{
    [TestClass]
    public class ObsoleteLoggerMethodUsageCodeFixProviderTests : CodeFixVerifier
    {
        [TestMethod]
        public void NoWarnings_NoDiagnosticsExpected()
        {
            var test = @"
    using System;

    using NLog;

    namespace ConsoleApplication1
    {
        class TypeName
        {
            private readonly ILogger _logger = LogManager.GetCurrentClassLogger();

            public static void Main(string[] args)
            {
                _logger.Error(new Exception());
                _logger.Error(new Exception(), ""Error message"");
            }
        }
    }";
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void NonExceptionSuffixObsoleteMethod_DiagnosticExpected()
        {
            var test = @"
    using System;

    using NLog;

    namespace ConsoleApplication1
    {
        class TypeName
        {
            private readonly ILogger _logger = LogManager.GetCurrentClassLogger();

            public static void Main(string[] args)
            {
                _logger.Error(""Error message"", new Exception());
            }
        }
    }";
            VerifyCSharpDiagnostic(test,
                new DiagnosticResult
                {
                    Id = ObsoleteLoggerMethodUsageAnalyzer.DiagnosticId,
                    Message = ObsoleteLoggerMethodUsageAnalyzer.Message,
                    Severity = DiagnosticSeverity.Warning,
                    Locations = new [] { new DiagnosticResultLocation("Test0.cs", 14, 17) }
                });
        }

        [TestMethod]
        public void ExceptionSuffixObsoleteMethod_DiagnosticExpected()
        {
            var test = @"
    using System;

    using NLog;

    namespace ConsoleApplication1
    {
        class TypeName
        {
            private readonly ILogger _logger = LogManager.GetCurrentClassLogger();

            public static void Main(string[] args)
            {
                _logger.ErrorException(""Error message"", new Exception());
            }
        }
    }";
            VerifyCSharpDiagnostic(
                test,
                new DiagnosticResult
                {
                    Id = ObsoleteLoggerMethodUsageAnalyzer.DiagnosticId,
                    Message = ObsoleteLoggerMethodUsageAnalyzer.Message,
                    Severity = DiagnosticSeverity.Warning,
                    Locations = new[] { new DiagnosticResultLocation("Test0.cs", 14, 17) }
                });
        }

        [TestMethod]
        public void FixTriggered_ExpectedDocumentToBeFixed()
        {
            var test = @"
    using System;

    using NLog;

    namespace ConsoleApplication1
    {
        class TypeName
        {
            private readonly ILogger _logger = LogManager.GetCurrentClassLogger();

            public static void Main(string[] args)
            {
                _logger.Trace(""Error message"", new Exception());
                _logger.Debug(""Error message"", new Exception());
                _logger.Info(""Error message"", new Exception());
                _logger.Warn(""Error message"", new Exception());
                _logger.Error(""Error message"", new Exception());
                _logger.Fatal(""Error message"", new Exception());
                _logger.TraceException(""Error message"", new Exception());
                _logger.DebugException(""Error message"", new Exception());
                _logger.InfoException(""Error message"", new Exception());
                _logger.WarnException(""Error message"", new Exception());
                _logger.ErrorException(""Error message"", new Exception());
                _logger.FatalException(""Error message"", new Exception());
            }
        }
    }";

            var fixtest = @"
    using System;

    using NLog;

    namespace ConsoleApplication1
    {
        class TypeName
        {
            private readonly ILogger _logger = LogManager.GetCurrentClassLogger();

            public static void Main(string[] args)
            {
                _logger.Trace(new Exception(), ""Error message"");
                _logger.Debug(new Exception(), ""Error message"");
                _logger.Info(new Exception(), ""Error message"");
                _logger.Warn(new Exception(), ""Error message"");
                _logger.Error(new Exception(), ""Error message"");
                _logger.Fatal(new Exception(), ""Error message"");
                _logger.Trace(new Exception(), ""Error message"");
                _logger.Debug(new Exception(), ""Error message"");
                _logger.Info(new Exception(), ""Error message"");
                _logger.Warn(new Exception(), ""Error message"");
                _logger.Error(new Exception(), ""Error message"");
                _logger.Fatal(new Exception(), ""Error message"");
            }
        }
    }";
            VerifyCSharpFix(test, fixtest, allowNewCompilerDiagnostics: true);
        }

        [TestMethod]
        public void StringArgSpansMultipleLines_ExpectedArgToBeExtractedIntoVariable()
        {
            var test = @"
    using System;

    using NLog;

    namespace ConsoleApplication1
    {
        class TypeName
        {
            private readonly ILogger _logger = LogManager.GetCurrentClassLogger();

            public static void Main(string[] args)
            {
                _logger.Trace(
                    string.Format(
                        ""Important message. Parameters: [ID1={0}]"",
                        1234567),
                    new Exception());
            }
        }
    }";

            var fixtest = @"
    using System;

    using NLog;

    namespace ConsoleApplication1
    {
        class TypeName
        {
            private readonly ILogger _logger = LogManager.GetCurrentClassLogger();

            public static void Main(string[] args)
            {
                string traceLogMessage = string.Format(
                        ""Important message. Parameters: [ID1={0}]"",
                        1234567);
                _logger.Trace(
                    new Exception(), traceLogMessage);
            }
        }
    }";
            VerifyCSharpFix(test, fixtest, allowNewCompilerDiagnostics: true);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new ObsoleteLoggerMethodUsageCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new ObsoleteLoggerMethodUsageAnalyzer();
        }
    }
}

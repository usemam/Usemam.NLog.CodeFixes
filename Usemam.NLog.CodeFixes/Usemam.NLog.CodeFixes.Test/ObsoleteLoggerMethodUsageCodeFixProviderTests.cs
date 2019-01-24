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
        public void CodeFixTriggered_ExpectedMethodArgsToBeSwapped()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    using NLog;

    namespace ConsoleApplication1
    {
        class TypeName
        {
            private readonly ILogger _logger = LogManager.GetCurrentClassLogger();

            public void Method()
            {
                _logger.Error(""Error message"", new Exception());
            }
        }
    }";

            var fixtest = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    using NLog;

    namespace ConsoleApplication1
    {
        class TypeName
        {
            private readonly ILogger _logger = LogManager.GetCurrentClassLogger();

            public void Method()
            {
                _logger.Error(new Exception(), ""Error message"");
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

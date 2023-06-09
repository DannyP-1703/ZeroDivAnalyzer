﻿using Microsoft.CodeAnalysis.Formatting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using VerifyCS = ZeroDivAnalyzer.Test.CSharpAnalyzerVerifier<
    ZeroDivAnalyzer.ZeroDivAnalyzerAnalyzer>;



namespace ZeroDivAnalyzer.Test
{
    [TestClass]
    public class ZeroDivAnalyzerUnitTest
    {
        [TestMethod]
        public async Task TestNoDiv_NoDiagnostics()
        {
            var test = @"
namespace ConsoleApplication1
    {
        class Program
        {   
            int a = 6;
        }
    }";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task TestSimpleLiteral()
        {
            var test = @"
    namespace ConsoleApplication1
    {
        class Program
        {   
            int a = {|CS0020:(35 + 89) / 0.0m|};
        }
    }";
            var expected = VerifyCS.Diagnostic("ZeroDivAnalyzer").WithSpan(6, 21, 6, 37).WithArguments("0.0m");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task TestVarEqualsZero()
        {
            var test = @"
    using System;

    namespace ConsoleApplication1
    {
        class Program
        {   
            int b = 1;

            private static void Main(string[] args)
            {
                int a = 5;
                int b = 0;
                Console.WriteLine(a + [|a / b|]);
            }
        }
    }";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task TestVarReassigned_NoDiagnostics()
        {
            var test = @"
    using System;

    namespace ConsoleApplication1
    {
        class Program
        {   
            int b = 1;

            private static int foo() { return 5; }

            private static void Main(string[] args)
            {
                int b = 0;
                int a = 5;
                b = foo();
                Console.WriteLine(a + a / b);
            }
        }
    }";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task TestSubtractionLiteral()
        {
            var test = @"
    using System;

    namespace ConsoleApplication1
    {
        class Program
        {   
            private static void Main(string[] args)
            {
                Console.WriteLine({|CS0020:(8745 - 855) / (5 - 5)|});
            }
        }
    }";
            var expected = VerifyCS.Diagnostic("ZeroDivAnalyzer").WithSpan(10, 35, 10, 57).WithArguments("(5 - 5)");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task TestSubtractionVar()
        {
            var test = @"
    using System;

    namespace ConsoleApplication1
    {
        class Program
        {   
            private static int foo() { return 5; }
            private static void Main(string[] args)
            {
                Int64 val = 5;
                Console.WriteLine([|foo() / (val - val)|]);
            }
        }
    }";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task TestSubtraction1_NoDiagnostics()
        {
            var test = @"
    using System;

    namespace ConsoleApplication1
    {
        class Program
        {   
            private static int foo() { return 7; }
            private static void Main(string[] args)
            {
                Console.WriteLine((8745 - 855) / (foo() - foo()));
            }
        }
    }";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task TestSubtraction2_NoDiagnostics()
        {
            var test = @"
    using System;

    namespace ConsoleApplication1
    {
        class Program
        {   
            private static void Main(string[] args)
            {
                int a = 6;
                int b = 5;
                Console.WriteLine(b / (a - a + 1));
            }
        }
    }";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task TestMultiplyVar()
        {
            var test = @"
    using System;

    namespace ConsoleApplication1
    {
        class Program
        {   
            private static int foo() { return 5; }

            private static void Main(string[] args)
            {
                int b = 0;
                int a = 0;
                b = foo();
                Console.WriteLine(a + [|a / (b * a)|]);
            }
        }
    }";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task TestMultiplyLiteral()
        {
            var test = @"
    using System;

    namespace ConsoleApplication1
    {
        class Program
        {   
            private static int foo() { return 5; }

            private static void Main(string[] args)
            {
                int a = 5;
                Console.WriteLine(a + [|a / (0 * a)|]);
            }
        }
    }";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task TestMultiply_NoDiagnostics()
        {
            var test = @"
    using System;

    namespace ConsoleApplication1
    {
        class Program
        {   
            private static int foo() { return 5; }

            private static void Main(string[] args)
            {
                int a = 5, b = 0;
                b = a + b;
                Console.WriteLine(a + a / (b * a));
            }
        }
    }";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task TestFuncLiteral()
        {
            var test = @"
    using System;

    namespace ConsoleApplication1
    {
        class Program
        {   
            private static int foo() 
            { 
                return 0; 
            }

            private static void Main(string[] args)
            {
                int a = 5;
                Console.WriteLine([|a / Program.foo()|]);
            }
        }
    }";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task TestFuncVar()
        {
            var test = @"
    using System;

    namespace ConsoleApplication1
    {
        class Program
        {   
            private static int foo(int a) 
            { 
                int value = 0;
                Console.WriteLine(a);
                return value; 
            }

            private static void Main(string[] args)
            {
                int a = 5;
                Console.WriteLine([|a / Program.foo(a)|]);
            }
        }
    }";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task TestFuncVar_NoDiagnostics()
        {
            var test = @"
    using System;

    namespace ConsoleApplication1
    {
        class Program
        {   
            private static int foo(int a) 
            { 
                int value = 0;
                Console.WriteLine(value++ + a);
                return value; 
            }

            private static void Main(string[] args)
            {
                int a = 5;
                Console.WriteLine(a / Program.foo(a));
            }
        }
    }";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task TestFuncExpression()
        {
            var test = @"
    using System;

    namespace ConsoleApplication1
    {
        class Program
        {   
            private static int foo(int a) 
            { 
                int value = 0;
                Console.WriteLine(a);
                return a * value; 
            }

            private static void Main(string[] args)
            {
                int a = 5;
                Console.WriteLine([|a / Program.foo(a)|]);
            }
        }
    }";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task TestFuncAssigned()
        {
            var test = @"
    using System;

    namespace ConsoleApplication1
    {
        class Program
        {   
            private static double foo(double a) 
            { 
                double value = 0.0;
                a += 1;
                double smthn = value;
                for (int i = 0; i < 6; ++i)
                {
                    Console.WriteLine(a);
                    if (i == a)
                    {
                        return a; 
                    }
                }
                return smthn * a;
            }

            private static void Main(string[] args)
            {
                double a = 5.0;
                double b = Program.foo(a);
                Console.WriteLine([|a / b|]);
            }
        }
    }";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task TestFuncSeveralReturns()
        {
            var test = @"
    using System;

    namespace ConsoleApplication1
    {
        class Program
        {   
            private static int foo(int a) 
            { 
                int val = 10;
                switch(a)
                {
                   case 1:
                        return val;
                   case 2:
                        return val * val;
                   case 3:
                        return 0;
                   default:
                        return 1;
                }
            }

            private static void Main(string[] args)
            {
                Console.WriteLine([|5 / foo(6)|]);
            }
        }
    }";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task TestRecursion()
        {
            var test = @"
    using System;

    namespace ConsoleApplication1
    {
        class Program
        {   
            private static double foo(double a) 
            { 
                double value = 0.0;
                a++;
                double smthn = value;
                for (int i = 0; i < 6; ++i)
                {
                    Console.WriteLine(a);
                    if (i == a)
                    {
                        return foo(smthn);
                    }
                }
                return smthn;
            }

            private static void Main(string[] args)
            {
                double a = 5.0;
                double b = Program.foo(a);
                Console.WriteLine([|a / b|]);
            }
        }
    }";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}

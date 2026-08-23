using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

internal static class RunCompiledBehaviorTests
{
    private static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: Run-CompiledBehaviorTests <test-assembly>");
            return 2;
        }

        var assembly = Assembly.LoadFrom(args[0]);
        var passed = 0;
        var failed = 0;

        foreach (var type in assembly.GetTypes())
        {
            object instance = null;
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var invocations = GetInvocations(method);
                if (invocations == null)
                {
                    continue;
                }

                if (instance == null)
                {
                    instance = Activator.CreateInstance(type);
                }

                foreach (var arguments in invocations)
                {
                    try
                    {
                        method.Invoke(instance, arguments);
                        Console.WriteLine("PASS " + type.Name + "." + method.Name);
                        passed++;
                    }
                    catch (TargetInvocationException exception)
                    {
                        var cause = exception.InnerException ?? exception;
                        Console.WriteLine("FAIL " + type.Name + "." + method.Name + ": " + cause.Message);
                        failed++;
                    }
                }
            }
        }

        Console.WriteLine("SUMMARY passed=" + passed + " failed=" + failed);
        return failed == 0 ? 0 : 1;
    }

    private static IReadOnlyList<object[]> GetInvocations(MethodInfo method)
    {
        var cases = (TestCaseAttribute[])method.GetCustomAttributes(typeof(TestCaseAttribute), false);
        if (cases.Length > 0)
        {
            var invocations = new List<object[]>();
            foreach (var testCase in cases)
            {
                invocations.Add(testCase.Arguments);
            }

            return invocations;
        }

        return method.IsDefined(typeof(TestAttribute), false)
            ? new[] { Array.Empty<object>() }
            : null;
    }
}

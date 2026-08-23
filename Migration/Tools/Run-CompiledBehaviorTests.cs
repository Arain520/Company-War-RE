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
            var setupMethods = GetLifecycleMethods<SetUpAttribute>(type);
            var tearDownMethods = GetLifecycleMethods<TearDownAttribute>(type);
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
                        InvokeAll(instance, setupMethods);
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
                    finally
                    {
                        InvokeAll(instance, tearDownMethods);
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

    private static IReadOnlyList<MethodInfo> GetLifecycleMethods<TAttribute>(Type type)
        where TAttribute : Attribute
    {
        var methods = new List<MethodInfo>();
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (method.IsDefined(typeof(TAttribute), true))
            {
                methods.Add(method);
            }
        }

        return methods;
    }

    private static void InvokeAll(object instance, IReadOnlyList<MethodInfo> methods)
    {
        foreach (var method in methods)
        {
            method.Invoke(instance, Array.Empty<object>());
        }
    }
}

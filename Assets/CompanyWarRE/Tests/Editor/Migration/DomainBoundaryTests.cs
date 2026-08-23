using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace CompanyWarRE.Migration.Tests
{
    public sealed class DomainBoundaryTests
    {
        private static readonly string[] ForbiddenDomainDependencies =
        {
            "using UnityEngine",
            "using UnityEditor",
            "using QFramework",
            "UnityEngine.",
            "QFramework.",
            "Cysharp.Threading.Tasks",
            "DG.Tweening",
            "UnityEngine.AddressableAssets"
        };

        [Test]
        public void DomainSource_RemainsPureCSharp()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("Cannot resolve the Unity project root.");
            var domainRoot = Path.Combine(projectRoot, "Assets", "CompanyWarRE", "Domain");
            if (!Directory.Exists(domainRoot))
            {
                Assert.Pass("Domain production code has not been migrated in the first batch.");
                return;
            }

            var violations = new List<string>();
            foreach (var file in Directory.GetFiles(domainRoot, "*.cs", SearchOption.AllDirectories))
            {
                var source = File.ReadAllText(file);
                foreach (var forbidden in ForbiddenDomainDependencies)
                {
                    if (source.Contains(forbidden))
                    {
                        violations.Add($"{file}: {forbidden}");
                    }
                }
            }

            Assert.That(violations, Is.Empty, "Domain boundary violations:\n" + string.Join("\n", violations));
        }
    }
}

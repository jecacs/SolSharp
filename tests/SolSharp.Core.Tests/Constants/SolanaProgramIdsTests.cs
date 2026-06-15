using System.Reflection;
using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;

namespace SolSharp.Core.Tests.Constants;

public static class SolanaProgramIdsTests
{
    // Reflect over every constant so new additions are guarded automatically.
    public static IEnumerable<TestCaseData> AllConstants()
    {
        foreach (var type in new[] { typeof(SolanaProgramIds), typeof(Sysvars), typeof(Mints) })
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
                    yield return new TestCaseData((string)field.GetRawConstantValue()!).SetName($"{type.Name}.{field.Name}");
            }
        }
    }

    [TestFixture]
    public sealed class Validity
    {
        [TestCaseSource(typeof(SolanaProgramIdsTests), nameof(AllConstants))]
        public void IsValidThirtyTwoByteKey(string base58)
            => PublicKey.TryParse(base58, out _).Should().BeTrue();
    }
}

namespace WinUtil.Tests;

internal static class TestAssert
{
    internal static void Empty<T>(IReadOnlyCollection<T> values)
    {
        if (values.Count != 0)
        {
            throw new InvalidOperationException($"Expected no values but found {values.Count}.");
        }
    }

    internal static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected '{expected}' but found '{actual}'.");
        }
    }

    internal static void False(bool value)
    {
        if (value)
        {
            throw new InvalidOperationException("Expected false but found true.");
        }
    }

    internal static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual)
    {
        if (!expected.SequenceEqual(actual))
        {
            throw new InvalidOperationException("Expected both sequences to contain the same values in the same order.");
        }
    }

    internal static void True(bool value)
    {
        if (!value)
        {
            throw new InvalidOperationException("Expected true but found false.");
        }
    }

    internal static T Single<T>(IReadOnlyCollection<T> values)
    {
        if (values.Count != 1)
        {
            throw new InvalidOperationException($"Expected one value but found {values.Count}.");
        }

        return values.Single();
    }
}

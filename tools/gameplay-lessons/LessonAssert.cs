using System;

namespace GameplayLessons
{
    internal static class LessonAssert
    {
        internal static void That(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        internal static void Near(float actual, float expected, string message)
        {
            if (float.IsNaN(actual) || Math.Abs(actual - expected) > .00001f)
                throw new InvalidOperationException(message + ": expected " + expected + ", actual " + actual);
        }

        internal static void Throws<TException>(Action action, string message) where TException : Exception
        {
            try { action(); }
            catch (TException) { return; }
            throw new InvalidOperationException(message);
        }
    }
}

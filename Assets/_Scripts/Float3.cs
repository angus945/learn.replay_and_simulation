using System;


public readonly struct Float3
{
    public static readonly Float3 Zero = new Float3(0f, 0f, 0f);

    public readonly float X;
    public readonly float Y;
    public readonly float Z;

    public Float3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public Float3 Normalized()
    {
        float magnitudeSquared = X * X + Y * Y + Z * Z;
        if (magnitudeSquared <= 0f)
            return Zero;

        float inverseMagnitude = 1f / (float)Math.Sqrt(magnitudeSquared);
        return new Float3(
            X * inverseMagnitude,
            Y * inverseMagnitude,
            Z * inverseMagnitude);
    }

    public static Float3 operator +(Float3 left, Float3 right)
    {
        return new Float3(
            left.X + right.X,
            left.Y + right.Y,
            left.Z + right.Z);
    }

    public static Float3 operator *(Float3 value, float scalar)
    {
        return new Float3(
            value.X * scalar,
            value.Y * scalar,
            value.Z * scalar);
    }

    public override string ToString()
    {
        return $"({X}, {Y}, {Z})";
    }
}

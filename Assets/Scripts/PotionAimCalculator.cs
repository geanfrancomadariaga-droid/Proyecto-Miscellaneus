using System;
using UnityEngine;

/// <summary>Calcula el apuntado sin depender de componentes o de la entrada.</summary>
public static class PotionAimCalculator
{
    public readonly struct Result
    {
        public Vector2 Direction { get; }
        public float Intensity { get; }
        public float Distance { get; }
        public bool IsValid { get; }

        internal Result(Vector2 direction, float intensity, float distance)
        {
            Direction = direction;
            Intensity = intensity;
            Distance = distance;
            IsValid = true;
        }
    }

    /// <summary>
    /// La zona muerta es una fraccion del radio en [0, 1).
    /// La intensidad usa el radio completo, sin restar la zona muerta.
    /// El limite de la zona muerta tambien es invalido.
    /// Devuelve un resultado invalido con valores cero si cualquier entrada
    /// no es finita, el radio no es positivo, la zona muerta esta fuera de
    /// [0, 1), o los alcances no cumplen 0 <= minRange <= maxRange.
    /// </summary>
    public static Result Calculate(Vector2 drag, float maxDragRadius,
        float deadZone, float minRange, float maxRange)
    {
        if (!IsFinite(drag.x) || !IsFinite(drag.y) ||
            !IsFinite(maxDragRadius) || maxDragRadius <= 0f ||
            !IsFinite(deadZone) || deadZone < 0f || deadZone >= 1f ||
            !IsFinite(minRange) || !IsFinite(maxRange) ||
            minRange < 0f || maxRange < minRange)
            return default;

        // Double evita desbordamientos al medir vectores float muy grandes.
        double length = Math.Sqrt((double)drag.x * drag.x + (double)drag.y * drag.y);
        if (length <= (double)maxDragRadius * deadZone)
            return default;

        Vector2 direction = new Vector2((float)(drag.x / length), (float)(drag.y / length));
        float intensity = (float)Math.Min(length / maxDragRadius, 1d);
        float distance = Mathf.Lerp(minRange, maxRange, intensity);
        return new Result(direction, intensity, distance);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

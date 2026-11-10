using System;
using UnityEngine;

/// <summary>Desplaza una bomba hasta un destino fijo, sin gestionar su ciclo de vida.</summary>
public sealed class BombProjectile : MonoBehaviour
{
    public event Action Arrived;

    private Vector3 origin;
    private Vector3 destination;
    private float duration;
    private float elapsed;
    private bool initialized;
    private bool arrived;

    /// <summary>
    /// Inicializa un unico trayecto en coordenadas del mundo.
    /// Devuelve false si ya fue inicializado. Una duracion <= 0 llega inmediatamente.
    /// Suscribirse a Arrived antes de llamar a este metodo.
    /// </summary>
    public bool Initialize(Vector3 origin, Vector3 destination, float duration)
    {
        if (initialized)
            return false;

        initialized = true;
        this.origin = origin;
        this.destination = destination;
        this.duration = duration;
        transform.position = origin;

        if (duration <= 0f)
            CompleteArrival();

        return true;
    }

    private void Update()
    {
        Advance(Time.deltaTime);
    }

    /// <summary>Avanza el trayecto; ignora deltas no positivos y trayectos inactivos.</summary>
    public void Advance(float deltaTime)
    {
        if (!initialized || arrived || deltaTime <= 0f)
            return;

        elapsed = Mathf.Min(elapsed + deltaTime, duration);
        if (elapsed >= duration)
        {
            CompleteArrival();
            return;
        }

        transform.position = Vector3.Lerp(origin, destination, elapsed / duration);
    }

    private void CompleteArrival()
    {
        // Cerrar el trayecto antes de notificar impide duplicados por llamadas desde el evento.
        arrived = true;
        transform.position = destination;
        Arrived?.Invoke();
    }
}

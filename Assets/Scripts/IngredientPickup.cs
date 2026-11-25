using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Entrega ingredientes a un inventario y desaparece solo si este los acepta.
/// Colocar junto a un Collider2D con Is Trigger activado. El jugador necesita
/// un Collider2D y un Rigidbody2D simulado (dinamico, sin gravedad).
/// El Rigidbody2D existente del jugador basta; no hace falta otro en el pickup.
/// Las capas de ambos colliders deben permitir el contacto y sus callbacks.
/// </summary>
[DisallowMultipleComponent]
public sealed class IngredientPickup : MonoBehaviour
{
    [Tooltip("Ingrediente que se entrega al inventario del jugador.")]
    [SerializeField] private PotionEffect ingredient = PotionEffect.Fuego;

    [Tooltip("Cantidad positiva que se entrega si el inventario la acepta.")]
    [SerializeField, Min(1)] private int amount = 1;

    private bool collected;
    [Tooltip("En preparación, volver a entrar permite recolectar otra unidad.")]
    [SerializeField] private bool renewable;
    private readonly HashSet<Collider2D> contacts = new HashSet<Collider2D>();
    public PotionEffect Ingredient => ingredient;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Unity puede enviar eventos de trigger a MonoBehaviours deshabilitados.
        if (!isActiveAndEnabled || other == null)
            return;

        PlayerIngredientInventory inventory = other.GetComponentInParent<PlayerIngredientInventory>();
        if (inventory == null) return;
        if (renewable) contacts.Add(other);
        if (collected || !inventory.TryAdd(ingredient, amount))
            return;

        // Destroy es diferido: bloquear otros contactos desde este mismo momento.
        collected = true;
        if (!renewable) Destroy(gameObject);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (renewable && contacts.Remove(other) && contacts.Count == 0)
            collected = false;
    }

    private void OnDisable()
    {
        contacts.Clear();
        if (renewable) collected = false;
    }
}

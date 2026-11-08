using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Un clic de X resuelve un golpe; mantenerlo pulsado no repite.</summary>
[RequireComponent(typeof(PlayerController))]
public sealed class PlayerMeleeAttack : MonoBehaviour
{
    [SerializeField] private Button attackButton;
    [SerializeField] private Health testTarget;
    [SerializeField] private Text targetHealthLabel;
    [SerializeField] private LineRenderer hitOutline;
    [SerializeField, Min(0.01f)] private float reachOffset = 0.9f;
    [SerializeField, Min(0.01f)] private float hitRadius = 0.45f;
    [SerializeField, Min(0.01f)] private float feedbackSeconds = 0.18f;

    private PlayerController player;
    private float hideFeedbackAt;
    private readonly HashSet<Health> damaged = new HashSet<Health>();

    private void Awake()
    {
        player = GetComponent<PlayerController>();
    }

    private void OnEnable()
    {
        if (attackButton != null)
            attackButton.onClick.AddListener(Attack);
        if (testTarget != null)
            testTarget.HealthChanged += UpdateHealthLabel;
    }

    private void Start()
    {
        if (hitOutline != null)
            hitOutline.enabled = false;
        if (testTarget != null)
            UpdateHealthLabel(testTarget.CurrentHealth, testTarget.MaxHealth);
    }

    public void Attack()
    {
        if (!isActiveAndEnabled)
            return;

        Vector2 center = (Vector2)transform.position + player.LastMovementDirection * reachOffset;
        damaged.Clear();
        // Una consulta por golpe: el contorno visible no aplica dano por contacto.
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(center, hitRadius))
        {
            Health health = hit.GetComponentInParent<Health>();
            if (health == null || health.transform == transform ||
                health.transform.IsChildOf(transform) || !damaged.Add(health))
                continue;
            health.TakeDamage(5);
        }

        if (hitOutline == null)
            return;
        const int points = 32;
        hitOutline.useWorldSpace = true;
        hitOutline.loop = true;
        hitOutline.positionCount = points;
        for (int i = 0; i < points; i++)
        {
            float angle = i * Mathf.PI * 2f / points;
            hitOutline.SetPosition(i, new Vector3(center.x + Mathf.Cos(angle) * hitRadius,
                center.y + Mathf.Sin(angle) * hitRadius, 0f));
        }
        hitOutline.enabled = true;
        hideFeedbackAt = Time.unscaledTime + feedbackSeconds;
    }

    private void Update()
    {
        if (hitOutline != null && hitOutline.enabled && Time.unscaledTime >= hideFeedbackAt)
            hitOutline.enabled = false;
    }

    private void UpdateHealthLabel(int current, int maximum)
    {
        if (targetHealthLabel != null)
            targetHealthLabel.text = $"Objetivo: {current}/{maximum} PV";
    }

    private void OnDrawGizmosSelected()
    {
        PlayerController controller = GetComponent<PlayerController>();
        Vector2 direction = controller != null ? controller.LastMovementDirection : Vector2.down;
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere((Vector2)transform.position + direction * reachOffset, hitRadius);
    }

    private void OnDisable()
    {
        if (attackButton != null)
            attackButton.onClick.RemoveListener(Attack);
        if (testTarget != null)
            testTarget.HealthChanged -= UpdateHealthLabel;
        if (hitOutline != null)
            hitOutline.enabled = false;
    }
}

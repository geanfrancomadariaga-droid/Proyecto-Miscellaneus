using UnityEngine;

/// <summary>Advierte una zona fija de ataque; no aplica dano ni mueve al enemigo.</summary>
[RequireComponent(typeof(EnemyCombatState), typeof(Health))]
[DisallowMultipleComponent]
public sealed class EnemyMeleeTelegraph : MonoBehaviour
{
    [Tooltip("Referencia explicita al Transform del Player.")]
    [SerializeField] private Transform player;
    [Tooltip("Alcance provisional del GDD, medido entre los centros en XY.")]
    [SerializeField, Min(0.01f)] private float meleeRange = 1.2f;
    [Tooltip("Anchura provisional de la zona rectangular, en unidades del mundo.")]
    [SerializeField, Min(0.01f)] private float zoneWidth = 0.8f;
    [Tooltip("Provisional: el GDD no confirma la duracion exacta del aviso cercano.")]
    [SerializeField, Min(0.01f)] private float warningSeconds = 0.6f;
    [Tooltip("Provisional: espera DESPUES del aviso. Ciclo inicial total: 2.4 s.")]
    [SerializeField, Min(0f)] private float recoverySeconds = 1.8f;
    [Tooltip("Orden dentro de la Sorting Layer del SpriteRenderer del enemigo.")]
    [SerializeField] private int sortingOrder = 20;

    private EnemyCombatState combatState;
    private Health health;
    private GameObject zoneObject;
    private SpriteRenderer zoneRenderer;
    private Texture2D zoneTexture;
    private Sprite zoneSprite;
    private float warningEndsAt;
    private float nextWarningAt;

    public bool IsWarning { get; private set; }
    public Vector2 LockedDirection { get; private set; }
    public Vector2 LockedCenter { get; private set; }
    public Vector2 LockedSize { get; private set; }
    public float LockedAngle { get; private set; }

    // Centro, dimensiones completas y angulo en grados de la zona anunciada.
    public event System.Action<Vector2, Vector2, float> WarningCompleted;

    private void Awake()
    {
        combatState = GetComponent<EnemyCombatState>();
        health = GetComponent<Health>();
    }

    private void OnEnable()
    {
        health.Died += CancelWarning;
    }

    private void Update()
    {
        TickAt(Time.time);
    }

    // Reloj inyectado solo en las pruebas, como PlayerMeleeAttack.AttackAt.
    private void TickAt(float now)
    {
        if (!isActiveAndEnabled || health == null || health.IsDead ||
            combatState == null || !combatState.isActiveAndEnabled ||
            combatState.CurrentState != EnemyCombatState.Mode.DueloCercano || player == null)
        {
            CancelWarning();
            return;
        }

        if (IsWarning)
        {
            if (now >= warningEndsAt)
            {
                CancelWarning();
                WarningCompleted?.Invoke(LockedCenter, LockedSize, LockedAngle);
            }
            return;
        }

        Vector2 offset = (Vector2)player.position - (Vector2)transform.position;
        float range = Mathf.Max(0.01f, meleeRange);
        if (now < nextWarningAt || offset.sqrMagnitude > range * range)
            return;

        LockedDirection = offset.sqrMagnitude > 0.000001f ? offset.normalized : Vector2.down;
        LockedCenter = (Vector2)transform.position + LockedDirection * (range * 0.5f);
        LockedSize = new Vector2(range, Mathf.Max(0.01f, zoneWidth));
        LockedAngle = Mathf.Atan2(LockedDirection.y, LockedDirection.x) * Mathf.Rad2Deg;
        CreateVisualIfNeeded();
        // Objeto sin padre: ni el jugador ni el enemigo arrastran la zona fijada.
        zoneObject.transform.SetPositionAndRotation(
            new Vector3(LockedCenter.x, LockedCenter.y, transform.position.z),
            Quaternion.Euler(0f, 0f, LockedAngle));
        zoneObject.transform.localScale = new Vector3(LockedSize.x, LockedSize.y, 1f);
        zoneRenderer.sortingOrder = sortingOrder;
        SpriteRenderer enemySprite = GetComponent<SpriteRenderer>();
        zoneRenderer.sortingLayerID = enemySprite != null ? enemySprite.sortingLayerID : 0;
        zoneObject.layer = gameObject.layer;
        zoneObject.SetActive(true);
        IsWarning = true;
        warningEndsAt = now + Mathf.Max(0.01f, warningSeconds);
        nextWarningAt = warningEndsAt + Mathf.Max(0f, recoverySeconds);
    }

    private void CreateVisualIfNeeded()
    {
        if (zoneObject != null)
            return;

        // Un unico recurso pequeno por enemigo, reutilizado entre avisos.
        // Borde negro + blanco: visible sobre fondos claros y oscuros, sin luces.
        const int size = 16;
        zoneTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        zoneTexture.name = "Enemy melee warning (runtime)";
        zoneTexture.hideFlags = HideFlags.DontSave;
        zoneTexture.filterMode = FilterMode.Point;
        zoneTexture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int edge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
            pixels[y * size + x] = edge == 0 ? Color.black :
                edge <= 2 ? Color.white : new Color(1f, 1f, 1f, 0.2f);
        }
        zoneTexture.SetPixels(pixels);
        zoneTexture.Apply(false, true);
        zoneSprite = Sprite.Create(zoneTexture, new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
        zoneSprite.hideFlags = HideFlags.DontSave;
        zoneObject = new GameObject("Enemy melee danger zone (runtime)");
        zoneObject.hideFlags = HideFlags.DontSave;
        zoneObject.SetActive(false);
        zoneRenderer = zoneObject.AddComponent<SpriteRenderer>();
        // Material compartido predeterminado del SpriteRenderer; no instanciamos materiales.
        zoneRenderer.sprite = zoneSprite;
    }

    internal void CancelWarning()
    {
        IsWarning = false;
        if (zoneObject != null)
            zoneObject.SetActive(false);
    }

    private void OnDisable()
    {
        if (health != null)
            health.Died -= CancelWarning;
        CancelWarning();
        // Conservar nextWarningAt evita saltarse la recuperacion al reactivar.
    }

    private void OnDestroy()
    {
        if (zoneObject != null) Destroy(zoneObject);
        if (zoneSprite != null) Destroy(zoneSprite);
        if (zoneTexture != null) Destroy(zoneTexture);
    }
}

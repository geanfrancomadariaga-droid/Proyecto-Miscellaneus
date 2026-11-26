using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Conecta instancias de la escena mediante propiedades serializadas y una operación Undo.</summary>
public static class DuelSceneSetupEditor
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string Operation = "Integrar tres duelos";

    [MenuItem("Tools/Miscellaneus/Integrar efectos mágicos")]
    public static void IntegrateMagic()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath ||
            PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            Debug.LogError("Abre SampleScene como escena activa, fuera de Play y del modo Prefab.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        try
        {
            var player = Root(scene, "Player");
            var canvas = Root(scene, "Canvas");
            var sequence = Require<DuelSequenceController>(canvas);
            var combat = ReadRef<GameObject>(Require<DuelFlowUI>(canvas), "combatControls");
            var aim = Require<PotionAimInput>(Child(combat, "PotionAimControl"));
            var selector = Require<PotionSelectionButton>(Child(combat, "PotionSelectionButtonUI"));
            var charges = Require<EquippedPotionCharges>(player);
            var health = Require<Health>(player);
            CheckRef(selector, "charges", charges);
            CheckClick(Require<Button>(selector.gameObject), selector, nameof(selector.SelectNext));
            ConnectMagicLaunchers(scene, player, health, charges, sequence, aim);
            ConfigureVialControls(combat, selector, charges, sequence, aim);

            EditorSceneManager.MarkSceneDirty(scene);
            Undo.SetCurrentGroupName("Integrar efectos mágicos");
            Undo.CollapseUndoOperations(group);
            Debug.Log("Efectos vinculados al jugador y a tres rivales. Revisa SampleScene antes de guardarla.", canvas);
        }
        catch (Exception e)
        {
            Undo.FlushUndoRecordObjects();
            Undo.RevertAllDownToGroup(group);
            Debug.LogError($"Integrar efectos mágicos: {e.Message}");
        }
    }

    private static void ConnectMagicLaunchers(Scene scene, GameObject player, Health health,
        EquippedPotionCharges charges, DuelSequenceController sequence, PotionAimInput aim)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/FireBomb.prefab");
        if (prefab == null) throw new InvalidOperationException("Falta Assets/Prefabs/FireBomb.prefab.");
        var projectile = Require<BombProjectile>(prefab);
        Require<FireExplosion>(prefab);

        var rivals = new SerializedObject(sequence).FindProperty("rivals");
        if (rivals.arraySize != 3)
            throw new InvalidOperationException("Revisa los tres rivales de DuelSequenceController.");
        var launchers = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<PotionThrowController>(true)).ToArray();
        if (launchers.Length != 3)
            throw new InvalidOperationException($"Se esperaban tres lanzadores, encontrados {launchers.Length}.");

        Link(Ensure<PotionCombatEffects>(player), "sequence", sequence);
        var assigned = new HashSet<EnemyVictoryController>();
        for (int i = 0; i < rivals.arraySize; i++)
        {
            var rival = rivals.GetArrayElementAtIndex(i).objectReferenceValue as EnemyVictoryController;
            if (rival == null || rival.gameObject.scene != scene || !assigned.Add(rival))
                throw new InvalidOperationException($"Revisa el rival {i + 1} de la secuencia.");
            var host = i == 0 ? player : rival.gameObject;
            var launcher = Require<PotionThrowController>(host);
            if (i == 0) CheckRef(sequence, "firstLauncher", launcher);
            Require<EnemyChaseMovement>(rival.gameObject);
            Link(Ensure<PotionCombatEffects>(rival.gameObject), "sequence", sequence);
            Link(launcher, "sequence", sequence);
            Link(launcher, "charges", charges);
            Link(launcher, "aimInput", aim);
            Link(launcher, "playerHealth", health);
            Link(launcher, "enemyHealth", Require<Health>(rival.gameObject));
            Link(launcher, "projectilePrefab", projectile);
        }
    }

    private static void ConfigureVialControls(GameObject combat, PotionSelectionButton selector,
        EquippedPotionCharges charges, DuelSequenceController sequence, PotionAimInput aim)
    {
        var vial = NewButton(combat, "Usar vial", "USAR VIAL", 0, 0, 1, 1);
        if (vial.onClick.GetPersistentEventCount() != 0)
            throw new InvalidOperationException("USAR VIAL recibe el clic en tiempo de juego. Revisa su lista On Click.");
        var aimRect = Require<RectTransform>(aim.gameObject);
        var vialRect = Require<RectTransform>(vial.gameObject);
        Undo.RecordObject(vialRect, "Colocar botón de vial");
        vialRect.anchorMin = aimRect.anchorMin;
        vialRect.anchorMax = aimRect.anchorMax;
        vialRect.pivot = aimRect.pivot;
        vialRect.sizeDelta = aimRect.sizeDelta;
        vialRect.anchoredPosition = aimRect.anchoredPosition;
        var selectorRect = Require<RectTransform>(selector.gameObject);
        Undo.RecordObject(selectorRect, "Mostrar la fórmula equipada");
        selectorRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 172f);
        var status = NewLabel(combat, "Estado de efectos", 20, .02f, .71f, .72f, .85f);
        var controls = Ensure<PotionCombatControls>(combat);
        Link(controls, "sequence", sequence);
        Link(controls, "charges", charges);
        Link(controls, "aimInput", aim);
        Link(controls, "useVial", vial);
        Link(controls, "status", status);
    }

    [MenuItem("Tools/Miscellaneus/Integrar tres duelos")]
    public static void Integrate()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath ||
            PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            Debug.LogError("Abre SampleScene como escena activa, fuera de Play y del modo Prefab.");
            return;
        }
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(Operation);
        try
        {
            var player = Root(scene, "Player");
            var canvas = Root(scene, "Canvas");
            Require<Canvas>(canvas);
            var defeat = Require<PlayerDefeatController>(player);
            var crafting = Require<PotionCraftingController>(canvas);
            var sequence = Ensure<DuelSequenceController>(canvas);
            var first = Root(scene, "MeleeTestTarget");
            var rivals = new EnemyVictoryController[3];
            var launcher = Require<PotionThrowController>(player);
            CheckRef(launcher, "aimInput", Require<PotionAimInput>(Child(canvas, "PotionAimControl")));
            CheckRef(launcher, "playerHealth", Require<Health>(player));
            CheckRef(launcher, "charges", Require<EquippedPotionCharges>(player));
            CheckRef(launcher, "enemyHealth", Require<Health>(first));
            var projectile = ReadRef<BombProjectile>(launcher, "projectilePrefab");
            Require<FireExplosion>(projectile.gameObject);
            int[] hp = { 100, 120, 150 };
            for (int i = 0; i < rivals.Length; i++)
            {
                var enemy = i == 0 ? first : Variant(scene, first, i + 1);
                rivals[i] = ConnectEnemy(enemy, player, hp[i]);
                if (i > 0)
                {
                    // Cada copia usa el lanzador existente solo mientras su rival está activo.
                    var copy = Ensure<PotionThrowController>(enemy);
                    Link(copy, "aimInput", ReadRef<PotionAimInput>(launcher, "aimInput"));
                    Link(copy, "playerHealth", Require<Health>(player));
                    Link(copy, "charges", Require<EquippedPotionCharges>(player));
                    Link(copy, "projectilePrefab", projectile);
                    Link(copy, "enemyHealth", Require<Health>(enemy));
                    var data = new SerializedObject(copy);
                    data.FindProperty("flightDuration").floatValue =
                        new SerializedObject(launcher).FindProperty("flightDuration").floatValue;
                    data.ApplyModifiedProperties();
                }
                Active(enemy, false);
            }
            var serialized = new SerializedObject(sequence);
            var list = serialized.FindProperty("rivals");
            list.arraySize = rivals.Length;
            for (int i = 0; i < rivals.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = rivals[i];
            serialized.ApplyModifiedProperties();

            CheckRef(crafting, "inventory", Require<PlayerIngredientInventory>(player));
            CheckRef(crafting, "equipment", Require<EquippedPotionCharges>(player));
            CheckRef(crafting, "resultText", Require<Text>(Child(canvas, "TextResultadoElaboracion")));
            CheckRef(crafting, "selectionText", Require<Text>(Child(canvas, "TextFormulaSeleccionada")));
            CheckRef(defeat, "playerHealth", Require<Health>(player));
            CheckRef(defeat, "playerMovement", Require<PlayerController>(player));
            CheckRef(defeat, "playerBody", Require<Rigidbody2D>(player));
            CheckRef(defeat, "playerAttack", Require<PlayerMeleeAttack>(player));
            CheckRef(defeat, "potionAimInput", ReadRef<PotionAimInput>(launcher, "aimInput"));
            Link(sequence, "defeat", defeat);
            Link(sequence, "crafting", crafting);
            Link(sequence, "firstLauncher", launcher);
            Undo.RecordObject(launcher, Operation);
            launcher.enabled = false;
            Link(crafting, "sequence", sequence);
            for (int i = 0; i < 3; i++)
            {
                var effect = (PotionEffect)i;
                var pickup = Require<IngredientPickup>(Root(scene, $"Pickup_{effect}_01"));
                if (pickup.Ingredient != effect || !Require<Collider2D>(pickup.gameObject).isTrigger)
                    throw new InvalidOperationException($"Revisa ingrediente y trigger de {pickup.name}.");
                var data = new SerializedObject(pickup);
                data.FindProperty("renewable").boolValue = true;
                data.ApplyModifiedProperties();
                Active(pickup.gameObject, effect != PotionEffect.Savia);
                if (effect == PotionEffect.Savia) Link(sequence, "sapPickup", pickup);
            }
            ConfigureUI(canvas, player, sequence, crafting);
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(group);
            Debug.Log("Tres duelos vinculados. Revisa SampleScene y guárdala antes de probar el reintento.", canvas);
        }
        catch (Exception e)
        {
            Undo.FlushUndoRecordObjects();
            Undo.RevertAllDownToGroup(group);
            Debug.LogError($"{Operation}: {e.Message}");
        }
    }

    private static GameObject Variant(Scene scene, GameObject source, int duel)
    {
        string name = $"MeleeTestTarget_Duelo{duel}";
        var existing = Root(scene, name, false);
        if (existing != null) return existing;
        var clone = Object.Instantiate(source);
        clone.name = name;
        Undo.RegisterCreatedObjectUndo(clone, Operation);
        return clone;
    }

    private static EnemyVictoryController ConnectEnemy(GameObject enemy, GameObject player, int hp)
    {
        var body = Require<Rigidbody2D>(enemy);
        if (body.bodyType != RigidbodyType2D.Dynamic || body.gravityScale != 0f ||
            body.linearDamping != 8f || body.constraints != RigidbodyConstraints2D.FreezeRotation ||
            !body.simulated || !Require<Collider2D>(enemy).enabled)
            throw new InvalidOperationException($"Revisa en {enemy.name} el collider y Rigidbody2D (Dynamic, gravedad 0, amortiguación 8, Freeze Rotation).");
        var health = Require<Health>(enemy);
        var data = new SerializedObject(health);
        data.FindProperty("maxHealth").intValue = hp;
        data.ApplyModifiedProperties();
        var combat = Require<EnemyCombatState>(enemy);
        var telegraph = Require<EnemyMeleeTelegraph>(enemy);
        var strike = Require<EnemyMeleeStrike>(enemy);
        var victory = Require<EnemyVictoryController>(enemy);
        Link(combat, "playerAttack", Require<PlayerMeleeAttack>(player));
        Link(telegraph, "player", player.transform);
        Link(strike, "playerHealth", Require<Health>(player));
        Link(Require<EnemyChaseMovement>(enemy), "player", player.transform);
        Link(victory, "enemyHealth", health);
        Link(victory, "enemyCombatState", combat);
        Link(victory, "enemyTelegraph", telegraph);
        Link(victory, "enemyStrike", strike);
        return victory;
    }

    private static void ConfigureUI(GameObject canvas, GameObject player,
        DuelSequenceController sequence, PotionCraftingController crafting)
    {
        var ui = Ensure<DuelFlowUI>(canvas);
        var attack = Require<PlayerMeleeAttack>(player);
        var defeatUI = Require<DefeatScreenUI>(Child(canvas, "DefeatScreenController"));
        var defeatPanel = ReadRef<GameObject>(defeatUI, "defeatPanel");
        var retry = ReadRef<Button>(defeatUI, "retryButton");
        if (!retry.transform.IsChildOf(defeatPanel.transform) || defeatPanel.transform.parent != canvas.transform)
            throw new InvalidOperationException("Revisa la jerarquía de DefeatPanel y RetryButton.");
        var retryCaption = retry.GetComponentInChildren<Text>(true);
        if (retryCaption == null) throw new InvalidOperationException("Falta el texto de RetryButton.");
        Undo.RecordObject(retryCaption, Operation);
        retryCaption.text = "REINTENTAR PARTIDA";
        CheckRef(defeatUI, "defeatController", Require<PlayerDefeatController>(player));
        var banner = Require<VictoryBannerUI>(Child(canvas, "VictoryUIController"));
        Undo.RecordObject(banner, Operation);
        banner.enabled = false;
        Active(ReadRef<GameObject>(banner, "victoryBanner"), false);

        var move = Group(canvas, "Movimiento táctil");
        var combat = Group(canvas, "Controles de combate");
        foreach (string name in new[] { "Joystick", "JoystickRecoveryTest" }) Move(Child(canvas, name), move);
        foreach (string name in new[] { "MeleeButtonX", "PotionAimControl", "PotionSelectionButtonUI" })
            Move(Child(canvas, name), combat);
        var bar = Group(canvas, "Cabecera de duelo");
        // El HUD de campo deja libres los controles táctiles inferiores.
        Layout(bar, 0, .85f, 1, 1);
        Background(bar, .88f);
        var status = Require<Text>(Child(canvas, "TargetHealth"));
        Move(status.gameObject, bar);
        StyleText(status, 20);
        Layout(status.gameObject, .02f, .05f, .76f, .95f);
        Link(attack, "testTarget", null);
        Link(attack, "targetHealthLabel", null);
        var fieldButton = NewButton(bar, "Acción de campo", "VISITAR BRUJA", .77f, .12f, .98f, .88f);

        var shop = Group(canvas, "Mesa de la bruja");
        Background(shop, .98f);
        var objective = NewLabel(shop, "Preparación", 24, .04f, .80f, .96f, .98f);
        var inventoryRow = Group(canvas, "Ingredientes recogidos");
        Layout(inventoryRow, .04f, .71f, .96f, .80f);
        Background(inventoryRow, .85f);
        inventoryRow.GetComponent<Image>().raycastTarget = false;
        string[] stocks = { "TextIngredienteFuego", "TextIngredienteEscarcha", "TextIngredienteSavia" };
        string[] fields = { "fireText", "frostText", "sapText" };
        var inventoryHUD = Require<IngredientInventoryHUD>(canvas);
        CheckRef(inventoryHUD, "inventory", Require<PlayerIngredientInventory>(player));
        for (int i = 0; i < stocks.Length; i++)
        {
            CheckRef(inventoryHUD, fields[i], Require<Text>(Child(canvas, stocks[i])));
            PlaceText(canvas, inventoryRow, stocks[i], i / 3f, 0f, (i + 1) / 3f, 1f);
        }
        PlaceText(canvas, shop, "TextFormulaSeleccionada", .03f, .62f, .97f, .71f);
        PlaceText(canvas, shop, "TextResultadoElaboracion", .02f, .21f, .98f, .30f);
        ExistingButton(canvas, shop, "ButtonCambiarBase", crafting, "NextBase", .04f, .49f, .33f, .60f);
        ExistingButton(canvas, shop, "ButtonCambiarEfecto", crafting, "NextEffect", .355f, .49f, .645f, .60f);
        var modifier = ExistingButton(canvas, shop, "ButtonCambiarModificador", crafting, "ToggleModifier", .67f, .49f, .96f, .60f);
        ExistingButton(canvas, shop, "ButtonElaborarFormula", crafting, "CraftSelectedFormula", .04f, .33f, .33f, .45f);
        ExistingButton(canvas, shop, "ButtonElaborarBomba", crafting, "CraftFireBomb", .355f, .33f, .645f, .45f);
        var remove = NewButton(shop, "Retirar carga", "RETIRAR 1 CARGA\nDEL EFECTO ELEGIDO", .67f, .33f, .96f, .45f);
        if (remove.onClick.GetPersistentEventCount() == 0)
            UnityEventTools.AddPersistentListener(remove.onClick, crafting.RemoveOneConflictingCharge);
        CheckClick(remove, crafting, nameof(crafting.RemoveOneConflictingCharge));
        var back = NewButton(shop, "Recolectar", "VOLVER A RECOLECTAR", .08f, .04f, .46f, .17f);
        var fight = NewButton(shop, "Comenzar duelo", "COMENZAR DUELO", .54f, .04f, .92f, .17f);

        var modal = Group(canvas, "Pantalla de partida");
        Background(modal, .96f);
        var message = NewLabel(modal, "Mensaje", 36, .07f, .48f, .93f, .79f);
        var action = NewButton(modal, "Acción de partida", "INICIAR PARTIDA", .28f, .23f, .72f, .39f);
        Link(ui, "sequence", sequence);
        Link(ui, "movement", Require<PlayerController>(player));
        Link(ui, "attack", attack);
        Link(ui, "movementControls", move);
        Link(ui, "combatControls", combat);
        Link(ui, "fieldBar", bar);
        Link(ui, "workshop", shop);
        Link(ui, "inventoryRow", Require<RectTransform>(inventoryRow));
        Link(ui, "modal", modal);
        Link(ui, "fieldText", status);
        Link(ui, "workshopText", objective);
        Link(ui, "modalText", message);
        Link(ui, "fieldAction", fieldButton);
        Link(ui, "modalAction", action);
        Link(ui, "fightButton", fight);
        Link(ui, "returnButton", back);
        Link(ui, "modifierButton", modifier);
        Active(move, false);
        Active(combat, false);
        Active(bar, false);
        Active(shop, false);
        Active(inventoryRow, false);
        Active(modal, true);
        Active(defeatPanel, false);
        Undo.SetSiblingIndex(defeatPanel.transform, canvas.transform.childCount - 1, Operation);
        Undo.RecordObject(message, Operation);
        message.text = "Proyecto Miscellaneus\nTres duelos con el mago hongo";
    }

    private static GameObject Root(Scene scene, string name, bool required = true) =>
        Unique(scene.GetRootGameObjects().Where(go => go.name == name).ToArray(), name, required);

    private static GameObject Child(GameObject parent, string name) =>
        Unique(parent.GetComponentsInChildren<Transform>(true).Where(t => t.name == name)
            .Select(t => t.gameObject).ToArray(), name, true);

    private static GameObject Unique(GameObject[] matches, string name, bool required)
    {
        if (matches.Length == 0 && !required) return null;
        if (matches.Length != 1)
            throw new InvalidOperationException($"Se esperaba un objeto '{name}', encontrados {matches.Length}.");
        return matches[0];
    }

    private static T Require<T>(GameObject go) where T : Component =>
        go.GetComponent<T>() ?? throw new InvalidOperationException($"Falta {typeof(T).Name} en {go.name}.");

    private static T Ensure<T>(GameObject go) where T : Component =>
        go.GetComponent<T>() ?? Undo.AddComponent<T>(go);

    private static T ReadRef<T>(Object owner, string field) where T : Object =>
        new SerializedObject(owner).FindProperty(field)?.objectReferenceValue as T ??
        throw new InvalidOperationException($"Falta {owner.name}.{field}.");

    private static void CheckRef(Object owner, string field, Object expected)
    {
        if (ReadRef<Object>(owner, field) != expected)
            throw new InvalidOperationException($"Revisa {owner.name}.{field}. Se esperaba {expected.name}.");
    }

    private static void Link(Object owner, string field, Object value)
    {
        var data = new SerializedObject(owner);
        var property = data.FindProperty(field) ??
            throw new InvalidOperationException($"No existe {owner.GetType().Name}.{field}.");
        property.objectReferenceValue = value;
        data.ApplyModifiedProperties();
    }

    private static void Active(GameObject go, bool active)
    {
        if (go.activeSelf == active) return;
        Undo.RecordObject(go, Operation);
        go.SetActive(active);
    }

    private static GameObject Rect(GameObject parent, string name)
    {
        var existing = parent.transform.Find(name);
        if (existing != null) return existing.gameObject;
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.layer;
        Undo.RegisterCreatedObjectUndo(go, Operation);
        Undo.SetTransformParent(go.transform, parent.transform, false, Operation);
        return go;
    }

    private static GameObject Group(GameObject parent, string name)
    {
        var go = Rect(parent, name);
        Layout(go, 0, 0, 1, 1);
        return go;
    }

    private static void Move(GameObject go, GameObject parent)
    {
        if (go.transform.parent != parent.transform)
            Undo.SetTransformParent(go.transform, parent.transform, false, Operation);
    }

    private static void Layout(GameObject go, float left, float bottom, float right, float top)
    {
        var rect = Require<RectTransform>(go);
        Undo.RecordObject(rect, Operation);
        rect.anchorMin = new Vector2(left, bottom);
        rect.anchorMax = new Vector2(right, top);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void Background(GameObject go, float alpha)
    {
        var image = Ensure<Image>(go);
        Undo.RecordObject(image, Operation);
        image.color = new Color(.09f, .08f, .10f, alpha);
        image.raycastTarget = true;
    }

    private static void StyleText(Text label, int size)
    {
        Undo.RecordObject(label, Operation);
        if (label.font == null) label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.alignment = TextAnchor.MiddleCenter;
        label.fontSize = size;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 14;
        label.resizeTextMaxSize = size;
        label.color = Color.white;
        label.raycastTarget = false;
    }

    private static Text NewLabel(GameObject parent, string name, int size,
        float left, float bottom, float right, float top)
    {
        var go = Rect(parent, name);
        Layout(go, left, bottom, right, top);
        var label = Ensure<Text>(go);
        StyleText(label, size);
        return label;
    }

    private static void PlaceText(GameObject canvas, GameObject parent, string name,
        float left, float bottom, float right, float top)
    {
        var go = Child(canvas, name);
        Move(go, parent);
        Layout(go, left, bottom, right, top);
        StyleText(Require<Text>(go), 20);
    }

    private static Button NewButton(GameObject parent, string name, string caption,
        float left, float bottom, float right, float top)
    {
        var go = Rect(parent, name);
        Layout(go, left, bottom, right, top);
        var image = Ensure<Image>(go);
        Undo.RecordObject(image, Operation);
        image.color = new Color(.30f, .28f, .32f);
        var button = Ensure<Button>(go);
        Undo.RecordObject(button, Operation);
        button.targetGraphic = image;
        var label = NewLabel(go, "Texto", 20, .02f, .02f, .98f, .98f);
        label.text = caption;
        return button;
    }

    private static Button ExistingButton(GameObject canvas, GameObject parent, string name,
        PotionCraftingController crafting, string method, float left, float bottom, float right, float top)
    {
        var go = Child(canvas, name);
        var button = Require<Button>(go);
        CheckClick(button, crafting, method);
        Move(go, parent);
        Layout(go, left, bottom, right, top);
        return button;
    }

    private static void CheckClick(Button button, Object target, string method)
    {
        var click = button.onClick;
        if (click.GetPersistentEventCount() != 1 || click.GetPersistentTarget(0) != target ||
            click.GetPersistentMethodName(0) != method ||
            click.GetPersistentListenerState(0) == UnityEngine.Events.UnityEventCallState.Off)
            throw new InvalidOperationException($"Revisa On Click de {button.name}: debe llamar una vez a {method}.");
    }
}

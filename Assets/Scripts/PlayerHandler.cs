using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System;

[RequireComponent(typeof(Inventory))]
public class PlayerHandler : MonoBehaviour, ITextInfoOverlay, IDamageable
{
    [System.Serializable]
    private class BuildOption
    {
        public string name;
        public GameObject prefab;
        public BuildCost[] costs;
    }

    Rigidbody rb;
    Inventory inventory;
    Camera mainCamera;

    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float interactRange = 10f;
    [SerializeField] private float pickupRange = 2.5f; // Actual pickup reach distance
    [SerializeField] private int damage = 1;
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private float rotationSpeed = 200f; // how fast player rotates toward movement
    [SerializeField] private bool showCrosshair = true;
    [SerializeField] [Range(0.1f, 0.9f)] private float crosshairVerticalViewport = 0.4f;
    [SerializeField] private bool showDebugOverlay = true; // Toggle debug overlay display
    [Header("Building")]
    [SerializeField] private bool enableBuildSystem = true;
    [SerializeField] private float buildRange = 12f;
    [SerializeField] private Vector3 buildGridSnap = new Vector3(1f, 0f, 1f);
    [SerializeField] private LayerMask buildPlacementMask = ~0;
    [SerializeField] private GameObject[] buildPrefabs;
    [SerializeField] private Color buildGhostValidColor = new Color(0.2f, 1f, 0.2f, 0.5f);
    [SerializeField] private Color buildGhostInvalidColor = new Color(1f, 0.2f, 0.2f, 0.5f);

    private Vector3 movementInput;
    private int currentHealth;

    private LineRenderer playerRayRenderer;
    private LineRenderer cameraRayRenderer;
    private ITextInfoOverlay currentHoveredObject = null;
    private Dictionary<ITextInfoOverlay, Vector3> visibleOverlayObjects = new Dictionary<ITextInfoOverlay, Vector3>();
    private List<BuildOption> buildOptions = new List<BuildOption>();
    private bool isBuildMenuOpen;
    private int selectedBuildIndex = -1;
    private GameObject buildPreviewInstance;
    private Vector3 buildPreviewPosition;
    private Quaternion buildPreviewRotation = Quaternion.identity;
    private bool canPlaceAtPreview;
    private Vector3 previewHalfExtents = Vector3.one * 0.5f;
    private float previewBottomOffset = -0.5f;
    private Collider[] playerColliders;
    private string buildPlacementDebugReason = string.Empty;
    private bool isNpcCommandMenuOpen;
    private bool isNpcGatherTypeMenuOpen;
    private INpc selectedNpcForCommand;
    private string npcCommandStatus = string.Empty;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        inventory = GetComponent<Inventory>();
        mainCamera = Camera.main;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        currentHealth = maxHealth;

        // Setup Player Ray LineRenderer for E interact and hit debuggin
        GameObject playerRayGO = new GameObject("PlayerRayDebug");
        playerRayGO.transform.SetParent(transform);
        playerRayGO.transform.localPosition = Vector3.zero;
        playerRayRenderer = playerRayGO.AddComponent<LineRenderer>();
        playerRayRenderer.startWidth = 0.05f;
        playerRayRenderer.endWidth = 0.05f;
        playerRayRenderer.positionCount = 2;
        playerRayRenderer.material = new Material(Shader.Find("Sprites/Default"));
        playerRayRenderer.startColor = Color.cyan;
        playerRayRenderer.endColor = Color.white;
        playerRayRenderer.useWorldSpace = true;

        // Setup Camera Ray LineRenderer for line of sight and aiming debugging
        GameObject cameraRayGO = new GameObject("CameraRayDebug");
        cameraRayGO.transform.SetParent(transform);
        cameraRayGO.transform.localPosition = Vector3.zero;
        cameraRayRenderer = cameraRayGO.AddComponent<LineRenderer>();
        cameraRayRenderer.startWidth = 0.05f;
        cameraRayRenderer.endWidth = 0.05f;
        cameraRayRenderer.positionCount = 2;
        cameraRayRenderer.material = new Material(Shader.Find("Sprites/Default"));
        cameraRayRenderer.startColor = Color.magenta;
        cameraRayRenderer.endColor = Color.white;
        cameraRayRenderer.useWorldSpace = true;

        this.tag = "Player";
        playerColliders = GetComponentsInChildren<Collider>();
        InitializeBuildOptions();
    }

    void OnEnable()
    {
        GameEvents.OnHit += HandleHit;
    }

    void Update()
    {
        Vector2 move = Keyboard.current != null
            ? new Vector2(
                (Keyboard.current.dKey.isPressed ? 1 : 0) - (Keyboard.current.aKey.isPressed ? 1 : 0),
                (Keyboard.current.wKey.isPressed ? 1 : 0) - (Keyboard.current.sKey.isPressed ? 1 : 0)
              )
            : Vector2.zero;

        movementInput = new Vector3(move.x, 0f, move.y);

        if (enableBuildSystem && Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame)
        {
            ToggleBuildMenu();
        }

        if (isBuildMenuOpen)
        {
            movementInput = Vector3.zero;
            return;
        }

        if (isNpcCommandMenuOpen)
        {
            movementInput = Vector3.zero;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CloseNpcCommandMenu();
            }
            return;
        }

        if (enableBuildSystem && selectedBuildIndex >= 0)
        {
            UpdateBuildPreview();

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                TryPlaceSelectedBuilding();
            }

            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                CancelBuildPlacement();
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CancelBuildPlacement();
            }

            return;
        }

        // Update hover detection for overlay
        UpdateHoveredObject();

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryHit();
        }

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            TryInteract();
        }
    }

    void FixedUpdate()
    {
        if (isBuildMenuOpen)
        {
            return;
        }

        if (movementInput.sqrMagnitude > 0.01f)
        {
            // Camera-relative direction
            Vector3 camForward = Camera.main.transform.forward;
            Vector3 camRight = Camera.main.transform.right;

            // Flatten camera vectors to horizontal plane
            camForward.y = 0;
            camRight.y = 0;
            camForward.Normalize();
            camRight.Normalize();

            // Direction to move (relative to camera)
            Vector3 moveDir = camForward * movementInput.z + camRight * movementInput.x;
            moveDir.Normalize();

            // Move
            Vector3 movement = moveDir * moveSpeed * Time.fixedDeltaTime;
            rb.MovePosition(rb.position + movement);

            // Rotate player toward movement direction (Y axis only)
            float targetAngle = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
            float currentAngle = transform.eulerAngles.y;
            float rotationAmount = Mathf.Min(rotationSpeed * Time.fixedDeltaTime, 1f);
            float rotatedAngle = Mathf.LerpAngle(currentAngle, targetAngle, rotationAmount);
            transform.rotation = Quaternion.Euler(0, rotatedAngle, 0);
        }
    }

    void UpdateHoveredObject()
    {
        // Cast ray from camera to see what info object we're looking at
        Ray cameraRay = GetCrosshairRay();
        currentHoveredObject = null;

        if (Physics.Raycast(cameraRay, out RaycastHit hit, interactRange))
        {
            ITextInfoOverlay infoOverlay = hit.collider.GetComponent<ITextInfoOverlay>();
            if (infoOverlay == null)
            {
                infoOverlay = hit.collider.GetComponentInParent<ITextInfoOverlay>();
            }
            currentHoveredObject = infoOverlay;
        }

        // Find all ITextInfoOverlay objects within range
        visibleOverlayObjects.Clear();
        
        var allOverlayObjects = FindObjectsByType<MonoBehaviour>();
        foreach (var obj in allOverlayObjects)
        {
            if (obj is ITextInfoOverlay overlay)
            {
                float distance = Vector3.Distance(transform.position, obj.transform.position);
                if (distance <= interactRange)
                {
                    visibleOverlayObjects[overlay] = obj.transform.position;
                }
            }
        }
    }

    void TryHit()
    {
        // Cast ray FROM CAMERA through crosshair - defines AIM DIRECTION
        Ray cameraRay = GetCrosshairRay();
        float cameraRayDistance = 1000f; // Infinite aiming direction
        Vector3 aimPoint = cameraRay.origin + cameraRay.direction * cameraRayDistance;
        
        // Debug: Draw camera ray showing aim direction (magenta)
        DrawDebugRay(cameraRay.origin, cameraRay.direction, cameraRayDistance, cameraRayRenderer, Color.magenta);

        // If camera ray hits ANY collider, use that point as precise aim target.
        bool hasCameraHit = RaycastIgnoreSelf(cameraRay, cameraRayDistance, out RaycastHit cameraHit);
        if (hasCameraHit)
        {
            aimPoint = cameraHit.point;
        }
        
        // Build player attack ray: toward hit point if available, else forward camera direction.
        Vector3 playerPos = transform.position + Vector3.up * 1.0f;
        Vector3 playerToAim = hasCameraHit ? (aimPoint - playerPos).normalized : cameraRay.direction;
        if (playerToAim.sqrMagnitude < 0.0001f)
        {
            playerToAim = transform.forward;
        }
        Ray playerRay = new Ray(playerPos, playerToAim);

        // Rotate player toward aim direction (snap immediately — this is a single-frame action)
        Vector3 flatAim = new Vector3(playerToAim.x, 0, playerToAim.z);
        if (flatAim.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(flatAim, Vector3.up);

        // Debug line draw - player ray in red, limited to weapon range
        DrawDebugRay(playerRay.origin, playerRay.direction, interactRange, playerRayRenderer, Color.red);

        // Check hitting - ALWAYS attempt to attack in that direction
        if (RaycastIgnoreSelf(playerRay, interactRange, out RaycastHit hit))
        {
            // Context of the hit
            HitEvent.HitCtx ctx = new HitEvent.HitCtx { dmg = damage };

            // Send event handler
            GameEvents.OnHit?.Invoke(new HitEvent(this.gameObject, hit.collider.gameObject, ctx));
        }
    }

    void TryInteract()
    {
        BuildPlayerAimRayFromCrosshair(1000f, false, out Ray cameraRay, out Ray playerRay, out Vector3 playerPos, out bool hasCameraHit, out RaycastHit cameraHit);

        DrawDebugRay(cameraRay.origin, cameraRay.direction, 1000f, cameraRayRenderer, Color.magenta);
        DrawDebugRay(playerRay.origin, playerRay.direction, interactRange, playerRayRenderer, Color.cyan);

        if (!TryGetCrosshairInteractionHit(interactRange, out RaycastHit hit, playerPos, hasCameraHit, cameraHit))
        {
            return;
        }

        // --- NPC interaction ---
        INpc npc = hit.collider.GetComponent<INpc>() ?? hit.collider.GetComponentInParent<INpc>();
        if (npc != null)
        {
            if (npc.IsNeutral() && npc.CanBeRecruited())
            {
                npc.Recruit(gameObject);
                Debug.Log($"Recruited {npc.GetDisplayName()}");
            }
            else if (npc.GetOwner() == gameObject)
            {
                OpenNpcCommandMenu(npc);
            }
            else if (!npc.CanBeRecruited())
            {
                Debug.Log($"{npc.GetDisplayName()} is hostile and cannot be recruited.");
            }
            return;
        }

        // --- Fall through to normal pickup ---
        TryPickup();
    }

    void OpenNpcCommandMenu(INpc npc)
    {
        if (npc == null)
        {
            return;
        }

        selectedNpcForCommand = npc;
        isNpcCommandMenuOpen = true;
        isNpcGatherTypeMenuOpen = false;
        npcCommandStatus = string.Empty;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void CloseNpcCommandMenu()
    {
        isNpcCommandMenuOpen = false;
        isNpcGatherTypeMenuOpen = false;
        selectedNpcForCommand = null;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void ApplyNpcBehaviourCommand(NpcBehaviour behaviour)
    {
        if (!isNpcCommandMenuOpen || selectedNpcForCommand == null)
        {
            return;
        }

        if (behaviour == NpcBehaviour.Gather)
        {
            isNpcGatherTypeMenuOpen = true;
            npcCommandStatus = "Choose gather target type";
            return;
        }

        selectedNpcForCommand.SetBehaviour(behaviour);
        npcCommandStatus = $"{selectedNpcForCommand.GetDisplayName()} => {behaviour}";

        Debug.Log(npcCommandStatus);
        CloseNpcCommandMenu();
    }

    void ApplyNpcGatherPreference(GatherResourcePreference preference)
    {
        if (!isNpcCommandMenuOpen || selectedNpcForCommand == null)
        {
            return;
        }

        selectedNpcForCommand.SetGatherPreference(preference);
        selectedNpcForCommand.SetBehaviour(NpcBehaviour.Gather);

        GameObject gatherTarget = FindNearestGatherableTarget(selectedNpcForCommand as MonoBehaviour, preference);
        if (gatherTarget != null)
        {
            selectedNpcForCommand.GatherFrom(gatherTarget);
            npcCommandStatus = $"{selectedNpcForCommand.GetDisplayName()} gathering {preference}: {gatherTarget.name}";
        }
        else
        {
            npcCommandStatus = $"{selectedNpcForCommand.GetDisplayName()} searching for {preference}";
        }

        Debug.Log(npcCommandStatus);
        CloseNpcCommandMenu();
    }

    GameObject FindNearestGatherableTarget(MonoBehaviour npcBehaviour, GatherResourcePreference preference)
    {
        if (npcBehaviour == null)
        {
            return null;
        }

        MonoBehaviour[] allBehaviours = FindObjectsByType<MonoBehaviour>();
        GameObject nearestTarget = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < allBehaviours.Length; i++)
        {
            if (allBehaviours[i] is not IGatherable)
            {
                continue;
            }

            if (!MatchesGatherPreference(allBehaviours[i].gameObject, preference))
            {
                continue;
            }

            float distance = Vector3.Distance(npcBehaviour.transform.position, allBehaviours[i].transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearestTarget = allBehaviours[i].gameObject;
            }
        }

        return nearestTarget;
    }

    bool MatchesGatherPreference(GameObject candidate, GatherResourcePreference preference)
    {
        if (candidate == null || preference == GatherResourcePreference.Closest)
        {
            return candidate != null;
        }

        if (preference == GatherResourcePreference.Tree)
        {
            return candidate.GetComponent<Tree>() != null || candidate.GetComponentInParent<Tree>() != null;
        }

        if (preference == GatherResourcePreference.Rock)
        {
            return candidate.GetComponent<RockNode>() != null || candidate.GetComponentInParent<RockNode>() != null;
        }

        return true;
    }

    void TryPickup()
    {
        BuildPlayerAimRayFromCrosshair(1000f, false, out Ray cameraRay, out Ray playerRay, out Vector3 playerPos, out bool hasCameraHit, out RaycastHit cameraHit);

        DrawDebugRay(cameraRay.origin, cameraRay.direction, 1000f, cameraRayRenderer, Color.magenta);
        DrawDebugRay(playerRay.origin, playerRay.direction, pickupRange, playerRayRenderer, Color.cyan);

        if (TryGetCrosshairInteractionHit(pickupRange, out RaycastHit hit, playerPos, hasCameraHit, cameraHit))
        {
            Debug.Log("Pick up ray reached object: " + hit.collider.gameObject.name + " " + hit.collider.gameObject.tag);

            // Search for IPickable on the hit collider's GameObject and its parents
            IPickable pickable = hit.collider.GetComponent<IPickable>();
            if (pickable == null)
            {
                pickable = hit.collider.GetComponentInParent<IPickable>();
            }

            if (pickable != null)
            {

                // Draw line to pickable item in green - showing successful reach
                DrawDebugRay(playerRay.origin, playerRay.direction, Vector3.Distance(playerPos, hit.point), playerRayRenderer, Color.green);

                ItemData itemData = pickable.GetItemData();
                GameEvents.OnPickup?.Invoke(new PickupEvent(this.gameObject, hit.collider.gameObject, itemData));
                pickable.OnPickup();
                Debug.Log("Picked up item: " + itemData.itemType);
            }
            else
            {
                // Draw line to non-pickable object in yellow
                DrawDebugRay(playerRay.origin, playerRay.direction, Vector3.Distance(playerPos, hit.point), playerRayRenderer, Color.yellow);
                Debug.Log("Hit non-pickable object: " + hit.collider.gameObject.name);
            }
        }
    }

    void DrawDebugRay(Vector3 origin, Vector3 direction, float distance, LineRenderer renderer, Color color)
    {
        renderer.startColor = color;
        renderer.endColor = Color.white;  // Gradient to white to show direction
        renderer.SetPosition(0, origin);
        renderer.SetPosition(1, origin + direction * distance);
    }

    void BuildPlayerAimRayFromCrosshair(float cameraRayDistance, bool rotatePlayer, out Ray cameraRay, out Ray playerRay, out Vector3 playerPos, out bool hasCameraHit, out RaycastHit cameraHit)
    {
        cameraRay = GetCrosshairRay();
        Vector3 aimPoint = cameraRay.origin + cameraRay.direction * cameraRayDistance;

        hasCameraHit = RaycastIgnoreSelf(cameraRay, cameraRayDistance, out cameraHit);
        if (hasCameraHit)
        {
            aimPoint = cameraHit.point;
        }

        playerPos = transform.position + Vector3.up * 1.0f;
        Vector3 playerToAim = hasCameraHit ? (aimPoint - playerPos).normalized : cameraRay.direction;
        if (playerToAim.sqrMagnitude < 0.0001f)
        {
            playerToAim = transform.forward;
        }

        playerRay = new Ray(playerPos, playerToAim);

        if (rotatePlayer)
        {
            Vector3 flatAim = new Vector3(playerToAim.x, 0, playerToAim.z);
            if (flatAim.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(flatAim, Vector3.up);
            }
        }
    }

    bool TryGetCrosshairInteractionHit(float maxRange, out RaycastHit hit, Vector3 playerPos, bool hasCameraHit, RaycastHit cameraHit)
    {
        hit = default;

        if (!hasCameraHit)
        {
            return false;
        }

        float playerDistanceToCameraTarget = Vector3.Distance(playerPos, cameraHit.point);
        if (playerDistanceToCameraTarget <= maxRange)
        {
            hit = cameraHit;
            return true;
        }

        return false;
    }

    Ray GetCrosshairRay()
    {
        float crosshairScreenY = Screen.height * (1f - crosshairVerticalViewport);
        return mainCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, crosshairScreenY, 0f));
    }

    bool RaycastIgnoreSelf(Ray ray, float maxDistance, out RaycastHit closestHit)
    {
        RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance);
        float closestDistance = float.MaxValue;
        closestHit = default;

        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTransform = hits[i].collider.transform;
            if (hitTransform.IsChildOf(transform))
            {
                continue;
            }

            if (hits[i].distance < closestDistance)
            {
                closestDistance = hits[i].distance;
                closestHit = hits[i];
            }
        }

        return closestDistance < float.MaxValue;
    }

    bool TryGetBuildPlacementHit(Ray ray, float maxDistance, out RaycastHit placementHit)
    {
        RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, buildPlacementMask, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        RaycastHit fallbackHit = default;
        bool hasFallback = false;

        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTransform = hits[i].collider.transform;
            if (hitTransform.IsChildOf(transform))
            {
                continue;
            }

            if (buildPreviewInstance != null && hitTransform.IsChildOf(buildPreviewInstance.transform))
            {
                continue;
            }

            if (!hasFallback)
            {
                fallbackHit = hits[i];
                hasFallback = true;
            }

            if (hits[i].normal.y >= 0.35f)
            {
                placementHit = hits[i];
                return true;
            }
        }

        placementHit = fallbackHit;
        return hasFallback;
    }

    void OnGUI()
    {
        if (showCrosshair)
        {
            // Draw crosshair at adjustable height, centered horizontally
            float centerX = Screen.width / 2f;
            float centerY = Screen.height * crosshairVerticalViewport;

            GUI.color = Color.white;
            GUI.Label(new Rect(centerX - 10, centerY - 10, 20, 20), "+");
        }

        // Draw all debug overlay info
        if (showDebugOverlay && visibleOverlayObjects.Count > 0)
        {
            const float labelWidth = 200f;
            const float padding    = 8f;

            foreach (var kvp in visibleOverlayObjects)
            {
                ITextInfoOverlay overlay = kvp.Key;
                Vector3 worldPos = kvp.Value;

                string infoText = overlay.GetInfoText();
                if (string.IsNullOrEmpty(infoText))
                    continue;

                // WorldToScreenPoint has Y=0 at bottom; OnGUI has Y=0 at top — flip it.
                Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);
                if (screenPos.z <= 0f) continue;
                float guiX = screenPos.x;
                float guiY = Screen.height - screenPos.y;

                bool isHovered = (overlay == currentHoveredObject);

                GUIStyle textStyle = new GUIStyle(GUI.skin.label);
                textStyle.wordWrap  = true;
                textStyle.richText  = false;
                if (isHovered)
                {
                    textStyle.fontStyle = FontStyle.Bold;
                    textStyle.fontSize  = 14;
                }

                // Measure text so the box always fits every line.
                float labelHeight = textStyle.CalcHeight(new GUIContent(infoText), labelWidth);
                float boxWidth    = labelWidth + padding * 2f;
                float boxHeight   = labelHeight + padding * 2f;

                // Place box just above the world-space anchor point.
                float boxLeft = guiX - boxWidth * 0.5f;
                float boxTop  = guiY - boxHeight - 6f;

                GUI.color = new Color(0f, 0f, 0f, 0.75f);
                GUI.Box(new Rect(boxLeft, boxTop, boxWidth, boxHeight), "");

                GUI.color = isHovered ? Color.yellow : Color.white;
                GUI.Label(new Rect(boxLeft + padding, boxTop + padding, labelWidth, labelHeight), infoText, textStyle);
            }
        }

        DrawBuildUI();
        DrawNpcCommandUI();
    }

    void OnDisable()
    {
        GameEvents.OnHit -= HandleHit;

        if (buildPreviewInstance != null)
        {
            Destroy(buildPreviewInstance);
            buildPreviewInstance = null;
        }
    }

    void HandleHit(HitEvent hit)
    {
        if (hit.dst != gameObject && (hit.dst == null || !hit.dst.transform.IsChildOf(transform)))
        {
            return;
        }

        TakeDamage(hit.ctx.dmg);
    }

    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        if (currentHealth < 0)
        {
            currentHealth = 0;
        }

        if (currentHealth == 0)
        {
            Debug.Log("Player died.");
        }
    }

    void InitializeBuildOptions()
    {
        buildOptions.Clear();

        if (buildPrefabs != null)
        {
            for (int i = 0; i < buildPrefabs.Length; i++)
            {
                if (buildPrefabs[i] == null)
                {
                    continue;
                }

                IBuildable buildable = GetBuildableFromObject(buildPrefabs[i]);
                if (buildable == null)
                {
                    Debug.LogWarning($"Build prefab '{buildPrefabs[i].name}' is missing an IBuildable component and will be ignored.");
                    continue;
                }

                string displayName = string.IsNullOrWhiteSpace(buildable.GetDisplayName()) ? buildPrefabs[i].name : buildable.GetDisplayName();
                BuildCost[] costs = buildable.GetBuildCosts() ?? Array.Empty<BuildCost>();
                buildOptions.Add(new BuildOption { name = displayName, prefab = buildPrefabs[i], costs = costs });
            }
        }
    }

    void ToggleBuildMenu()
    {
        bool nextState = !isBuildMenuOpen;
        isBuildMenuOpen = nextState;

        if (isBuildMenuOpen)
        {
            selectedBuildIndex = -1;
            if (buildPreviewInstance != null)
            {
                Destroy(buildPreviewInstance);
                buildPreviewInstance = null;
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void StartBuildPlacement(int buildIndex)
    {
        if (buildIndex < 0 || buildIndex >= buildOptions.Count)
        {
            return;
        }

        if (!CanAffordBuild(buildOptions[buildIndex], out string missingCost))
        {
            buildPlacementDebugReason = $"Missing {missingCost}";
            return;
        }

        selectedBuildIndex = buildIndex;
        isBuildMenuOpen = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        RebuildPreviewObject();
    }

    void CancelBuildPlacement()
    {
        selectedBuildIndex = -1;
        canPlaceAtPreview = false;

        if (buildPreviewInstance != null)
        {
            Destroy(buildPreviewInstance);
            buildPreviewInstance = null;
        }
    }

    void RebuildPreviewObject()
    {
        if (buildPreviewInstance != null)
        {
            Destroy(buildPreviewInstance);
            buildPreviewInstance = null;
        }

        BuildOption option = buildOptions[selectedBuildIndex];
        buildPreviewInstance = Instantiate(option.prefab);

        buildPreviewInstance.name = "BuildPreview_" + option.name;

        MonoBehaviour[] behaviours = buildPreviewInstance.GetComponentsInChildren<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            behaviours[i].enabled = false;
        }

        Collider[] previewColliders = buildPreviewInstance.GetComponentsInChildren<Collider>();
        for (int i = 0; i < previewColliders.Length; i++)
        {
            previewColliders[i].enabled = false;
        }

        Rigidbody[] rigidbodies = buildPreviewInstance.GetComponentsInChildren<Rigidbody>();
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            rigidbodies[i].isKinematic = true;
            rigidbodies[i].detectCollisions = false;
        }

        ComputePreviewExtents();
        SetPreviewVisualState(false);
    }

    void ComputePreviewExtents()
    {
        if (buildPreviewInstance == null)
        {
            previewHalfExtents = Vector3.one * 0.5f;
            previewBottomOffset = -previewHalfExtents.y;
            return;
        }

        Renderer[] renderers = buildPreviewInstance.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds combined = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                combined.Encapsulate(renderers[i].bounds);
            }

            previewHalfExtents = combined.extents;
            previewBottomOffset = combined.min.y - buildPreviewInstance.transform.position.y;
            return;
        }

        IBuildable buildable = GetBuildableFromObject(buildPreviewInstance);
        if (buildable != null)
        {
            previewHalfExtents = buildable.GetFootprintSize() * 0.5f;
            previewBottomOffset = -previewHalfExtents.y;
            return;
        }

        previewHalfExtents = Vector3.one * 0.5f;
        previewBottomOffset = -previewHalfExtents.y;
    }

    void UpdateBuildPreview()
    {
        if (selectedBuildIndex < 0 || selectedBuildIndex >= buildOptions.Count)
        {
            return;
        }

        if (buildPreviewInstance == null)
        {
            RebuildPreviewObject();
        }

        if (!CanAffordBuild(buildOptions[selectedBuildIndex], out string missingCost))
        {
            canPlaceAtPreview = false;
            buildPlacementDebugReason = $"Missing {missingCost}";
            SetPreviewVisualState(false);
            return;
        }

        Ray cameraRay = GetCrosshairRay();
        float cameraRayDistance = 1000f;
        Vector3 aimPoint = cameraRay.origin + cameraRay.direction * cameraRayDistance;

        bool hasCameraHit = RaycastIgnoreSelf(cameraRay, cameraRayDistance, out RaycastHit cameraHit);
        if (hasCameraHit)
        {
            aimPoint = cameraHit.point;
        }

        Vector3 playerPos = transform.position + Vector3.up * 1.0f;
        Vector3 playerToAim = hasCameraHit ? (aimPoint - playerPos).normalized : cameraRay.direction;
        if (playerToAim.sqrMagnitude < 0.0001f)
        {
            playerToAim = transform.forward;
        }

        Ray playerRay = new Ray(playerPos, playerToAim);
        if (!TryGetBuildPlacementHit(playerRay, buildRange, out RaycastHit hit))
        {
            canPlaceAtPreview = false;
            buildPlacementDebugReason = "No surface in range";
            SetPreviewVisualState(false);
            return;
        }

        float snapX = Mathf.Max(0.01f, buildGridSnap.x);
        float snapZ = Mathf.Max(0.01f, buildGridSnap.z);
        Vector3 snappedPosition = hit.point;
        snappedPosition.x = Mathf.Round(snappedPosition.x / snapX) * snapX;
        snappedPosition.z = Mathf.Round(snappedPosition.z / snapZ) * snapZ;
        snappedPosition.y = hit.point.y - previewBottomOffset;

        buildPreviewPosition = snappedPosition;
        buildPreviewInstance.transform.SetPositionAndRotation(buildPreviewPosition, buildPreviewRotation);

        Vector3 overlapExtents = new Vector3(
            Mathf.Max(0.05f, previewHalfExtents.x * 0.95f),
            Mathf.Max(0.05f, previewHalfExtents.y * 0.9f),
            Mathf.Max(0.05f, previewHalfExtents.z * 0.95f)
        );
        Vector3 overlapCenter = buildPreviewPosition + Vector3.up * 0.05f;
        canPlaceAtPreview = !IsBlockedByCollider(overlapCenter, overlapExtents, buildPreviewRotation, hit.collider);
        if (canPlaceAtPreview)
        {
            buildPlacementDebugReason = "Clear";
        }
        SetPreviewVisualState(canPlaceAtPreview);
    }

    bool IsBlockedByCollider(Vector3 center, Vector3 halfExtents, Quaternion rotation, Collider surfaceCollider)
    {
        Collider[] overlaps = Physics.OverlapBox(center, halfExtents, rotation, buildPlacementMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < overlaps.Length; i++)
        {
            if (buildPreviewInstance != null && overlaps[i].transform.IsChildOf(buildPreviewInstance.transform))
            {
                continue;
            }

            if (surfaceCollider != null && overlaps[i] == surfaceCollider)
            {
                continue;
            }

            buildPlacementDebugReason = $"Blocked by {overlaps[i].gameObject.name}";

            return true;
        }

        return false;
    }

    void SetPreviewVisualState(bool isValid)
    {
        if (buildPreviewInstance == null)
        {
            return;
        }

        Color tint = isValid ? buildGhostValidColor : buildGhostInvalidColor;
        Renderer[] renderers = buildPreviewInstance.GetComponentsInChildren<Renderer>();
        MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].GetPropertyBlock(propertyBlock);

            if (renderers[i].sharedMaterial != null && renderers[i].sharedMaterial.HasProperty("_BaseColor"))
            {
                propertyBlock.SetColor("_BaseColor", tint);
            }

            if (renderers[i].sharedMaterial != null && renderers[i].sharedMaterial.HasProperty("_Color"))
            {
                propertyBlock.SetColor("_Color", tint);
            }

            renderers[i].SetPropertyBlock(propertyBlock);
        }
    }

    void TryPlaceSelectedBuilding()
    {
        if (!canPlaceAtPreview || selectedBuildIndex < 0 || selectedBuildIndex >= buildOptions.Count)
        {
            return;
        }

        BuildOption option = buildOptions[selectedBuildIndex];
        if (!CanAffordBuild(option, out string missingCost))
        {
            buildPlacementDebugReason = $"Missing {missingCost}";
            canPlaceAtPreview = false;
            SetPreviewVisualState(false);
            return;
        }

        GameObject placedObject = Instantiate(option.prefab, buildPreviewPosition, buildPreviewRotation);
        ConsumeBuildCosts(option);

        IBuildable buildable = GetBuildableFromObject(placedObject);
        buildable?.OnPlaced(gameObject);
    }

    IBuildable GetBuildableFromObject(GameObject target)
    {
        if (target == null)
        {
            return null;
        }

        IBuildable buildable = target.GetComponent<IBuildable>();
        if (buildable != null)
        {
            return buildable;
        }

        return target.GetComponentInChildren<IBuildable>();
    }

    bool CanAffordBuild(BuildOption option, out string missingCost)
    {
        missingCost = string.Empty;

        if (option == null || option.costs == null)
        {
            return true;
        }

        for (int i = 0; i < option.costs.Length; i++)
        {
            if (option.costs[i].quantity <= 0 || string.IsNullOrWhiteSpace(option.costs[i].itemType))
            {
                continue;
            }

            if (!inventory.HasItem(option.costs[i].itemType, option.costs[i].quantity))
            {
                missingCost = $"{option.costs[i].itemType} x{option.costs[i].quantity}";
                return false;
            }
        }

        return true;
    }

    void ConsumeBuildCosts(BuildOption option)
    {
        if (option == null || option.costs == null)
        {
            return;
        }

        for (int i = 0; i < option.costs.Length; i++)
        {
            if (option.costs[i].quantity <= 0 || string.IsNullOrWhiteSpace(option.costs[i].itemType))
            {
                continue;
            }

            inventory.RemoveItem(option.costs[i].itemType, option.costs[i].quantity);
        }
    }

    string FormatBuildCosts(BuildCost[] costs)
    {
        if (costs == null || costs.Length == 0)
        {
            return "Free";
        }

        List<string> parts = new List<string>();
        for (int i = 0; i < costs.Length; i++)
        {
            if (costs[i].quantity <= 0 || string.IsNullOrWhiteSpace(costs[i].itemType))
            {
                continue;
            }

            parts.Add($"{costs[i].itemType} x{costs[i].quantity}");
        }

        return parts.Count == 0 ? "Free" : string.Join(", ", parts);
    }

    void DrawBuildUI()
    {
        if (isNpcCommandMenuOpen)
        {
            return;
        }

        if (!enableBuildSystem)
        {
            return;
        }

        if (isBuildMenuOpen)
        {
            float width = 420f;
            float height = 260f;
            Rect window = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUI.Box(window, "Build Menu");

            GUI.Label(new Rect(window.x + 12, window.y + 28, width - 24, 25), "Choose a building to place");

            if (buildOptions.Count == 0)
            {
                GUI.Label(new Rect(window.x + 12, window.y + 60, width - 24, 40), "No valid build prefabs found. Add prefabs with an IBuildable component.");
                if (GUI.Button(new Rect(window.x + width - 95f, window.y + height - 42f, 80f, 30f), "Close"))
                {
                    ToggleBuildMenu();
                }
                return;
            }

            int columns = 3;
            float buttonWidth = (width - 36f) / columns;
            float buttonHeight = 56f;

            for (int i = 0; i < buildOptions.Count; i++)
            {
                int row = i / columns;
                int col = i % columns;
                Rect buttonRect = new Rect(window.x + 12f + col * buttonWidth, window.y + 55f + row * (buttonHeight + 8f), buttonWidth - 8f, buttonHeight);
                bool canAfford = CanAffordBuild(buildOptions[i], out string missingCost);
                string buttonLabel = $"{buildOptions[i].name}\n{FormatBuildCosts(buildOptions[i].costs)}";
                if (!canAfford)
                {
                    buttonLabel += $"\nMissing: {missingCost}";
                }

                GUI.enabled = canAfford;
                if (GUI.Button(buttonRect, buttonLabel))
                {
                    StartBuildPlacement(i);
                }
                GUI.enabled = true;
            }

            if (GUI.Button(new Rect(window.x + width - 95f, window.y + height - 42f, 80f, 30f), "Close"))
            {
                ToggleBuildMenu();
            }

            return;
        }

        if (selectedBuildIndex >= 0 && selectedBuildIndex < buildOptions.Count)
        {
            string placementState = canPlaceAtPreview ? "LMB Place" : "Blocked";
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.Box(new Rect(10, Screen.height - 70, 520, 60), "");
            GUI.color = canPlaceAtPreview ? Color.green : Color.red;
            GUI.Label(new Rect(18, Screen.height - 64, 500, 24), $"Build Mode: {buildOptions[selectedBuildIndex].name} | {placementState} | RMB/Esc Cancel");
            GUI.color = Color.white;
            GUI.Label(new Rect(18, Screen.height - 40, 500, 20), $"Cost: {FormatBuildCosts(buildOptions[selectedBuildIndex].costs)} | Reason: {buildPlacementDebugReason}");
            GUI.color = Color.white;
        }
        else
        {
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.Box(new Rect(10, Screen.height - 36, 180, 26), "");
            GUI.color = Color.white;
            GUI.Label(new Rect(18, Screen.height - 32, 170, 20), "Press B to build");
        }
    }

    void DrawNpcCommandUI()
    {
        if (!isNpcCommandMenuOpen)
        {
            return;
        }

        if (selectedNpcForCommand == null)
        {
            CloseNpcCommandMenu();
            return;
        }

        float width = 360f;
        float height = 260f;
        Rect window = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        GUI.Box(window, isNpcGatherTypeMenuOpen ? $"{selectedNpcForCommand.GetDisplayName()} Gather" : $"{selectedNpcForCommand.GetDisplayName()} Commands");

        GUI.Label(new Rect(window.x + 12f, window.y + 30f, width - 24f, 20f), isNpcGatherTypeMenuOpen ? "Select resource target" : "Select NPC behaviour");

        int columns = 2;
        float buttonWidth = (width - 36f) / columns;
        float buttonHeight = 44f;

        if (isNpcGatherTypeMenuOpen)
        {
            GatherResourcePreference[] gatherPreferences = new[]
            {
                GatherResourcePreference.Closest,
                GatherResourcePreference.Tree,
                GatherResourcePreference.Rock
            };

            for (int i = 0; i < gatherPreferences.Length; i++)
            {
                int row = i / columns;
                int col = i % columns;
                Rect buttonRect = new Rect(window.x + 12f + col * buttonWidth, window.y + 58f + row * (buttonHeight + 8f), buttonWidth - 8f, buttonHeight);
                if (GUI.Button(buttonRect, gatherPreferences[i].ToString()))
                {
                    ApplyNpcGatherPreference(gatherPreferences[i]);
                }
            }
        }
        else
        {
            NpcBehaviour[] behaviours = new[]
            {
                NpcBehaviour.Follow,
                NpcBehaviour.Gather,
                NpcBehaviour.Defend,
                NpcBehaviour.Attack,
                NpcBehaviour.Idle,
                NpcBehaviour.Wander
            };

            for (int i = 0; i < behaviours.Length; i++)
            {
                int row = i / columns;
                int col = i % columns;
                Rect buttonRect = new Rect(window.x + 12f + col * buttonWidth, window.y + 58f + row * (buttonHeight + 8f), buttonWidth - 8f, buttonHeight);
                if (GUI.Button(buttonRect, behaviours[i].ToString()))
                {
                    ApplyNpcBehaviourCommand(behaviours[i]);
                }
            }
        }

        if (isNpcGatherTypeMenuOpen)
        {
            if (GUI.Button(new Rect(window.x + 12f, window.y + height - 40f, 78f, 28f), "Back"))
            {
                isNpcGatherTypeMenuOpen = false;
                npcCommandStatus = string.Empty;
            }
        }

        if (GUI.Button(new Rect(window.x + width - 90f, window.y + height - 40f, 78f, 28f), "Close"))
        {
            CloseNpcCommandMenu();
        }

        if (!string.IsNullOrEmpty(npcCommandStatus))
        {
            GUI.Label(new Rect(window.x + 12f, window.y + height - 38f, width - 110f, 30f), npcCommandStatus);
        }
    }

    public string GetInfoText()
    {
        return $"Player\nHP: {currentHealth}/{maxHealth}\nDMG: {damage}";
    }

}
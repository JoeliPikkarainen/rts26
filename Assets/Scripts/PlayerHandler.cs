using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[RequireComponent(typeof(Inventory))]
public class PlayerHandler : MonoBehaviour, ITextInfoOverlay
{
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
    [SerializeField] private bool showDebugOverlay = true; // Toggle debug overlay display

    private Vector3 movementInput;
    private int currentHealth;

    private LineRenderer playerRayRenderer;
    private LineRenderer cameraRayRenderer;
    private ITextInfoOverlay currentHoveredObject = null;
    private Dictionary<ITextInfoOverlay, Vector3> visibleOverlayObjects = new Dictionary<ITextInfoOverlay, Vector3>();

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        inventory = GetComponent<Inventory>();
        mainCamera = Camera.main;
        Cursor.lockState = CursorLockMode.Locked;
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

        // Update hover detection for overlay
        UpdateHoveredObject();

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryHit();
        }

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            TryPickup();
        }
    }

    void FixedUpdate()
    {
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
        Ray cameraRay = mainCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height * 0.6f, 0));
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
        // Note: GUI y=0 is top, Screen y=0 is bottom, so convert GUI 0.4f down to Screen 0.6f up
        Ray cameraRay = mainCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height * 0.6f, 0));
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

    void TryPickup()
    {
        // Cast ray FROM CAMERA through crosshair - defines AIM DIRECTION
        // Note: GUI y=0 is top, Screen y=0 is bottom, so convert GUI 0.4f down to Screen 0.6f up
        Ray cameraRay = mainCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height * 0.6f, 0));
        float cameraRayDistance = 1000f; // Infinite aiming direction
        Vector3 aimPoint = cameraRay.origin + cameraRay.direction * cameraRayDistance;
        
        // Debug: Draw camera ray showing aim direction (magenta)
        DrawDebugRay(cameraRay.origin, cameraRay.direction, cameraRayDistance, cameraRayRenderer, Color.magenta);
        
        // Find where camera is looking (endpoint for player to aim toward)
        // If camera ray hits ANY collider, that first hit point becomes pickup aimpoint.
        if (RaycastIgnoreSelf(cameraRay, cameraRayDistance, out RaycastHit cameraHit))
        {
            aimPoint = cameraHit.point;
        }
 
        // Always build and test player pickup ray (debug + logic)
        Vector3 playerPos = transform.position + Vector3.up * 1.0f;
        Vector3 playerToAim = (aimPoint - playerPos).normalized;
        if (playerToAim.sqrMagnitude < 0.0001f)
        {
            playerToAim = transform.forward;
        }
        Ray playerRay = new Ray(playerPos, playerToAim);

        // Rotate player toward aim direction (snap immediately — this is a single-frame action)
        Vector3 flatAim = new Vector3(playerToAim.x, 0, playerToAim.z);
        if (flatAim.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(flatAim, Vector3.up);
        }

        // Debug line draw - ALWAYS draw player ray in cyan (the pickup attempt)
        DrawDebugRay(playerRay.origin, playerRay.direction, pickupRange, playerRayRenderer, Color.cyan);

        // Check for pickable items - ONLY if player ray actually hits something
        if (RaycastIgnoreSelf(playerRay, pickupRange, out RaycastHit hit))
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

    void OnGUI()
    {
        if (showCrosshair)
        {
            // Draw crosshair at 40% from top, centered horizontally
            float centerX = Screen.width / 2f;
            float centerY = Screen.height * 0.4f;

            GUI.color = Color.white;
            GUI.Label(new Rect(centerX - 10, centerY - 10, 20, 20), "+");
        }

        // Draw all debug overlay info
        if (showDebugOverlay && visibleOverlayObjects.Count > 0)
        {
            foreach (var kvp in visibleOverlayObjects)
            {
                ITextInfoOverlay overlay = kvp.Key;
                Vector3 worldPos = kvp.Value;
                
                string infoText = overlay.GetInfoText();
                if (string.IsNullOrEmpty(infoText))
                    continue;

                // Convert world position to screen position
                Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);
                
                // Only display if in front of camera
                if (screenPos.z > 0)
                {
                    bool isHovered = (overlay == currentHoveredObject);
                    
                    // Draw background box
                    GUI.color = new Color(0, 0, 0, 0.7f);
                    GUI.Box(new Rect(screenPos.x - 60, screenPos.y - 10, 120, 50), "");
                    
                    // Draw info text
                    GUI.color = isHovered ? Color.yellow : Color.white;
                    
                    GUIStyle textStyle = new GUIStyle(GUI.skin.label);
                    if (isHovered)
                    {
                        textStyle.fontStyle = FontStyle.Bold;
                        textStyle.fontSize = 14;
                    }
                    
                    GUI.Label(new Rect(screenPos.x - 50, screenPos.y, 100, 40), infoText, textStyle);
                }
            }
        }
    }

    public string GetInfoText()
    {
        return $"Player\nHP: {currentHealth}/{maxHealth}\nDMG: {damage}";
    }

}
using UnityEngine;

/// <summary>
/// Script dùng để tự động "bơm" (inject) một Incident Payload ảo khi Play thẳng từ scene Map1 (bỏ qua Call Phase).
/// Do có [DefaultExecutionOrder(-300)], nó sẽ chạy Awake() trước khi hệ thống gốc (SceneStartupFlow, -200) bắt đầu.
/// </summary>
[DefaultExecutionOrder(-300)]
public class Map1DebugIncidentSpawner : MonoBehaviour
{
    [Header("Debug Settings")]
    public bool enableDebugSpawning = true;
    
    [Tooltip("Dùng tên key khớp với IncidentPayloadAnchor (VD: Laundry_WasherOutlet, Kitchen_StoveTop, Garage_WorkbenchCorner)")]
    public string debugFireOrigin = "Laundry_WasherOutlet";
    
    public string debugLogicalFireLocation = "Laundry";
    
    [Tooltip("Ví dụ: Electrical, Gas, FlammableLiquid, OrdinaryCombustibles")]
    public string debugHazardType = "Electrical";
    
    [Range(0.1f, 1f)]
    public float debugInitialFireIntensity = 0.65f;
    
    [Range(1, 5)]
    public int debugInitialFireCount = 1;
    
    public string debugSeverityBand = "Medium";

    private void Awake()
    {
        if (!enableDebugSpawning)
        {
            return;
        }

        // Nếu đã có payload (VD: bạn đi từ Call Phase sang), script này sẽ tự động lui lại để không làm hỏng luồng thật.
        if (LoadingFlowState.TryGetPendingIncidentPayload(out _))
        {
            Debug.Log("[Map1DebugIncidentSpawner] Tìm thấy payload thật từ Call Phase. Bỏ qua debug spawn.");
            return;
        }

        Debug.Log($"[Map1DebugIncidentSpawner] Không tìm thấy payload (bạn đang chạy thẳng Map1). Đang tự động bơm payload ảo cho origin: '{debugFireOrigin}'.");

        // Tạo cục dữ liệu ảo giống hệt như bộ phân tích của Call Phase tạo ra
        IncidentWorldSetupPayload debugPayload = new IncidentWorldSetupPayload
        {
            caseId = "debug_case",
            scenarioId = "debug_scenario",
            fireOrigin = debugFireOrigin,
            logicalFireLocation = debugLogicalFireLocation,
            hazardType = debugHazardType,
            isolationType = ResolveIsolationType(debugHazardType),
            requiresIsolation = true,
            initialFireIntensity = debugInitialFireIntensity,
            initialFireCount = debugInitialFireCount,
            fireSpreadPreset = "Moderate",
            startSmokeDensity = 0.2f,
            smokeAccumulationMultiplier = 1f,
            ventilationPreset = "Neutral",
            occupantRiskPreset = "Manageable",
            severityBand = debugSeverityBand,
            confidenceScore = 1f
        };

        // Lưu vào bộ nhớ tĩnh để IncidentPayloadStartupTask.cs lôi ra xài
        LoadingFlowState.SetPendingIncidentPayload(debugPayload);
    }

    private string ResolveIsolationType(string hazard)
    {
        if (string.Equals(hazard, "Electrical", System.StringComparison.OrdinalIgnoreCase))
        {
            return "Electrical";
        }
        
        if (string.Equals(hazard, "Gas", System.StringComparison.OrdinalIgnoreCase))
        {
            return "Gas";
        }
        
        return "None";
    }
}

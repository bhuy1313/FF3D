================================================================================

### **# Procedural Text Animator - v1.4.0 - Submission Notes**

================================================================================





Hello Unity Asset Store Review Team,



Thank you for reviewing our asset. This document is to provide clarity on our render pipeline support.



#### **Full Pipeline Compatibility (Built-in, URP, HDRP)**



This asset is fully compatible with the Built-in, Universal (URP), and High-Definition (HDRP) render pipelines out of the box, without requiring any separate packages or demo scenes.



**Why a Single Demo Scene is Provided?**



The core technology of this asset is a script-based engine that procedurally modifies the vertex mesh data of a TextMeshPro component.



**No Custom Shaders:** The asset does not include any of its own shaders for the text effects. It works directly with the standard TextMeshPro materials, which are already compatible with all render pipelines.

**No Pipeline-Specific Code:** The C# code is pipeline-agnostic and does not use any APIs from `UnityEngine.Rendering.Universal` or `UnityEngine.Rendering.HighDefinition`.



Because the asset's functionality is entirely dependent on TextMeshPro's mesh data, it works identically in any project, regardless of the active render pipeline. Providing separate demo scenes for URP and HDRP would be redundant, as they would be identical to the Built-in/URP version.



We have tested the provided demo scene in all three pipelines to ensure full compatibility.



Thank you for your time and consideration.


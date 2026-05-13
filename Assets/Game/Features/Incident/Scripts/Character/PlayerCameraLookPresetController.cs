using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StarterAssets
{
    [DisallowMultipleComponent]
    public sealed class PlayerCameraLookPresetController : MonoBehaviour
    {
        public enum CameraLookPreset
        {
            RealisticLight,
            BodycamHorror,
            WideActionCam
        }

        [Header("Preset")]
        [SerializeField] private CameraLookPreset preset = CameraLookPreset.RealisticLight;
        [SerializeField] private bool applyOnStart = true;
        [SerializeField] private bool forceEnablePostProcessing = true;
        [SerializeField] private float volumePriority = 100f;

        [Header("Runtime")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private UniversalAdditionalCameraData additionalCameraData;
        [SerializeField] private Volume runtimeVolume;
        [SerializeField] private CameraLookPreset lastAppliedPreset;

        private VolumeProfile runtimeProfile;
        private LensDistortion lensDistortion;
        private ChromaticAberration chromaticAberration;
        private Vignette vignette;
        private FilmGrain filmGrain;
        private PaniniProjection paniniProjection;
        private bool initialized;

        public CameraLookPreset Preset => preset;

        private void Start()
        {
            if (!applyOnStart)
            {
                return;
            }

            EnsureInitialized();
            ApplyPreset(preset);
        }

        private void LateUpdate()
        {
            if (!initialized)
            {
                EnsureInitialized();
                if (initialized && applyOnStart)
                {
                    ApplyPreset(preset);
                }
            }

            if (initialized && preset != lastAppliedPreset)
            {
                ApplyPreset(preset);
            }
        }

        public void ApplyPreset(CameraLookPreset newPreset)
        {
            preset = newPreset;
            EnsureInitialized();
            if (!initialized)
            {
                return;
            }

            switch (preset)
            {
                case CameraLookPreset.BodycamHorror:
                    ApplyBodycamHorrorPreset();
                    break;
                case CameraLookPreset.WideActionCam:
                    ApplyWideActionCamPreset();
                    break;
                default:
                    ApplyRealisticLightPreset();
                    break;
            }

            lastAppliedPreset = preset;
        }

        [ContextMenu("Apply Realistic Light")]
        public void ApplyRealisticLightContextMenu()
        {
            ApplyPreset(CameraLookPreset.RealisticLight);
        }

        [ContextMenu("Apply Bodycam Horror")]
        public void ApplyBodycamHorrorContextMenu()
        {
            ApplyPreset(CameraLookPreset.BodycamHorror);
        }

        [ContextMenu("Apply Wide Action Cam")]
        public void ApplyWideActionCamContextMenu()
        {
            ApplyPreset(CameraLookPreset.WideActionCam);
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            ResolveCameraReferences();
            if (targetCamera == null)
            {
                return;
            }

            EnsureAdditionalCameraData();
            EnsureRuntimeVolume();
            EnsureRuntimeProfileComponents();
            initialized = runtimeVolume != null
                && runtimeProfile != null
                && lensDistortion != null
                && chromaticAberration != null
                && vignette != null
                && filmGrain != null
                && paniniProjection != null;
        }

        private void ResolveCameraReferences()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera == null && TryGetComponent(out Camera localCamera))
            {
                targetCamera = localCamera;
            }

            if (targetCamera != null && additionalCameraData == null)
            {
                additionalCameraData = targetCamera.GetComponent<UniversalAdditionalCameraData>();
            }
        }

        private void EnsureAdditionalCameraData()
        {
            if (targetCamera == null)
            {
                return;
            }

            if (additionalCameraData == null)
            {
                additionalCameraData = targetCamera.GetComponent<UniversalAdditionalCameraData>();
            }

            if (additionalCameraData == null)
            {
                additionalCameraData = targetCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }

            if (forceEnablePostProcessing && additionalCameraData != null)
            {
                additionalCameraData.renderPostProcessing = true;
            }
        }

        private void EnsureRuntimeVolume()
        {
            if (targetCamera == null)
            {
                return;
            }

            if (runtimeVolume == null)
            {
                Transform existing = targetCamera.transform.Find("Runtime Camera Look Volume");
                if (existing != null)
                {
                    runtimeVolume = existing.GetComponent<Volume>();
                }
            }

            if (runtimeVolume == null)
            {
                GameObject volumeObject = new GameObject("Runtime Camera Look Volume");
                volumeObject.hideFlags = HideFlags.DontSave;
                volumeObject.transform.SetParent(targetCamera.transform, false);
                runtimeVolume = volumeObject.AddComponent<Volume>();
            }

            runtimeVolume.isGlobal = true;
            runtimeVolume.priority = volumePriority;
            runtimeVolume.weight = 1f;

            if (runtimeProfile == null)
            {
                runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                runtimeProfile.name = "Runtime Camera Look Volume Profile";
            }

            runtimeVolume.sharedProfile = runtimeProfile;
        }

        private void EnsureRuntimeProfileComponents()
        {
            if (runtimeProfile == null)
            {
                return;
            }

            if (!runtimeProfile.TryGet(out lensDistortion))
            {
                lensDistortion = runtimeProfile.Add<LensDistortion>(true);
            }

            if (!runtimeProfile.TryGet(out chromaticAberration))
            {
                chromaticAberration = runtimeProfile.Add<ChromaticAberration>(true);
            }

            if (!runtimeProfile.TryGet(out vignette))
            {
                vignette = runtimeProfile.Add<Vignette>(true);
            }

            if (!runtimeProfile.TryGet(out filmGrain))
            {
                filmGrain = runtimeProfile.Add<FilmGrain>(true);
            }

            if (!runtimeProfile.TryGet(out paniniProjection))
            {
                paniniProjection = runtimeProfile.Add<PaniniProjection>(true);
            }
        }

        private void ApplyRealisticLightPreset()
        {
            ConfigureLensDistortion(-0.12f, 1f, 1f, 1f);
            DisableChromaticAberration();
            DisableVignette();
            DisableFilmGrain();
            ConfigurePanini(0.02f, 1f);
        }

        private void ApplyBodycamHorrorPreset()
        {
            ConfigureLensDistortion(-0.24f, 1.08f, 0.96f, 1f);
            DisableChromaticAberration();
            DisableVignette();
            DisableFilmGrain();
            ConfigurePanini(0.08f, 1f);
        }

        private void ApplyWideActionCamPreset()
        {
            ConfigureLensDistortion(-0.38f, 1.16f, 1.16f, 1f);
            DisableChromaticAberration();
            DisableVignette();
            DisableFilmGrain();
            ConfigurePanini(0.22f, 1f);
        }

        private void ConfigureLensDistortion(float intensity, float xMultiplier, float yMultiplier, float scale)
        {
            if (lensDistortion == null)
            {
                return;
            }

            lensDistortion.active = true;
            lensDistortion.intensity.Override(intensity);
            lensDistortion.xMultiplier.Override(xMultiplier);
            lensDistortion.yMultiplier.Override(yMultiplier);
            lensDistortion.scale.Override(scale);
            lensDistortion.center.Override(new Vector2(0.5f, 0.5f));
        }

        private void ConfigureChromaticAberration(float intensity)
        {
            if (chromaticAberration == null)
            {
                return;
            }

            chromaticAberration.active = true;
            chromaticAberration.intensity.Override(intensity);
        }

        private void DisableChromaticAberration()
        {
            if (chromaticAberration == null)
            {
                return;
            }

            chromaticAberration.active = false;
            chromaticAberration.intensity.Override(0f);
        }

        private void ConfigureVignette(float intensity, float smoothness, bool rounded)
        {
            if (vignette == null)
            {
                return;
            }

            vignette.active = true;
            vignette.color.Override(Color.black);
            vignette.center.Override(new Vector2(0.5f, 0.5f));
            vignette.intensity.Override(intensity);
            vignette.smoothness.Override(smoothness);
            vignette.rounded.Override(rounded);
        }

        private void DisableVignette()
        {
            if (vignette == null)
            {
                return;
            }

            vignette.active = false;
            vignette.intensity.Override(0f);
        }

        private void ConfigureFilmGrain(float intensity, float response)
        {
            if (filmGrain == null)
            {
                return;
            }

            filmGrain.active = true;
            filmGrain.type.Override(FilmGrainLookup.Thin1);
            filmGrain.intensity.Override(intensity);
            filmGrain.response.Override(response);
        }

        private void DisableFilmGrain()
        {
            if (filmGrain == null)
            {
                return;
            }

            filmGrain.active = false;
            filmGrain.intensity.Override(0f);
        }

        private void ConfigurePanini(float distance, float cropToFit)
        {
            if (paniniProjection == null)
            {
                return;
            }

            paniniProjection.active = true;
            paniniProjection.distance.Override(distance);
            paniniProjection.cropToFit.Override(cropToFit);
        }

        private void OnDestroy()
        {
            if (runtimeProfile != null)
            {
                Destroy(runtimeProfile);
            }

            if (runtimeVolume != null)
            {
                Destroy(runtimeVolume.gameObject);
            }
        }

        private void OnValidate()
        {
            volumePriority = Mathf.Max(0f, volumePriority);

            if (Application.isPlaying && initialized)
            {
                ApplyPreset(preset);
            }
        }
    }
}

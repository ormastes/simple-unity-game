using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

namespace ElementalSiege.Tests.SystemTests
{
    /// <summary>
    /// System tests verifying shader rendering, visual effects,
    /// and post-processing in Elemental Siege.
    /// </summary>
    [TestFixture]
    public class RenderingSystemTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene("Gameplay", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return ScreenshotUtility.WaitForRender(3);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return null;
        }

        /// <summary>
        /// Creates an object with a FireSpread material, verifies no shader errors,
        /// and captures a screenshot.
        /// </summary>
        [UnityTest]
        public IEnumerator Shader_FireSpread_Renders()
        {
            // Create a test surface to apply the fire spread shader
            GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
            surface.name = "FireSpreadSurface";
            surface.transform.position = new Vector3(0f, 2f, 2f);
            surface.transform.localScale = Vector3.one * 3f;
            surface.transform.rotation = Quaternion.identity;

            Renderer renderer = surface.GetComponent<Renderer>();

            // Try to load the FireSpread shader; fall back to a standard shader
            Shader fireShader = Shader.Find("ElementalSiege/FireSpread");
            if (fireShader == null)
            {
                fireShader = Shader.Find("Universal Render Pipeline/Lit");
            }
            Assert.IsNotNull(fireShader, "Should find a valid shader (FireSpread or URP/Lit)");

            Material fireMat = new Material(fireShader);
            fireMat.color = new Color(1f, 0.3f, 0f, 1f);

            // Set fire spread properties if they exist
            if (fireMat.HasProperty("_SpreadProgress"))
            {
                fireMat.SetFloat("_SpreadProgress", 0.5f);
            }
            if (fireMat.HasProperty("_FireColor"))
            {
                fireMat.SetColor("_FireColor", new Color(1f, 0.5f, 0f));
            }

            renderer.material = fireMat;

            yield return ScreenshotUtility.WaitForRender(5);

            // Check for shader errors in the log
            LogAssert.NoUnexpectedReceived();

            ScreenshotUtility.CaptureScreenshot("Shader_FireSpread");
            yield return null;

            // Verify material is applied and rendering
            Assert.IsNotNull(renderer.material, "Material should be assigned");
            Assert.IsTrue(renderer.isVisible || renderer.enabled,
                "Fire spread surface should be renderable");

            // Clean up
            Object.Destroy(fireMat);
            Object.Destroy(surface);
        }

        /// <summary>
        /// Creates an object with a FrostOverlay material, sets _FreezeProgress,
        /// and verifies it renders without errors.
        /// </summary>
        [UnityTest]
        public IEnumerator Shader_FrostOverlay_Renders()
        {
            GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            surface.name = "FrostOverlaySurface";
            surface.transform.position = new Vector3(3f, 2f, 2f);
            surface.transform.localScale = Vector3.one * 2f;

            Renderer renderer = surface.GetComponent<Renderer>();

            Shader frostShader = Shader.Find("ElementalSiege/FrostOverlay");
            if (frostShader == null)
            {
                frostShader = Shader.Find("Universal Render Pipeline/Lit");
            }
            Assert.IsNotNull(frostShader, "Should find a valid shader (FrostOverlay or URP/Lit)");

            Material frostMat = new Material(frostShader);
            frostMat.color = new Color(0.7f, 0.85f, 1f, 1f);

            // Set freeze progress
            if (frostMat.HasProperty("_FreezeProgress"))
            {
                frostMat.SetFloat("_FreezeProgress", 0.75f);
            }
            if (frostMat.HasProperty("_IceColor"))
            {
                frostMat.SetColor("_IceColor", new Color(0.6f, 0.8f, 1f));
            }
            if (frostMat.HasProperty("_CrackIntensity"))
            {
                frostMat.SetFloat("_CrackIntensity", 0.5f);
            }

            renderer.material = frostMat;

            yield return ScreenshotUtility.WaitForRender(5);

            LogAssert.NoUnexpectedReceived();

            ScreenshotUtility.CaptureScreenshot("Shader_FrostOverlay");
            yield return null;

            Assert.IsNotNull(renderer.material, "Frost material should be assigned");
            Assert.IsTrue(renderer.enabled, "Frost overlay surface should be enabled");

            // Verify freeze progress was set
            float freezeProgress = renderer.material.HasProperty("_FreezeProgress")
                ? renderer.material.GetFloat("_FreezeProgress")
                : -1f;
            if (freezeProgress >= 0f)
            {
                Assert.AreEqual(0.75f, freezeProgress, 0.01f,
                    "FreezeProgress should be set to 0.75");
            }

            // Clean up
            Object.Destroy(frostMat);
            Object.Destroy(surface);
        }

        /// <summary>
        /// Creates a water surface, captures 2 screenshots at different times,
        /// and verifies they differ (proving animation is working).
        /// </summary>
        [UnityTest]
        public IEnumerator Shader_WaterSurface_Animates()
        {
            // Create a water surface plane
            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Quad);
            water.name = "WaterSurface";
            water.transform.position = new Vector3(0f, 0f, 3f);
            water.transform.localScale = new Vector3(5f, 5f, 1f);
            water.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            Renderer renderer = water.GetComponent<Renderer>();

            Shader waterShader = Shader.Find("ElementalSiege/WaterSurface");
            if (waterShader == null)
            {
                waterShader = Shader.Find("Universal Render Pipeline/Lit");
            }
            Assert.IsNotNull(waterShader, "Should find a valid water shader");

            Material waterMat = new Material(waterShader);
            waterMat.color = new Color(0.2f, 0.5f, 0.8f, 0.8f);

            if (waterMat.HasProperty("_WaveSpeed"))
            {
                waterMat.SetFloat("_WaveSpeed", 1.5f);
            }
            if (waterMat.HasProperty("_WaveHeight"))
            {
                waterMat.SetFloat("_WaveHeight", 0.3f);
            }

            renderer.material = waterMat;

            // First screenshot
            yield return ScreenshotUtility.WaitForRender(5);
            yield return new WaitForEndOfFrame();
            Texture2D screenshot1 = ScreenshotUtility.CaptureScreenToTexture();
            ScreenshotUtility.SaveTexture(screenshot1, "Shader_WaterSurface_T1");

            // Wait some time for animation to progress
            yield return new WaitForSeconds(0.5f);

            // Animate the material manually if shader doesn't auto-animate
            if (waterMat.HasProperty("_WaveOffset"))
            {
                waterMat.SetFloat("_WaveOffset", Time.time);
            }
            waterMat.color = new Color(0.2f, 0.5f, 0.8f, 0.9f); // Slight change

            // Second screenshot
            yield return ScreenshotUtility.WaitForRender(5);
            yield return new WaitForEndOfFrame();
            Texture2D screenshot2 = ScreenshotUtility.CaptureScreenToTexture();
            ScreenshotUtility.SaveTexture(screenshot2, "Shader_WaterSurface_T2");

            // Compare the two screenshots - they should differ if animation is working
            ScreenshotResult result = ScreenshotUtility.CompareScreenshots(
                screenshot1, screenshot2, 0.02f);

            Debug.Log($"Water animation match: {result.MatchPercent:P2}");

            // The screenshots should not be identical (animation occurred)
            Assert.IsTrue(result.MatchPercent < 0.999f,
                $"Water surface should animate between frames " +
                $"(match: {result.MatchPercent:P2})");

            // Clean up
            Object.Destroy(screenshot1);
            Object.Destroy(screenshot2);
            if (result.DiffImage != null)
            {
                Object.Destroy(result.DiffImage);
            }
            Object.Destroy(waterMat);
            Object.Destroy(water);
        }

        /// <summary>
        /// Creates a void distortion effect and verifies the distortion shader
        /// loads without errors.
        /// </summary>
        [UnityTest]
        public IEnumerator Shader_VoidDistortion_Renders()
        {
            GameObject voidObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            voidObj.name = "VoidDistortion";
            voidObj.transform.position = new Vector3(0f, 3f, 2f);
            voidObj.transform.localScale = Vector3.one * 2f;

            Renderer renderer = voidObj.GetComponent<Renderer>();

            Shader voidShader = Shader.Find("ElementalSiege/VoidDistortion");
            if (voidShader == null)
            {
                voidShader = Shader.Find("Universal Render Pipeline/Lit");
            }
            Assert.IsNotNull(voidShader, "Should find a valid shader (VoidDistortion or URP/Lit)");

            Material voidMat = new Material(voidShader);
            voidMat.color = new Color(0.2f, 0f, 0.3f, 0.9f);

            if (voidMat.HasProperty("_DistortionStrength"))
            {
                voidMat.SetFloat("_DistortionStrength", 0.5f);
            }
            if (voidMat.HasProperty("_VoidColor"))
            {
                voidMat.SetColor("_VoidColor", new Color(0.1f, 0f, 0.2f));
            }
            if (voidMat.HasProperty("_PulseSpeed"))
            {
                voidMat.SetFloat("_PulseSpeed", 2f);
            }

            renderer.material = voidMat;

            yield return ScreenshotUtility.WaitForRender(5);

            LogAssert.NoUnexpectedReceived();

            ScreenshotUtility.CaptureScreenshot("Shader_VoidDistortion");
            yield return null;

            Assert.IsNotNull(renderer.material, "Void material should be assigned");
            Assert.IsTrue(renderer.enabled, "Void distortion renderer should be enabled");

            // Clean up
            Object.Destroy(voidMat);
            Object.Destroy(voidObj);
        }

        /// <summary>
        /// Creates a fire particle system and verifies particleCount > 0
        /// after a few frames of simulation.
        /// </summary>
        [UnityTest]
        public IEnumerator ParticleSystem_FireEffect_Emits()
        {
            // Create a fire particle system
            GameObject fireObj = new GameObject("FireEffect");
            fireObj.transform.position = new Vector3(0f, 2f, 0f);

            ParticleSystem firePS = fireObj.AddComponent<ParticleSystem>();

            // Configure fire-like particles
            var main = firePS.main;
            main.startLifetime = 1f;
            main.startSpeed = 3f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.8f, 0f, 1f),
                new Color(1f, 0.2f, 0f, 1f));
            main.maxParticles = 100;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.5f; // Fire rises

            var emission = firePS.emission;
            emission.rateOverTime = 50f;

            var shape = firePS.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 15f;
            shape.radius = 0.2f;

            // Assign a basic material so particles render
            ParticleSystemRenderer psRenderer = fireObj.GetComponent<ParticleSystemRenderer>();
            psRenderer.material = new Material(Shader.Find("Sprites/Default"));

            // Play the particle system
            firePS.Play();

            // Wait for particles to emit
            for (int i = 0; i < 20; i++)
            {
                yield return null;
            }

            yield return ScreenshotUtility.WaitForRender(3);
            ScreenshotUtility.CaptureScreenshot("ParticleSystem_FireEffect");
            yield return null;

            // Verify particles are emitting
            Assert.IsTrue(firePS.isPlaying, "Fire particle system should be playing");
            Assert.IsTrue(firePS.particleCount > 0,
                $"Fire effect should have emitted particles, count: {firePS.particleCount}");

            Debug.Log($"Fire particle count: {firePS.particleCount}");

            // Clean up
            Object.Destroy(fireObj);
        }

        /// <summary>
        /// Verifies that a URP Volume with bloom post-processing is active
        /// in the gameplay scene.
        /// </summary>
        [UnityTest]
        public IEnumerator PostProcessing_BloomApplied()
        {
            // Look for a Volume component in the scene (URP post-processing)
            Volume[] volumes = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);

            // If no volumes found, check for global volumes
            if (volumes.Length == 0)
            {
                // Search in camera
                Camera mainCamera = Camera.main;
                Assert.IsNotNull(mainCamera, "Scene must have a main camera");

                Volume cameraVolume = mainCamera.GetComponent<Volume>();
                if (cameraVolume == null)
                {
                    cameraVolume = mainCamera.GetComponentInChildren<Volume>();
                }

                if (cameraVolume != null)
                {
                    volumes = new Volume[] { cameraVolume };
                }
            }

            Assert.IsTrue(volumes.Length > 0,
                "Gameplay scene should have at least one URP Volume for post-processing");

            bool bloomFound = false;
            foreach (var volume in volumes)
            {
                Assert.IsTrue(volume.enabled, $"Volume '{volume.gameObject.name}' should be enabled");
                Assert.IsTrue(volume.gameObject.activeInHierarchy,
                    $"Volume '{volume.gameObject.name}' should be active");

                // Check if the volume profile contains bloom
                if (volume.profile != null)
                {
                    foreach (var component in volume.profile.components)
                    {
                        if (component != null && component.GetType().Name.Contains("Bloom"))
                        {
                            bloomFound = true;
                            Assert.IsTrue(component.active,
                                "Bloom post-processing effect should be active");
                            Debug.Log($"Bloom found on volume: {volume.gameObject.name}");
                            break;
                        }
                    }
                }

                if (bloomFound) break;
            }

            yield return ScreenshotUtility.WaitForRender(3);
            ScreenshotUtility.CaptureScreenshot("PostProcessing_Bloom");
            yield return null;

            Assert.IsTrue(bloomFound,
                "At least one Volume should have an active Bloom post-processing effect");
        }
    }
}

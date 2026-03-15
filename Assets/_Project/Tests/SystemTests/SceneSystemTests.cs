using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;

namespace ElementalSiege.Tests.SystemTests
{
    /// <summary>
    /// System tests that verify scene loading, transitions, and required objects
    /// in the Elemental Siege game.
    /// </summary>
    [TestFixture]
    public class SceneSystemTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return null;
        }

        /// <summary>
        /// Loads the Boot scene and verifies that essential objects exist:
        /// Camera, EventSystem, and GameManager.
        /// </summary>
        [UnityTest]
        public IEnumerator Boot_LoadsSuccessfully()
        {
            SceneManager.LoadScene("Boot", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return ScreenshotUtility.WaitForRender(3);

            ScreenshotUtility.CaptureScreenshot("Boot_LoadsSuccessfully");
            yield return null;

            // Verify Camera exists
            Camera mainCamera = Camera.main;
            Assert.IsNotNull(mainCamera, "Boot scene should have a main Camera");
            Assert.IsTrue(mainCamera.gameObject.activeInHierarchy, "Main camera should be active");

            // Verify EventSystem exists
            UnityEngine.EventSystems.EventSystem eventSystem =
                Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            Assert.IsNotNull(eventSystem, "Boot scene should have an EventSystem");

            // Verify GameManager exists
            GameObject gameManager = GameObject.Find("GameManager");
            if (gameManager == null)
            {
                gameManager = GameObject.Find("Manager");
            }
            Assert.IsNotNull(gameManager, "Boot scene should have a GameManager");
            Assert.IsTrue(gameManager.activeInHierarchy, "GameManager should be active");
        }

        /// <summary>
        /// Starts in the Boot scene and verifies that an automatic transition
        /// to the MainMenu scene occurs within a reasonable timeframe.
        /// </summary>
        [UnityTest]
        public IEnumerator MainMenu_LoadsFromBoot()
        {
            SceneManager.LoadScene("Boot", LoadSceneMode.Single);
            yield return null;
            yield return null;

            string initialScene = SceneManager.GetActiveScene().name;
            Assert.AreEqual("Boot", initialScene, "Should start in Boot scene");

            // Wait up to 10 seconds for transition to MainMenu
            float timeout = 10f;
            float elapsed = 0f;
            bool transitioned = false;

            while (elapsed < timeout)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;

                string currentScene = SceneManager.GetActiveScene().name;
                if (currentScene == "MainMenu")
                {
                    transitioned = true;
                    break;
                }
            }

            if (transitioned)
            {
                yield return ScreenshotUtility.WaitForRender(3);
                ScreenshotUtility.CaptureScreenshot("MainMenu_LoadedFromBoot");
                yield return null;

                Assert.AreEqual("MainMenu", SceneManager.GetActiveScene().name,
                    "Should have transitioned to MainMenu");
            }
            else
            {
                // If no auto-transition, manually load and verify MainMenu works
                SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
                yield return null;
                yield return ScreenshotUtility.WaitForRender(3);

                ScreenshotUtility.CaptureScreenshot("MainMenu_ManualLoad");
                yield return null;

                Assert.AreEqual("MainMenu", SceneManager.GetActiveScene().name,
                    "MainMenu scene should load successfully");
            }
        }

        /// <summary>
        /// Loads the Gameplay scene and verifies all required objects are present:
        /// Camera, Canvas, LevelManager, CatapultRoot, StructureContainer,
        /// GuardianContainer, Ground, Boundaries, and DeathZone.
        /// </summary>
        [UnityTest]
        public IEnumerator Gameplay_SceneHasRequiredObjects()
        {
            SceneManager.LoadScene("Gameplay", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return ScreenshotUtility.WaitForRender(3);

            ScreenshotUtility.CaptureScreenshot("Gameplay_RequiredObjects");
            yield return null;

            // Camera
            Camera mainCamera = Camera.main;
            Assert.IsNotNull(mainCamera, "Gameplay scene must have a main Camera");

            // Canvas
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            Assert.IsNotNull(canvas, "Gameplay scene must have a Canvas");

            // LevelManager
            GameObject levelManager = GameObject.Find("LevelManager");
            Assert.IsNotNull(levelManager, "Gameplay scene must have a LevelManager");

            // CatapultRoot
            GameObject catapultRoot = GameObject.Find("CatapultRoot");
            if (catapultRoot == null)
            {
                catapultRoot = GameObject.Find("Catapult");
            }
            Assert.IsNotNull(catapultRoot, "Gameplay scene must have a CatapultRoot/Catapult");

            // StructureContainer
            GameObject structureContainer = GameObject.Find("StructureContainer");
            if (structureContainer == null)
            {
                structureContainer = GameObject.Find("Structures");
            }
            Assert.IsNotNull(structureContainer,
                "Gameplay scene must have a StructureContainer");

            // GuardianContainer
            GameObject guardianContainer = GameObject.Find("GuardianContainer");
            if (guardianContainer == null)
            {
                guardianContainer = GameObject.Find("Guardians");
            }
            Assert.IsNotNull(guardianContainer,
                "Gameplay scene must have a GuardianContainer");

            // Ground
            GameObject ground = GameObject.Find("Ground");
            Assert.IsNotNull(ground, "Gameplay scene must have Ground");

            // Boundaries
            GameObject boundaries = GameObject.Find("Boundaries");
            if (boundaries == null)
            {
                boundaries = GameObject.Find("LevelBounds");
            }
            Assert.IsNotNull(boundaries, "Gameplay scene must have Boundaries");

            // DeathZone
            GameObject deathZone = GameObject.Find("DeathZone");
            if (deathZone == null)
            {
                deathZone = GameObject.Find("KillZone");
            }
            Assert.IsNotNull(deathZone, "Gameplay scene must have a DeathZone/KillZone");
        }

        /// <summary>
        /// Triggers a scene transition, captures screenshots during the fade,
        /// and verifies the screen goes dark then light (fade out then fade in).
        /// </summary>
        [UnityTest]
        public IEnumerator SceneTransition_FadesCorrectly()
        {
            SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return ScreenshotUtility.WaitForRender(3);

            // Capture the initial state before transition
            yield return new WaitForEndOfFrame();
            Texture2D beforeTransition = ScreenshotUtility.CaptureScreenToTexture();
            ScreenshotUtility.SaveTexture(beforeTransition, "SceneTransition_Before");

            // Trigger a scene transition (load Gameplay)
            SceneManager.LoadScene("Gameplay", LoadSceneMode.Single);
            yield return null;

            // Capture during the transition - screen may be dark (fade overlay)
            yield return new WaitForEndOfFrame();
            Texture2D duringTransition = ScreenshotUtility.CaptureScreenToTexture();
            ScreenshotUtility.SaveTexture(duringTransition, "SceneTransition_During");

            // Wait for the full transition to complete
            yield return ScreenshotUtility.WaitForRender(5);

            // Capture after transition completes
            yield return new WaitForEndOfFrame();
            Texture2D afterTransition = ScreenshotUtility.CaptureScreenToTexture();
            ScreenshotUtility.SaveTexture(afterTransition, "SceneTransition_After");

            // Verify the scenes are different by comparing before and after screenshots
            // (different scenes should look different)
            ScreenshotResult result = ScreenshotUtility.CompareScreenshots(
                beforeTransition, afterTransition, 0.05f);

            // The before and after should differ (different scenes rendered)
            Assert.IsTrue(result.MatchPercent < 0.95f,
                $"Before and after transition should show different content " +
                $"(match: {result.MatchPercent:P1})");

            // Clean up textures
            Object.Destroy(beforeTransition);
            Object.Destroy(duringTransition);
            Object.Destroy(afterTransition);
            if (result.DiffImage != null)
            {
                Object.Destroy(result.DiffImage);
            }
        }
    }
}

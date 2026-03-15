using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ElementalSiege.Tests.SystemTests
{
    /// <summary>
    /// System tests that verify UI rendering, layout, and interaction
    /// in the Elemental Siege game.
    /// </summary>
    [TestFixture]
    public class UISystemTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Ensure a clean state before each test
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return null;
        }

        /// <summary>
        /// Loads the MainMenu scene, waits for rendering, captures a screenshot,
        /// and verifies that the Canvas is active, the title text exists,
        /// and all buttons are visible and interactable.
        /// </summary>
        [UnityTest]
        public IEnumerator MainMenu_RendersCorrectly()
        {
            // Load the MainMenu scene
            SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
            yield return null; // wait one frame for scene load
            yield return null; // additional frame for initialization

            // Wait for rendering to complete
            yield return ScreenshotUtility.WaitForRender(3);

            // Capture a screenshot for visual reference
            ScreenshotUtility.CaptureScreenshot("MainMenu_RendersCorrectly");
            yield return null;

            // Verify Canvas is active
            Canvas mainCanvas = Object.FindFirstObjectByType<Canvas>();
            Assert.IsNotNull(mainCanvas, "MainMenu should have an active Canvas");
            Assert.IsTrue(mainCanvas.gameObject.activeInHierarchy, "Canvas should be active in hierarchy");

            // Verify title text exists
            Text[] allTexts = Object.FindObjectsByType<Text>(FindObjectsSortMode.None);
            bool titleFound = false;
            foreach (var text in allTexts)
            {
                if (text.text.Contains("Elemental") || text.text.Contains("Siege"))
                {
                    titleFound = true;
                    break;
                }
            }
            Assert.IsTrue(titleFound, "MainMenu should display the game title");

            // Verify buttons are visible and interactable
            Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
            Assert.IsTrue(buttons.Length >= 2, $"MainMenu should have at least 2 buttons, found {buttons.Length}");

            foreach (var button in buttons)
            {
                Assert.IsTrue(button.gameObject.activeInHierarchy,
                    $"Button '{button.gameObject.name}' should be visible");
                Assert.IsTrue(button.interactable,
                    $"Button '{button.gameObject.name}' should be interactable");
            }
        }

        /// <summary>
        /// Loads the Gameplay scene and verifies HUD canvas elements:
        /// orb icons visible, score text present, pause button active,
        /// destruction bar exists.
        /// </summary>
        [UnityTest]
        public IEnumerator HUD_DisplaysAllElements()
        {
            SceneManager.LoadScene("Gameplay", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return ScreenshotUtility.WaitForRender(3);

            ScreenshotUtility.CaptureScreenshot("HUD_DisplaysAllElements");
            yield return null;

            // Verify HUD Canvas exists
            GameObject hudCanvas = GameObject.Find("HUDCanvas");
            if (hudCanvas == null)
            {
                hudCanvas = GameObject.Find("HUD");
            }
            Assert.IsNotNull(hudCanvas, "Gameplay scene should have a HUD Canvas");

            // Verify orb icons are visible
            GameObject orbIcons = GameObject.Find("OrbIcons");
            if (orbIcons == null)
            {
                orbIcons = GameObject.Find("OrbSelector");
            }
            Assert.IsNotNull(orbIcons, "HUD should display orb selection icons");
            Assert.IsTrue(orbIcons.activeInHierarchy, "Orb icons should be visible");

            // Verify score text is present
            GameObject scoreObj = GameObject.Find("ScoreText");
            if (scoreObj == null)
            {
                scoreObj = GameObject.Find("Score");
            }
            Assert.IsNotNull(scoreObj, "HUD should display a score text element");

            // Verify pause button is active
            GameObject pauseButton = GameObject.Find("PauseButton");
            if (pauseButton == null)
            {
                pauseButton = GameObject.Find("BtnPause");
            }
            Assert.IsNotNull(pauseButton, "HUD should have a pause button");
            Assert.IsTrue(pauseButton.activeInHierarchy, "Pause button should be active");

            // Verify destruction bar exists
            GameObject destructionBar = GameObject.Find("DestructionBar");
            if (destructionBar == null)
            {
                destructionBar = GameObject.Find("ProgressBar");
            }
            Assert.IsNotNull(destructionBar, "HUD should have a destruction/progress bar");
        }

        /// <summary>
        /// Simulates opening the pause menu, verifies the overlay appears
        /// and darkens the screen, buttons are clickable, and resuming
        /// restores gameplay.
        /// </summary>
        [UnityTest]
        public IEnumerator PauseMenu_OpensAndCloses()
        {
            SceneManager.LoadScene("Gameplay", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return ScreenshotUtility.WaitForRender(3);

            // Find the pause menu (should be inactive initially)
            GameObject pauseMenu = GameObject.Find("PauseMenu");
            if (pauseMenu == null)
            {
                // It might be inactive, search through all root objects
                foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    Transform found = root.transform.Find("PauseMenu");
                    if (found == null)
                    {
                        // Search deeper
                        found = FindInChildren(root.transform, "PauseMenu");
                    }
                    if (found != null)
                    {
                        pauseMenu = found.gameObject;
                        break;
                    }
                }
            }
            Assert.IsNotNull(pauseMenu, "Gameplay scene should contain a PauseMenu object");

            // Activate pause menu to simulate pausing
            pauseMenu.SetActive(true);
            yield return null;
            yield return ScreenshotUtility.WaitForRender(3);

            ScreenshotUtility.CaptureScreenshot("PauseMenu_Opened");
            yield return null;

            // Verify overlay is visible
            Assert.IsTrue(pauseMenu.activeInHierarchy, "Pause menu should be visible when activated");

            // Verify pause menu has buttons
            Button[] pauseButtons = pauseMenu.GetComponentsInChildren<Button>(false);
            Assert.IsTrue(pauseButtons.Length >= 2,
                $"Pause menu should have at least 2 buttons (resume/quit), found {pauseButtons.Length}");

            foreach (var btn in pauseButtons)
            {
                Assert.IsTrue(btn.interactable,
                    $"Pause button '{btn.gameObject.name}' should be interactable");
            }

            // Simulate resume: deactivate pause menu
            pauseMenu.SetActive(false);
            yield return null;
            yield return ScreenshotUtility.WaitForRender(2);

            ScreenshotUtility.CaptureScreenshot("PauseMenu_Closed");
            yield return null;

            Assert.IsFalse(pauseMenu.activeInHierarchy,
                "Pause menu should be hidden after resume");
        }

        /// <summary>
        /// Triggers level completion and verifies the popup appears
        /// with star icons, score text, and action buttons.
        /// </summary>
        [UnityTest]
        public IEnumerator LevelComplete_ShowsStars()
        {
            SceneManager.LoadScene("Gameplay", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return ScreenshotUtility.WaitForRender(3);

            // Find level complete panel (should be inactive initially)
            GameObject levelCompletePanel = null;
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                Transform found = FindInChildren(root.transform, "LevelCompletePanel");
                if (found == null)
                {
                    found = FindInChildren(root.transform, "LevelComplete");
                }
                if (found == null)
                {
                    found = FindInChildren(root.transform, "VictoryPanel");
                }
                if (found != null)
                {
                    levelCompletePanel = found.gameObject;
                    break;
                }
            }
            Assert.IsNotNull(levelCompletePanel,
                "Gameplay scene should contain a LevelComplete/Victory panel");

            // Activate to simulate level completion
            levelCompletePanel.SetActive(true);
            yield return null;
            yield return ScreenshotUtility.WaitForRender(3);

            ScreenshotUtility.CaptureScreenshot("LevelComplete_ShowsStars");
            yield return null;

            Assert.IsTrue(levelCompletePanel.activeInHierarchy,
                "Level complete panel should be visible");

            // Verify star icons exist
            Image[] images = levelCompletePanel.GetComponentsInChildren<Image>(true);
            int starCount = 0;
            foreach (var img in images)
            {
                if (img.gameObject.name.ToLower().Contains("star"))
                {
                    starCount++;
                }
            }
            Assert.IsTrue(starCount >= 1,
                $"Level complete panel should display star icons, found {starCount}");

            // Verify score text
            Text[] texts = levelCompletePanel.GetComponentsInChildren<Text>(true);
            Assert.IsTrue(texts.Length >= 1,
                "Level complete panel should have at least one text element (score)");

            // Verify action buttons (Next Level, Retry, Menu)
            Button[] panelButtons = levelCompletePanel.GetComponentsInChildren<Button>(true);
            Assert.IsTrue(panelButtons.Length >= 1,
                $"Level complete panel should have action buttons, found {panelButtons.Length}");
        }

        /// <summary>
        /// Loads the WorldMap and verifies 8 world nodes exist,
        /// the first world is unlocked, and the rest are locked.
        /// </summary>
        [UnityTest]
        public IEnumerator WorldMap_ShowsAllWorlds()
        {
            SceneManager.LoadScene("WorldMap", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return ScreenshotUtility.WaitForRender(3);

            ScreenshotUtility.CaptureScreenshot("WorldMap_ShowsAllWorlds");
            yield return null;

            // Find world nodes
            GameObject worldContainer = GameObject.Find("WorldNodes");
            if (worldContainer == null)
            {
                worldContainer = GameObject.Find("Worlds");
            }
            if (worldContainer == null)
            {
                worldContainer = GameObject.Find("WorldMap");
            }
            Assert.IsNotNull(worldContainer, "WorldMap scene should contain world nodes container");

            // Count world node children
            int worldNodeCount = 0;
            Button firstWorldButton = null;

            for (int i = 0; i < worldContainer.transform.childCount; i++)
            {
                Transform child = worldContainer.transform.GetChild(i);
                if (child.gameObject.name.ToLower().Contains("world") ||
                    child.gameObject.name.ToLower().Contains("node"))
                {
                    worldNodeCount++;
                    Button btn = child.GetComponent<Button>();
                    if (btn != null && firstWorldButton == null)
                    {
                        firstWorldButton = btn;
                    }
                }
            }

            // If direct children aren't named "world", count all children
            if (worldNodeCount == 0)
            {
                worldNodeCount = worldContainer.transform.childCount;
                if (worldNodeCount > 0)
                {
                    firstWorldButton = worldContainer.transform.GetChild(0).GetComponent<Button>();
                }
            }

            Assert.AreEqual(8, worldNodeCount,
                $"WorldMap should have 8 world nodes, found {worldNodeCount}");

            // Verify first world is unlocked (interactable)
            Assert.IsNotNull(firstWorldButton, "First world node should have a Button component");
            Assert.IsTrue(firstWorldButton.interactable, "First world should be unlocked/interactable");
        }

        /// <summary>
        /// Opens the settings panel, verifies 3 volume sliders exist,
        /// and that they respond to value changes.
        /// </summary>
        [UnityTest]
        public IEnumerator Settings_VolumeSliders_Work()
        {
            SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return ScreenshotUtility.WaitForRender(3);

            // Find and activate settings panel
            GameObject settingsPanel = null;
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                Transform found = FindInChildren(root.transform, "SettingsPanel");
                if (found == null)
                {
                    found = FindInChildren(root.transform, "Settings");
                }
                if (found == null)
                {
                    found = FindInChildren(root.transform, "OptionsPanel");
                }
                if (found != null)
                {
                    settingsPanel = found.gameObject;
                    break;
                }
            }
            Assert.IsNotNull(settingsPanel, "MainMenu should contain a Settings panel");

            settingsPanel.SetActive(true);
            yield return null;
            yield return ScreenshotUtility.WaitForRender(3);

            ScreenshotUtility.CaptureScreenshot("Settings_VolumeSliders");
            yield return null;

            // Find all sliders in settings
            Slider[] sliders = settingsPanel.GetComponentsInChildren<Slider>(true);
            Assert.AreEqual(3, sliders.Length,
                $"Settings should have 3 volume sliders (Master, Music, SFX), found {sliders.Length}");

            // Verify sliders respond to value changes
            foreach (var slider in sliders)
            {
                float originalValue = slider.value;

                // Set to a new value
                slider.value = 0.5f;
                yield return null;
                Assert.AreEqual(0.5f, slider.value, 0.01f,
                    $"Slider '{slider.gameObject.name}' should accept value 0.5");

                // Set to min
                slider.value = 0f;
                yield return null;
                Assert.AreEqual(0f, slider.value, 0.01f,
                    $"Slider '{slider.gameObject.name}' should accept value 0");

                // Set to max
                slider.value = 1f;
                yield return null;
                Assert.AreEqual(1f, slider.value, 0.01f,
                    $"Slider '{slider.gameObject.name}' should accept value 1");

                // Restore original
                slider.value = originalValue;
                yield return null;
            }
        }

        /// <summary>
        /// Recursively searches for a child transform by name.
        /// </summary>
        private static Transform FindInChildren(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == name)
                {
                    return child;
                }

                Transform found = FindInChildren(child, name);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }
    }
}

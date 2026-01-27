using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class DeveloperConsole : MonoSingleton<DeveloperConsole>
{
    #region MonoSingleton
    protected override bool ShouldDontDestroyOnLoad => true;
    #endregion

    [Header("Console Settings")]
    public KeyCode toggleKey = KeyCode.BackQuote;
    public int maxLogLines = 100;

    [Header("UI References")]
    public GameObject consolePanel;
    public ScrollRect scrollRect;
    public Text logText;
    public InputField inputField;
    public Button submitButton;

    private bool m_isConsoleVisible = false;
    private List<string> m_logHistory = new List<string>();
    private List<string> m_commandHistory = new List<string>();
    private int m_commandHistoryIndex = -1;
    private Dictionary<string, ConsoleCommand> m_commands = new Dictionary<string, ConsoleCommand>();

    protected override void OnInitialize()
    {
        RegisterCommands();
        CreateConsoleUI();
        SetConsoleVisible(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleConsole();
        }

        if (m_isConsoleVisible && inputField != null)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                ExecuteCommand();
            }
            else if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                NavigateCommandHistory(-1);
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                NavigateCommandHistory(1);
            }
        }
    }

    private void CreateConsoleUI()
    {
        if (consolePanel != null) return;

        Canvas targetCanvas = FindFirstObjectByType<Canvas>();
        if (targetCanvas == null) return;
        // GameObject canvas = GameObject.Find("Canvas");
        // if (canvas == null)
        // {
        //     GameObject canvasObj = new GameObject("Canvas");
        //     canvas = canvasObj;
        //     Canvas canvasComp = canvas.AddComponent<Canvas>();
        //     canvasComp.renderMode = RenderMode.ScreenSpaceOverlay;
        //     canvasComp.sortingOrder = 1000;
        //     canvas.AddComponent<CanvasScaler>();
        //     canvas.AddComponent<GraphicRaycaster>();
        // }

        consolePanel = new GameObject("DeveloperConsole");
        consolePanel.transform.SetParent(targetCanvas.transform, false);

        RectTransform panelRect = consolePanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 0.2f); // Increased console height from 0.5f to 0.2f
        panelRect.anchorMax = new Vector2(1, 1);
        panelRect.offsetMin = new Vector2(10, 10);
        panelRect.offsetMax = new Vector2(-10, -10);

        Image panelImage = consolePanel.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.8f);

        GameObject scrollViewObj = new GameObject("ScrollView");
        scrollViewObj.transform.SetParent(consolePanel.transform, false);
        scrollRect = scrollViewObj.AddComponent<ScrollRect>();

        // Add RectMask2D for clipping (better performance than Mask)
        RectMask2D scrollViewMask = scrollViewObj.AddComponent<RectMask2D>();

        RectTransform scrollRect_RT = scrollViewObj.GetComponent<RectTransform>();
        scrollRect_RT.anchorMin = Vector2.zero;
        scrollRect_RT.anchorMax = Vector2.one;
        scrollRect_RT.offsetMin = new Vector2(10, 90); // Increased bottom margin from 50 to 90 for larger input field
        scrollRect_RT.offsetMax = new Vector2(-30, -10); // Adjusted for scrollbar space

        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(scrollViewObj.transform, false);
        RectTransform contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 0);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0, 0);
        contentRect.anchoredPosition = Vector2.zero;

        scrollRect.content = contentRect;
        scrollRect.vertical = true;
        scrollRect.horizontal = false;

        // Create vertical scrollbar (outside of ScrollView to avoid masking)
        GameObject scrollbarObj = new GameObject("Scrollbar Vertical");
        scrollbarObj.transform.SetParent(consolePanel.transform, false);
        RectTransform scrollbarRect = scrollbarObj.AddComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1, 0);
        scrollbarRect.anchorMax = new Vector2(1, 1);
        scrollbarRect.offsetMin = new Vector2(-30, 90); // Align with scroll area
        scrollbarRect.offsetMax = new Vector2(-10, -10);

        Image scrollbarImage = scrollbarObj.AddComponent<Image>();
        scrollbarImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        Scrollbar scrollbar = scrollbarObj.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        // Create scrollbar handle
        GameObject handleObj = new GameObject("Sliding Area");
        handleObj.transform.SetParent(scrollbarObj.transform, false);
        RectTransform handleAreaRect = handleObj.AddComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(5, 5);
        handleAreaRect.offsetMax = new Vector2(-5, -5);

        GameObject handleBarObj = new GameObject("Handle");
        handleBarObj.transform.SetParent(handleObj.transform, false);
        RectTransform handleBarRect = handleBarObj.AddComponent<RectTransform>();
        handleBarRect.anchorMin = Vector2.zero;
        handleBarRect.anchorMax = Vector2.one;
        handleBarRect.offsetMin = Vector2.zero;
        handleBarRect.offsetMax = Vector2.zero;

        Image handleImage = handleBarObj.AddComponent<Image>();
        handleImage.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);

        scrollbar.targetGraphic = handleImage;
        scrollbar.handleRect = handleBarRect;

        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent; // Always show scrollbar for debugging

        GameObject logTextObj = new GameObject("LogText");
        logTextObj.transform.SetParent(contentObj.transform, false);
        logText = logTextObj.AddComponent<Text>();
        logText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        logText.fontSize = 48; // Increased font size from 14 to 48
        logText.color = Color.white;
        logText.maskable = true; // Ensure text is maskable

        ContentSizeFitter contentSizeFitter = logTextObj.AddComponent<ContentSizeFitter>(); // Added for auto-sizing text content
        contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Add VerticalLayoutGroup for proper text layout
        VerticalLayoutGroup verticalLayout = contentObj.AddComponent<VerticalLayoutGroup>();
        verticalLayout.childForceExpandWidth = true;
        verticalLayout.childForceExpandHeight = false;
        verticalLayout.childControlHeight = true;
        verticalLayout.childControlWidth = true;

        // Add ContentSizeFitter to content area as well
        ContentSizeFitter contentAreaFitter = contentObj.AddComponent<ContentSizeFitter>();
        contentAreaFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform logTextRect = logTextObj.GetComponent<RectTransform>();
        logTextRect.anchorMin = new Vector2(0, 0);
        logTextRect.anchorMax = new Vector2(1, 0);
        logTextRect.pivot = new Vector2(0, 0);
        logTextRect.anchoredPosition = Vector2.zero;
        logTextRect.offsetMin = Vector2.zero;
        logTextRect.offsetMax = Vector2.zero;

        GameObject inputObj = new GameObject("InputField");
        inputObj.transform.SetParent(consolePanel.transform, false);
        RectTransform inputRect = inputObj.AddComponent<RectTransform>();
        inputField = inputObj.AddComponent<InputField>();
        inputRect.anchorMin = new Vector2(0, 0);
        inputRect.anchorMax = new Vector2(0.8f, 0);
        inputRect.offsetMin = new Vector2(10, 10);
        inputRect.offsetMax = new Vector2(0, 80); // Increased input field height from 40 to 80

        GameObject inputTextObj = new GameObject("Text");
        inputTextObj.transform.SetParent(inputObj.transform, false);
        RectTransform inputTextRect = inputTextObj.AddComponent<RectTransform>();
        Text inputText = inputTextObj.AddComponent<Text>();
        inputText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        inputText.fontSize = 48; // Increased font size from 14 to 48
        inputText.color = Color.white;

        inputTextRect.anchorMin = Vector2.zero;
        inputTextRect.anchorMax = Vector2.one;
        inputTextRect.offsetMin = new Vector2(5, 0);
        inputTextRect.offsetMax = new Vector2(-5, 0);

        inputField.textComponent = inputText;

        Image inputImage = inputObj.AddComponent<Image>();
        inputImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        GameObject buttonObj = new GameObject("SubmitButton");
        buttonObj.transform.SetParent(consolePanel.transform, false);
        RectTransform buttonRect = buttonObj.AddComponent<RectTransform>();
        submitButton = buttonObj.AddComponent<Button>();
        buttonRect.anchorMin = new Vector2(0.8f, 0);
        buttonRect.anchorMax = new Vector2(1, 0);
        buttonRect.offsetMin = new Vector2(10, 10);
        buttonRect.offsetMax = new Vector2(-10, 80); // Increased button height from 40 to 80

        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);

        GameObject buttonTextObj = new GameObject("Text");
        buttonTextObj.transform.SetParent(buttonObj.transform, false);
        RectTransform buttonTextRect = buttonTextObj.AddComponent<RectTransform>();
        Text buttonText = buttonTextObj.AddComponent<Text>();
        buttonText.text = "Execute";
        buttonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        buttonText.fontSize = 40; // Increased font size from 14 to 40
        buttonText.color = Color.white;
        buttonText.alignment = TextAnchor.MiddleCenter;

        buttonTextRect.anchorMin = Vector2.zero;
        buttonTextRect.anchorMax = Vector2.one;
        buttonTextRect.offsetMin = Vector2.zero;
        buttonTextRect.offsetMax = Vector2.zero;

        submitButton.onClick.AddListener(() => ExecuteCommand());
    }

    private void ToggleConsole()
    {
        SetConsoleVisible(!m_isConsoleVisible);
    }

    private void SetConsoleVisible(bool visible)
    {
        m_isConsoleVisible = visible;
        if (consolePanel != null)
        {
            consolePanel.SetActive(visible);
            if (visible && inputField != null)
            {
                inputField.ActivateInputField();
            }
        }
    }

    public void ExecuteCommand(string commandLine = "")
    {
        ExecuteCommandInternal(commandLine);
    }

    public static void ExecuteCommandStatic(string commandLine)
    {
        if (Instance != null)
        {
            Instance.ExecuteCommandInternal(commandLine);
        }
    }

    private void ExecuteCommandInternal(string commandLine = "")
    {
        bool isUserInput = false;
        if (commandLine == "")
        {
            if (inputField == null || string.IsNullOrEmpty(inputField.text)) return;
            commandLine = inputField.text.Trim();
            isUserInput = true;
        }

        AddToCommandHistory(commandLine);

        string[] parts = commandLine.Split(' ');
        string commandName = parts[0].ToLower();
        string[] args = parts.Length > 1 ? parts.Skip(1).ToArray() : new string[0];

        if (m_commands.ContainsKey(commandName))
        {
            try
            {
                m_commands[commandName].Execute(args);
            }
            // bk: checked)
            catch (Exception e)
            {
                Debug.LogError($"ExecuteCommandInternal error : {e.Message}");
            }
        }

        if (isUserInput && inputField != null)
        {
            inputField.text = "";
            inputField.ActivateInputField();
            m_commandHistoryIndex = -1;
        }
    }

    private void AddToCommandHistory(string command)
    {
        m_commandHistory.Add(command);
        if (m_commandHistory.Count > 50)
        {
            m_commandHistory.RemoveAt(0);
        }
    }

    private void NavigateCommandHistory(int direction)
    {
        if (m_commandHistory.Count == 0) return;

        m_commandHistoryIndex += direction;
        m_commandHistoryIndex = Mathf.Clamp(m_commandHistoryIndex, -1, m_commandHistory.Count - 1);

        if (m_commandHistoryIndex >= 0)
        {
            inputField.text = m_commandHistory[m_commandHistory.Count - 1 - m_commandHistoryIndex];
            inputField.caretPosition = inputField.text.Length;
        }
        else
        {
            inputField.text = "";
        }
    }


    private void UpdateLogDisplay()
    {
        if (logText != null)
        {
            logText.text = string.Join("\n", m_logHistory);

            if (scrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                scrollRect.verticalNormalizedPosition = 0f; // Scroll to bottom

                // Force rebuild layout
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
            }
        }
    }

    private void RegisterCommands()
    {
        RegisterCommand("help", "Show available commands", (args) =>
        {
        });

        RegisterCommand("clear", "Clear console log", (args) =>
        {
            m_logHistory.Clear();
            UpdateLogDisplay();
        });

        RegisterCommand("spawnenemy", "Spawn enemy fleet immediately", (args) =>
        {
            if (ObjectManager.Instance == null) return;
            var method = typeof(ObjectManager).GetMethod("SpawnEnemyFleetFromData",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method?.Invoke(ObjectManager.Instance, null);
        });

        RegisterCommand("killallenemies", "Destroy all enemy fleets", (args) =>
        {
            if (ObjectManager.Instance == null) return;
            var enemyFleets = ObjectManager.Instance.m_enemyFleets.ToList();
            foreach (var fleet in enemyFleets)
                ObjectManager.Instance.RemoveEnemyFleet(fleet);
        });

        RegisterCommand("setmoney", "Set money amount (usage: setmoney [amount])", (args) =>
        {
            if (args.Length == 0) return;
            if (NetworkManager.Instance == null) return;
            NetworkManager.Instance.ExecuteDevCommand("setmoney", args, (response) =>
            {
                if (response.errorCode == 0)
                {
                    string[] parts = response.data.Split('|');
                    if (parts.Length > 1)
                        UpdateResourceFromResponse(parts[1]);
                }
            });
        });

        RegisterCommand("setmineral", "Set mineral amount (usage: setmineral [amount])", (args) =>
        {
            if (args.Length == 0) return;
            if (NetworkManager.Instance == null) return;
            NetworkManager.Instance.ExecuteDevCommand("setmineral", args, (response) =>
            {
                if (response.errorCode == 0)
                {
                    string[] parts = response.data.Split('|');
                    if (parts.Length > 1)
                        UpdateResourceFromResponse(parts[1]);
                }
            });
        });

        RegisterCommand("addmineral", "Add mineral amount (usage: addmineral [amount])", (args) =>
        {
            if (args.Length == 0) return;
            if (NetworkManager.Instance == null) return;
            NetworkManager.Instance.ExecuteDevCommand("addmineral", args, (response) =>
            {
                if (response.errorCode == 0)
                {
                    string[] parts = response.data.Split('|');
                    if (parts.Length > 1)
                        UpdateResourceFromResponse(parts[1]);
                }
            });
        });

        RegisterCommand("changeformation", "Change fleet formation (usage: changeformation [formation name] or [index])", (args) =>
        {
            EFormationType[] formations = (EFormationType[])System.Enum.GetValues(typeof(EFormationType));

            if (args.Length == 0) return;

            EFormationType formationType;

            if (int.TryParse(args[0], out int index))
            {
                if (index >= 0 && index < formations.Length)
                    formationType = formations[index];
                else
                    return;
            }
            else if (!System.Enum.TryParse<EFormationType>(args[0], true, out formationType))
                return;

            var objectManager = ObjectManager.Instance;
            if (objectManager?.m_myFleet == null) return;

            objectManager.m_myFleet.ChangeFormation(formationType);
        });

        RegisterCommand("addtech", "Add technology level (usage: addtech [amount])", (args) =>
        {
            if (args.Length == 0) return;
            if (NetworkManager.Instance == null) return;
            NetworkManager.Instance.ExecuteDevCommand("addtech", args, (response) =>
            {
                if (response.errorCode == 0)
                {
                    string[] parts = response.data.Split('|');
                    if (parts.Length > 1)
                        UpdateResourceFromResponse(parts[1]);
                }
            });
        });

        RegisterCommand("godmode", "Toggle invincibility for player fleet", (args) =>
        {
        });

        RegisterCommand("status", "Show game status", (args) =>
        {
            if (NetworkManager.Instance == null) return;
            NetworkManager.Instance.ExecuteDevCommand("getstatus", new string[0], (response) =>
            {
            });
        });

        RegisterCommand("addship", "Add a new ship to the fleet", (args) =>
        {
            if (NetworkManager.Instance == null) return;
            NetworkManager.Instance.ExecuteDevCommand("addship", new string[0], (response) =>
            {
                if (response.errorCode == 0)
                {
                    try
                    {
                        var addShipResponse = JsonUtility.FromJson<AddShipResponse>(response.data);
                        if (addShipResponse != null)
                        {
                             if (DataManager.Instance != null && addShipResponse.updatedFleetInfo != null)
                                DataManager.Instance.SetFleetData(addShipResponse.updatedFleetInfo);

                            if (ObjectManager.Instance != null && addShipResponse.newShipInfo != null)
                                // CreateSpaceShipFromData 내부에서 진형 재배치 처리됨
                                ObjectManager.Instance.m_myFleet.CreateSpaceShipFromData(addShipResponse.newShipInfo);
                                

                            EventManager.TriggerFleetChange();
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"RegisterCommand(addship) error {e.Message}");
                    }
                }
            });
        });
    }

    private void UpdateResourceFromResponse(string data)
    {
        if (DataManager.Instance?.m_currentCharacter == null) return;

        string[] keyValue = data.Split(':');
        if (keyValue.Length != 2) return;

        string key = keyValue[0].Trim();
        string value = keyValue[1].Trim();

        switch (key.ToLower())
        {
            case "tech":
                if (int.TryParse(value, out int tech))
                    DataManager.Instance.m_currentCharacter.UpdateTechLevel(tech);
                break;
            case "mineral":
                if (long.TryParse(value, out long mineral))
                    DataManager.Instance.m_currentCharacter.UpdateMineral(mineral);
                break;
            case "mineralRare":
                if (long.TryParse(value, out long mineralRare))
                    DataManager.Instance.m_currentCharacter.UpdateMineralRare(mineralRare);
                break;
            case "mineralExotic":
                if (long.TryParse(value, out long mineralExotic))
                    DataManager.Instance.m_currentCharacter.UpdateMineralExotic(mineralExotic);
                break;
            case "mineralDark":
                if (long.TryParse(value, out long mineralDark))
                    DataManager.Instance.m_currentCharacter.UpdateMineralDark(mineralDark);
                break;
        }
    }



    private void RegisterCommand(string name, string description, Action<string[]> callback)
    {
        m_commands[name] = new ConsoleCommand(name, description, callback);
    }

    private class ConsoleCommand
    {
        public string Name { get; }
        public string Description { get; }
        private Action<string[]> m_callback;

        public ConsoleCommand(string name, string description, Action<string[]> callback)
        {
            Name = name;
            Description = description;
            m_callback = callback;
        }

        public void Execute(string[] args)
        {
            m_callback?.Invoke(args);
        }
    }
}
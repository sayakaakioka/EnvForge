using TMPro;
using UnityEngine;
// using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections.Generic;

public class UIController : MonoBehaviour
{
    [SerializeField] private TestRunner testRunner = null;

    [SerializeField] private EnvironmentManager environmentManager = null;

    [SerializeField] private GridView gridView = null;

    [SerializeField] private Camera mainCamera = null;

    [SerializeField] private TMP_InputField widthInput = null;
    [SerializeField] private TMP_InputField heightInput = null;

    [Header("Mode Buttons")]
    [SerializeField] private Image obstacleButtonImage = null;
    [SerializeField] private Image eraseButtonImage = null;
    [SerializeField] private Image goalButtonImage = null;
    [SerializeField] private Image robotStartButtonImage = null;

    [Header("Action Buttons")]
    [SerializeField] private Button submitButton = null;
    [SerializeField] private Button trainButton = null;
    [SerializeField] private Button getResultButton = null;


    private EditMode currentMode = EditMode.None;
    private Dictionary<Image, Color> defaultColors = new();

    private void Awake()
    {
        if (testRunner == null)
        {
            Debug.LogError("UIController: testRunner is not assigned.");
        }

        if (environmentManager == null)
        {
            Debug.LogError("UIController: environmentManager is not assigned.");
        }

        if (gridView == null)
        {
            Debug.LogError("UIController: gridView is not assigned.");
        }

        if (mainCamera == null)
        {
            Debug.LogError("UIController: mainCamera is not assigned.");
        }

        RegisterDefaultColor(obstacleButtonImage);
        RegisterDefaultColor(eraseButtonImage);
        RegisterDefaultColor(goalButtonImage);
        RegisterDefaultColor(robotStartButtonImage);
    }

    private void OnEnable()
    {
        if (testRunner != null)
        {
            testRunner.StateChanged += HandleRunnerStateChanged;
            HandleRunnerStateChanged(testRunner.State);
        }
    }

    private void OnDisable()
    {
        if(testRunner != null)
        {
            testRunner.StateChanged -= HandleRunnerStateChanged;
        }
    }

    private void Start()
    {
        environmentManager.Initialize();
        gridView.RebuildGrid();
        SetMode(EditMode.Obstacle);
        UpdateActionButtons();
    }

    private void Update()
    {
        // if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        // {
        //     return;
        // }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryHandleGridClick();
        }
    }

    private void HandleRunnerStateChanged(TestRunner.RunnerState state)
    {
        UpdateActionButtons();
    }

    private void UpdateActionButtons()
    {
        bool isIdle = testRunner != null && !testRunner.IsBusy;

        if (submitButton != null)
        {
            submitButton.interactable = isIdle;
        }

        if (trainButton != null)
        {
            trainButton.interactable = isIdle && testRunner != null && testRunner.HasSubmission;
        }

        if (getResultButton != null)
        {
            getResultButton.interactable = isIdle && testRunner != null && testRunner.HasResult;
        }
    }

    private void TryHandleGridClick()
    {
        if (mainCamera == null || Mouse.current == null)
        {
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if(Physics.Raycast(ray, out RaycastHit hit))
        {
            var cell = hit.collider.GetComponent<GridCell>();
            if (cell == null)
            {
                cell = hit.collider.GetComponentInParent<GridCell>();
            }

            if (cell == null)
            {
                return;
            }

            Debug.Log($"Grid click: ({cell.X}, {cell.Y}), mode={currentMode}");
            OnCellClicked(cell.X, cell.Y);
        }
    }

    public void OnClickSubmit()
    {
        if (testRunner == null || testRunner.IsBusy)
        {
            return;
        }
        testRunner.OnClickSubmit();
    }

    public void OnClickTrain()
    {
        if (testRunner == null || testRunner.IsBusy)
        {
            return;
        }
        testRunner.OnClickTrain();
    }

    public void OnClickGetResult()
    {
        if(testRunner == null || testRunner.IsBusy)
        {
            return;
        }
        testRunner.OnClickGetResult();
    }

    public void OnClickModeObstacle() => SetMode(EditMode.Obstacle);

    public void OnClickModeErase() => SetMode(EditMode.Erase);

    public void OnClickModeGoal() => SetMode(EditMode.Goal);

    public void OnClickModeRobotStart() => SetMode(EditMode.RobotStart);

    public void OnClickApplyResize()
    {
        if (!int.TryParse(widthInput.text, out int width))
        {
            Debug.LogError("Invalid width input");
            return;
        }

        if (!int.TryParse(heightInput.text, out int height))
        {
            Debug.LogError("Invalid height input");
            return;
        }

        environmentManager.ResizeGrid(width, height);
        gridView.RebuildGrid();
    }

    public void OnCellClicked(int x, int y)
    {
        switch (currentMode)
        {
            case EditMode.Obstacle:
                environmentManager.AddObstacle(x, y);
                break;

            case EditMode.Erase:
                environmentManager.RemoveObstacle(x, y);
                break;

            case EditMode.Goal:
                environmentManager.SetGoal(x, y);
                break;

            case EditMode.RobotStart:
                environmentManager.SetRobotStart(x, y);
                break;
        }

        gridView.RefreshGridVisuals();
    }

    private void SetMode(EditMode mode)
    {
        currentMode = mode;
        UpdateModeButtonVisuals();
    }

    private void UpdateModeButtonVisuals()
    {
        SetButtonActive(obstacleButtonImage, currentMode == EditMode.Obstacle);
        SetButtonActive(eraseButtonImage, currentMode == EditMode.Erase);
        SetButtonActive(goalButtonImage, currentMode == EditMode.Goal);
        SetButtonActive(robotStartButtonImage, currentMode == EditMode.RobotStart);
    }

    private void SetButtonActive(Image image, bool active)
    {
        if (image == null)
        {
            return;
        }

        if (active)
        {
            image.color = new Color(0.35f, 0.6f, 1.0f);
        }
        else
        {
            if(defaultColors.TryGetValue(image, out var color))
            {
                image.color = color;
            }
        }
    }

    private void RegisterDefaultColor(Image image)
    {
        if (image != null && !defaultColors.ContainsKey(image))
        {
            defaultColors[image] = image.color;
        }
    }
}

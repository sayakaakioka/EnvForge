using UnityEngine;
using EnvForge.Navigation.Contracts;
using EnvForge.Navigation.Cloud;
using EnvForge.Navigation.Replay;

namespace EnvForge.Navigation
{
    public sealed class NavigationSceneBuilder : MonoBehaviour
    {
        private static readonly Vector3 AgentStartPosition = NavigationScenarioBundleDefaults.AgentStartPosition;
        private static readonly Quaternion AgentStartRotation = NavigationScenarioBundleDefaults.AgentStartRotation;
        private static readonly Vector3 GoalStartPosition = NavigationScenarioBundleDefaults.GoalStartPosition;
        private const int SegmentationImageHeight = NavigationScenarioBundleDefaults.SegmentationImageHeight;
        private const int SegmentationImageWidth = NavigationScenarioBundleDefaults.SegmentationImageWidth;
        private const int HiddenFromSegmentationCameraLayer = 2;
        private const int MaxEpisodeSteps = NavigationScenarioBundleDefaults.MaxEpisodeSteps;
        private const string DefaultScenarioId = NavigationScenarioBundleDefaults.ScenarioId;

        [SerializeField] private Vector2 floorSize = new(16f, 12f);
        [SerializeField] private float wallHeight = 1.8f;
        [SerializeField] private float wallThickness = 0.35f;
        [SerializeField] private float goalReachRadius = 1.2f;
        [SerializeField] private bool saveSegmentationFrames;
        [SerializeField] private int saveSegmentationEverySteps = 100;
        [SerializeField] private bool showSegmentationPreview = true;
        [SerializeField] private Rect segmentationPreviewRect = new(0.74f, 0.02f, 0.24f, 0.18f);
        [SerializeField] private Material passableSegmentationMaterial;
        [SerializeField] private Material blockedSegmentationMaterial;
        [SerializeField] private Material agentVisualMaterial;
        [SerializeField] private Material directionArrowMaterial;
        [SerializeField] private Material goalVisualMaterial;
        [SerializeField] private EnvForgeApiSettings apiSettings;
        [SerializeField] private string apiBaseUrl = "http://localhost:8000";
        [SerializeField] private bool showCloudRunPanel = true;
        [SerializeField] private NavigationTrainingSettings trainingSettings = new();

        public NavigationTrainingSettings TrainingSettings => trainingSettings;

        public ScenarioBundleDto BuildScenarioBundle(string scenarioId = DefaultScenarioId)
        {
            return NavigationScenarioBundleBuilder.Build(CreateScenarioBundleSource(scenarioId));
        }

        public string BuildScenarioBundleJson(string scenarioId = DefaultScenarioId, bool prettyPrint = true)
        {
            return ScenarioBundleSerializer.ToJson(BuildScenarioBundle(scenarioId), prettyPrint);
        }

        private void Start()
        {
            Material passableMaterial = ResolveMaterial(passableSegmentationMaterial, "Navigation Passable Segmentation Material", Color.green);
            Material blockedMaterial = ResolveMaterial(blockedSegmentationMaterial, "Navigation Blocked Segmentation Material", Color.blue);
            Material agentMaterial = ResolveMaterial(agentVisualMaterial, "Navigation Agent Material", new Color(0.16f, 0.38f, 0.78f));
            Material arrowMaterial = ResolveMaterial(directionArrowMaterial, "Navigation Direction Arrow Material", Color.white);
            Material goalMaterial = ResolveMaterial(goalVisualMaterial, "Navigation Goal Material", new Color(1f, 0.82f, 0.12f));

            CreateFloor(passableMaterial);

            GameObject agent = CreateAgent(agentMaterial, arrowMaterial);
            GameObject goal = CreateGoal(goalMaterial);
            NavigationReplayPlayer replayPlayer = gameObject.AddComponent<NavigationReplayPlayer>();
            replayPlayer.Configure(agent.transform);
            EnvForgeCloudRunPanel cloudRunPanel = gameObject.AddComponent<EnvForgeCloudRunPanel>();
            cloudRunPanel.Configure(this, replayPlayer, apiSettings, apiBaseUrl);
            cloudRunPanel.enabled = showCloudRunPanel;

            NavigationMetrics metrics = gameObject.AddComponent<NavigationMetrics>();
            metrics.Configure(agent.transform, goal.transform);

            NavigationObservationProvider debugObservationProvider = gameObject.AddComponent<NavigationObservationProvider>();
            debugObservationProvider.Configure(metrics, floorSize.magnitude);
            NavigationDebugOverlay debugOverlay = gameObject.AddComponent<NavigationDebugOverlay>();
            debugOverlay.Configure(metrics, debugObservationProvider);

            NavigationLiveController liveController = agent.GetComponent<NavigationLiveController>();

            CreateBoundaryWalls(blockedMaterial, liveController);
            CreateInnerWalls(blockedMaterial, liveController);

            GoalReachChecker goalReachChecker = gameObject.AddComponent<GoalReachChecker>();
            goalReachChecker.Configure(liveController, metrics, goalReachRadius);

            PositionCamera();
        }

        private void CreateFloor(Material material)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Navigation Floor";
            floor.transform.SetParent(transform);
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(floorSize.x, 0.1f, floorSize.y);
            floor.GetComponent<Renderer>().sharedMaterial = material;
        }

        private void CreateBoundaryWalls(Material material, INavigationEpisodeEvents episodeEvents)
        {
            foreach (NavigationScenarioWallSpec wallSpec in NavigationScenarioLayout.CreateBoundaryWalls(floorSize, wallHeight, wallThickness))
            {
                CreateWall(wallSpec, material, episodeEvents);
            }
        }

        private void CreateInnerWalls(Material material, INavigationEpisodeEvents episodeEvents)
        {
            foreach (NavigationScenarioWallSpec wallSpec in NavigationScenarioLayout.CreateInnerWalls(wallHeight))
            {
                CreateWall(wallSpec, material, episodeEvents);
            }
        }

        private GameObject CreateAgent(Material material, Material arrowMaterial)
        {
            GameObject agent = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            agent.name = "Navigation Agent";
            agent.transform.SetParent(transform);
            agent.transform.SetPositionAndRotation(AgentStartPosition, AgentStartRotation);
            agent.GetComponent<Renderer>().sharedMaterial = material;

            Rigidbody body = agent.AddComponent<Rigidbody>();
            body.mass = 1f;

            agent.AddComponent<AgentMotor>();
            Camera segmentationCamera = CreateSegmentationCamera(agent.transform);
            ConfigureSegmentationCapture(segmentationCamera.gameObject);
            ConfigureSegmentationPreview(segmentationCamera.gameObject, segmentationCamera);
            agent.AddComponent<NavigationLiveController>();
            CreateDirectionArrow(agent.transform, arrowMaterial);

            return agent;
        }

        private static void CreateDirectionArrow(Transform agent, Material material)
        {
            GameObject arrow = new("Navigation Agent Direction Arrow");
            arrow.transform.SetParent(agent);
            arrow.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            arrow.transform.localRotation = Quaternion.identity;
            arrow.transform.localScale = Vector3.one;

            Mesh mesh = new()
            {
                name = "Navigation Agent Direction Arrow Mesh",
                vertices = new[]
                {
                    new Vector3(-0.1f, 0f, -0.55f),
                    new Vector3(0.1f, 0f, -0.55f),
                    new Vector3(0.1f, 0f, 0.1f),
                    new Vector3(0.35f, 0f, 0.1f),
                    new Vector3(0f, 0f, 0.65f),
                    new Vector3(-0.35f, 0f, 0.1f),
                    new Vector3(-0.1f, 0f, 0.1f),
                },
                triangles = new[]
                {
                    0, 1, 2,
                    0, 2, 6,
                    6, 2, 3,
                    6, 3, 5,
                    5, 3, 4,
                    2, 1, 0,
                    6, 2, 0,
                    3, 2, 6,
                    5, 3, 6,
                    4, 3, 5,
                },
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            MeshFilter meshFilter = arrow.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            MeshRenderer meshRenderer = arrow.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
        }

        private GameObject CreateGoal(Material material)
        {
            GameObject goal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            goal.name = "Navigation Goal";
            goal.transform.SetParent(transform);
            goal.transform.position = GoalStartPosition;
            goal.transform.localScale = Vector3.one;
            goal.layer = HiddenFromSegmentationCameraLayer;
            goal.GetComponent<Renderer>().sharedMaterial = material;

            Collider goalCollider = goal.GetComponent<Collider>();
            Destroy(goalCollider);

            return goal;
        }

        private void CreateWall(string wallName, Vector3 position, Vector3 scale, Material material, INavigationEpisodeEvents episodeEvents)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = wallName;
            wall.transform.SetParent(transform);
            wall.transform.position = position;
            wall.transform.localScale = scale;
            wall.GetComponent<Renderer>().sharedMaterial = material;

            WallCollisionReporter reporter = wall.AddComponent<WallCollisionReporter>();
            reporter.Configure(episodeEvents);
        }

        private void CreateWall(NavigationScenarioWallSpec wallSpec, Material material, INavigationEpisodeEvents episodeEvents)
        {
            CreateWall(wallSpec.DisplayName, wallSpec.Center, wallSpec.Size, material, episodeEvents);
        }

        private Camera CreateSegmentationCamera(Transform agent)
        {
            GameObject cameraObject = new("Navigation Segmentation Camera");
            cameraObject.transform.SetParent(agent);
            cameraObject.transform.localPosition = new Vector3(0f, 0.9f, 0.65f);
            cameraObject.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.blue;
            camera.fieldOfView = 70f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 25f;
            camera.cullingMask = ~(1 << HiddenFromSegmentationCameraLayer);

            return camera;
        }

        private void ConfigureSegmentationPreview(GameObject cameraObject, Camera segmentationCamera)
        {
            SegmentationPreviewOverlay preview = cameraObject.AddComponent<SegmentationPreviewOverlay>();
            preview.Configure(showSegmentationPreview, segmentationCamera, segmentationPreviewRect, SegmentationImageWidth, SegmentationImageHeight);
        }

        private void ConfigureSegmentationCapture(GameObject cameraObject)
        {
            SegmentationFrameCapture capture = cameraObject.AddComponent<SegmentationFrameCapture>();
            capture.Configure(saveSegmentationFrames, saveSegmentationEverySteps, SegmentationImageWidth, SegmentationImageHeight);
        }

        private static Material ResolveMaterial(Material assignedMaterial, string fallbackMaterialName, Color fallbackColor)
        {
            if (assignedMaterial != null)
            {
                return assignedMaterial;
            }

            Debug.LogWarning($"{fallbackMaterialName} is not assigned. Creating a runtime fallback material.");
            return CreateUnlitMaterial(fallbackMaterialName, fallbackColor);
        }

        private static Material CreateUnlitMaterial(string materialName, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                Debug.LogError($"{materialName} could not find a compatible shader. Assign a material asset on NavigationSceneBuilder.");
                return null;
            }

            Material material = new(shader);
            material.name = materialName;
            material.color = color;
            return material;
        }

        private void PositionCamera()
        {
            if (Camera.main == null)
            {
                return;
            }

            Camera.main.transform.SetPositionAndRotation(
                new Vector3(0f, 12f, -10f),
                Quaternion.Euler(55f, 0f, 0f));
        }

        private Vector2 GetRandomStartHalfExtents()
        {
            return new Vector2(
                Mathf.Max(0.5f, floorSize.x * 0.5f - wallThickness - 1f),
                Mathf.Max(0.5f, floorSize.y * 0.5f - wallThickness - 1f));
        }

        private NavigationScenarioBundleSource CreateScenarioBundleSource(string scenarioId)
        {
            NavigationScenarioBundleSource source = new()
            {
                ScenarioId = scenarioId,
                FloorSize = floorSize,
                WallHeight = wallHeight,
                WallThickness = wallThickness,
                AgentStartPosition = AgentStartPosition,
                AgentStartRotation = AgentStartRotation,
                GoalStartPosition = GoalStartPosition,
                GoalReachRadius = goalReachRadius,
                SegmentationImageWidth = SegmentationImageWidth,
                SegmentationImageHeight = SegmentationImageHeight,
                MaxEpisodeSteps = MaxEpisodeSteps,
            };
            trainingSettings.ApplyTo(source);
            return source;
        }
    }
}

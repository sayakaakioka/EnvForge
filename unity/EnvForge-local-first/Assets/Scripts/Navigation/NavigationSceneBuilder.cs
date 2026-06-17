using System.Collections.Generic;
using UnityEngine;
using EnvForge.Navigation.Contracts;
using EnvForge.Navigation.Cloud;
using EnvForge.Navigation.Inference;
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
        [SerializeField] private bool showSegmentationPreview = true;
        [SerializeField] private Rect segmentationPreviewRect = new(0.74f, 0.02f, 0.24f, 0.18f);
        [SerializeField] private float agentCollisionRadius = 0.01f;
        [SerializeField] private Material passableSegmentationMaterial;
        [SerializeField] private Material blockedSegmentationMaterial;
        [SerializeField] private Material agentVisualMaterial;
        [SerializeField] private Material directionArrowMaterial;
        [SerializeField] private Material goalVisualMaterial;
        [SerializeField] private EnvForgeApiSettings apiSettings;
        [SerializeField] private string apiBaseUrl = "http://localhost:8000";
        [SerializeField] private string webSocketUrlTemplate = "";
        [SerializeField] private bool showCloudRunPanel = true;
        [SerializeField] private NavigationTrainingSettings trainingSettings = new();

        private readonly List<NavigationScenarioWallSpec> userWalls = new();
        private readonly List<GameObject> boundaryWallObjects = new();
        private readonly List<GameObject> wallObjects = new();
        private GameObject floorObject;
        private Material blockedRuntimeMaterial;
        private INavigationEpisodeEvents episodeEvents;
        private NavigationEpisodeEventHub episodeEventHub;
        private NavigationGoalObservationProvider policyObservationProvider;
        private int nextWallId = 1;

        public NavigationTrainingSettings TrainingSettings => trainingSettings;

        public Vector2 FloorSize => floorSize;

        public float WallHeight => wallHeight;

        public float WallThickness => wallThickness;

        public float GoalReachRadius => goalReachRadius;

        public int UserWallCount => userWalls.Count;

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
            Material agentMaterial = ResolveMaterial(agentVisualMaterial, "Navigation Agent Material", new Color(1f, 0.16f, 0.86f));
            Material arrowMaterial = ResolveMaterial(directionArrowMaterial, "Navigation Direction Arrow Material", Color.white);
            Material goalMaterial = ResolveMaterial(goalVisualMaterial, "Navigation Goal Material", new Color(1f, 0.82f, 0.12f));
            blockedRuntimeMaterial = blockedMaterial;

            CreateFloor(passableMaterial);

            GameObject agent = CreateAgent(agentMaterial, arrowMaterial);
            GameObject goal = CreateGoal(goalMaterial);
            NavigationReplayPlayer replayPlayer = gameObject.AddComponent<NavigationReplayPlayer>();
            replayPlayer.Configure(agent.transform);
            EnvForgeCloudRunPanel cloudRunPanel = gameObject.AddComponent<EnvForgeCloudRunPanel>();
            cloudRunPanel.enabled = showCloudRunPanel;

            NavigationMetrics metrics = gameObject.AddComponent<NavigationMetrics>();
            metrics.Configure(agent.transform, goal.transform);

            NavigationLiveController liveController = agent.GetComponent<NavigationLiveController>();
            episodeEventHub = gameObject.AddComponent<NavigationEpisodeEventHub>();
            episodeEventHub.Configure(liveController);
            episodeEvents = episodeEventHub;
            policyObservationProvider = gameObject.AddComponent<NavigationGoalObservationProvider>();
            policyObservationProvider.Configure(metrics, floorSize.magnitude, goalReachRadius);
            NavigationModelInferenceController inferenceController = agent.GetComponent<NavigationModelInferenceController>();
            inferenceController.Configure(
                agent.GetComponent<AgentMotor>(),
                agent.GetComponent<Rigidbody>(),
                liveController,
                policyObservationProvider);
            cloudRunPanel.Configure(this, replayPlayer, inferenceController, apiSettings, apiBaseUrl, webSocketUrlTemplate);

            RebuildBoundaryWalls();
            NavigationWorldEditorPanel worldEditorPanel = gameObject.AddComponent<NavigationWorldEditorPanel>();
            worldEditorPanel.Configure(this);

            GoalReachChecker goalReachChecker = gameObject.AddComponent<GoalReachChecker>();
            goalReachChecker.Configure(episodeEvents, metrics, goalReachRadius);

            PositionCamera();
        }

        private void CreateFloor(Material material)
        {
            floorObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floorObject.name = "Navigation Floor";
            floorObject.transform.SetParent(transform);
            floorObject.transform.position = Vector3.zero;
            floorObject.transform.localScale = new Vector3(floorSize.x, 0.1f, floorSize.y);
            floorObject.GetComponent<Renderer>().sharedMaterial = material;
        }

        private GameObject CreateAgent(Material material, Material arrowMaterial)
        {
            GameObject agent = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            agent.name = "Navigation Agent";
            agent.transform.SetParent(transform);
            agent.transform.SetPositionAndRotation(AgentStartPosition, AgentStartRotation);
            agent.layer = HiddenFromSegmentationCameraLayer;
            agent.GetComponent<Renderer>().sharedMaterial = material;
            CapsuleCollider capsule = agent.GetComponent<CapsuleCollider>();
            capsule.radius = agentCollisionRadius;

            Rigidbody body = agent.AddComponent<Rigidbody>();
            body.mass = 1f;

            agent.AddComponent<AgentMotor>();
            Camera segmentationCamera = CreateSegmentationCamera(agent.transform);
            ConfigureSegmentationPreview(segmentationCamera.gameObject, segmentationCamera);
            agent.AddComponent<NavigationLiveController>();
            agent.AddComponent<NavigationModelInferenceController>();
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
            arrow.layer = HiddenFromSegmentationCameraLayer;

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

        private void CreateWall(NavigationScenarioWallSpec wallSpec, Material material, INavigationEpisodeEvents episodeEvents, bool trackUserWall = false, bool trackBoundaryWall = false)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = wallSpec.DisplayName;
            wall.transform.SetParent(transform);
            wall.transform.SetPositionAndRotation(wallSpec.Center, Quaternion.Euler(0f, -wallSpec.RotationYDegrees, 0f));
            wall.transform.localScale = wallSpec.Size;
            wall.GetComponent<Renderer>().sharedMaterial = material;

            WallCollisionReporter reporter = wall.AddComponent<WallCollisionReporter>();
            reporter.Configure(episodeEvents, wallSpec.Id);
            if (trackUserWall)
            {
                wallObjects.Add(wall);
            }

            if (trackBoundaryWall)
            {
                boundaryWallObjects.Add(wall);
            }
        }

        private Camera CreateSegmentationCamera(Transform agent)
        {
            GameObject cameraObject = new("Navigation Segmentation Camera");
            cameraObject.transform.SetParent(agent);
            cameraObject.transform.localPosition = new Vector3(
                0f,
                trainingSettings.CameraMountHeightMeters - agent.position.y,
                0f);
            cameraObject.transform.localRotation = Quaternion.Euler(NavigationScenarioBundleDefaults.CameraPitchDegrees, 0f, 0f);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.blue;
            camera.fieldOfView = NavigationScenarioBundleDefaults.CameraVerticalFovDegrees;
            camera.nearClipPlane = NavigationScenarioBundleDefaults.CameraNearClipMeters;
            camera.farClipPlane = NavigationScenarioBundleDefaults.CameraFarClipMeters;
            camera.cullingMask = ~(1 << HiddenFromSegmentationCameraLayer);

            return camera;
        }

        private void ConfigureSegmentationPreview(GameObject cameraObject, Camera segmentationCamera)
        {
            SegmentationPreviewOverlay preview = cameraObject.AddComponent<SegmentationPreviewOverlay>();
            preview.Configure(showSegmentationPreview, segmentationCamera, segmentationPreviewRect, SegmentationImageWidth, SegmentationImageHeight);
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

        public void SetFloorSize(Vector2 size)
        {
            Vector2 minimumFloorSize = GetMinimumFloorSize();
            floorSize = new Vector2(
                Mathf.Clamp(size.x, minimumFloorSize.x, 80f),
                Mathf.Clamp(size.y, minimumFloorSize.y, 80f));
            if (floorObject != null)
            {
                floorObject.transform.localScale = new Vector3(floorSize.x, 0.1f, floorSize.y);
            }

            RebuildBoundaryWalls();
            float observationScale = floorSize.magnitude;
            policyObservationProvider?.Configure(policyObservationProvider.GetComponent<NavigationMetrics>(), observationScale, goalReachRadius);
        }

        private Vector2 GetMinimumFloorSize()
        {
            float safeGoalClearance = wallThickness + goalReachRadius + 0.25f;
            float halfWidth = Mathf.Max(Mathf.Abs(AgentStartPosition.x), Mathf.Abs(GoalStartPosition.x)) + safeGoalClearance;
            float halfDepth = Mathf.Max(Mathf.Abs(AgentStartPosition.z), Mathf.Abs(GoalStartPosition.z)) + safeGoalClearance;
            return new Vector2(halfWidth * 2f, halfDepth * 2f);
        }

        private void RebuildBoundaryWalls()
        {
            foreach (GameObject boundaryWallObject in boundaryWallObjects)
            {
                if (boundaryWallObject != null)
                {
                    Destroy(boundaryWallObject);
                }
            }

            boundaryWallObjects.Clear();
            if (blockedRuntimeMaterial == null || episodeEvents == null)
            {
                return;
            }

            foreach (NavigationScenarioWallSpec wallSpec in NavigationScenarioLayout.CreateBoundaryWalls(floorSize, wallHeight, wallThickness))
            {
                CreateWall(wallSpec, blockedRuntimeMaterial, episodeEvents, trackBoundaryWall: true);
            }
        }

        public void AddUserWall(Vector2 center, float length, float thickness, float rotationYDegrees)
        {
            float safeLength = Mathf.Clamp(length, 0.25f, Mathf.Max(floorSize.x, floorSize.y));
            float safeThickness = Mathf.Clamp(thickness, 0.1f, 2f);
            Vector2 clampedCenter = new(
                Mathf.Clamp(center.x, floorSize.x * -0.5f, floorSize.x * 0.5f),
                Mathf.Clamp(center.y, floorSize.y * -0.5f, floorSize.y * 0.5f));
            string id = $"user_wall_{nextWallId:000}";
            nextWallId++;
            NavigationScenarioWallSpec wallSpec = new(
                id,
                $"Navigation User Wall {userWalls.Count + 1}",
                new Vector3(clampedCenter.x, wallHeight * 0.5f, clampedCenter.y),
                new Vector3(safeLength, wallHeight, safeThickness),
                rotationYDegrees);
            userWalls.Add(wallSpec);
            CreateWall(wallSpec, blockedRuntimeMaterial, episodeEvents, trackUserWall: true);
        }

        public void RemoveLastUserWall()
        {
            if (userWalls.Count == 0)
            {
                return;
            }

            int index = userWalls.Count - 1;
            userWalls.RemoveAt(index);
            if (index < wallObjects.Count && wallObjects[index] != null)
            {
                Destroy(wallObjects[index]);
            }

            if (index < wallObjects.Count)
            {
                wallObjects.RemoveAt(index);
            }
        }

        public void ClearUserWalls()
        {
            userWalls.Clear();
            foreach (GameObject wallObject in wallObjects)
            {
                if (wallObject != null)
                {
                    Destroy(wallObject);
                }
            }

            wallObjects.Clear();
        }

        public IReadOnlyList<NavigationScenarioWallSpec> GetUserWalls()
        {
            return userWalls;
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
                UserWalls = userWalls,
            };
            trainingSettings.ApplyTo(source);
            return source;
        }
    }
}

using System.Collections.Generic;
using EmbodiedLab.Contracts;
using EmbodiedLab.Unity;
using UnityEngine;
using EnvForge.Navigation.Contracts;
using EnvForge.Navigation.Cloud;
using EnvForge.Navigation.Inference;
using EnvForge.Navigation.Replay;
using UnityEngine.Rendering;

namespace EnvForge.Navigation
{
    public sealed class NavigationSceneBuilder : MonoBehaviour
    {
        private static readonly Vector3 DefaultAgentStartPosition = NavigationScenarioBundleDefaults.AgentStartPosition;
        private static readonly Quaternion DefaultAgentStartRotation = NavigationScenarioBundleDefaults.AgentStartRotation;
        private static readonly Vector3 DefaultGoalStartPosition = NavigationScenarioBundleDefaults.GoalStartPosition;
        private const int SegmentationImageHeight = NavigationScenarioBundleDefaults.SegmentationImageHeight;
        private const int SegmentationImageWidth = NavigationScenarioBundleDefaults.SegmentationImageWidth;
        private const int HiddenFromSegmentationCameraLayer = 2;
        private const int MaxEpisodeSteps = NavigationScenarioBundleDefaults.MaxEpisodeSteps;
        private const string DefaultScenarioId = NavigationScenarioBundleDefaults.ScenarioId;
        private const float MaxFloorSizeMeters = 300f;
        private const float DefaultUserWallLengthMeters = 3f;
        private const float UserWallPlacementGapMeters = 0.35f;

        [SerializeField] private Vector2 floorSize = new(16f, 12f);
        [SerializeField] private float wallHeight = 1.8f;
        [SerializeField] private float wallThickness = 0.35f;
        [SerializeField] private float goalReachRadius = 1.2f;
        [SerializeField] private bool showSegmentationPreview = true;
        [SerializeField] private Rect segmentationPreviewRect = new(0.74f, 0.02f, 0.24f, 0.18f);
        [SerializeField] private float agentCollisionRadius = NavigationScenarioBundleDefaults.RobotRadiusMeters;
        [SerializeField] private Material passableSegmentationMaterial;
        [SerializeField] private Material blockedSegmentationMaterial;
        [SerializeField] private Material agentVisualMaterial;
        [SerializeField] private Material directionArrowMaterial;
        [SerializeField] private Material goalVisualMaterial;
        [SerializeField] private EnvForgeApiSettings apiSettings;
        [SerializeField] private string apiBaseUrl = "http://localhost:8000";
        [SerializeField] private string webSocketBaseUrl = "";
        [SerializeField] private bool showCloudRunPanel = true;
        [SerializeField] private NavigationTrainingSettings trainingSettings = new();

        private readonly List<NavigationScenarioWallSpec> userWalls = new();
        private readonly List<GameObject> boundaryWallObjects = new();
        private readonly List<GameObject> wallObjects = new();
        private GameObject floorObject;
        private Material blockedRuntimeMaterial;
        private INavigationEpisodeEvents episodeEvents;
        private NavigationEpisodeEventHub episodeEventHub;
        private NavigationMetrics navigationMetrics;
        private GoalReachChecker goalReachChecker;
        private NavigationGoalObservationProvider policyObservationProvider;
        private NavigationCameraController cameraController;
        private Transform agentTransform;
        private Transform goalTransform;
        private NavigationLiveController liveController;
        private Vector3 agentStartPosition = NavigationScenarioBundleDefaults.AgentStartPosition;
        private Quaternion agentStartRotation = NavigationScenarioBundleDefaults.AgentStartRotation;
        private Vector3 goalStartPosition = NavigationScenarioBundleDefaults.GoalStartPosition;
        private readonly List<Vector3> recentRuntimeStartPositions = new();
        private Vector3? lastRuntimeStartPosition;
        private int runtimeStartSerial;
        private int nextWallId = 1;

        public NavigationTrainingSettings TrainingSettings => trainingSettings;

        public Vector2 FloorSize => floorSize;

        public float WallHeight => wallHeight;

        public float WallThickness => wallThickness;

        public float GoalReachRadius => goalReachRadius;

        public float AgentCollisionRadius => agentCollisionRadius;

        public int UserWallCount => userWalls.Count;

        public Vector3 AgentStartPosition => agentStartPosition;

        public Vector3 GoalStartPosition => goalStartPosition;

        public string LastRuntimeStartSummary { get; private set; } = "start -";

        public string CurrentScenarioSourceSummary { get; private set; } = "Map: current editor";

        public string NextCameraViewLabel => cameraController == null ? "Top" : cameraController.NextViewModeLabel;

        public string CurrentCameraViewLabel => cameraController == null ? "Angle" : cameraController.CurrentViewModeLabel;

        public void ToggleCameraView()
        {
            cameraController?.ToggleViewMode();
        }

        public void SetTopCameraView()
        {
            cameraController?.SetTopView();
        }

        public void SetAngledCameraView()
        {
            cameraController?.SetAngledView();
        }

        public void SetAgentCollisionRadius(float radius)
        {
            agentCollisionRadius = Mathf.Max(0.01f, radius);
            if (agentTransform != null && agentTransform.TryGetComponent(out CapsuleCollider capsule))
            {
                capsule.radius = agentCollisionRadius;
            }
        }

        public ScenarioBundle BuildScenarioBundle(string scenarioId = DefaultScenarioId)
        {
            return NavigationScenarioBundleBuilder.Build(CreateScenarioBundleSource(scenarioId));
        }

        public string BuildScenarioBundleJson(string scenarioId = DefaultScenarioId, bool prettyPrint = true)
        {
            return ScenarioBundleJson.Serialize(BuildScenarioBundle(scenarioId), prettyPrint);
        }

        public void ResetToDefaultScenario()
        {
            ApplyScenarioBundle(NavigationScenarioBundleBuilder.Build(NavigationScenarioBundleDefaults.CreateSource()));
            RecordScenarioSource("Map: default scenario");
        }

        public void ApplyScenarioBundle(ScenarioBundle scenario)
        {
            if (scenario == null)
            {
                ResetToDefaultScenario();
                return;
            }

            floorSize = GetScenarioFloorSize(scenario);
            goalReachRadius = scenario.World?.Goal?.Radius > 0
                ? (float)scenario.World.Goal.Radius
                : NavigationScenarioBundleDefaults.CreateSource().GoalReachRadius;
            SetAgentCollisionRadius(scenario.Robot != null && scenario.Robot.Radius > 0
                ? (float)scenario.Robot.Radius
                : NavigationScenarioBundleDefaults.RobotRadiusMeters);
            ApplyWallDimensionsFromScenario(scenario);

            ClearUserWalls();
            ApplyFloorState();

            if (scenario.Robot?.StartPose != null)
            {
                agentStartRotation = Quaternion.Euler(0f, (float)scenario.Robot.StartPose.RotationYDegrees, 0f);
                SetAgentStartPosition(ToVector2(scenario.Robot.StartPose.Position));
            }
            else
            {
                agentStartRotation = DefaultAgentStartRotation;
                SetAgentStartPosition(new Vector2(DefaultAgentStartPosition.x, DefaultAgentStartPosition.z));
            }

            if (scenario.World?.Goal?.Position != null)
            {
                SetGoalStartPosition(ToVector2(scenario.World.Goal.Position));
            }
            else
            {
                SetGoalStartPosition(new Vector2(DefaultGoalStartPosition.x, DefaultGoalStartPosition.z));
            }

            ApplyUserWallsFromScenario(scenario);
            nextWallId = userWalls.Count + 1;
            ConfigureRuntimeScenarioContracts();
        }

        public void RecordScenarioSource(string source)
        {
            CurrentScenarioSourceSummary = string.IsNullOrWhiteSpace(source)
                ? "Map: current editor"
                : source;
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
            agentTransform = agent.transform;
            goalTransform = goal.transform;
            NavigationReplayPlayer replayPlayer = gameObject.AddComponent<NavigationReplayPlayer>();
            replayPlayer.Configure(agent.transform);
            EnvForgeCloudRunPanel cloudRunPanel = gameObject.AddComponent<EnvForgeCloudRunPanel>();
            cloudRunPanel.enabled = showCloudRunPanel;

            navigationMetrics = gameObject.AddComponent<NavigationMetrics>();
            navigationMetrics.Configure(agent.transform, goal.transform);

            liveController = agent.GetComponent<NavigationLiveController>();
            episodeEventHub = gameObject.AddComponent<NavigationEpisodeEventHub>();
            episodeEventHub.Configure(liveController);
            episodeEvents = episodeEventHub;
            policyObservationProvider = gameObject.AddComponent<NavigationGoalObservationProvider>();
            ConfigureRuntimeScenarioContracts();
            NavigationModelInferenceController inferenceController = agent.GetComponent<NavigationModelInferenceController>();
            inferenceController.Configure(
                agent.GetComponent<AgentMotor>(),
                agent.GetComponent<Rigidbody>(),
                liveController,
                episodeEventHub,
                policyObservationProvider);
            cloudRunPanel.Configure(this, replayPlayer, inferenceController, apiSettings, apiBaseUrl, webSocketBaseUrl);

            RebuildBoundaryWalls();
            NavigationWorldEditorPanel worldEditorPanel = gameObject.AddComponent<NavigationWorldEditorPanel>();
            worldEditorPanel.Configure(this);

            goalReachChecker = gameObject.AddComponent<GoalReachChecker>();
            ConfigureRuntimeScenarioContracts();

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
            agent.transform.SetPositionAndRotation(agentStartPosition, agentStartRotation);
            agent.layer = HiddenFromSegmentationCameraLayer;
            agent.GetComponent<Renderer>().sharedMaterial = material;
            CapsuleCollider capsule = agent.GetComponent<CapsuleCollider>();
            capsule.radius = agentCollisionRadius;

            Rigidbody body = agent.AddComponent<Rigidbody>();
            body.mass = 1f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;

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
            goal.transform.position = goalStartPosition;
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
            Renderer renderer = wall.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

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

            cameraController = Camera.main.GetComponent<NavigationCameraController>();
            if (cameraController == null)
            {
                cameraController = Camera.main.gameObject.AddComponent<NavigationCameraController>();
            }

            cameraController.Configure(Camera.main, floorSize);
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
                Mathf.Clamp(size.x, minimumFloorSize.x, MaxFloorSizeMeters),
                Mathf.Clamp(size.y, minimumFloorSize.y, MaxFloorSizeMeters));
            ApplyFloorState();
        }

        private void ApplyFloorState()
        {
            if (floorObject != null)
            {
                floorObject.transform.localScale = new Vector3(floorSize.x, 0.1f, floorSize.y);
            }

            RebuildBoundaryWalls();
            RefitUserWallsToFloor();
            cameraController?.FitToFloor(floorSize);
            ConfigureRuntimeScenarioContracts();
        }

        private void ConfigureRuntimeScenarioContracts()
        {
            float observationScale = Mathf.Max(1f, floorSize.magnitude);
            policyObservationProvider?.Configure(navigationMetrics, observationScale, goalReachRadius);
            goalReachChecker?.Configure(episodeEvents, navigationMetrics, goalReachRadius);
        }

        private Vector2 GetMinimumFloorSize()
        {
            float safeGoalClearance = wallThickness + goalReachRadius + 0.25f;
            float halfWidth = Mathf.Max(Mathf.Abs(agentStartPosition.x), Mathf.Abs(goalStartPosition.x)) + safeGoalClearance;
            float halfDepth = Mathf.Max(Mathf.Abs(agentStartPosition.z), Mathf.Abs(goalStartPosition.z)) + safeGoalClearance;
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

        public int AddDefaultUserWall(Vector2 center)
        {
            return AddUserWall(center, DefaultUserWallLengthMeters, wallThickness, 0f);
        }

        public int AddUserWall(Vector2 center, float length, float thickness, float rotationYDegrees)
        {
            float safeLength = Mathf.Clamp(length, 0.25f, Mathf.Max(floorSize.x, floorSize.y));
            float safeThickness = Mathf.Clamp(thickness, 0.1f, 4f);
            Vector2 clampedCenter = FindOpenUserWallCenter(center, safeLength, safeThickness, rotationYDegrees);
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
            return userWalls.Count - 1;
        }

        public bool TryGetUserWall(int index, out NavigationScenarioWallSpec wallSpec)
        {
            if (index < 0 || index >= userWalls.Count)
            {
                wallSpec = default;
                return false;
            }

            wallSpec = userWalls[index];
            return true;
        }

        public bool TryRaycastUserWall(Ray ray, out int wallIndex)
        {
            wallIndex = -1;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < wallObjects.Count; i++)
            {
                GameObject wallObject = wallObjects[i];
                if (wallObject == null || !wallObject.TryGetComponent(out Collider collider))
                {
                    continue;
                }

                if (!collider.Raycast(ray, out RaycastHit hit, 1000f) || hit.distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = hit.distance;
                wallIndex = i;
            }

            return wallIndex >= 0;
        }

        public bool UpdateUserWall(int index, Vector2 center, float length, float rotationYDegrees)
        {
            if (index < 0 || index >= userWalls.Count)
            {
                return false;
            }

            NavigationScenarioWallSpec previous = userWalls[index];
            float safeLength = Mathf.Clamp(length, 0.25f, Mathf.Max(floorSize.x, floorSize.y));
            Vector2 clampedCenter = ClampUserWallCenter(center, safeLength, wallThickness, rotationYDegrees);
            if (!CanPlaceUserWall(index, clampedCenter, safeLength, rotationYDegrees))
            {
                return false;
            }

            NavigationScenarioWallSpec updated = new(
                previous.Id,
                previous.DisplayName,
                new Vector3(clampedCenter.x, wallHeight * 0.5f, clampedCenter.y),
                new Vector3(safeLength, wallHeight, wallThickness),
                rotationYDegrees);
            userWalls[index] = updated;

            if (index >= wallObjects.Count || wallObjects[index] == null)
            {
                RebuildUserWalls();
                return true;
            }

            ApplyWallSpec(wallObjects[index], updated);
            return true;
        }

        private Vector2 ClampUserWallCenter(Vector2 center, float length, float thickness, float rotationYDegrees)
        {
            float radians = -rotationYDegrees * Mathf.Deg2Rad;
            Vector2 axis = new(Mathf.Cos(radians), Mathf.Sin(radians));
            Vector2 normal = new(-axis.y, axis.x);
            float halfWidth = floorSize.x * 0.5f;
            float halfDepth = floorSize.y * 0.5f;
            float wallHalfWidth = Mathf.Abs(axis.x) * length * 0.5f + Mathf.Abs(normal.x) * thickness * 0.5f;
            float wallHalfDepth = Mathf.Abs(axis.y) * length * 0.5f + Mathf.Abs(normal.y) * thickness * 0.5f;
            float boundaryClearance = wallThickness * 0.5f;
            float maxX = Mathf.Max(0f, halfWidth - boundaryClearance - wallHalfWidth);
            float maxZ = Mathf.Max(0f, halfDepth - boundaryClearance - wallHalfDepth);
            return new Vector2(
                Mathf.Clamp(center.x, -maxX, maxX),
                Mathf.Clamp(center.y, -maxZ, maxZ));
        }

        private Vector2 FindOpenUserWallCenter(Vector2 requestedCenter, float length, float thickness, float rotationYDegrees)
        {
            Vector2 clampedRequestedCenter = ClampUserWallCenter(requestedCenter, length, thickness, rotationYDegrees);
            if (!OverlapsExistingUserWall(clampedRequestedCenter, length, thickness, rotationYDegrees, -1, includeWallClearance: true))
            {
                return clampedRequestedCenter;
            }

            float stride = Mathf.Max(thickness + UserWallPlacementGapMeters, 0.5f);
            int maxRing = Mathf.CeilToInt(Mathf.Max(floorSize.x, floorSize.y) / stride);
            for (int ring = 1; ring <= maxRing; ring++)
            {
                for (int x = -ring; x <= ring; x++)
                {
                    for (int z = -ring; z <= ring; z++)
                    {
                        if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) != ring)
                        {
                            continue;
                        }

                        Vector2 candidate = ClampUserWallCenter(
                            requestedCenter + new Vector2(x * stride, z * stride),
                            length,
                            thickness,
                            rotationYDegrees);
                        if (!OverlapsExistingUserWall(candidate, length, thickness, rotationYDegrees, -1, includeWallClearance: true))
                        {
                            return candidate;
                        }
                    }
                }
            }

            return clampedRequestedCenter;
        }

        public bool CanPlaceUserWall(int indexToIgnore, Vector2 center, float length, float rotationYDegrees)
        {
            return !OverlapsExistingUserWall(center, length, wallThickness, rotationYDegrees, indexToIgnore, includeWallClearance: false);
        }

        private bool OverlapsExistingUserWall(
            Vector2 center,
            float length,
            float thickness,
            float rotationYDegrees,
            int indexToIgnore,
            bool includeWallClearance)
        {
            float wallClearance = includeWallClearance ? UserWallPlacementGapMeters : 0f;
            for (int i = 0; i < userWalls.Count; i++)
            {
                if (i == indexToIgnore)
                {
                    continue;
                }

                NavigationScenarioWallSpec wallSpec = userWalls[i];
                Vector2 otherCenter = new(wallSpec.Center.x, wallSpec.Center.z);
                if (OrientedRectsOverlap(
                    center,
                    length,
                    thickness + wallClearance,
                    rotationYDegrees,
                    otherCenter,
                    wallSpec.Size.x,
                    wallSpec.Size.z + wallClearance,
                    wallSpec.RotationYDegrees))
                {
                    return true;
                }
            }

            Vector3 agentPosition = agentTransform != null ? agentTransform.position : agentStartPosition;
            if (ContainsProtectedPoint(
                center,
                length,
                thickness,
                rotationYDegrees,
                new Vector2(agentPosition.x, agentPosition.z),
                agentCollisionRadius + UserWallPlacementGapMeters))
            {
                return true;
            }

            return ContainsProtectedPoint(
                center,
                length,
                thickness,
                rotationYDegrees,
                new Vector2(goalStartPosition.x, goalStartPosition.z),
                goalReachRadius + UserWallPlacementGapMeters);
        }

        private static bool ContainsProtectedPoint(
            Vector2 wallCenter,
            float wallLength,
            float wallThickness,
            float wallRotationYDegrees,
            Vector2 point,
            float clearance)
        {
            Vector2 axis = GetWallAxis(wallRotationYDegrees);
            Vector2 normal = new(-axis.y, axis.x);
            Vector2 delta = point - wallCenter;
            float localLength = Mathf.Abs(Vector2.Dot(delta, axis));
            float localThickness = Mathf.Abs(Vector2.Dot(delta, normal));
            return localLength <= wallLength * 0.5f + clearance &&
                localThickness <= wallThickness * 0.5f + clearance;
        }

        private static bool OrientedRectsOverlap(
            Vector2 firstCenter,
            float firstLength,
            float firstThickness,
            float firstRotationYDegrees,
            Vector2 secondCenter,
            float secondLength,
            float secondThickness,
            float secondRotationYDegrees)
        {
            Vector2 firstAxis = GetWallAxis(firstRotationYDegrees);
            Vector2 firstNormal = new(-firstAxis.y, firstAxis.x);
            Vector2 secondAxis = GetWallAxis(secondRotationYDegrees);
            Vector2 secondNormal = new(-secondAxis.y, secondAxis.x);
            return OverlapsOnAxis(firstCenter, firstAxis, firstNormal, firstLength, firstThickness, secondCenter, secondAxis, secondNormal, secondLength, secondThickness, firstAxis) &&
                   OverlapsOnAxis(firstCenter, firstAxis, firstNormal, firstLength, firstThickness, secondCenter, secondAxis, secondNormal, secondLength, secondThickness, firstNormal) &&
                   OverlapsOnAxis(firstCenter, firstAxis, firstNormal, firstLength, firstThickness, secondCenter, secondAxis, secondNormal, secondLength, secondThickness, secondAxis) &&
                   OverlapsOnAxis(firstCenter, firstAxis, firstNormal, firstLength, firstThickness, secondCenter, secondAxis, secondNormal, secondLength, secondThickness, secondNormal);
        }

        private static bool OverlapsOnAxis(
            Vector2 firstCenter,
            Vector2 firstAxis,
            Vector2 firstNormal,
            float firstLength,
            float firstThickness,
            Vector2 secondCenter,
            Vector2 secondAxis,
            Vector2 secondNormal,
            float secondLength,
            float secondThickness,
            Vector2 testAxis)
        {
            float distance = Mathf.Abs(Vector2.Dot(secondCenter - firstCenter, testAxis));
            float firstRadius = Mathf.Abs(Vector2.Dot(firstAxis, testAxis)) * firstLength * 0.5f +
                Mathf.Abs(Vector2.Dot(firstNormal, testAxis)) * firstThickness * 0.5f;
            float secondRadius = Mathf.Abs(Vector2.Dot(secondAxis, testAxis)) * secondLength * 0.5f +
                Mathf.Abs(Vector2.Dot(secondNormal, testAxis)) * secondThickness * 0.5f;
            return distance < firstRadius + secondRadius - 0.001f;
        }

        private static Vector2 GetWallAxis(float rotationYDegrees)
        {
            float radians = -rotationYDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        public void SetAgentStartPosition(Vector2 center)
        {
            Vector2 clampedCenter = ClampPointInsideBoundary(center, 0.6f);
            agentStartPosition = new Vector3(clampedCenter.x, DefaultAgentStartPosition.y, clampedCenter.y);
            if (agentTransform != null)
            {
                if (liveController != null)
                {
                    liveController.SetResetPose(agentStartPosition, agentStartRotation);
                }
                else
                {
                    agentTransform.SetPositionAndRotation(agentStartPosition, agentStartRotation);
                }
            }
        }

        public void SetGoalStartPosition(Vector2 center)
        {
            Vector2 clampedCenter = ClampPointInsideBoundary(center, goalReachRadius);
            goalStartPosition = new Vector3(clampedCenter.x, DefaultGoalStartPosition.y, clampedCenter.y);
            if (goalTransform != null)
            {
                goalTransform.position = goalStartPosition;
            }
        }

        public bool TrySelectRandomAgentPose(out Vector3 position, out Quaternion rotation, out string reason)
        {
            reason = string.Empty;
            position = agentTransform != null ? agentTransform.position : agentStartPosition;
            rotation = agentTransform != null ? agentTransform.rotation : agentStartRotation;
            if (agentTransform == null)
            {
                reason = "agent missing";
                return false;
            }

            Collider agentCollider = agentTransform.GetComponent<Collider>();
            Vector2 halfExtents = GetRandomStartHalfExtents();
            Vector3 currentPosition = agentTransform.position;
            int startSerial = runtimeStartSerial++;
            if (TryFindRandomAgentPose(halfExtents, currentPosition, agentCollider, startSerial, requireDistinctStart: true, out position, out rotation) ||
                TryFindRandomAgentPose(halfExtents, currentPosition, agentCollider, startSerial + 17, requireDistinctStart: false, out position, out rotation))
            {
                return true;
            }

            reason = "no open random start";
            return false;
        }

        public void RecordRuntimeStart(Vector3 position, Quaternion rotation)
        {
            lastRuntimeStartPosition = position;
            recentRuntimeStartPositions.Add(position);
            while (recentRuntimeStartPositions.Count > 8)
            {
                recentRuntimeStartPositions.RemoveAt(0);
            }

            LastRuntimeStartSummary = $"start x={position.x:0.00} z={position.z:0.00} yaw={rotation.eulerAngles.y:0.#}";
        }

        private bool TryFindRandomAgentPose(
            Vector2 halfExtents,
            Vector3 currentPosition,
            Collider agentCollider,
            int startSerial,
            bool requireDistinctStart,
            out Vector3 position,
            out Quaternion rotation)
        {
            float minimumDistinctStartDistanceMeters = requireDistinctStart
                ? GetRuntimeStartMinimumDistance()
                : 0.75f;
            position = currentPosition;
            rotation = agentTransform != null ? agentTransform.rotation : Quaternion.identity;
            for (int attempt = 0; attempt < 240; attempt++)
            {
                Vector2 normalized = GenerateRuntimeStartSample(startSerial, attempt);
                float x = Mathf.Lerp(-halfExtents.x, halfExtents.x, normalized.x);
                float z = Mathf.Lerp(-halfExtents.y, halfExtents.y, normalized.y);
                Vector3 candidatePosition = new(x, currentPosition.y, z);
                if (IsNearExistingStart(candidatePosition, currentPosition, minimumDistinctStartDistanceMeters))
                {
                    continue;
                }

                if (OverlapsBlockingGeometry(candidatePosition, Mathf.Max(0.1f, agentCollisionRadius), agentCollider))
                {
                    continue;
                }

                if (Vector3.Distance(candidatePosition, goalStartPosition) <= goalReachRadius * 1.75f)
                {
                    continue;
                }

                Quaternion candidateRotation = Quaternion.Euler(0f, Random.Range(-180f, 180f), 0f);
                position = candidatePosition;
                rotation = candidateRotation;
                return true;
            }

            return false;
        }

        private float GetRuntimeStartMinimumDistance()
        {
            return Mathf.Clamp(Mathf.Min(floorSize.x, floorSize.y) * 0.25f, 1.5f, 4f);
        }

        private static Vector2 GenerateRuntimeStartSample(int startSerial, int attempt)
        {
            float x = Mathf.Repeat((startSerial * 0.61803398875f) + (attempt * 0.38196601125f) + Random.value * 0.13f, 1f);
            float z = Mathf.Repeat((startSerial * 0.75487766625f) + (attempt * 0.569840291f) + Random.value * 0.13f, 1f);
            return new Vector2(x, z);
        }

        private bool IsNearExistingStart(Vector3 candidatePosition, Vector3 currentPosition, float minimumDistanceMeters)
        {
            if (Vector3.Distance(candidatePosition, agentStartPosition) < minimumDistanceMeters)
            {
                return true;
            }

            if (Vector3.Distance(candidatePosition, currentPosition) < minimumDistanceMeters)
            {
                return true;
            }

            foreach (Vector3 recentPosition in recentRuntimeStartPositions)
            {
                if (Vector3.Distance(candidatePosition, recentPosition) < minimumDistanceMeters)
                {
                    return true;
                }
            }

            return lastRuntimeStartPosition.HasValue &&
                Vector3.Distance(candidatePosition, lastRuntimeStartPosition.Value) < minimumDistanceMeters;
        }

        private void ApplyAgentRuntimePose(Vector3 position, Quaternion rotation)
        {
            Rigidbody body = agentTransform != null ? agentTransform.GetComponent<Rigidbody>() : null;
            if (body != null)
            {
#if UNITY_6000_0_OR_NEWER
                body.linearVelocity = Vector3.zero;
#else
                body.velocity = Vector3.zero;
#endif
                body.angularVelocity = Vector3.zero;
                body.isKinematic = false;
                body.position = position;
                body.rotation = rotation;
            }

            if (agentTransform != null)
            {
                agentTransform.SetPositionAndRotation(position, rotation);
            }

            if (liveController != null)
            {
                liveController.SetResetPose(position, rotation, applyImmediately: false);
            }

            Physics.SyncTransforms();
        }

        private bool OverlapsBlockingGeometry(Vector3 position, float radius, Collider ignoredCollider)
        {
            Collider[] overlaps = Physics.OverlapSphere(position, radius, ~0, QueryTriggerInteraction.Ignore);
            for (int index = 0; index < overlaps.Length; index++)
            {
                Collider overlap = overlaps[index];
                if (overlap == null ||
                    overlap == ignoredCollider ||
                    IsAgentCollider(overlap) ||
                    IsFloorCollider(overlap))
                {
                    continue;
                }

                if (overlap != null)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsAgentCollider(Collider collider)
        {
            return agentTransform != null && collider.transform.IsChildOf(agentTransform);
        }

        private bool IsFloorCollider(Collider collider)
        {
            return floorObject != null && collider.transform.IsChildOf(floorObject.transform);
        }

        private Vector2 ClampPointInsideBoundary(Vector2 center, float clearance)
        {
            float safeClearance = wallThickness + Mathf.Max(0f, clearance) + 0.05f;
            float maxX = Mathf.Max(0f, floorSize.x * 0.5f - safeClearance);
            float maxZ = Mathf.Max(0f, floorSize.y * 0.5f - safeClearance);
            return new Vector2(
                Mathf.Clamp(center.x, -maxX, maxX),
                Mathf.Clamp(center.y, -maxZ, maxZ));
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

        public bool RemoveUserWall(int index)
        {
            if (index < 0 || index >= userWalls.Count)
            {
                return false;
            }

            userWalls.RemoveAt(index);
            if (index < wallObjects.Count && wallObjects[index] != null)
            {
                Destroy(wallObjects[index]);
            }

            if (index < wallObjects.Count)
            {
                wallObjects.RemoveAt(index);
            }

            return true;
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

        private void RebuildUserWalls()
        {
            foreach (GameObject wallObject in wallObjects)
            {
                if (wallObject != null)
                {
                    Destroy(wallObject);
                }
            }

            wallObjects.Clear();
            if (blockedRuntimeMaterial == null || episodeEvents == null)
            {
                return;
            }

            foreach (NavigationScenarioWallSpec wallSpec in userWalls)
            {
                CreateWall(wallSpec, blockedRuntimeMaterial, episodeEvents, trackUserWall: true);
            }
        }

        private void RefitUserWallsToFloor()
        {
            for (int i = 0; i < userWalls.Count; i++)
            {
                NavigationScenarioWallSpec wallSpec = userWalls[i];
                Vector2 center = new(wallSpec.Center.x, wallSpec.Center.z);
                Vector2 clampedCenter = ClampUserWallCenter(center, wallSpec.Size.x, wallSpec.Size.z, wallSpec.RotationYDegrees);
                userWalls[i] = new NavigationScenarioWallSpec(
                    wallSpec.Id,
                    wallSpec.DisplayName,
                    new Vector3(clampedCenter.x, wallSpec.Center.y, clampedCenter.y),
                    wallSpec.Size,
                    wallSpec.RotationYDegrees);
            }

            RebuildUserWalls();
        }

        private static void ApplyWallSpec(GameObject wallObject, NavigationScenarioWallSpec wallSpec)
        {
            wallObject.name = wallSpec.DisplayName;
            wallObject.transform.SetPositionAndRotation(wallSpec.Center, Quaternion.Euler(0f, -wallSpec.RotationYDegrees, 0f));
            wallObject.transform.localScale = wallSpec.Size;
        }

        public IReadOnlyList<NavigationScenarioWallSpec> GetUserWalls()
        {
            return userWalls;
        }

        private void ApplyUserWallsFromScenario(ScenarioBundle scenario)
        {
            if (scenario.World?.StaticWalls == null)
            {
                return;
            }

            foreach (StaticWall wall in scenario.World.StaticWalls)
            {
                if (IsBoundaryWall(wall))
                {
                    continue;
                }

                NavigationScenarioWallSpec wallSpec = new(
                    string.IsNullOrWhiteSpace(wall.Id) ? $"user_wall_{userWalls.Count + 1:000}" : wall.Id,
                    $"Navigation User Wall {userWalls.Count + 1}",
                    new Vector3((float)(wall.Center?.X ?? 0d), (float)wall.Height * 0.5f, (float)(wall.Center?.Z ?? 0d)),
                    new Vector3(Mathf.Max(0.25f, (float)(wall.Size?.X ?? DefaultUserWallLengthMeters)), Mathf.Max(0.1f, (float)wall.Height), Mathf.Max(0.1f, (float)(wall.Size?.Z ?? wallThickness))),
                    (float)wall.RotationYDegrees);
                userWalls.Add(wallSpec);
                CreateWall(wallSpec, blockedRuntimeMaterial, episodeEvents, trackUserWall: true);
            }
        }

        private void ApplyWallDimensionsFromScenario(ScenarioBundle scenario)
        {
            if (scenario.World?.StaticWalls == null)
            {
                return;
            }

            foreach (StaticWall wall in scenario.World.StaticWalls)
            {
                if (!IsBoundaryWall(wall))
                {
                    continue;
                }

                if (wall.Height > 0)
                {
                    wallHeight = (float)wall.Height;
                }

                if (wall.Size != null)
                {
                    float candidateThickness = Mathf.Min(Mathf.Abs((float)wall.Size.X), Mathf.Abs((float)wall.Size.Z));
                    if (candidateThickness > 0f)
                    {
                        wallThickness = candidateThickness;
                    }
                }

                return;
            }
        }

        private static bool IsBoundaryWall(StaticWall wall)
        {
            return wall != null &&
                (wall.Id == "wall_north" ||
                 wall.Id == "wall_south" ||
                 wall.Id == "wall_east" ||
                 wall.Id == "wall_west");
        }

        private static Vector2 GetScenarioFloorSize(ScenarioBundle scenario)
        {
            Bounds2D bounds = scenario.World?.Bounds;
            if (bounds?.Min == null || bounds.Max == null)
            {
                return NavigationScenarioBundleDefaults.FloorSize;
            }

            return new Vector2(
                Mathf.Clamp((float)(bounds.Max.X - bounds.Min.X), 1f, MaxFloorSizeMeters),
                Mathf.Clamp((float)(bounds.Max.Z - bounds.Min.Z), 1f, MaxFloorSizeMeters));
        }

        private static Vector2 ToVector2(Position2D value)
        {
            return value == null ? Vector2.zero : new Vector2((float)value.X, (float)value.Z);
        }

        private NavigationScenarioBundleSource CreateScenarioBundleSource(string scenarioId)
        {
            NavigationScenarioBundleSource source = new()
            {
                ScenarioId = scenarioId,
                FloorSize = floorSize,
                WallHeight = wallHeight,
                WallThickness = wallThickness,
                AgentStartPosition = agentStartPosition,
                AgentStartRotation = agentStartRotation,
                RobotRadiusMeters = agentCollisionRadius,
                GoalStartPosition = goalStartPosition,
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

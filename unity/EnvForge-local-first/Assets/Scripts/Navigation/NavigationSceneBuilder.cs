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

        [SerializeField] private Vector2 floorSize = new(16f, 12f);
        [SerializeField] private float wallHeight = 1.8f;
        [SerializeField] private float wallThickness = 0.35f;
        [SerializeField] private float goalReachRadius = 1.2f;
        [SerializeField] private bool showSegmentationPreview = true;
        [SerializeField] private Rect segmentationPreviewRect = new(0.74f, 0.02f, 0.24f, 0.18f);
        [SerializeField] private float agentCollisionRadius = 0.45f;
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
        private NavigationCameraController cameraController;
        private Transform agentTransform;
        private Transform goalTransform;
        private NavigationLiveController liveController;
        private Vector3 agentStartPosition = NavigationScenarioBundleDefaults.AgentStartPosition;
        private Quaternion agentStartRotation = NavigationScenarioBundleDefaults.AgentStartRotation;
        private Vector3 goalStartPosition = NavigationScenarioBundleDefaults.GoalStartPosition;
        private int nextWallId = 1;

        public NavigationTrainingSettings TrainingSettings => trainingSettings;

        public Vector2 FloorSize => floorSize;

        public float WallHeight => wallHeight;

        public float WallThickness => wallThickness;

        public float GoalReachRadius => goalReachRadius;

        public int UserWallCount => userWalls.Count;

        public Vector3 AgentStartPosition => agentStartPosition;

        public Vector3 GoalStartPosition => goalStartPosition;

        public string NextCameraViewLabel => cameraController == null ? "Top" : cameraController.NextViewModeLabel;

        public void ToggleCameraView()
        {
            cameraController?.ToggleViewMode();
        }

        public ScenarioBundleDto BuildScenarioBundle(string scenarioId = DefaultScenarioId)
        {
            return NavigationScenarioBundleBuilder.Build(CreateScenarioBundleSource(scenarioId));
        }

        public string BuildScenarioBundleJson(string scenarioId = DefaultScenarioId, bool prettyPrint = true)
        {
            return ScenarioBundleSerializer.ToJson(BuildScenarioBundle(scenarioId), prettyPrint);
        }

        public void ResetToDefaultScenario()
        {
            ApplyScenarioBundle(NavigationScenarioBundleBuilder.Build(NavigationScenarioBundleDefaults.CreateSource()));
        }

        public void ApplyScenarioBundle(ScenarioBundleDto scenario)
        {
            if (scenario == null)
            {
                ResetToDefaultScenario();
                return;
            }

            floorSize = GetScenarioFloorSize(scenario);
            goalReachRadius = scenario.world?.goal?.radius > 0f
                ? scenario.world.goal.radius
                : NavigationScenarioBundleDefaults.CreateSource().GoalReachRadius;
            ApplyWallDimensionsFromScenario(scenario);

            ClearUserWalls();
            ApplyFloorState();

            if (scenario.robot?.start_pose != null)
            {
                agentStartRotation = Quaternion.Euler(0f, scenario.robot.start_pose.rotation_y_degrees, 0f);
                SetAgentStartPosition(ToVector2(scenario.robot.start_pose.position));
            }
            else
            {
                agentStartRotation = DefaultAgentStartRotation;
                SetAgentStartPosition(new Vector2(DefaultAgentStartPosition.x, DefaultAgentStartPosition.z));
            }

            if (scenario.world?.goal?.position != null)
            {
                SetGoalStartPosition(ToVector2(scenario.world.goal.position));
            }
            else
            {
                SetGoalStartPosition(new Vector2(DefaultGoalStartPosition.x, DefaultGoalStartPosition.z));
            }

            ApplyUserWallsFromScenario(scenario);
            nextWallId = userWalls.Count + 1;
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

            NavigationMetrics metrics = gameObject.AddComponent<NavigationMetrics>();
            metrics.Configure(agent.transform, goal.transform);

            liveController = agent.GetComponent<NavigationLiveController>();
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
            float observationScale = floorSize.magnitude;
            policyObservationProvider?.Configure(policyObservationProvider.GetComponent<NavigationMetrics>(), observationScale, goalReachRadius);
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
            Vector2 clampedCenter = ClampUserWallCenter(center, safeLength, safeThickness, rotationYDegrees);
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

        public void UpdateUserWall(int index, Vector2 center, float length, float rotationYDegrees)
        {
            if (index < 0 || index >= userWalls.Count)
            {
                return;
            }

            NavigationScenarioWallSpec previous = userWalls[index];
            float safeLength = Mathf.Clamp(length, 0.25f, Mathf.Max(floorSize.x, floorSize.y));
            Vector2 clampedCenter = ClampUserWallCenter(center, safeLength, wallThickness, rotationYDegrees);
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
                return;
            }

            ApplyWallSpec(wallObjects[index], updated);
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
            float boundaryClearance = wallThickness + 0.05f;
            float maxX = Mathf.Max(0f, halfWidth - boundaryClearance - wallHalfWidth);
            float maxZ = Mathf.Max(0f, halfDepth - boundaryClearance - wallHalfDepth);
            return new Vector2(
                Mathf.Clamp(center.x, -maxX, maxX),
                Mathf.Clamp(center.y, -maxZ, maxZ));
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

        private void ApplyUserWallsFromScenario(ScenarioBundleDto scenario)
        {
            if (scenario.world?.static_walls == null)
            {
                return;
            }

            foreach (StaticWallDto wall in scenario.world.static_walls)
            {
                if (IsBoundaryWall(wall))
                {
                    continue;
                }

                NavigationScenarioWallSpec wallSpec = new(
                    string.IsNullOrWhiteSpace(wall.id) ? $"user_wall_{userWalls.Count + 1:000}" : wall.id,
                    $"Navigation User Wall {userWalls.Count + 1}",
                    new Vector3(wall.center?.x ?? 0f, wall.height * 0.5f, wall.center?.z ?? 0f),
                    new Vector3(Mathf.Max(0.25f, wall.size?.x ?? DefaultUserWallLengthMeters), Mathf.Max(0.1f, wall.height), Mathf.Max(0.1f, wall.size?.z ?? wallThickness)),
                    wall.rotation_y_degrees);
                userWalls.Add(wallSpec);
                CreateWall(wallSpec, blockedRuntimeMaterial, episodeEvents, trackUserWall: true);
            }
        }

        private void ApplyWallDimensionsFromScenario(ScenarioBundleDto scenario)
        {
            if (scenario.world?.static_walls == null)
            {
                return;
            }

            foreach (StaticWallDto wall in scenario.world.static_walls)
            {
                if (!IsBoundaryWall(wall))
                {
                    continue;
                }

                if (wall.height > 0f)
                {
                    wallHeight = wall.height;
                }

                if (wall.size != null)
                {
                    float candidateThickness = Mathf.Min(Mathf.Abs(wall.size.x), Mathf.Abs(wall.size.z));
                    if (candidateThickness > 0f)
                    {
                        wallThickness = candidateThickness;
                    }
                }

                return;
            }
        }

        private static bool IsBoundaryWall(StaticWallDto wall)
        {
            return wall != null &&
                (wall.id == "wall_north" ||
                 wall.id == "wall_south" ||
                 wall.id == "wall_east" ||
                 wall.id == "wall_west");
        }

        private static Vector2 GetScenarioFloorSize(ScenarioBundleDto scenario)
        {
            Bounds2DDto bounds = scenario.world?.bounds;
            if (bounds?.min == null || bounds.max == null)
            {
                return NavigationScenarioBundleDefaults.FloorSize;
            }

            return new Vector2(
                Mathf.Clamp(bounds.max.x - bounds.min.x, 1f, MaxFloorSizeMeters),
                Mathf.Clamp(bounds.max.z - bounds.min.z, 1f, MaxFloorSizeMeters));
        }

        private static Vector2 ToVector2(Vector2Dto value)
        {
            return value == null ? Vector2.zero : new Vector2(value.x, value.z);
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

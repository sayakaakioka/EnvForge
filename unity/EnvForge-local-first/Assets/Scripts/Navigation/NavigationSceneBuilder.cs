using Unity.InferenceEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace EnvForge.Navigation
{
    public sealed class NavigationSceneBuilder : MonoBehaviour
    {
        private static readonly Vector3 AgentStartPosition = new(-6f, 0.6f, -4f);
        private static readonly Quaternion AgentStartRotation = Quaternion.Euler(0f, 45f, 0f);
        private static readonly Vector3 GoalStartPosition = new(6f, 1.2f, 4f);
        private const int SegmentationImageHeight = 84;
        private const int SegmentationImageWidth = 112;
        private const int HiddenFromSegmentationCameraLayer = 2;
        private const int MaxEpisodeSteps = 1000;

        [SerializeField] private Vector2 floorSize = new(16f, 12f);
        [SerializeField] private float wallHeight = 1.8f;
        [SerializeField] private float wallThickness = 0.35f;
        [SerializeField] private float goalReachRadius = 1.2f;
        [SerializeField] private string behaviorName = "NavigationFinal";
        [SerializeField] private ModelAsset inferenceModel;
        [SerializeField] private InferenceDevice inferenceDevice = InferenceDevice.Default;
        [SerializeField] private bool deterministicInference = true;
        [SerializeField] private bool saveSegmentationFrames;
        [SerializeField] private int saveSegmentationEverySteps = 100;
        [SerializeField] private bool showSegmentationPreview = true;
        [SerializeField] private Rect segmentationPreviewRect = new(0.74f, 0.02f, 0.24f, 0.18f);

        private void Start()
        {
            ConfigureCommunicatorMode();

            Material passableMaterial = CreateUnlitMaterial("Navigation Passable Segmentation Material", Color.green);
            Material blockedMaterial = CreateUnlitMaterial("Navigation Blocked Segmentation Material", Color.blue);
            Material agentMaterial = CreateUnlitMaterial("Navigation Agent Material", new Color(0.16f, 0.38f, 0.78f));
            Material arrowMaterial = CreateUnlitMaterial("Navigation Direction Arrow Material", Color.white);
            Material goalMaterial = CreateUnlitMaterial("Navigation Goal Material", new Color(1f, 0.82f, 0.12f));

            CreateFloor(passableMaterial);

            GameObject agent = CreateAgent(agentMaterial, arrowMaterial);
            GameObject goal = CreateGoal(goalMaterial);

            NavigationMetrics metrics = gameObject.AddComponent<NavigationMetrics>();
            metrics.Configure(agent.transform, goal.transform);

            NavigationObservationProvider debugObservationProvider = gameObject.AddComponent<NavigationObservationProvider>();
            debugObservationProvider.Configure(metrics, floorSize.magnitude);
            NavigationDebugOverlay debugOverlay = gameObject.AddComponent<NavigationDebugOverlay>();
            debugOverlay.Configure(metrics, debugObservationProvider);

            NavigationGoalObservationProvider mvp3ObservationProvider = gameObject.AddComponent<NavigationGoalObservationProvider>();
            mvp3ObservationProvider.Configure(metrics, floorSize.magnitude);

            NavigationAgent navigationAgent = agent.GetComponent<NavigationAgent>();
            navigationAgent.Configure(
                goal.transform,
                agent.GetComponent<Rigidbody>(),
                agent.GetComponent<AgentMotor>(),
                metrics,
                mvp3ObservationProvider,
                AgentStartPosition,
                AgentStartRotation,
                GoalStartPosition,
                GetRandomStartHalfExtents());

            CreateBoundaryWalls(blockedMaterial, navigationAgent);
            CreateInnerWalls(blockedMaterial, navigationAgent);

            GoalReachChecker goalReachChecker = gameObject.AddComponent<GoalReachChecker>();
            goalReachChecker.Configure(navigationAgent, metrics, goalReachRadius);

            PositionCamera();
        }

        private void ConfigureCommunicatorMode()
        {
            CommunicatorFactory.Enabled = inferenceModel == null;
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
            float halfWidth = floorSize.x * 0.5f;
            float halfDepth = floorSize.y * 0.5f;

            CreateWall("Navigation Wall North", new Vector3(0f, wallHeight * 0.5f, halfDepth), new Vector3(floorSize.x, wallHeight, wallThickness), material, episodeEvents);
            CreateWall("Navigation Wall South", new Vector3(0f, wallHeight * 0.5f, -halfDepth), new Vector3(floorSize.x, wallHeight, wallThickness), material, episodeEvents);
            CreateWall("Navigation Wall East", new Vector3(floorSize.x * 0.5f, wallHeight * 0.5f, 0f), new Vector3(wallThickness, wallHeight, floorSize.y), material, episodeEvents);
            CreateWall("Navigation Wall West", new Vector3(-halfWidth, wallHeight * 0.5f, 0f), new Vector3(wallThickness, wallHeight, floorSize.y), material, episodeEvents);
        }

        private void CreateInnerWalls(Material material, INavigationEpisodeEvents episodeEvents)
        {
            CreateWall("Navigation Inner Wall A", new Vector3(-1.5f, wallHeight * 0.5f, -1.2f), new Vector3(0.35f, wallHeight, 4.2f), material, episodeEvents);
            CreateWall("Navigation Inner Wall B", new Vector3(2.8f, wallHeight * 0.5f, 1.6f), new Vector3(4.4f, wallHeight, 0.35f), material, episodeEvents);
            CreateWall("Navigation Inner Wall C", new Vector3(4.8f, wallHeight * 0.5f, -2.2f), new Vector3(0.35f, wallHeight, 2.5f), material, episodeEvents);
            CreateWall("Navigation Inner Wall D", new Vector3(-4.2f, wallHeight * 0.5f, 2.2f), new Vector3(2.4f, wallHeight, 0.35f), material, episodeEvents);
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

            ConfigureBehaviorParameters(agent.AddComponent<BehaviorParameters>());
            agent.AddComponent<AgentMotor>();
            Camera segmentationCamera = CreateSegmentationCamera(agent.transform);
            ConfigureCameraSensor(agent, segmentationCamera, showSegmentationPreview);
            ConfigureSegmentationCapture(segmentationCamera.gameObject);
            ConfigureSegmentationPreview(segmentationCamera.gameObject, segmentationCamera);
            NavigationAgent navigationAgent = agent.AddComponent<NavigationAgent>();
            navigationAgent.MaxStep = MaxEpisodeSteps;
            ConfigureDecisionRequester(agent.AddComponent<DecisionRequester>());
            CreateDirectionArrow(agent.transform, arrowMaterial);

            return agent;
        }

        private void ConfigureBehaviorParameters(BehaviorParameters behaviorParameters)
        {
            behaviorParameters.BehaviorName = behaviorName;
            behaviorParameters.BehaviorType = BehaviorType.Default;
            behaviorParameters.BrainParameters.VectorObservationSize = NavigationGoalObservation.ValueCount;
            behaviorParameters.BrainParameters.NumStackedVectorObservations = 1;
            behaviorParameters.BrainParameters.ActionSpec = ActionSpec.MakeContinuous(2);

            if (inferenceModel == null)
            {
                Debug.Log("Navigation inference model is not assigned. Using the default ML-Agents behavior.");
                return;
            }

            behaviorParameters.Model = inferenceModel;
            behaviorParameters.InferenceDevice = inferenceDevice;
            behaviorParameters.DeterministicInference = deterministicInference;
            behaviorParameters.BehaviorType = BehaviorType.InferenceOnly;
            Debug.Log($"Navigation inference model assigned: {inferenceModel.name}.");
        }

        private static void ConfigureDecisionRequester(DecisionRequester decisionRequester)
        {
            decisionRequester.DecisionPeriod = 5;
            decisionRequester.TakeActionsBetweenDecisions = true;
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

        private static void ConfigureCameraSensor(GameObject agent, Camera segmentationCamera, bool runtimeCameraEnable)
        {
            CameraSensorComponent cameraSensor = agent.AddComponent<CameraSensorComponent>();
            cameraSensor.Camera = segmentationCamera;
            cameraSensor.SensorName = "SegmentationCamera";
            cameraSensor.Width = SegmentationImageWidth;
            cameraSensor.Height = SegmentationImageHeight;
            cameraSensor.Grayscale = false;
            cameraSensor.ObservationStacks = 1;
            cameraSensor.ObservationType = ObservationType.Default;
            cameraSensor.CompressionType = SensorCompressionType.PNG;
            cameraSensor.RuntimeCameraEnable = runtimeCameraEnable;
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
    }
}

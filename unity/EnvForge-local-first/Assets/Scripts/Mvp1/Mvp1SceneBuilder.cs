using EnvForge.Mvp0;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents;
using UnityEngine;

namespace EnvForge.Mvp1
{
    public sealed class Mvp1SceneBuilder : MonoBehaviour
    {
        private static readonly Vector3 AgentStartPosition = new(-6f, 0.6f, -4f);
        private static readonly Quaternion AgentStartRotation = Quaternion.Euler(0f, 45f, 0f);
        private static readonly Vector3 GoalStartPosition = new(6f, 1.2f, 4f);

        [SerializeField] private Vector2 floorSize = new(16f, 12f);
        [SerializeField] private float wallHeight = 1.8f;
        [SerializeField] private float wallThickness = 0.35f;
        [SerializeField] private float goalReachRadius = 1.2f;
        [SerializeField] private string behaviorName = "Mvp1Navigation";

        private void Start()
        {
            Material floorMaterial = CreateMaterial("MVP 1 Floor Material", new Color(0.36f, 0.42f, 0.38f));
            Material wallMaterial = CreateMaterial("MVP 1 Wall Material", new Color(0.62f, 0.23f, 0.21f));
            Material agentMaterial = CreateMaterial("MVP 1 Agent Material", new Color(0.16f, 0.38f, 0.78f));
            Material arrowMaterial = CreateMaterial("MVP 1 Direction Arrow Material", Color.white);
            Material goalMaterial = CreateMaterial("MVP 1 Goal Material", new Color(1f, 0.82f, 0.12f));

            CreateFloor(floorMaterial);

            GameObject agent = CreateAgent(agentMaterial, arrowMaterial);
            GameObject goal = CreateGoal(goalMaterial);

            NavigationMetrics metrics = gameObject.AddComponent<NavigationMetrics>();
            metrics.Configure(agent.transform, goal.transform);
            NavigationObservationProvider observationProvider = gameObject.AddComponent<NavigationObservationProvider>();
            observationProvider.Configure(metrics, floorSize.magnitude);
            NavigationDebugOverlay debugOverlay = gameObject.AddComponent<NavigationDebugOverlay>();
            debugOverlay.Configure(metrics, observationProvider);

            NavigationAgent navigationAgent = agent.GetComponent<NavigationAgent>();
            navigationAgent.Configure(
                goal.transform,
                agent.GetComponent<Rigidbody>(),
                agent.GetComponent<AgentMotor>(),
                observationProvider,
                AgentStartPosition,
                AgentStartRotation,
                GoalStartPosition);

            CreateBoundaryWalls(wallMaterial, navigationAgent);
            CreateInnerWalls(wallMaterial, navigationAgent);

            GoalReachChecker goalReachChecker = gameObject.AddComponent<GoalReachChecker>();
            goalReachChecker.Configure(navigationAgent, metrics, goalReachRadius);

            PositionCamera();
        }

        private void CreateFloor(Material material)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "MVP 1 Floor";
            floor.transform.SetParent(transform);
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(floorSize.x, 0.1f, floorSize.y);
            floor.GetComponent<Renderer>().sharedMaterial = material;
        }

        private void CreateBoundaryWalls(Material material, INavigationEpisodeEvents episodeEvents)
        {
            float halfWidth = floorSize.x * 0.5f;
            float halfDepth = floorSize.y * 0.5f;

            CreateWall("MVP 1 Wall North", new Vector3(0f, wallHeight * 0.5f, halfDepth), new Vector3(floorSize.x, wallHeight, wallThickness), material, episodeEvents);
            CreateWall("MVP 1 Wall South", new Vector3(0f, wallHeight * 0.5f, -halfDepth), new Vector3(floorSize.x, wallHeight, wallThickness), material, episodeEvents);
            CreateWall("MVP 1 Wall East", new Vector3(halfWidth, wallHeight * 0.5f, 0f), new Vector3(wallThickness, wallHeight, floorSize.y), material, episodeEvents);
            CreateWall("MVP 1 Wall West", new Vector3(-halfWidth, wallHeight * 0.5f, 0f), new Vector3(wallThickness, wallHeight, floorSize.y), material, episodeEvents);
        }

        private void CreateInnerWalls(Material material, INavigationEpisodeEvents episodeEvents)
        {
            CreateWall("MVP 1 Inner Wall A", new Vector3(-1.5f, wallHeight * 0.5f, -1.2f), new Vector3(0.35f, wallHeight, 4.2f), material, episodeEvents);
            CreateWall("MVP 1 Inner Wall B", new Vector3(2.8f, wallHeight * 0.5f, 1.6f), new Vector3(4.4f, wallHeight, 0.35f), material, episodeEvents);
        }

        private GameObject CreateAgent(Material material, Material arrowMaterial)
        {
            GameObject agent = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            agent.name = "MVP 1 Agent";
            agent.transform.SetParent(transform);
            agent.transform.SetPositionAndRotation(AgentStartPosition, AgentStartRotation);
            agent.GetComponent<Renderer>().sharedMaterial = material;

            Rigidbody body = agent.AddComponent<Rigidbody>();
            body.mass = 1f;

            ConfigureBehaviorParameters(agent.AddComponent<BehaviorParameters>());
            agent.AddComponent<AgentMotor>();
            agent.AddComponent<NavigationAgent>();
            ConfigureDecisionRequester(agent.AddComponent<DecisionRequester>());
            CreateDirectionArrow(agent.transform, arrowMaterial);

            return agent;
        }

        private void ConfigureBehaviorParameters(BehaviorParameters behaviorParameters)
        {
            behaviorParameters.BehaviorName = behaviorName;
            behaviorParameters.BehaviorType = BehaviorType.Default;
            behaviorParameters.BrainParameters.VectorObservationSize = NavigationObservation.ValueCount;
            behaviorParameters.BrainParameters.NumStackedVectorObservations = 1;
            behaviorParameters.BrainParameters.ActionSpec = ActionSpec.MakeContinuous(2);
        }

        private static void ConfigureDecisionRequester(DecisionRequester decisionRequester)
        {
            decisionRequester.DecisionPeriod = 5;
            decisionRequester.TakeActionsBetweenDecisions = true;
        }

        private static void CreateDirectionArrow(Transform agent, Material material)
        {
            GameObject arrow = new("MVP 1 Agent Direction Arrow");
            arrow.transform.SetParent(agent);
            arrow.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            arrow.transform.localRotation = Quaternion.identity;
            arrow.transform.localScale = Vector3.one;

            Mesh mesh = new()
            {
                name = "MVP 1 Agent Direction Arrow Mesh",
                vertices = new[]
                {
                    new Vector3(0f, 0f, 0.55f),
                    new Vector3(-0.35f, 0f, -0.35f),
                    new Vector3(0.35f, 0f, -0.35f),
                    new Vector3(0f, 0f, 0.55f),
                    new Vector3(0.35f, 0f, -0.35f),
                    new Vector3(-0.35f, 0f, -0.35f),
                },
                triangles = new[]
                {
                    0, 1, 2,
                    3, 4, 5,
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
            goal.name = "MVP 1 Goal";
            goal.transform.SetParent(transform);
            goal.transform.position = GoalStartPosition;
            goal.transform.localScale = Vector3.one;
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

        private static Material CreateMaterial(string materialName, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
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
    }
}

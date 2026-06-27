using UnityEngine;

public class UdonPathfindVisualizerMono : MonoBehaviour
{
    private static readonly Color WALL_COLOR = new Color(1f, 0.25f, 0.25f, 1f);
    private static readonly Color PATH_COLOR = new Color(1f, 0.82f, 0.4f, 1f);
    private static readonly Color START_MARKER_COLOR = new Color(0.02f, 0.84f, 0.63f, 1f);
    private static readonly Color GOAL_MARKER_COLOR = new Color(0.94f, 0.28f, 0.44f, 1f);
    private static readonly Color BORDER_COLOR = new Color(0.2f, 0.2f, 0.33f, 0.3f);

    public UdonPathfindingManager manager;

    private Mesh cubeMesh;
    private Mesh sphereMesh;
    private Material wallMat;
    private Material startMat;
    private Material goalMat;

    private Matrix4x4[] wallMatrices;
    private int wallCount;
    private bool wallInstancesInitialized;

    private GameObject startMarkerObj;
    private GameObject goalMarkerObj;
    private LineRenderer pathLineObj;

    private Vector3[] lastWaypoints;

    private const int MAX_INSTANCES_PER_DRAW = 1023;

    void Start()
    {
        if (manager == null)
        {
            Debug.LogWarning("UdonPathfindVisualizer: manager is not assigned");
            enabled = false;
            return;
        }

        CreateMeshes();
        CreateMaterials();
        StartCoroutine(InitializeWallInstancesCoroutine());
        CreateMarkers();
        CreateBorderFrame();
    }

    private void CreateMeshes()
    {
        var cubeGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cubeMesh = cubeGO.GetComponent<MeshFilter>().sharedMesh;
        Destroy(cubeGO);

        var sphereGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphereMesh = sphereGO.GetComponent<MeshFilter>().sharedMesh;
        Destroy(sphereGO);
    }

    private Material CreateMaterial(Color color, int renderQueue = 3000)
    {
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        mat.enableInstancing = true;
        mat.SetFloat("_Glossiness", 0.1f);
        mat.SetFloat("_Metallic", 0f);

        if (color.a >= 1f)
        {
            mat.SetFloat("_Mode", 0f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetInt("_ZWrite", 1);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = renderQueue;
        }
        else
        {
            mat.SetFloat("_Mode", 3f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = renderQueue;
        }
        return mat;
    }

    private void CreateMaterials()
    {
        wallMat = CreateMaterial(WALL_COLOR, 2900);
        startMat = CreateMaterial(START_MARKER_COLOR, 3100);
        goalMat = CreateMaterial(GOAL_MARKER_COLOR, 3100);
    }

    private bool IsGridDataValid()
    {
        if (manager == null) return false;
        int[] v2l = manager.voxelToLeaf;
        if (v2l == null) return false;
        int expected = manager.gridSizeX * manager.gridSizeY * manager.gridSizeZ;
        return v2l.Length == expected;
    }

    private System.Collections.IEnumerator InitializeWallInstancesCoroutine()
    {
        int attempts = 0;
        while (!IsGridDataValid() && attempts < 60)
        {
            attempts++;
            yield return null;
        }

        if (IsGridDataValid())
        {
            CreateWallInstances();
            wallInstancesInitialized = true;
        }
        else
        {
            Debug.LogWarning("UdonPathfindVisualizer: manager voxel data is not valid after waiting");
        }
    }

    private void CreateWallInstances()
    {
        int gsX = manager.gridSizeX;
        int gsY = manager.gridSizeY;
        int gsZ = manager.gridSizeZ;
        float cs = manager.cellSize;
        Vector3 origin = manager.gridOrigin;

        int[] v2l = manager.voxelToLeaf;
        if (v2l == null) return;

        var positions = new System.Collections.Generic.List<Matrix4x4>();
        for (int z = 0; z < gsZ; z++)
        {
            for (int y = 0; y < gsY; y++)
            {
                for (int x = 0; x < gsX; x++)
                {
                    int vi = x + y * gsX + z * gsX * gsY;
                    if (vi >= 0 && vi < v2l.Length && v2l[vi] == -1)
                    {
                        Vector3 pos = origin + new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) * cs;
                        positions.Add(Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one * cs * 0.95f));
                    }
                }
            }
        }
        wallCount = positions.Count;
        wallMatrices = positions.ToArray();
    }

    private void CreateMarkers()
    {
        float markerScale = manager.cellSize * 0.5f;

        startMarkerObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(startMarkerObj.GetComponent<Collider>());
        startMarkerObj.transform.localScale = Vector3.one * markerScale;
        startMarkerObj.GetComponent<Renderer>().material = startMat;
        startMarkerObj.SetActive(false);

        goalMarkerObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(goalMarkerObj.GetComponent<Collider>());
        goalMarkerObj.transform.localScale = Vector3.one * markerScale;
        goalMarkerObj.GetComponent<Renderer>().material = goalMat;
        goalMarkerObj.SetActive(false);
    }

    private void CreateBorderFrame()
    {
        int gsX = manager.gridSizeX;
        int gsY = manager.gridSizeY;
        int gsZ = manager.gridSizeZ;
        float cs = manager.cellSize;
        Vector3 origin = manager.gridOrigin;

        Vector3 size = new Vector3(gsX * cs, gsY * cs, gsZ * cs);
        Vector3 max = origin + size;
        Vector3 center = origin + size * 0.5f;

        var border = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(border.GetComponent<Collider>());
        border.transform.position = center;
        border.transform.localScale = size;
        border.GetComponent<Renderer>().material = CreateMaterial(BORDER_COLOR, 2500);

        var wireGO = new GameObject("Wireframe");
        var lr = wireGO.AddComponent<LineRenderer>();
        lr.positionCount = 24;
        lr.startWidth = 0.03f * cs;
        lr.endWidth = 0.03f * cs;
        lr.material = new Material(Shader.Find("Unlit/Color"));
        lr.material.color = new Color(0.2f, 0.2f, 0.33f, 0.6f);

        Vector3[] corners = new Vector3[]
        {
            origin,
            new Vector3(max.x, origin.y, origin.z),
            new Vector3(max.x, origin.y, max.z),
            new Vector3(origin.x, origin.y, max.z),
            new Vector3(origin.x, max.y, origin.z),
            new Vector3(max.x, max.y, origin.z),
            max,
            new Vector3(origin.x, max.y, max.z),
        };

        Vector3[] lines = new Vector3[]
        {
            corners[0], corners[1], corners[1], corners[2], corners[2], corners[3], corners[3], corners[0],
            corners[4], corners[5], corners[5], corners[6], corners[6], corners[7], corners[7], corners[4],
            corners[0], corners[4], corners[1], corners[5], corners[2], corners[6], corners[3], corners[7],
        };
        lr.SetPositions(lines);
    }

    private void LateUpdate()
    {
        if (!wallInstancesInitialized) return;
        RenderInstanced(wallMatrices, wallCount, wallMat);
    }

    private void Update()
    {
        if (manager == null) return;

        UpdatePathLine(manager.waypoints);
        UpdateMarkers();
    }

    private void RenderInstanced(Matrix4x4[] matrices, int count, Material mat)
    {
        if (count == 0) return;
        int offset = 0;
        while (offset < count)
        {
            int batch = Mathf.Min(MAX_INSTANCES_PER_DRAW, count - offset);
            Matrix4x4[] batchMatrices = new Matrix4x4[batch];
            for (int i = 0; i < batch; i++) batchMatrices[i] = matrices[offset + i];
            Graphics.DrawMeshInstanced(cubeMesh, 0, mat, batchMatrices, batch);
            offset += batch;
        }
    }

    private void UpdateMarkers()
    {
        if (manager.startLeafIdx >= 0)
        {
            startMarkerObj.transform.position = manager.startWorld;
            startMarkerObj.SetActive(true);
        }
        else
        {
            startMarkerObj.SetActive(false);
        }

        if (manager.goalLeafIdx >= 0)
        {
            goalMarkerObj.transform.position = manager.goalWorld;
            goalMarkerObj.SetActive(true);
        }
        else
        {
            goalMarkerObj.SetActive(false);
        }
    }

    private void UpdatePathLine(Vector3[] waypoints)
    {
        if (lastWaypoints == waypoints) return;
        lastWaypoints = waypoints;

        if (pathLineObj != null)
        {
            Destroy(pathLineObj.gameObject);
            pathLineObj = null;
        }

        if (waypoints == null || waypoints.Length < 2) return;

        GameObject go = new GameObject("PathLine");
        pathLineObj = go.AddComponent<LineRenderer>();
        pathLineObj.positionCount = waypoints.Length;
        pathLineObj.startWidth = 0.15f * manager.cellSize;
        pathLineObj.endWidth = 0.15f * manager.cellSize;
        pathLineObj.material = new Material(Shader.Find("Unlit/Color"));
        pathLineObj.material.color = PATH_COLOR;

        for (int i = 0; i < waypoints.Length; i++)
        {
            pathLineObj.SetPosition(i, waypoints[i]);
        }
    }

    private void OnDestroy()
    {
        if (wallMat != null) Destroy(wallMat);
        if (startMat != null) Destroy(startMat);
        if (goalMat != null) Destroy(goalMat);
    }
}

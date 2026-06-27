using UdonSharp;
using UnityEngine;
using TMPro;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class UdonPathfindDemo : UdonSharpBehaviour
{
    [Header("Pathfinding")]
    public UdonPathfindingManager manager;
    public Vector3 start = new Vector3(1f, 1f, 1f);
    public Vector3 goal = new Vector3(14f, 14f, 14f);

    [Header("Auto Trigger")]
    public bool triggerOnStart = true;
    public int startDelayFrames = 60;

    [Header("Manual Trigger")]
    public KeyCode manualTriggerKey = KeyCode.Space;

    [Header("Queue Test")]
    public bool testQueue = false;

    [Header("Follow Movement")]
    public bool followOnPathFound = true;
    public float moveSpeed = 3f;
    public bool useSimpleFollow = false;

    private int frameCount;
    private bool triggered;
    private int demoWaypointIndex;
    private int followIndex = -1;
    private bool followDone;
    private bool pathReady;

    [SerializeField]
    TMPro.TMP_Text debugText;
    void Start()
    {
        if (manager != null)
        {
            manager.resultReceiver = this;
        }
        else
        {
            Debug.LogWarning("[UdonPathfindDemo] manager is not assigned");
        }
    }

    void Update()
    {
        if (manager == null) return;

        if (triggerOnStart && !triggered)
        {
            frameCount++;
            if (frameCount >= startDelayFrames)
            {
                triggered = true;
                RequestDemoPath();
            }
        }

        if (followOnPathFound && pathReady && !followDone)
        {
            UpdateFollowMovement();
        }
    }

    private void UpdateFollowMovement()
    {
        Vector3[] wps = manager.waypoints;
        if (wps == null || wps.Length == 0) return;

        Vector3 pos = transform.position;
        Vector3 target;

        if (useSimpleFollow)
        {
            target = manager.GetFollowTarget(wps, pos);
            float distToGoal = Vector3.Distance(pos, wps[wps.Length - 1]);
            if (distToGoal < manager.GetCellSize() * 0.5f)
            {
                followDone = true;
                Debug.Log("[UdonPathfindDemo] (simple) Reached goal");
                debugText.text = "(simple) Reached goal";
                return;
            }
        }
        else
        {
            float snapDist = manager.GetCellSize() * 0.5f;
            float reachDist = manager.GetCellSize() * 0.5f;
            bool reached;
            target = manager.GetFollowTarget(wps, pos, followIndex, snapDist, reachDist, out followIndex, out reached);
            if (reached)
            {
                followDone = true;
                Debug.Log(string.Format("[UdonPathfindDemo] Reached goal at {0}", pos));
                debugText.text = string.Format("Reached goal at {0}", pos);
                return;
            }
        }

        Vector3 dir = target - pos;
        float dist = dir.magnitude;
        if (dist < 0.001f) return;

        Vector3 move = dir.normalized * moveSpeed * Time.deltaTime;
        if (move.magnitude > dist) move = dir;
        transform.position = pos + move;
    }

    public void RequestDemoPath()
    {
        if (manager == null)
        {
            Debug.LogWarning("[UdonPathfindDemo] manager is not assigned");
            return;
        }

        if (testQueue)
        {
            Debug.Log("[UdonPathfindDemo] Requesting 3 queued paths");
            manager.RequestPath(start, goal);
            manager.RequestPath(start, new Vector3(goal.x, goal.y, 1f));
            manager.RequestPath(new Vector3(goal.x, 1f, goal.z), goal);
        }
        else
        {
            Debug.Log(string.Format("[UdonPathfindDemo] Requesting path: {0} -> {1}", start, goal));
            debugText.text = string.Format("[UdonPathfindDemo] Requesting path: {0} -> {1}", start, goal);
            manager.RequestPath(start, goal);
        }
    }

    public void OnPathFound()
    {
        if (manager == null) return;

        Vector3[] wps = manager.waypoints;
        int wc = (wps != null) ? wps.Length : 0;
        Debug.Log(string.Format("[UdonPathfindDemo] Path found! waypoints={0}", wc));

        followIndex = -1;
        followDone = false;
        pathReady = true;

        for (int i = 0; i < wc; i++)
        {
            Debug.Log(string.Format("[UdonPathfindDemo] waypoint[{0}] = {1}", i, wps[i]));
        }

        demoWaypointIndex = manager.ResetWaypointProgress(wc);
        float pathLen = manager.GetPathLength(wps);

        Debug.Log(string.Format("[UdonPathfindDemo] pathLength={0:F2} remaining={1:F2} progress={2:F2}",
            pathLen, manager.GetRemainingDistance(wps, demoWaypointIndex), manager.GetProgress(wps, demoWaypointIndex)));
        Debug.Log(string.Format("[UdonPathfindDemo] currentIdx={0} currentWaypoint={1}",
            demoWaypointIndex, manager.GetCurrentWaypoint(wps, demoWaypointIndex)));

        Vector3 probe = wps[wc > 1 ? 1 : 0];
        Debug.Log(string.Format("[UdonPathfindDemo] closestPointTo({0}) = {1} closestIdx={2}",
            probe, manager.GetClosestPointOnPath(wps, probe), manager.GetClosestWaypointIndex(wps, probe)));

        Vector3[] resampled = manager.ResamplePath(wps, manager.GetCellSize() * 0.5f);
        Debug.Log(string.Format("[UdonPathfindDemo] resampled (spacing=0.5*cell) count={0}", resampled.Length));

        int next = manager.AdvanceWaypoint(demoWaypointIndex, wc);
        Debug.Log(string.Format("[UdonPathfindDemo] advanceWaypoint({0}) -> {1}", demoWaypointIndex, next));
        if (next >= 0)
        {
            Debug.Log(string.Format("[UdonPathfindDemo] nextWaypoint={0}", manager.GetCurrentWaypoint(wps, next)));
        }

        debugText.text = string.Format("Path found! waypoints={0} length={1:F1} progress={2:F2}",
            wc, pathLen, manager.GetProgress(wps, demoWaypointIndex));
    }

    public void OnPathFailed()
    {
        if (manager == null) return;

        Debug.LogWarning(string.Format("[UdonPathfindDemo] Path failed: {0}", manager.pathError));
        debugText.text = string.Format("[UdonPathfindDemo] Path failed: {0}", manager.pathError);
    }

    public override void Interact()
    {
        Debug.Log("[UdonPathfindDemo] Interact called, requesting path");
        RequestDemoPath();
    }
}

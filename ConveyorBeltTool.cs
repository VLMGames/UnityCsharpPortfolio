using System.Collections.Generic;
using System.Threading.Tasks;
using Core;
using Core.Localization;
using Electricity.ConveyorSystem;
using Inventory;
using Message;
using UnityEngine;
using UnityEngine.Splines;

namespace Electricity.ConveyorBelt
{
    public class ConveyorBeltTool : SingletonGObj<ConveyorBeltTool>
    {
        [Header("Settings")]
        public GameObject splineConveyorPrefab;
        [SerializeField] private float detectingRange = 25f;
        [SerializeField] private float sphereRadius = 0.35f;
        [SerializeField] private LayerMask obstacleMask = ~0;

        [Header("Snapping")]
        [SerializeField] private float snapRadius = 1f;

        [Header("Placement")]
        [SerializeField] private LineRenderer conveyorPreviewPrefab;
        [SerializeField] private float verticalOffset = 0.1f;
        [SerializeField] private float maxHeightFromGround = 1.5f;
        [SerializeField] private float minDistance = 1.0f;
        [SerializeField] private float clearanceRadius = 1.0f;
        [SerializeField] private float maxTurnAngle = 60f;

        [SerializeField] private Material validMaterial;
        [SerializeField] private Material invalidMaterial;
        [SerializeField] private Color validColor = Color.green;
        [SerializeField] private Color invalidColor = Color.red;

        private Camera _mainCamera;
        private List<Vector3> _pathPoints = new List<Vector3>();
        private bool _isActive;
        private Vector3 _currentHitPoint;
        private LineRenderer _conveyorPreview;

        private void Start()
        {
            _mainCamera = Camera.main;
            if (InventoryManager.Instance != null)
                InventoryManager.Instance.onActiveSlotChanged += OnActiveSlotChanged;

            _conveyorPreview = Instantiate(conveyorPreviewPrefab);
            _conveyorPreview.enabled = false;
        }

        private void OnActiveSlotChanged()
        {
            _isActive = InventoryManager.Instance.GetActiveSlot()?.item?.itemSO is ConveyorBeltItemSO;
            if (!_isActive) ResetTool();
            else _conveyorPreview.enabled = true;
        }

        private void Update()
        {
            if (!_isActive) return;

            GetClickPoint(out Vector3 hit);
            _currentHitPoint = SnapToGround(hit);
            _currentHitPoint = CheckPortSnapping(_currentHitPoint, out bool isSnappedToInput);

            UpdatePreview(isSnappedToInput);
            HandleInput(isSnappedToInput);
        }

        private void UpdatePreview(bool isSnappedToInput)
        {
            if (_pathPoints.Count == 0)
            {
                _conveyorPreview.positionCount = 0;
                return;
            }

            Vector3 lastPoint = _pathPoints[^1];
            bool invalid = IsPathBlocked(lastPoint, _currentHitPoint);

            _conveyorPreview.enabled = true;
            _conveyorPreview.positionCount = _pathPoints.Count + 1;

            for (int i = 0; i < _pathPoints.Count; i++)
                _conveyorPreview.SetPosition(i, _pathPoints[i]);

            _conveyorPreview.SetPosition(_pathPoints.Count, _currentHitPoint);
            _conveyorPreview.material = invalid ? invalidMaterial : validMaterial;
        }

        private void HandleInput(bool isSnappedToInput)
        {
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                if (_pathPoints.Count > 1) _pathPoints.RemoveAt(_pathPoints.Count - 1);
                else ResetTool();
            }

            bool invalid = IsPathBlocked(_pathPoints.Count > 0 ? _pathPoints[^1] : Vector3.zero, _currentHitPoint);

            if (Input.GetMouseButtonDown(0) && _currentHitPoint != Vector3.zero && !invalid)
            {
                if (TryAddPoint())
                {
                    if (isSnappedToInput && _pathPoints.Count > 1)
                    {
                        CreateSplineConveyor(_pathPoints);
                        ResetTool();
                    }
                }
            }

            if (Input.GetMouseButtonDown(1) && _pathPoints.Count > 1)
            {
                CreateSplineConveyor(_pathPoints);
                ResetTool();
            }
        }

        private Vector3 CheckPortSnapping(Vector3 currentPoint, out bool isSnappedToInput)
        {
            isSnappedToInput = false;
            Collider[] colliders = Physics.OverlapSphere(currentPoint, snapRadius, obstacleMask);
            ConveyorPort closestPort = null;
            float minDistance = float.MaxValue;

            foreach (var col in colliders)
            {
                ConveyorPort port = col.GetComponentInParent<ConveyorPort>();
                if (port == null) continue;

                bool isStart = _pathPoints.Count == 0;
                if (isStart && port.portType != ConveyorPort.PortType.Output) continue;
                if (!isStart && port.portType != ConveyorPort.PortType.Input) continue;

                float dist = Vector3.Distance(currentPoint, port.SnapPosition);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closestPort = port;
                }
            }

            if (closestPort != null && closestPort.portType == ConveyorPort.PortType.Input)
                isSnappedToInput = true;

            return closestPort != null ? closestPort.SnapPosition : currentPoint;
        }

        private bool TryAddPoint()
        {
            if (_pathPoints.Count > 0)
            {
                float dist = Vector3.Distance(_pathPoints[^1], _currentHitPoint);
                if (dist < minDistance)
                {
                    MessageManager.Instance.ThrowMessage(L.T("Nodes are too close.", "nodesTooClose"));
                    return false;
                }
            }

            if (IsAngleTooSharp(_currentHitPoint))
            {
                MessageManager.Instance.ThrowMessage(L.T("Turn angle is too sharp.", "angleTooSharp"));
                return false;
            }

            if (IsTooCloseToPath(_pathPoints.Count > 0 ? _pathPoints[^1] : Vector3.zero, _currentHitPoint))
            {
                MessageManager.Instance.ThrowMessage(L.T("Conveyor cannot intersect itself.", "ConveyorCannotIntersectItself"));
                return false;
            }

            _pathPoints.Add(_currentHitPoint);
            return true;
        }

        public async void CreateSplineConveyor(List<Vector3> points)
        {
            InventoryManager.Instance.RemoveItemAtActiveSlot(1);
            GameObject conveyorObj = Instantiate(splineConveyorPrefab);
            await SetupConveyorComponents(conveyorObj, points);
        }

        public async void LoadSplineConveyor(GameObject loadedObj, List<Vector3> points)
        {
            await SetupConveyorComponents(loadedObj, points);
        }

        private async Task SetupConveyorComponents(GameObject conveyorObj, List<Vector3> points)
        {
            List<Vector3> localPoints = new List<Vector3>(points);
            SplineContainer container = conveyorObj.GetComponent<SplineContainer>();
            Spline newSpline = new Spline();

            foreach (Vector3 p in localPoints)
                newSpline.Add(new BezierKnot(container.transform.InverseTransformPoint(p)), TangentMode.AutoSmooth);
            
            container.Spline = newSpline;

            conveyorObj.GetComponent<SplineMeshTools.Core.SplineMeshResolution>()?.GenerateMeshAlongSpline();

            MeshFilter filter = conveyorObj.GetComponent<MeshFilter>();
            MeshCollider collider = conveyorObj.GetComponent<MeshCollider>();

            if (filter?.sharedMesh != null && collider != null)
            {
                int meshID = filter.sharedMesh.GetInstanceID();
                await Task.Run(() => Physics.BakeMesh(meshID, false));
                collider.sharedMesh = filter.sharedMesh;
            }

            var logic = conveyorObj.GetComponent<ConveyorSystem.ConveyorBelt>();
            if (logic != null)
            {
                logic.Init(container, localPoints);
                ConnectPort(localPoints[0], logic, ConveyorPort.PortType.Output);
                ConnectPort(localPoints[^1], logic, ConveyorPort.PortType.Input);
            }
        }

        private void ConnectPort(Vector3 pos, ConveyorSystem.ConveyorBelt logic, ConveyorPort.PortType type)
        {
            Collider[] cols = Physics.OverlapSphere(pos, 0.5f, ~0, QueryTriggerInteraction.Collide);
            foreach (var col in cols)
            {
                ConveyorPort port = col.GetComponentInParent<ConveyorPort>();
                if (port != null && port.portType == type)
                {
                    port.connectedNode = logic;
                    break;
                }
            }
        }

        private bool IsPathBlocked(Vector3 from, Vector3 to)
        {
            if (from == Vector3.zero) return false;
            float dist = Vector3.Distance(from, to);
            if (Physics.SphereCast(from + Vector3.up * 0.5f, sphereRadius, (to - from).normalized, out _, dist, obstacleMask, QueryTriggerInteraction.Ignore))
                return true;

            int steps = Mathf.CeilToInt(dist);
            for (int i = 1; i < steps; i++)
            {
                Vector3 p = Vector3.Lerp(from, to, (float)i / steps);
                if (!Physics.Raycast(p + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 20f, obstacleMask, QueryTriggerInteraction.Ignore)) return true;
                if (hit.point.y > p.y + 0.2f || p.y - hit.point.y > maxHeightFromGround) return true;
            }
            return false;
        }

        private Vector3 SnapToGround(Vector3 point)
        {
            return Physics.Raycast(point + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 5f, obstacleMask, QueryTriggerInteraction.Ignore) 
                ? hit.point + Vector3.up * verticalOffset : point;
        }

        private void GetClickPoint(out Vector3 point)
        {
            Ray ray = _mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            point = Physics.Raycast(ray, out RaycastHit hit, detectingRange, obstacleMask, QueryTriggerInteraction.Ignore) 
                ? hit.point : _mainCamera.transform.position + (_mainCamera.transform.forward * 5f);
        }

        private void ResetTool()
        {
            _pathPoints.Clear();
            _conveyorPreview.enabled = false;
        }

        private float GetDistToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector3.Distance(p, a + t * ab);
        }

        private bool IsTooCloseToPath(Vector3 start, Vector3 end)
        {
            if (_pathPoints.Count < 2) return false;
            int samples = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(start, end) / (clearanceRadius * 0.5f)));

            for (int i = 0; i < _pathPoints.Count - 2; i++)
            {
                for (int j = 0; j <= samples; j++)
                {
                    if (GetDistToSegment(Vector3.Lerp(start, end, (float)j / samples), _pathPoints[i], _pathPoints[i + 1]) < clearanceRadius)
                        return true;
                }
            }
            return false;
        }

        private bool IsAngleTooSharp(Vector3 point)
        {
            if (_pathPoints.Count < 2) return false;
            Vector3 d1 = (_pathPoints[^1] - _pathPoints[^2]).normalized;
            Vector3 d2 = (point - _pathPoints[^1]).normalized;
            d1.y = d2.y = 0;
            return Vector3.Angle(d1, d2) > maxTurnAngle;
        }

        private void OnDrawGizmos()
        {
            if (!_isActive || _pathPoints.Count == 0) return;
            Gizmos.color = validColor;
            for (int i = 0; i < _pathPoints.Count - 1; i++)
            {
                Gizmos.DrawSphere(_pathPoints[i], 0.15f);
                Gizmos.DrawLine(_pathPoints[i], _pathPoints[i + 1]);
            }
            Gizmos.color = IsPathBlocked(_pathPoints[^1], _currentHitPoint) ? invalidColor : validColor;
            Gizmos.DrawLine(_pathPoints[^1], _currentHitPoint);
        }
    }
}
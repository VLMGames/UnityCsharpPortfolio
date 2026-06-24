using System.Collections;
using System.Collections.Generic;
using BuildingSystem;
using Electricity.ConveyorBelt;
using Electricity.Units.ConveyorNodes;
using Inventory;
using SaveSystem;
using Units;
using UnityEngine;
using UnityEngine.Splines;

namespace Electricity.ConveyorSystem
{
    public class ConveyorBelt : MonoBehaviour, IConveyorNode, IDestructibleObject, ISavedUnit
    {
        [SerializeField] private float speed = 2.0f;
        [SerializeField] private Vector3 offsetPos;
        [SerializeField] private float searchRadius = 1.5f;
        [SerializeField] private ItemSO itemSO;

        private Dictionary<UnitOnGround, float> _unitsOnConveyer = new Dictionary<UnitOnGround, float>();
        private SplineContainer _container;
        private List<Vector3> _points;

        public void Init(SplineContainer container, List<Vector3> points)
        {
            _container = container;
            _points = points;
            SaveUtility.Register(this);
        }

        private void Update()
        {
            var units = new List<UnitOnGround>(_unitsOnConveyer.Keys);
            float length = _container.CalculateLength();

            foreach (var unit in units)
            {
                if (unit == null) { _unitsOnConveyer.Remove(unit); continue; }

                _unitsOnConveyer[unit] += (speed / length) * Time.deltaTime;
                unit.transform.position = (Vector3)_container.EvaluatePosition(_unitsOnConveyer[unit]) + offsetPos;

                Vector3 forward = (Vector3)_container.EvaluateTangent(_unitsOnConveyer[unit]);
                if (forward != Vector3.zero) unit.transform.rotation = Quaternion.LookRotation(forward);

                if (_unitsOnConveyer[unit] >= 1.0f)
                {
                    _unitsOnConveyer.Remove(unit);
                    TransferItemToNextNode(unit);
                }
            }
        }

        private void TransferItemToNextNode(UnitOnGround item)
        {
            var cols = Physics.OverlapSphere(item.transform.position, searchRadius);
            IConveyorNode closestNode = null;
            float minDistance = float.MaxValue;

            foreach (var col in cols)
            {
                IConveyorNode node = col.GetComponentInParent<IConveyorNode>();
                if (node != null && (Object)node != this && node.CanReceiveConveyorItem())
                {
                    float dist = Vector3.Distance(item.transform.position, col.transform.position);
                    if (dist < minDistance) { minDistance = dist; closestNode = node; }
                }
            }

            if (closestNode != null)
            {
                var leftover = closestNode.ReceiveConveyorItem(item);
                if (leftover?.item != null && leftover.count > 0)
                {
                    item.itemSO = leftover.item;
                    item.amount = leftover.count;
                    item.canPickUp = true;
                }
                else Destroy(item.gameObject);
            }
            else item.canPickUp = true;
        }

        public bool CanReceiveConveyorItem() => true;

        public ItemRequirement ReceiveConveyorItem(UnitOnGround unit)
        {
            _unitsOnConveyer.Add(unit, 0f);
            return new ItemRequirement();
        }

        private void OnDestroy() => SaveUtility.Unregister(this);

        public IEnumerator OnDestroyUnitCustom()
        {
            Destroy(gameObject);
            foreach (var unit in _unitsOnConveyer.Keys) if (unit) Destroy(unit.gameObject);
            yield break;
        }

        public Dictionary<ItemSO, int> GetDestroyItems()
        {
            var dict = new Dictionary<ItemSO, int> { { itemSO, 1 } };
            foreach (var unit in _unitsOnConveyer.Keys)
            {
                if (dict.ContainsKey(unit.itemSO)) dict[unit.itemSO] += unit.amount;
                else dict.Add(unit.itemSO, unit.amount);
            }
            return dict;
        }

        public UnitData GetSave()
        {
            UnitData data = SaveUtility.CreateSaveData(itemSO, transform);
            data.conveyerBeltPoints = _points.ConvertAll(p => new PosData(p.x, p.y, p.z));
            return data;
        }

        public void LoadSaveUnit(UnitData data)
        {
            List<Vector3> points = data.conveyerBeltPoints.ConvertAll(p => new Vector3(p.posX, p.posY, p.posZ));
            ConveyorBeltTool.Instance.LoadSplineConveyor(gameObject, points);
        }
    }
}
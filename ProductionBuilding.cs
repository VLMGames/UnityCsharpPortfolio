using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using Datas;
using Electricity;
using Electricity.ConveyorBelt;
using Electricity.Units.ConveyorNodes;
using Inventory;
using SaveSystem;
using Units;
using UnityEngine;

namespace BuildingSystem
{
    public class ProductionBuilding : ElectricDevice, IConveyorNode, ISavedUnit, IDestructibleObject
    {
        [Header("Building")]
        public List<BuildingRecipe> recipes;
        public float timeOneItem;

        [Header("Conveyor Ports")]
        private ConveyorPort _inputPort;
        private ConveyorPort _outputPort;
        [SerializeField] private float exportCooldown = 1.0f;

        [Header("Animation")]
        [SerializeField] protected Animator animator;
        protected static readonly int StartKey = Animator.StringToHash("StartDo");
        protected static readonly int StopKey = Animator.StringToHash("OnStop");
        protected static readonly int DoKey = Animator.StringToHash("OnDo");

        protected Action onStartDo;
        protected Action onDo;
        protected Action onEndDo;

        private float _currentTimeOneItem;
        private List<ItemRequirement> _currentInputItems = new List<ItemRequirement>();
        private ItemRequirement _currentOutputItem;
        private float _exportTimer;
        private bool _isActive;
        private BuildingRecipe _currentRecipe;
        private bool _isInitialize;

        public List<ItemRequirement> currentInputItems => _currentInputItems;
        public ItemRequirement currentOutputItem => _currentOutputItem;
        public bool isActive => _isActive;

        public override void Awake()
        {
            base.Awake();
            foreach (var port in GetComponentsInChildren<ConveyorPort>())
            {
                if (port.portType == ConveyorPort.PortType.Input) _inputPort = port;
                else if (port.portType == ConveyorPort.PortType.Output) _outputPort = port;
            }
            Initialize();
        }

        private void SetWantEnergy(int amount)
        {
            if (inputPorts != null && inputPorts.Count > 0)
                inputPorts[0].SetWantEnergy(amount);
        }

        protected override void OnDevicePowerStateChanged(bool newState)
        {
            base.OnDevicePowerStateChanged(newState);
            OnRefresh();
        }

        public void SetValues(List<ItemRequirement> inputItems, ItemRequirement outputItem)
        {
            if (inputItems.Count > _currentInputItems.Count) return;
            _currentInputItems = inputItems;
            _currentOutputItem = outputItem;
            OnRefresh();
        }

        protected override void OnRefresh()
        {
            bool canUpdate = CanUpdate();
            _isActive = isDeviceActive && canUpdate;

            if (!_isActive)
                onEndDo?.Invoke();

            if (canUpdate)
            {
                if (_isActive) onStartDo?.Invoke();
                SetDefaultWantEnergy();
            }
            else
            {
                onEndDo?.Invoke();
                SetWantEnergy(0);
            }
        }

        private void Initialize(List<ItemRequirement> inputItems = null, ItemRequirement outputItem = null)
        {
            if (_isInitialize) return;
            _isInitialize = true;

            if (inputItems != null && inputItems.Count > 0)
            {
                _currentInputItems = inputItems;
                _currentOutputItem = outputItem;
            }
            else
            {
                int maxInput = recipes.Any() ? recipes.Max(r => r.inputItems.Count) : 0;
                for (int i = 0; i < maxInput; i++) _currentInputItems.Add(null);
                _currentOutputItem = null;
            }
        }

        private void Update()
        {
            if (_isActive)
            {
                onDo?.Invoke();
                _currentTimeOneItem += Time.deltaTime;
                BuildingManager.Instance.TrySetProgressUI(this, _currentTimeOneItem / timeOneItem);

                if (_currentTimeOneItem >= timeOneItem)
                {
                    _currentTimeOneItem = 0;
                    if (CanUpdate())
                    {
                        foreach (var req in _currentRecipe.inputItems)
                        {
                            var input = _currentInputItems.FirstOrDefault(i => i?.item?.itemId == req.item.itemId);
                            if (input != null)
                            {
                                input.count -= req.count;
                                if (input.count <= 0) input.item = null;
                            }
                        }

                        if (_currentOutputItem == null || _currentOutputItem.item == null)
                            _currentOutputItem = new ItemRequirement(_currentRecipe.outputItem, _currentRecipe.outputCount);
                        else if (_currentOutputItem.item.isStackable)
                            _currentOutputItem.count += _currentRecipe.outputCount;

                        BuildingManager.Instance.TryUpdateUI(this);
                        _isActive = CanUpdate();
                    }
                }
            }
            HandleItemExport();
        }

        private void HandleItemExport()
        {
            if (_currentOutputItem?.item == null || _currentOutputItem.count <= 0) return;
            _exportTimer += Time.deltaTime;
            if (_exportTimer < exportCooldown) return;

            if (_outputPort?.connectedNode != null && _outputPort.connectedNode.CanReceiveConveyorItem())
            {
                GameObject spawnedObj = Instantiate(_currentOutputItem.item.prefabUnit, _outputPort.SnapPosition, _outputPort.transform.rotation);
                UnitOnGround unit = spawnedObj.GetComponent<UnitOnGround>() ?? spawnedObj.AddComponent<UnitOnGround>();
                unit.itemSO = _currentOutputItem.item;
                unit.amount = 1;
                unit.canPickUp = false;

                _outputPort.connectedNode.ReceiveConveyorItem(unit);
                _currentOutputItem.count--;
                if (_currentOutputItem.count <= 0) _currentOutputItem.item = null;

                BuildingManager.Instance.TryUpdateUI(this);
                OnRefresh();
                _exportTimer = 0f;
            }
        }

        private bool CanUpdate()
        {
            foreach (var recipe in recipes)
            {
                bool canFit = true;
                foreach (var req in recipe.inputItems)
                {
                    var found = _currentInputItems.FirstOrDefault(i => i?.item?.itemId == req.item.itemId && i.count >= req.count);
                    if (found == null) { canFit = false; break; }
                }

                if (canFit)
                {
                    if (_currentOutputItem?.item == null)
                    {
                        _currentRecipe = recipe;
                        return true;
                    }
                    if (_currentOutputItem.item.itemId == recipe.outputItem.itemId && _currentOutputItem.item.isStackable)
                    {
                        int free = _currentOutputItem.item.maxStackable - _currentOutputItem.count;
                        if (recipe.outputCount <= free)
                        {
                            _currentRecipe = recipe;
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private void ResetProgress()
        {
            _currentTimeOneItem = 0;
            BuildingManager.Instance.TryResetProgressUI(this);
        }

        public IEnumerator OnDestroyUnitCustom()
        {
            Destroy(gameObject);
            yield break;
        }

        public Dictionary<ItemSO, int> GetDestroyItems()
        {
            var dict = new Dictionary<ItemSO, int>();
            foreach (var input in _currentInputItems.Where(i => i?.item != null))
            {
                if (dict.ContainsKey(input.item)) dict[input.item] += input.count;
                else dict.Add(input.item, input.count);
            }
            if (_currentOutputItem?.item != null) dict.Add(_currentOutputItem.item, _currentOutputItem.count);
            dict.Add(itemSO, 1);
            return dict;
        }

        public UnitData GetSave()
        {
            UnitData data = SaveUtility.CreateSaveData(itemSO, transform);
            data.buildingInputItems = _currentInputItems.Select(i => i?.item != null ? new ItemData(i.item.itemId, i.count) : null).ToList();
            data.buildingOutputItem = _currentOutputItem?.item != null ? new ItemData(_currentOutputItem.item.itemId, _currentOutputItem.count) : null;
            data.buildingCurrentProgress = _currentTimeOneItem / timeOneItem;
            return data;
        }

        public void LoadSaveUnit(UnitData data)
        {
            var inputItems = data.buildingInputItems.Select(i => i != null ? new ItemRequirement(ItemDatabase.Instance.GetItemFromID(i.itemId), i.count) : null).ToList();
            var outputItem = data.buildingOutputItem != null ? new ItemRequirement(ItemDatabase.Instance.GetItemFromID(data.buildingOutputItem.itemId), data.buildingOutputItem.count) : null;
            _currentTimeOneItem = data.buildingCurrentProgress * timeOneItem;
            Initialize(inputItems, outputItem);
        }

        public bool CanReceiveConveyorItem() => _currentInputItems.Any(s => s == null || s.item == null || (s.item.isStackable && s.count < s.item.maxStackable));

        public ItemRequirement ReceiveConveyorItem(UnitOnGround unit)
        {
            ItemSO itemSo = unit.itemSO;
            int remaining = unit.amount;

            if (!recipes.Any(r => r.inputItems.Any(req => req.item?.itemId == itemSo.itemId)))
                return new ItemRequirement(itemSo, remaining);

            foreach (var slot in _currentInputItems.Where(s => s?.item?.itemId == itemSo.itemId && itemSo.isStackable))
            {
                int space = itemSo.maxStackable - slot.count;
                int add = Mathf.Min(remaining, space);
                slot.count += add;
                remaining -= add;
                if (remaining <= 0) break;
            }

            if (remaining > 0)
            {
                for (int i = 0; i < _currentInputItems.Count; i++)
                {
                    if (_currentInputItems[i] == null || _currentInputItems[i].item == null)
                    {
                        int add = itemSo.isStackable ? Mathf.Min(remaining, itemSo.maxStackable) : 1;
                        _currentInputItems[i] = new ItemRequirement(itemSo, add);
                        remaining -= add;
                        if (remaining <= 0) break;
                    }
                }
            }

            OnRefresh();
            BuildingManager.Instance.TryUpdateUI(this);
            return remaining > 0 ? new ItemRequirement(itemSo, remaining) : new ItemRequirement();
        }
    }

    [Serializable]
    public class ItemRequirement
    {
        public ItemSO item;
        public int count;
        public ItemRequirement(ItemSO item, int count) { this.item = item; this.count = count; }
        public ItemRequirement() { }
    }
}
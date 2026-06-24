using System.Collections.Generic;
using Core;
using GamePlay.States;
using Inventory;
using QFSW.QC;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BuildingSystem
{
    public class BuildingManager : SingletonGObj<BuildingManager>
    {
        [SerializeField] private InputActionReference closeBuildingUI;
        private ProductionBuilding _current;
        private bool _isOpened;

        private void Start() => closeBuildingUI.action.started += Close;

        public void Open(ProductionBuilding unit)
        {
            if (_isOpened) return;
            _isOpened = true;
            GameStateManager.Instance.GoToBuildingUI();
            _current = unit;

            int maxInput = unit.recipes.Count > 0 ? 0 : 0;
            foreach (var r in unit.recipes) if (r.inputItems.Count > maxInput) maxInput = r.inputItems.Count;

            BuildingUI.Instance.SpawnInputSlots(maxInput, unit.currentInputItems, unit.currentOutputItem);
            BuildingUI.Instance.ShowUI();
        }

        public void Close(InputAction.CallbackContext ctx = default)
        {
            if (!_isOpened || QuantumConsole.Instance.IsActive) return;

            BuildingUI.Instance.HideUI();
            if (GameStateManager.Instance.GetCurrentState() is BuildingUIState)
                GameStateManager.Instance.GoToGamePlay();
            _isOpened = false;
        }

        public void TryResetProgressUI(ProductionBuilding unit)
        {
            if (_isOpened && _current == unit) BuildingUI.Instance.SetProgress(0);
        }

        public void TrySetProgressUI(ProductionBuilding unit, float progress)
        {
            if (_isOpened && _current == unit) BuildingUI.Instance.SetProgress(progress);
        }

        public void TryUpdateUI(ProductionBuilding unit)
        {
            if (_current != unit) return;
            int maxInput = 0;
            foreach (var r in _current.recipes) if (r.inputItems.Count > maxInput) maxInput = r.inputItems.Count;
            BuildingUI.Instance.SpawnInputSlots(maxInput, _current.currentInputItems, _current.currentOutputItem);
        }

        public void OnUpdateUI()
        {
            if (!_isOpened) return;

            var inputs = new List<ItemRequirement>();
            foreach (var slot in BuildingUI.Instance.CurrentInputSlots)
            {
                inputs.Add(slot.item != null ? new ItemRequirement(slot.item.itemSO, slot.item.count) : new ItemRequirement(null, 0));
            }

            var output = new ItemRequirement
            {
                item = BuildingUI.Instance.OutputSlot.item?.itemSO,
                count = BuildingUI.Instance.OutputSlot.item?.count ?? 0
            };

            _current.SetValues(inputs, output);
        }
    }
}